/* eslint-disable @typescript-eslint/no-explicit-any */
import {
    computed,
    Directive,
    EnvironmentInjector,
    EventEmitter,
    inject,
    Input,
    OnInit,
    Output,
    QueryList,
    signal,
    Signal,
    WritableSignal
} from '@angular/core';
import { AbstractControl, FormArray, FormControl, FormControlStatus, FormGroup, ValidatorFn } from '@angular/forms';

import { asyncScheduler, delay, filter, map, merge, Observable, throttleTime } from 'rxjs';

import { clone } from 'lodash-es';
import { PartialDeep } from 'type-fest';
import { ArrayElement } from 'type-fest/source/internal';
import { IPlatformFormValidationError } from '../../form-validators';
import { FormHelpers } from '../../helpers';
import { distinctUntilObjectValuesChanged } from '../../rxjs';
import {
    immutableUpdate,
    ImmutableUpdateOptions,
    isDifferent,
    isSinglePrimitiveOrImmutableType,
    keys,
    task_delay,
    toPlainObj
} from '../../utils';
import { IPlatformVm, PlatformFormMode, requestStateDefaultKey } from '../../view-models';
import { ComponentStateStatus, PlatformComponent } from './platform.component';
import { PlatformVmComponent } from './platform.vm-component';

export interface IPlatformFormComponent<TViewModel extends IPlatformVm> {
    isFormValid(): boolean;

    isAllChildFormsValid(forms: QueryList<IPlatformFormComponent<IPlatformVm>>[]): boolean;

    validateForm(): boolean;

    validateAllChildForms(forms: QueryList<IPlatformFormComponent<IPlatformVm>>[]): boolean;

    formControls(key: keyof TViewModel): FormControl;

    formControlsError(controlKey: keyof TViewModel, errorKey: string): IPlatformFormValidationError | null;
}

/**
 * Key Features:
 *
 * Form Initialization and Configuration:
 *
 * The class initializes and configures the Angular reactive form based on the provided formConfig and form inputs.
 * If the formConfig and form are not provided via input, it initializes an empty form.
 * Throttles user input changes using RxJS throttleTime to enhance performance.
 *
 * Dynamic Form Control Handling:
 *
 * Dynamically handles different types of form controls, including regular form controls and form arrays.
 * Utilizes reactive form features like value changes subscriptions to update the view model and trigger validations.
 *
 * Form Validation:
 *
 * Provides methods for validating the entire form (validateForm) and all child forms (validateAllChildForms).
 * Supports group validations and dependent validations, allowing one control's validation to depend on others.
 *
 * Mode Management:
 *
 * Manages different modes for the form, such as 'create', 'update', and 'view'.
 * Automatically enables and disables the form based on the mode, triggering necessary updates and validations.
 *
 * Integration with ViewModel:
 *
 * Bridges the gap between the reactive form and the underlying view model (IPlatformVm).
 * Facilitates the synchronization of form values with the view model and vice versa.
 *
 * Event Emission:
 * Emits events when the view model changes (vmChangeEvent).
 */
@Directive()
export abstract class PlatformFormComponent<TViewModel extends IPlatformVm>
    extends PlatformVmComponent<TViewModel>
    implements IPlatformFormComponent<TViewModel>, OnInit
{
    public static readonly updateVmOnFormValuesChangeDelayMs = 1;
    // because we are using vm() signal, when updateVmOnFormValuesChange => setTimeout to ensure the value
    // in vm signal is updated => then run validation to make sure it works correctly if validation logic is using vm signal value
    public static readonly processFormValidationsDelays = 10;

    protected environmentInjector = inject(EnvironmentInjector);

    public constructor() {
        super();
    }

    public formStatus$: WritableSignal<FormControlStatus> = signal('VALID');

    protected _mode: PlatformFormMode = 'create';
    public get mode(): PlatformFormMode {
        return this._mode;
    }

    @Input()
    public set mode(v: PlatformFormMode) {
        const prevMode = this._mode;
        this._mode = v;

        if (!this.initiated$.value) return;

        if (prevMode == 'view' && (v == 'create' || v == 'update')) {
            this.form.enable();
            this.patchVmValuesToForm(this.currentVm());
            this.validateForm();
        }
    }

    private _form!: FormGroup<PlatformFormGroupControls<TViewModel>>;
    public get form(): FormGroup<PlatformFormGroupControls<TViewModel>> {
        return this._form;
    }

    @Input()
    public set form(v: FormGroup<PlatformFormGroupControls<TViewModel>>) {
        this._form = v;
        if (this.initiated$.value) this.onFormInputChanged();
    }

    /**
     * Form change event when values or status of the form changed
     * @public
     */
    @Output('formChange')
    public formChangeEvent = new EventEmitter<FormGroup<PlatformFormGroupControls<TViewModel>>>();

    @Input() public formConfig!: PlatformFormConfig<TViewModel>;

    public get isViewMode(): boolean {
        return this.mode === 'view';
    }

    public get isCreateMode(): boolean {
        return this.mode === 'create';
    }

    public get isUpdateMode(): boolean {
        return this.mode === 'update';
    }

    public isFormGivenFromInput = false;
    public isFormLoading = computed(
        () =>
            (this.formStatus$() == 'PENDING' && this.form?.status == 'PENDING') ||
            this.status$() == ComponentStateStatus.Loading
    );

    protected cachedFormLoading$: Dictionary<Signal<boolean | null>> = {};

    protected override updateVm(
        partialStateOrUpdaterFn:
            | PartialDeep<TViewModel>
            | Partial<TViewModel>
            | ((state: TViewModel) => void | PartialDeep<TViewModel>),
        onVmChanged?: (vm: TViewModel) => unknown,
        immutableUpdateOptions?: ImmutableUpdateOptions,
        markFormDirty: boolean = true
    ): TViewModel {
        return super.updateVm(
            partialStateOrUpdaterFn,
            vm => {
                if (onVmChanged != undefined) onVmChanged(vm);
                if (markFormDirty && this.form != undefined) this.form.markAsDirty();
            },
            immutableUpdateOptions
        );
    }

    /**
     * Returns an Signal that emits the form loading state (true or false) associated with the specified request key.
     * @param [requestKey=requestStateDefaultKey] (optional): A key to identify the request. Default is requestStateDefaultKey.
     */
    public isFormLoading$(requestKey: string = requestStateDefaultKey): Signal<boolean | null> {
        if (this.cachedFormLoading$[requestKey] == null) {
            this.cachedFormLoading$[requestKey] = computed(() => this.form.pending || this.isLoading$(requestKey)());
        }
        return this.cachedFormLoading$[requestKey]!;
    }

    /**
     * Abstract method responsible for providing the initial configuration for the platform form.
     *
     * @remarks
     * This method is meant to be implemented by derived classes to define the initial form configuration,
     * including form controls, group validations, dependent validations, and any other relevant settings.
     * It is crucial for initializing the platform form with the correct structure and settings.
     *
     * @example
     * // Example implementation in a derived class:
     * protected initialFormConfig(): PlatformFormConfig<TViewModel> | undefined {
     *    return {
     *        controls: {
     *            username: new FormControl('', [Validators.required, Validators.minLength(3)]),
     *            email: new FormControl('', [Validators.required, Validators.email]),
     *            // ... other form controls ...
     *        },
     *        groupValidations: [
     *            ['password', 'confirmPassword'], // Validation for matching passwords
     *            // ... other group validations ...
     *        ],
     *        dependentValidations: {
     *            dependentProp: ['dependedOnProp1', 'dependedOnProp2'],
     *            // ... other dependent validations ...
     *        },
     *        afterInit: () => {
     *            console.log('Form initialized successfully');
     *            // ... other initialization logic ...
     *        },
     *        childForms: () => [childForm1, childForm2, ...],
     *    };
     * }
     *
     * @returns Initial configuration for the platform form, including form controls and related settings.
     * @throws Error if the method is not implemented in the derived class or returns an undefined value.
     *
     * @typeparam TViewModel - The type representing the view model for the form.
     */
    protected abstract initialFormConfig: () => PlatformFormConfig<TViewModel> | undefined;

    private isFormGivenViaInput?: boolean;

    public override ngOnInit(): void {
        this.isFormGivenViaInput = this.formConfig != null && this.form != null;

        this.initVm(undefined, () => {
            // If form and formConfig has NOT been given via input
            if (!this.isFormGivenViaInput) {
                if (this.initiated$.value && this.vm() != null) {
                    this.initForm();
                } else {
                    // Init empty form
                    this._form = new FormGroup<PlatformFormGroupControls<TViewModel>>(<any>{});

                    this.storeAnonymousSubscription(
                        this.initiated$.pipe(filter(initiated => initiated)).subscribe(() => {
                            this.initForm(true);
                        })
                    );
                }
            } else {
                this.setUpInputForm();
            }
        });

        this.ngOnInitCalled$.next(true);
    }

    public onFormInputChanged() {
        this.setUpInputForm();
    }

    public setUpInputForm() {
        // Register on case child form given from parent but not self init
        // need to register activate change detection to show form info correctly on html
        this.registerFormEventsSignalAndChangeDetection();
        this.isFormGivenFromInput = true;

        if (!this.isViewMode) {
            // First time try validate form just to show errors but still want to imark form as pristine like it's never touched
            this.validateForm();
            this.form.markAsPristine();
        }
    }

    public registerFormEventsSignalAndChangeDetection() {
        this.cancelStoredSubscription('registerFormEventsChangeDetection');

        if (this.form != undefined) {
            this.formStatus$.set(this.form.status);

            this.storeSubscription(
                'registerFormEventsChangeDetection',
                merge(
                    ...[
                        this.form.statusChanges,
                        this.form.valueChanges,
                        ...Object.keys(this.form.controls).map(ctrKey => this.form.get(ctrKey)!.statusChanges),
                        ...Object.keys(this.form.controls).map(ctrKey => this.form.get(ctrKey)!.valueChanges)
                    ]
                )
                    .pipe(
                        this.untilDestroyed(),
                        throttleTime(PlatformComponent.defaultDetectChangesThrottleTime, asyncScheduler, {
                            leading: true,
                            trailing: true
                        })
                    )
                    .subscribe(v => {
                        this.formStatus$.set(this.form.status);
                        this.formChangeEvent.emit(this.form);
                        this.detectChanges();
                    })
            );
        }
    }

    /**
     * Reloads the platform form component by reinitializing the underlying view model, form, and clearing error messages.
     * This method is useful when you need to reset the form to its initial state, such as when navigating to a new item or
     * resetting the form after a submission.
     *
     * @remarks
     * This method triggers the initialization of the view model (`initVm`), the form (`initForm`), and clears all error
     * messages associated with the form (`clearAllErrorMsgs`). It is typically called when there is a need to reset the
     * state of the form, ensuring that it reflects the intended initial state.
     *
     * @example
     * // Example usage:
     * const platformForm = new MyPlatformFormComponent();
     * platformForm.reload(); // Reloads the form to its initial state.
     */
    public override reload() {
        this.clearAllErrorMsgs();
        this.initVm(true, () => {
            this.initForm(true);
        });
    }

    /**
     * Initializes the Angular reactive form associated with the platform form component.
     *
     * @param forceReinit - If set to true, forces the reinitialization of the form even if it has been previously initialized.
     *
     * @remarks
     * This method is responsible for constructing and configuring the reactive form based on the provided `formConfig`. If
     * the `formConfig` and `form` are not provided via input, it initializes an empty form. It also sets up throttling for
     * user input changes using RxJS `throttleTime` to enhance performance by reducing unnecessary emissions during fast typing.
     *
     * The method dynamically handles different types of form controls, including regular form controls and form arrays.
     * It utilizes reactive form features like value changes subscriptions to update the view model and trigger validations.
     *
     * Additionally, this method is part of the lifecycle of the platform form component and is automatically called during
     * the Angular component lifecycle (`ngOnInit`), ensuring that the form is properly initialized when the component is
     * created.
     *
     * @example
     * // Example usage within the Angular component:
     * class MyPlatformFormComponent extends PlatformFormComponent<MyViewModel> implements OnInit {
     *    ngOnInit(): void {
     *        this.initForm();
     *    }
     * }
     * ```
     *
     * @throws {Error} Throws an error if the initial form configuration is undefined or if both the form and formConfig
     *                are not provided via input.
     */
    protected initForm(forceReinit: boolean = false) {
        if (this.formConfig != null && this.form != null && !forceReinit) return;

        const initialFormConfig = this.initialFormConfig();
        if (initialFormConfig == undefined)
            throw new Error('initialFormConfig must not be undefined or formConfig and form must be input');

        this.formConfig = initialFormConfig;
        this._form = <FormGroup<PlatformFormGroupControls<TViewModel>>>this.buildForm(this.formConfig);

        this.registerFormEventsSignalAndChangeDetection();

        if (forceReinit) {
            keys(this.form.controls).forEach(formControlKey => {
                this.cancelStoredSubscription(buildControlValueChangesSubscriptionKey(formControlKey));
            });
        }

        /***
         ThrottleTime explain: Delay to enhance performance when user typing fast do not need to emit
         { leading: true, trailing: true } <=> emit the first item to ensure ui is not delay, but also ignore the sub-sequence,
         and still emit the latest item to ensure data is latest

         source_1:          --0--1-----2--3----4--5-6---7------------8-------9---------
         throttle interval: --[~~~~~~~~~~~I~~~~~~~~~~~I~~~~~~~~~~~I~~~~~~~~~~~I~~~~~~~~
         output:            --0-----------3-----------6-----------7-----------9--------

         source_2:          --0--------1------------------2--------------3---4---------
         throttle interval: --[~~~~~~~~~~~I~~~~~~~~~~~]---[~~~~~~~~~~~]--[~~~~~~~~~~~I~
         output_2:          --0-----------1---------------2--------------3-----------4-

         */
        keys(this.form.controls).forEach(formControlKey => {
            this.storeSubscription(
                buildControlValueChangesSubscriptionKey(formControlKey),
                (<FormControl>(<Dictionary<unknown>>this.form.controls)[formControlKey]).valueChanges
                    .pipe(
                        throttleTime(PlatformComponent.defaultDetectChangesThrottleTime, asyncScheduler, {
                            leading: true,
                            trailing: true
                        }),
                        distinctUntilObjectValuesChanged(),
                        map(value => {
                            const currentReactiveFormValues = <Dictionary<TViewModel[keyof TViewModel]>>(
                                this.getCurrentReactiveFormControlValues()
                            );
                            const hasVmChanged = this.updateVmOnFormValuesChange(
                                <Partial<TViewModel>>{ [formControlKey]: value },
                                currentReactiveFormValues
                            );

                            return hasVmChanged;
                        }),
                        delay(PlatformFormComponent.updateVmOnFormValuesChangeDelayMs) // Delay updateVmOnFormValuesChangeDelayMs to push item in async queue to ensure control value has been updated to run processMainFormValidations
                    )
                    .subscribe(hasVmChanged => {
                        if (hasVmChanged) this.processMainFormOtherRelatedValidations(<keyof TViewModel>formControlKey);
                    })
            );
        });

        this.patchVmValuesToForm(this.currentVm(), false);

        if (this.isViewMode) this.form.disable();

        if (this.formConfig.afterInit != null && this.form != undefined) this.formConfig.afterInit();

        // setTimeout to ensure revalidate form after form directive view rendered
        // validate form to trigger form-status change check and emit
        if (this.canSubmitPristineForm && !forceReinit) setTimeout(() => this.validateForm());

        function buildControlValueChangesSubscriptionKey(formControlKey: string): string {
            return `initForm_${formControlKey}_valueChanges`;
        }
    }

    protected override internalSetVm = (
        v: TViewModel | undefined,
        shallowCheckDiff: boolean = true,
        onVmChanged?: (vm: TViewModel | undefined) => unknown,
        currentReactiveFormValues?: Dictionary<TViewModel[keyof TViewModel]>
    ): boolean => {
        if (shallowCheckDiff == false || this._vm != v) {
            const prevVm = this._vm;

            this._vm = v;
            this.vm.set(v);

            if (v != undefined && this.initiated$.value && this.formConfig != undefined)
                this.patchVmValuesToForm(v, true, currentReactiveFormValues);
            if (this.initiated$.value || prevVm == undefined) this.vmChangeEvent.emit(v);

            if (onVmChanged != undefined) onVmChanged(this._vm);

            this.detectChanges();

            return true;
        }

        return false;
    };

    public isFormValid(): boolean {
        // form or formConfig if it's initiated asynchronous, waiting call api but the component template use isFormValid
        // so that it could be undefined. check to prevent the bug
        return (
            this.form?.valid &&
            (this.formConfig?.childForms == undefined || this.isAllChildFormsValid(this.formConfig.childForms()))
        );
    }

    public canSubmitForm() {
        return this.isFormValid() && !this.isFormLoading() && (this.canSubmitPristineForm || !this.form.pristine);
    }

    public get canSubmitPristineForm() {
        return false;
    }

    public isAllChildFormsValid(
        forms: (QueryList<IPlatformFormComponent<IPlatformVm>> | IPlatformFormComponent<IPlatformVm>)[]
    ): boolean {
        const invalidChildFormsGroup = forms.find(childFormOrFormsGroup =>
            childFormOrFormsGroup instanceof QueryList
                ? childFormOrFormsGroup.find(formComponent => !formComponent.isFormValid()) != undefined
                : !childFormOrFormsGroup.isFormValid()
        );

        return invalidChildFormsGroup == undefined;
    }

    public validateForm(): boolean {
        FormHelpers.validateAllFormControls(this.form);

        return this.isFormValid();
    }

    public validateAllChildForms(
        forms: (QueryList<IPlatformFormComponent<IPlatformVm>> | IPlatformFormComponent<IPlatformVm>)[]
    ): boolean {
        const invalidChildFormsGroup = forms.find(childFormOrFormsGroup =>
            childFormOrFormsGroup instanceof QueryList
                ? childFormOrFormsGroup.find(formComponent => !formComponent.validateForm()) != undefined
                : !childFormOrFormsGroup.validateForm()
        );

        return invalidChildFormsGroup == undefined;
    }

    /**
     * Updates the values of the Angular reactive form based on the provided view model (`vm`).
     *
     * @param vm - The view model containing the values to be patched into the form.
     * @param runFormValidation - If set to true, triggers form validation after patching the values.
     *
     * @remarks
     * This method synchronizes the values between the view model (`vm`) and the reactive form. It iterates through the
     * properties of the view model and updates the corresponding form controls with the values from the view model. It
     * supports dynamic handling of different types of form controls, including regular form controls and form arrays.
     *
     * The `runFormValidation` parameter determines whether form validation should be triggered after updating the values.
     * If `runFormValidation` is set to true and the form is not in view mode, it also processes group validations and
     * dependent validations, ensuring that the form remains in a valid state.
     *
     * This method is a crucial part of maintaining the bidirectional data flow between the reactive form and the view model
     * within the platform form component. It is typically called after changes to the view model to reflect those changes
     * in the form.
     *
     * @example
     * // Example usage within the Angular component:
     * class MyPlatformFormComponent extends PlatformFormComponent<MyViewModel> {
     *    // Other component methods...
     *
     *    someEventHandler(): void {
     *        // Assume vm is an instance of MyViewModel
     *        this.patchVmValuesToForm(vm);
     *    }
     * }
     * ```
     */
    public patchVmValuesToForm(
        vm: TViewModel,
        runFormValidation: boolean = true,
        currentReactiveFormValues?: Dictionary<TViewModel[keyof TViewModel]>
    ): void {
        currentReactiveFormValues ??= <Dictionary<TViewModel[keyof TViewModel]>>(
            this.getCurrentReactiveFormControlValues()
        );
        const vmFormValues = <Dictionary<TViewModel[keyof TViewModel]>>(
            this.getFromVmFormValues(vm, currentReactiveFormValues)
        );

        const formControls = <PlatformFormGroupControls<TViewModel>>this.form.controls;

        keys(vmFormValues).forEach(formKey => {
            const vmFormKey = <keyof TViewModel>formKey;

            const vmFormKeyValue = vmFormValues[formKey];
            const formControl = formControls[vmFormKey];

            if (isDifferent(vmFormKeyValue, currentReactiveFormValues[formKey])) {
                if (formControl instanceof FormArray && vmFormKeyValue instanceof Array) {
                    const listControlformConfig = <
                        PlatformFormGroupControlConfigPropArray<ArrayElement<TViewModel[keyof TViewModel]>>
                    >this.formConfig.controls[vmFormKey];
                    const previousControls = clone((<FormArray>formControl).controls);

                    formControl.clear({ emitEvent: false });
                    vmFormKeyValue.forEach((modelItem: ArrayElement<TViewModel[keyof TViewModel]>, index) => {
                        const fromArrayControlItem = this.buildFromArrayControlItem(
                            vmFormKey,
                            listControlformConfig,
                            modelItem,
                            index
                        );

                        formControl.push(fromArrayControlItem, {
                            emitEvent: false
                        });

                        if (previousControls[index] != null && previousControls[index]!.touched)
                            formControl.at(index)!.markAllAsTouched();
                        if (previousControls[index] != null && previousControls[index]!.dirty)
                            formControl.at(index)!.markAsDirty();
                        if (previousControls[index] != null && previousControls[index]!.pristine)
                            formControl.at(index)!.markAsPristine();
                    });
                }

                this.form.patchValue(<any>{ [formKey]: vmFormKeyValue }, {
                    emitEvent: false
                });

                if (!this.isViewMode && runFormValidation) {
                    setTimeout(() => {
                        this.validateFormControl(this.form, vmFormKey);
                    }, PlatformFormComponent.processFormValidationsDelays);
                    this.processMainFormOtherRelatedValidations(vmFormKey);
                }
            }
        });
    }

    private processMainFormOtherRelatedValidations(vmFormKey: keyof TViewModel) {
        this.processGroupValidation(this.formConfig.groupValidations, 'mainForm', this.form, vmFormKey);
        this.processDependentValidations(this.formConfig.dependentValidations, 'mainForm', this.form, vmFormKey);
    }

    public getCurrentReactiveFormControlValues(): Partial<TViewModel> {
        const reactiveFormValues: Partial<TViewModel> = {};

        keys(this.formConfig.controls).forEach(formControlKey => {
            (<Dictionary<unknown>>reactiveFormValues)[formControlKey] = this.form.controls[formControlKey].value;
        });

        return reactiveFormValues;
    }

    /**
     * Get form values from view model
     */
    public getFromVmFormValues(
        vmValue: TViewModel | Dictionary<unknown> | unknown[],
        currentReactiveFormValues: Dictionary<TViewModel[keyof TViewModel]> | Dictionary<unknown> | unknown[]
    ): Partial<TViewModel> {
        const vmFormValues: Partial<TViewModel> = {};

        // Explain 1: keys(currentReactiveFormValues)
        // use currentReactiveFormValues as object to get keys from to build object get value from view model with structure (count and order of properties) same as structure
        // from currentReactiveFormValues => so that when compare isValueDifferent would be correct
        keys(currentReactiveFormValues).forEach(formControlKey => {
            const vmValueItem = (<Dictionary<unknown>>(<unknown>vmValue))[formControlKey];

            // vmValueItem === undefined => return null to make sure the vmFormValues result return must exist property formControlKey.
            // If set undefine => key in the object will not exist. Need to set null the make the key exist in the return result
            if (currentReactiveFormValues[formControlKey] instanceof Array || vmValueItem instanceof Array) {
                (<Dictionary<unknown>>vmFormValues)[formControlKey] =
                    vmValueItem === undefined
                        ? null
                        : (<unknown[]>vmValueItem).map((vmValueArrayItem, index) => {
                              const currentReactiveFormValueArrayItem =
                                  <unknown[]>currentReactiveFormValues[formControlKey] != null
                                      ? (<unknown[]>currentReactiveFormValues[formControlKey])[index]
                                      : null;

                              if (
                                  !isSinglePrimitiveOrImmutableType(vmValueArrayItem) &&
                                  !isSinglePrimitiveOrImmutableType(currentReactiveFormValueArrayItem)
                              ) {
                                  return this.getFromVmFormValues(
                                      <Dictionary<unknown>>vmValueArrayItem,
                                      <Dictionary<unknown>>currentReactiveFormValueArrayItem
                                  );
                              }

                              return vmValueArrayItem;
                          });
            } else if (
                !isSinglePrimitiveOrImmutableType(vmValueItem) &&
                !isSinglePrimitiveOrImmutableType(currentReactiveFormValues[formControlKey])
            ) {
                (<Dictionary<unknown>>vmFormValues)[formControlKey] = this.getFromVmFormValues(
                    <Dictionary<unknown>>vmValueItem,
                    <Dictionary<unknown>>currentReactiveFormValues[formControlKey]
                );
            } else {
                // vmValueItem === undefined => return null to make sure the vmFormValues result return must exist property formControlKey.
                // If set undefine => key in the object will not exist. Need to set null the make the key exist in the return result
                (<Dictionary<unknown>>vmFormValues)[formControlKey] = vmValueItem === undefined ? null : vmValueItem;
            }
        });

        // To toPlainObj to ensure removing getter/setter which help angular lib can read prop keys and apply data from vm to form
        return toPlainObj(vmFormValues);
    }

    /**
     * Retrieves the Angular reactive form control associated with a specific key from the platform form component's form.
     *
     * @param key - The key (property name) corresponding to the control in the view model.
     *
     * @returns The Angular `FormControl` associated with the specified key.
     *
     * @remarks
     * This method provides a convenient way to access a specific form control within the platform form component's form.
     * It is especially useful for interacting with individual controls, such as getting or setting their values, checking
     * their validity, or applying specific actions based on user interactions with the form.
     *
     * The `key` parameter should match the property name in the view model for which you want to retrieve the form control.
     * If the property is an array, this method works with both regular form controls and form arrays, allowing dynamic
     * handling of different types of controls within the form.
     *
     * @example
     * // Example usage within the Angular component:
     * class MyPlatformFormComponent extends PlatformFormComponent<MyViewModel> {
     *    // Other component methods...
     *
     *    someEventHandler(): void {
     *        // Assume 'propertyName' is a key in the view model
     *        const control = this.formControls('propertyName');
     *        // Now you can interact with the 'control' as needed
     *    }
     * }
     * ```
     */
    public formControls<TKey extends keyof TViewModel>(key: TKey): FormControl<TViewModel[TKey]> {
        return <FormControl<TViewModel[TKey]>>this.form.get(<string>key);
    }

    public getFormControls<TFormModel, TKey extends keyof TFormModel>(
        formGroup: FormGroup<PlatformFormGroupControls<TFormModel>>,
        key: TKey
    ): FormControl<TFormModel[TKey]> {
        return <FormControl<TFormModel[TKey]>>formGroup.get(<string>key);
    }

    public tryFormControls<TKey extends keyof TViewModel>(key: TKey): FormControl<TViewModel[TKey]> | undefined {
        if (this.form == undefined) return undefined;
        return this.formControls(key);
    }

    public formControlValueChanges<TKey extends keyof TViewModel>(key: TKey): Observable<TViewModel[TKey]> {
        return this.formControls(key).valueChanges.pipe(
            delay(PlatformFormComponent.updateVmOnFormValuesChangeDelayMs, asyncScheduler), // Delay PlatformFormComponent.updateVmOnFormValuesChangeDelayMs to push item in async queue to ensure control value has been updated,
            distinctUntilObjectValuesChanged(),
            this.untilDestroyed()
        );
    }

    /**
     * Retrieves the Angular reactive form group control associated with a specific key from the platform form component's form.
     *
     * @param {TKey} key - The key (property name) corresponding to the form group control in the view model.
     *
     * @returns {FormGroup<PlatformFormGroupControls<TViewModel[TKey]>>} The Angular `FormGroup` control associated with the specified key.
     *
     * @remarks
     * This method provides a convenient way to access a specific form group control within the platform form component's form.
     * It is especially useful for interacting with a group of controls as a single unit, such as getting or setting their values,
     * checking their validity, or applying specific actions based on user interactions with the form.
     *
     * The `key` parameter should match the property name in the view model for which you want to retrieve the form group control.
     * If the property is an array, this method works with both regular form groups and nested form arrays, allowing dynamic
     * handling of different types of groups within the form.
     *
     * @example
     * // Example usage within the Angular component:
     * class MyPlatformFormComponent extends PlatformFormComponent<MyViewModel> {
     *    // Other component methods...
     *
     *    someEventHandler(): void {
     *        // Assume 'groupPropertyName' is a key in the view model
     *        const groupControl = this.formChildGroups('groupPropertyName');
     *        // Now you can interact with the 'groupControl' as needed
     *    }
     * }
     */
    public formChildGroups<TKey extends keyof TViewModel>(
        key: TKey
    ): FormGroup<PlatformFormGroupControls<TViewModel[TKey]>> {
        return <FormGroup<PlatformFormGroupControls<TViewModel[TKey]>>>(<unknown>this.form.get(<string>key));
    }

    /**
     * Retrieves the Angular reactive form array control associated with a specific key from the platform form component's form.
     *
     * @param key - The key (property name) corresponding to the form array control in the view model.
     *
     * @returns The Angular `FormArray` control associated with the specified key.
     *
     * @remarks
     * This method provides a convenient way to access a specific form array control within the platform form component's form.
     * It is especially useful for working with array controls, such as iterating over array elements, pushing new elements,
     * or accessing specific elements within the array.
     *
     * The `key` parameter should match the property name in the view model for which you want to retrieve the form array control.
     * This method is designed for cases where the property in the view model is an array, and it allows dynamic handling of
     * form array controls within the form.
     *
     * @example
     * // Example usage within the Angular component:
     * class MyPlatformFormComponent extends PlatformFormComponent<MyViewModel> {
     *    // Other component methods...
     *
     *    someEventHandler(): void {
     *        // Assume 'arrayPropertyName' is an array key in the view model
     *        const arrayControl = this.formArrayControls('arrayPropertyName');
     *        // Now you can interact with the 'arrayControl' as needed
     *    }
     * }
     * ```
     */
    public formArrayControls<TKey extends keyof TViewModel>(
        key: TKey
    ): FormArray<FormControl<ArrayElement<TViewModel[TKey]>>> {
        return <FormArray<FormControl<ArrayElement<TViewModel[TKey]>>>>(<unknown>this.form.get(<string>key));
    }

    /**
     * Retrieves a specific Angular reactive form control within a form array associated with a key from the platform form component's form.
     *
     * @param key - The key (property name) corresponding to the form array control in the view model.
     * @param index - The index of the form array element for which to retrieve the form control.
     *
     * @returns The Angular `FormControl` associated with the specified index in the form array control.
     *
     * @remarks
     * This method allows accessing a specific form control within a form array associated with the given key.
     * It is useful for scenarios where dynamic manipulation or validation of individual array elements is required.
     *
     * The `key` parameter should match the property name in the view model representing the form array,
     * and the `index` parameter specifies the position of the desired array element.
     *
     * @example
     * // Example usage within the Angular component:
     * class MyPlatformFormComponent extends PlatformFormComponent<MyViewModel> {
     *    // Other component methods...
     *
     *    someEventHandler(): void {
     *        // Assume 'arrayPropertyName' is an array key in the view model
     *        // and 'desiredIndex' is the index of the specific array element
     *        const specificControl = this.formArrayControlsItem('arrayPropertyName', desiredIndex);
     *        // Now you can interact with the 'specificControl' as needed
     *    }
     * }
     * ```
     */
    public formArrayControlsItem<TKey extends keyof TViewModel>(
        key: TKey,
        index: number
    ): FormControl<ArrayElement<TViewModel[TKey]>> {
        return this.formArrayControls(key).at(index);
    }

    /**
     * Retrieves the Angular reactive form array containing groups of form controls associated with a key from the platform form component's form.
     *
     * @param key - The key (property name) corresponding to the form array of groups in the view model.
     *
     * @returns The Angular `FormArray` containing groups of form controls associated with the specified key.
     *
     * @remarks
     * This method allows accessing a form array that contains groups of form controls associated with the given key.
     * It is particularly useful when dealing with complex nested forms where each element of the array represents a group of controls.
     *
     * The `key` parameter should match the property name in the view model representing the form array of groups.
     *
     * @example
     * // Example usage within the Angular component:
     * class MyPlatformFormComponent extends PlatformFormComponent<MyViewModel> {
     *    // Other component methods...
     *
     *    someEventHandler(): void {
     *        // Assume 'arrayOfGroups' is an array key in the view model
     *        const groupArray = this.formArrayGroups('arrayOfGroups');
     *        // Now you can interact with the 'groupArray' and its contained form groups
     *    }
     * }
     * ```
     */
    public formArrayGroups<TKey extends keyof TViewModel>(
        key: TKey
    ): FormArray<FormGroup<PlatformFormGroupControls<ArrayElement<TViewModel[TKey]>>>> {
        return <FormArray<FormGroup<PlatformFormGroupControls<ArrayElement<TViewModel[TKey]>>>>>(
            (<unknown>this.form.get(<string>key))
        );
    }

    public getFormArrayGroups<TFormModel, TKey extends keyof TFormModel>(
        formGroup: FormGroup<PlatformFormGroupControls<TFormModel>>,
        key: TKey
    ): FormArray<FormGroup<PlatformFormGroupControls<ArrayElement<TFormModel[TKey]>>>> {
        return <FormArray<FormGroup<PlatformFormGroupControls<ArrayElement<TFormModel[TKey]>>>>>(
            (<unknown>formGroup.get(<string>key))
        );
    }

    /**
     * Retrieves a specific form group from the Angular reactive form array containing groups of form controls associated with a key from the platform form component's form.
     *
     * @param key - The key (property name) corresponding to the form array of groups in the view model.
     * @param index - The index of the form group to retrieve within the form array.
     *
     * @returns The Angular `FormGroup` representing a specific group of form controls associated with the specified key and index.
     *
     * @remarks
     * This method allows accessing a specific form group within the form array that contains groups of form controls associated with the given key.
     * It is particularly useful when dealing with complex nested forms where each element of the array represents a group of controls.
     *
     * The `key` parameter should match the property name in the view model representing the form array of groups.
     * The `index` parameter represents the position of the desired form group within the form array.
     *
     * @example
     * // Example usage within the Angular component:
     * class MyPlatformFormComponent extends PlatformFormComponent<MyViewModel> {
     *    // Other component methods...
     *
     *    someEventHandler(): void {
     *        // Assume 'arrayOfGroups' is an array key in the view model, and you want to access the form group at index 0
     *        const groupAtIndex = this.formArrayGroupsItem('arrayOfGroups', 0);
     *        // Now you can interact with the 'groupAtIndex' and its contained form controls
     *    }
     * }
     * ```
     */
    public formArrayGroupsItem<TKey extends keyof TViewModel>(
        key: TKey,
        index: number
    ): FormGroup<PlatformFormGroupControls<ArrayElement<TViewModel[TKey]>>> {
        return this.formArrayGroups(key).at(index);
    }

    /**
     * Retrieves a specific form control from a nested form group within the Angular reactive form array containing groups of form controls associated with a key from the platform form component's form.
     *
     * @param key - The key (property name) corresponding to the form array of groups in the view model.
     * @param index - The index of the form group to retrieve within the form array.
     * @param arrayItemControlKey - The key (property name) corresponding to the specific form control within the nested form group.
     *
     * @returns The Angular `FormControl` representing a specific form control within a nested group associated with the specified key, index, and control key.
     *
     * @remarks
     * This method allows accessing a specific form control within a nested form group of the form array that contains groups of form controls associated with the given key.
     * It is particularly useful when dealing with complex nested forms where each element of the array represents a group of controls and each group contains specific controls.
     *
     * The `key` parameter should match the property name in the view model representing the form array of groups.
     * The `index` parameter represents the position of the desired form group within the form array.
     * The `arrayItemControlKey` parameter corresponds to the property name of the specific form control within the nested form group.
     *
     * @example
     * // Example usage within the Angular component:
     * class MyPlatformFormComponent extends PlatformFormComponent<MyViewModel> {
     *    // Other component methods...
     *
     *    someEventHandler(): void {
     *        // Assume 'arrayOfGroups' is an array key in the view model, 'groupControl' is a control key in the nested group, and you want to access the form control at index 0
     *        const controlAtIndex = this.formArrayGroupsControlsItem('arrayOfGroups', 0, 'groupControl');
     *        // Now you can interact with the 'controlAtIndex' and its properties
     *    }
     * }
     * ```
     */
    public formArrayGroupsControlsItem<
        TKey extends keyof TViewModel,
        TItemKey extends keyof ArrayElement<TViewModel[TKey]>
    >(key: TKey, index: number, arrayItemControlKey: TItemKey): FormControl<ArrayElement<TViewModel[TKey]>[TItemKey]> {
        return <FormControl<ArrayElement<TViewModel[TKey]>[TItemKey]>>this.formArrayGroups(key)
            .at(index)
            .get(<string>arrayItemControlKey);
    }

    /**
     * Retrieves the validation error associated with a specific form control key and error key within the Angular reactive form of the platform form component.
     *
     * @param controlKey - The key (property name) corresponding to the form control in the view model for which the validation error is to be retrieved.
     * @param errorKey - The key (property name) corresponding to the specific validation error within the form control.
     * @param onlyWhenTouchedOrDirty - Optional. If `true`, the method returns the validation error only when the form control has been touched or is dirty.
     * @param formToFind - Optional. The specific form group to search for the form control. If not provided, the method searches for the form control within the main form of the platform form component.
     *
     * @returns The validation error associated with the specified control key and error key, or `null` if no error is found.
     *
     * @remarks
     * This method allows retrieving validation errors for a specific form control within the Angular reactive form of the platform form component.
     * The `controlKey` parameter should match the property name in the view model representing the desired form control.
     * The `errorKey` parameter corresponds to the property name of the specific validation error within the form control's errors object.
     * The optional `onlyWhenTouchedOrDirty` parameter can be used to conditionally return the error only when the form control has been touched or is dirty.
     * The optional `formToFind` parameter allows specifying a particular form group in case the form control is nested within a subgroup.
     *
     * @example
     * // Example usage within the Angular component:
     * class MyPlatformFormComponent extends PlatformFormComponent<MyViewModel> {
     *    // Other component methods...
     *
     *    someEventHandler(): void {
     *        // Assume 'controlKey' is a key in the view model representing a form control, and 'errorKey' is a specific validation error key
     *        const validationError = this.formControlsError('controlKey', 'errorKey');
     *        if (validationError) {
     *            // Handle the validation error...
     *        }
     *    }
     * }
     * ```
     */
    public formControlsError(
        controlKey: keyof TViewModel,
        errorKey: string,
        onlyWhenTouchedOrDirty: boolean = false
    ): IPlatformFormValidationError | null {
        const form = this.form != null ? this.formControls(controlKey) : undefined;
        if (onlyWhenTouchedOrDirty && form?.touched == false && form?.dirty == false) return null;

        return form?.errors?.[errorKey];
    }

    /**
     * Retrieves the validation error associated with a specific form control key and error key within the provided form group.
     *
     * @template TFormModel - The type of the form model associated with the form group.
     *
     * @param {FormGroup<PlatformFormGroupControls<TFormModel>>} formGroup - The form group in which the form control resides.
     * @param {keyof TFormModel} controlKey - The key (property name) corresponding to the form control for which the validation error is to be retrieved.
     * @param {string} errorKey - The key (property name) corresponding to the specific validation error within the form control.
     * @param {boolean} [onlyWhenTouchedOrDirty=false] - Optional. If `true`, the method returns the validation error only when the form control has been touched or is dirty.
     *
     * @returns {IPlatformFormValidationError | null} The validation error associated with the specified control key and error key, or `null` if no error is found.
     */
    public getFormControlsError<TFormModel>(
        formGroup: FormGroup<PlatformFormGroupControls<TFormModel>>,
        controlKey: keyof TFormModel,
        errorKey: string,
        onlyWhenTouchedOrDirty: boolean = false
    ): IPlatformFormValidationError | null {
        const form = formGroup.get(<string>controlKey);
        if (onlyWhenTouchedOrDirty && form?.touched == false && form?.dirty == false) return null;

        return form?.errors?.[errorKey];
    }

    /**
     * Initiates the group validation process for a specified form control key within the Angular reactive form of the platform form component.
     *
     * @param formControlKey - The key (property name) corresponding to the form control in the view model for which group validation is to be triggered.
     *
     * @remarks
     * This method triggers the validation of form controls grouped together based on the provided group validation configuration.
     * Group validation is useful when the validity of one control depends on the values or states of other controls.
     * The method uses a delayed execution mechanism to ensure that the validation is not triggered immediately on every value change, providing a smoother user experience.
     */
    public processGroupValidation<TFormModel>(
        groupValidations: (keyof TFormModel)[][] | undefined,
        formGroupName: string,
        formGroup: FormGroup<PlatformFormGroupControls<TFormModel>>,
        formControlKey: keyof TFormModel
    ) {
        if (groupValidations == null) return;

        this.cancelStoredSubscription(`processGroupValidation_${formControlKey.toString()}`);

        this.storeSubscription(
            `processGroupValidation_${formGroupName}_${formControlKey.toString()}`,
            task_delay(() => {
                groupValidations?.forEach(groupValidators => {
                    if (groupValidators.includes(formControlKey))
                        groupValidators.forEach(groupValidatorControlKey => {
                            this.validateFormControl<TFormModel>(formGroup, groupValidatorControlKey);
                        });
                });

                this.detectChanges();
            }, PlatformFormComponent.processFormValidationsDelays)
        );
    }

    /**
     * Initiates the dependent validation process for a specified form control key within the Angular reactive form of the platform form component.
     *
     * @param formControlKey - The key (property name) corresponding to the form control in the view model for which dependent validation is to be triggered.
     *
     * @remarks
     * This method triggers the validation of form controls that depend on the values or states of the specified form control.
     * Dependent validation is useful when the validity of certain controls relies on changes in other controls.
     * The method uses a delayed execution mechanism to ensure that the validation is not triggered immediately on every value change, providing a smoother user experience.
     */
    public processDependentValidations<TFormModel>(
        dependentValidations: Partial<Record<keyof TFormModel, (keyof TFormModel)[] | undefined>> | undefined,
        formGroupName: string,
        formGroup: FormGroup<PlatformFormGroupControls<TFormModel>>,
        formControlKey: keyof TFormModel
    ) {
        if (dependentValidations == null) return;

        this.cancelStoredSubscription(`processDependentValidations_${formControlKey.toString()}`);

        this.storeSubscription(
            `processDependentValidations_${formGroupName}_${formControlKey.toString()}`,
            task_delay(() => {
                if (dependentValidations == undefined) return;

                Object.keys(dependentValidations).forEach(dependentValidationControlKey => {
                    const dependedOnOtherControlKeys =
                        dependentValidations[<keyof TFormModel>dependentValidationControlKey]!;

                    if (dependedOnOtherControlKeys.includes(formControlKey)) {
                        this.validateFormControl(formGroup, <keyof TFormModel>dependentValidationControlKey);
                    }
                });

                this.detectChanges();
            }, PlatformFormComponent.processFormValidationsDelays)
        );
    }

    public formValueChanges() {
        return this.form.valueChanges.pipe(
            this.untilDestroyed(),
            throttleTime(PlatformComponent.defaultDetectChangesThrottleTime, asyncScheduler, {
                leading: true,
                trailing: true
            }),
            distinctUntilObjectValuesChanged()
        );
    }

    protected validateFormControl<TFormModel>(
        formGroup: FormGroup<PlatformFormGroupControls<TFormModel>>,
        formControlKey: keyof TFormModel
    ) {
        const formControl = this.getFormControls(formGroup, formControlKey);

        this.validateAbstractControl(formControl);

        this.detectChanges();
    }

    protected validateFormArrayControl(formArrayControl: FormArray<AbstractControl<unknown>>) {
        formArrayControl.controls.forEach(childControl => {
            this.validateAbstractControl(childControl);
        });
    }

    protected validateFormGroupControl(formGroupControl: FormGroup<any>) {
        Object.keys(formGroupControl.controls).forEach(childControlKey => {
            const childControl = formGroupControl.controls[childControlKey]!;

            this.validateAbstractControl(childControl);
        });
    }

    protected validateAbstractControl(childControl: AbstractControl<unknown, unknown>) {
        if (childControl instanceof FormGroup) this.validateFormGroupControl(<FormGroup>childControl);
        else if (childControl instanceof FormArray) this.validateFormArrayControl(childControl);

        childControl.updateValueAndValidity({
            emitEvent: true,
            onlySelf: false
        });
    }

    /**
     * Creates a form array containing form groups for each item in the provided array, based on the specified form item group controls.
     *
     * @template TItemModel - The type of the item model associated with each form group in the array.
     *
     * @param {TItemModel[]} items - The array of items for which form groups are to be created.
     * @param {(item: TItemModel) => PlatformPartialFormGroupControls<TItemModel>} formItemGroupControls - A function that returns the form controls for each item.
     *
     * @returns {FormArray<FormGroup<PlatformFormGroupControls<TItemModel>>>} A form array containing form groups for each item in the provided array.
     */
    protected formGroupArrayFor<TItemModel>(
        items: TItemModel[],
        formItemGroupControls: (item: TItemModel) => PlatformPartialFormGroupControls<TItemModel>
    ): FormArray<FormGroup<PlatformFormGroupControls<TItemModel>>> {
        return new FormArray(
            items.map(item => new FormGroup(<PlatformFormGroupControls<TItemModel>>formItemGroupControls(item)))
        );
    }

    /**
     * Creates a form array containing form controls for each item in the provided array, based on the specified form item control.
     *
     * @template TItemModel - The type of the item model associated with each form control in the array.
     *
     * @param {TItemModel[]} items - The array of items for which form controls are to be created.
     * @param {(item: TItemModel) => FormControl<TItemModel>} formItemControl - A function that returns the form control for each item.
     *
     * @returns {FormArray<FormControl<TItemModel>>} A form array containing form controls for each item in the provided array.
     */
    protected formControlArrayFor<TItemModel>(
        items: TItemModel[],
        formItemControl: (item: TItemModel) => FormControl<TItemModel>
    ): FormArray<FormControl<TItemModel>> {
        return new FormArray(items.map(item => formItemControl(item)));
    }

    protected updateVmOnFormValuesChange(
        values: Partial<TViewModel>,
        currentReactiveFormValues: Dictionary<TViewModel[keyof TViewModel]>
    ) {
        const newUpdatedVm: TViewModel = immutableUpdate(this.currentVm(), values);

        if (newUpdatedVm != this._vm) {
            this.internalSetVm(newUpdatedVm, false, undefined, currentReactiveFormValues);
            return true;
        }

        return false;
    }

    protected buildForm(
        formOrInnerFromGroupConfig:
            | PlatformFormConfig<TViewModel>
            | PlatformPartialFormGroupControlsConfig<TViewModel[keyof TViewModel]>
    ):
        | FormGroup<PlatformFormGroupControls<TViewModel>>
        | FormGroup<PlatformFormGroupControls<TViewModel[keyof TViewModel]>> {
        const formConfig = <PlatformFormConfig<TViewModel>>formOrInnerFromGroupConfig;

        if (formConfig.controls != undefined) {
            const controls = <PlatformFormGroupControls<TViewModel>>{};

            keys(formConfig.controls).forEach((key: keyof TViewModel) => {
                const formControlConfigItem = <PlatformFormGroupControlConfigProp<TViewModel[keyof TViewModel]>>(
                    formConfig.controls[key]
                );
                const formArrayConfigItem = <
                    PlatformFormGroupControlConfigPropArray<ArrayElement<TViewModel[keyof TViewModel]>>
                >formConfig.controls[key];

                // TODO: comment pending experiment support inner form group ideal
                // const innerFormGroupConfigItem = <PlatformPartialFormGroupControlsConfig<TViewModel[keyof TViewModel]>>(
                //     formConfig.controls[key]
                // );

                if (formControlConfigItem instanceof FormControl) {
                    (<FormControl<TViewModel[keyof TViewModel]>>controls[key]) = <
                        FormControl<TViewModel[keyof TViewModel]>
                    >formControlConfigItem;
                } else if (
                    formArrayConfigItem.itemControl != undefined &&
                    formArrayConfigItem.modelItems != undefined
                ) {
                    (<PlatformFormGroupControlsFormArray<TViewModel[keyof TViewModel]>>controls[key]) = new FormArray(
                        formArrayConfigItem.modelItems().map((modelItem, index) => {
                            return this.buildFromArrayControlItem(key, formArrayConfigItem, modelItem, index);
                        }),
                        formArrayConfigItem.validators
                    );
                }

                // TODO: comment pending experiment support inner form group ideal
                // else if (typeof innerFormGroupConfigItem == 'object') {
                //     (<FormGroup<PlatformFormGroupControls<TViewModel[keyof TViewModel]>>>controls[key]) = <
                //         FormGroup<PlatformFormGroupControls<TViewModel[keyof TViewModel]>>
                //     >this.buildForm(innerFormGroupConfigItem);
                // }
            });

            return new FormGroup(controls);
        } else {
            const controls = <PlatformFormGroupControls<TViewModel[keyof TViewModel]>>formOrInnerFromGroupConfig;

            return new FormGroup(controls);
        }
    }

    protected buildFromArrayControlItem<TFormModel, TItemModel extends ArrayElement<TFormModel[keyof TFormModel]>>(
        formArrayControlKey: keyof TFormModel,
        formConfigControlsConfigArrayItem: PlatformFormGroupControlConfigPropArray<TItemModel>,
        modelItem: TItemModel,
        modelItemIndex: number
    ): PlatformFormSingleControlOrGroupItem<TItemModel> {
        const formControlOrGroup = formConfigControlsConfigArrayItem.itemControl(modelItem, modelItemIndex);

        const result =
            formControlOrGroup instanceof FormControl
                ? <PlatformFormSingleControlOrGroupItem<TItemModel>>formControlOrGroup
                : <PlatformFormSingleControlOrGroupItem<TItemModel>>(
                      new FormGroup(<PlatformFormGroupControls<TItemModel>>formControlOrGroup)
                  );

        // Setup form array item validations
        if (
            (formConfigControlsConfigArrayItem.groupValidations != undefined ||
                formConfigControlsConfigArrayItem.dependentValidations != undefined) &&
            result instanceof FormGroup
        ) {
            keys(result.controls).forEach(formControlKey => {
                this.cancelStoredSubscription(
                    buildFromArrayControlItemValueChangesSubscriptionKey(
                        formArrayControlKey,
                        formControlKey,
                        modelItemIndex
                    )
                );
                this.storeSubscription(
                    buildFromArrayControlItemValueChangesSubscriptionKey(
                        formArrayControlKey,
                        formControlKey,
                        modelItemIndex
                    ),
                    (<FormControl>(<Dictionary<unknown>>result.controls)[formControlKey]).valueChanges
                        .pipe(
                            delay(PlatformFormComponent.updateVmOnFormValuesChangeDelayMs, asyncScheduler), // Delay PlatformFormComponent.updateVmOnFormValuesChangeDelayMs to push item in async queue to ensure control value has been updated
                            throttleTime(PlatformComponent.defaultDetectChangesThrottleTime, asyncScheduler, {
                                leading: true,
                                trailing: true
                            }),
                            distinctUntilObjectValuesChanged()
                        )
                        .subscribe(value => {
                            this.processGroupValidation(
                                formConfigControlsConfigArrayItem.groupValidations,
                                `formArrayControl_${formArrayControlKey.toString()}`,
                                result,
                                <keyof TItemModel>formControlKey
                            );
                            this.processDependentValidations(
                                formConfigControlsConfigArrayItem.dependentValidations,
                                `formArrayControl_${formArrayControlKey.toString()}`,
                                result,
                                <keyof TItemModel>formControlKey
                            );
                        })
                );
            });
        }

        function buildFromArrayControlItemValueChangesSubscriptionKey(
            formArrayControlKey: keyof TFormModel,
            formControlKey: string,
            modelItemIndex: number
        ): string {
            return `buildFromArrayControlItemValueChanges_${formArrayControlKey.toString()}_${formControlKey}_${modelItemIndex}`;
        }

        return result;
    }
}

/**
 * Configuration object for the platform form component, specifying form controls, validations, and additional settings.
 *
 * @typeparam TFormModel - The type representing the view model for the form.
 */
export type PlatformFormConfig<TFormModel> = {
    /**
     * Definition of form controls and their configuration within the reactive form.
     *
     * @remarks
     * This property defines the structure and configuration of form controls in the reactive form,
     * including their initial values, validators, and other settings.
     * The shape of this property corresponds to the view model type `TFormModel`.
     *
     * @example
     * // Example usage:
     * controls: {
     *    username: new FormControl('', [Validators.required, Validators.minLength(3)]),
     *    email: new FormControl('', [Validators.required, Validators.email]),
     *    // ... other form controls ...
     * }
     */
    controls: PlatformPartialFormGroupControlsConfig<TFormModel>;

    /**
     * Optional group-level validations specifying relationships between form controls.
     *
     * @remarks
     * This property allows defining group validations, where the validity of one or more form controls
     * is dependent on the state of other form controls. It is an array of arrays, where each inner array
     * contains the keys (property names) of the form controls involved in the group validation.
     *
     * @example
     * // Example usage:
     * groupValidations: [
     *    ['password', 'confirmPassword'], // Validation for matching passwords
     *    // ... other group validations ...
     * ]
     */
    groupValidations?: (keyof TFormModel)[][];

    /**
     * Used to config that one control key validation is depended on other control values changes.
     *
     * @remarks
     * This property allows configuring dependent validations, where the validation of a specific form control
     * is triggered when the values or states of other specified form controls change.
     * It is represented as a partial record where keys are the control keys to be validated,
     * and values are arrays of control keys whose changes trigger the validation.
     *
     * @example
     * dependentValidations: {
     *    dependentProp: ['dependedOnProp1', 'dependedOnProp2']
     * }
     *
     * This mean that dependentProp will trigger validation when dependedOnProp1 or dependedOnProp2 changed
     */
    dependentValidations?: Partial<Record<keyof TFormModel, (keyof TFormModel)[] | undefined>>;

    /**
     * Callback function executed after the initialization of the form.
     *
     * @remarks
     * This property allows executing custom logic or actions after the platform form component has been initialized.
     * It is useful for performing additional setup or handling specific scenarios post-form initialization.
     *
     * @example
     * // Example usage:
     * afterInit: () => {
     *    console.log('Form initialized successfully');
     *    // ... other initialization logic ...
     * }
     */
    afterInit?: () => void;

    /**
     * Callback function returning an array of child form components or their query lists. Define validateForm help when form run validation, it will invoke validation of all childForms, also affect logic check isFormValid mean that all childForms need to be valid too
     *
     * @remarks
     * This property is a callback function that, when provided, returns an array of child form components
     * or their query lists. It is useful for obtaining references to child forms within the platform form component.
     *
     * @example
     * // Example usage:
     * childForms: () => [childForm1, childForm2, ...],
     * // or
     * childForms: () => formQueryList.toArray(),
     */
    childForms?: () => (QueryList<IPlatformFormComponent<IPlatformVm>> | IPlatformFormComponent<IPlatformVm>)[];
};

export type PlatformPartialFormGroupControlsConfig<TFormModel> = {
    [P in keyof TFormModel]?: TFormModel[P] extends unknown[]
        ?
              | FormControl<TFormModel[P] | null | undefined>
              | PlatformFormGroupControlConfigPropArray<ArrayElement<TFormModel[P]>>
        : FormControl<TFormModel[P] | null | undefined>;
};

// Need to be code duplicated used in "export type PlatformPartialFormGroupControlsConfig<TFormModel> = {"
// "[P in keyof TFormModel]?: TFormModel[P] ..." should be equal to PlatformFormGroupControlConfigProp<TFormModel[P]>
// dont know why it will get type errors when using if TFormModel[P] is enum
export type PlatformFormGroupControlConfigProp<TFormModelProp> = TFormModelProp extends unknown[]
    ? FormControl<TFormModelProp> | PlatformFormGroupControlConfigPropArray<ArrayElement<TFormModelProp>>
    : FormControl<TFormModelProp | null | undefined>;

export type PlatformFormGroupControlConfigPropArray<TItemModel> = {
    modelItems: () => TItemModel[];
    itemControl: PlatformFormGroupControlConfigPropArrayItemControlFn<TItemModel>;
    /**
     * Optional group-level validations specifying relationships between form controls.
     *
     * @remarks
     * This property allows defining group validations, where the validity of one or more form controls
     * is dependent on the state of other form controls. It is an array of arrays, where each inner array
     * contains the keys (property names) of the form controls involved in the group validation.
     *
     * @example
     * // Example usage:
     * groupValidations: [
     *    ['password', 'confirmPassword'], // Validation for matching passwords
     *    // ... other group validations ...
     * ]
     */
    groupValidations?: (keyof TItemModel)[][];

    /**
     * Used to config that one control key validation is depended on other control values changes.
     *
     * @remarks
     * This property allows configuring dependent validations, where the validation of a specific form control
     * is triggered when the values or states of other specified form controls change.
     * It is represented as a partial record where keys are the control keys to be validated,
     * and values are arrays of control keys whose changes trigger the validation.
     *
     * @example
     * dependentValidations: {
     *    dependentProp: ['dependedOnProp1', 'dependedOnProp2']
     * }
     *
     * This mean that dependentProp will trigger validation when dependedOnProp1 or dependedOnProp2 changed
     */
    dependentValidations?: Partial<Record<keyof TItemModel, (keyof TItemModel)[] | undefined>>;
    validators?: ValidatorFn | ValidatorFn[] | null;
};

export type PlatformFormGroupControlConfigPropArrayItemControlFn<TItemModel> = (
    item: TItemModel,
    itemIndex: number
) => PlatformPartialFormGroupControls<TItemModel> | FormControl<TItemModel>;

export type PlatformFormGroupControls<TFormModel> = {
    [P in keyof TFormModel]: TFormModel[P] extends unknown[]
        ? FormControl<TFormModel[P]> | PlatformFormGroupControlsFormArray<TFormModel[P]>
        : FormControl<TFormModel[P] | null | undefined>;
};

// Temp comment to find out how to support inner child form group
export type PlatformFormSingleControlOrGroupItem<T> =
    | FormControl<T | null | undefined>
    | FormGroup<PlatformFormGroupControls<T>>;

export type PlatformFormGroupControlsFormArray<T> = FormArray<PlatformFormSingleControlOrGroupItem<ArrayElement<T>>>;

export type PlatformPartialFormGroupControls<TFormModel> = {
    [P in keyof TFormModel]?: TFormModel[P] extends unknown[]
        ? FormControl<TFormModel[P]> | FormArray<PlatformFormSingleControlOrGroupItem<ArrayElement<TFormModel[P]>>>
        : FormControl<TFormModel[P] | null | undefined>;
};

// Need to be code duplicated used in "export type PlatformFormGroupControls<TFormModel> = {", "export type
// PlatformPartialFormGroupControls<TFormModel> = {" "[P in keyof TFormModel]: TFormModel[P] ..." should be equal to
// PlatformFormGroupControlProp<TFormModel[P]> dont know why it will get type errors when using if TFormModel[P] is
// enum, boolean
export type PlatformFormGroupControlProp<TFormModelProp> = TFormModelProp extends unknown[]
    ? FormControl<TFormModelProp> | PlatformFormGroupControlsFormArray<TFormModelProp>
    : FormControl<TFormModelProp | null | undefined>;
