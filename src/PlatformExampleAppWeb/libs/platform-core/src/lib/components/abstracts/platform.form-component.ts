/* eslint-disable @typescript-eslint/no-explicit-any */
import { Directive, Input, OnInit, QueryList } from '@angular/core';
import { FormArray, FormControl, FormGroup } from '@angular/forms';
import { asyncScheduler, filter, throttleTime } from 'rxjs';
import { ArrayElement } from 'type-fest/source/exact';

import { IPlatformFormValidationError } from '../../form-validators';
import { FormHelpers } from '../../helpers';
import { immutableUpdate, isDifferent, keys, task_delay, toPlainObj } from '../../utils';
import { IPlatformVm, PlatformFormMode } from '../../view-models';
import { PlatformVmComponent } from './platform.vm-component';

export interface IPlatformFormComponent<TViewModel extends IPlatformVm> {
    isFormValid(): boolean;

    isAllChildFormsValid(forms: QueryList<IPlatformFormComponent<IPlatformVm>>[]): boolean;

    validateForm(): boolean;

    validateAllChildForms(forms: QueryList<IPlatformFormComponent<IPlatformVm>>[]): boolean;

    formControls(key: keyof TViewModel): FormControl;

    formControlsError(controlKey: keyof TViewModel, errorKey: string): IPlatformFormValidationError | null;
}

@Directive()
export abstract class PlatformFormComponent<TViewModel extends IPlatformVm>
    extends PlatformVmComponent<TViewModel>
    implements IPlatformFormComponent<TViewModel>, OnInit
{
    public constructor() {
        super();
    }

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
            this.patchVmValuesToForm(this.vm);
            this.validateForm();
        }
    }

    @Input() public form!: FormGroup<PlatformFormGroupControls<TViewModel>>;
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

    protected abstract initialFormConfig: () => PlatformFormConfig<TViewModel> | undefined;

    public override ngOnInit(): void {
        super.ngOnInit();

        // If form and formConfig has NOT been given via input
        if (!this.formConfig && !this.form) {
            if (this.initiated$.value) {
                this.initForm();
            } else {
                // Init empty form
                this.form = new FormGroup<PlatformFormGroupControls<TViewModel>>(<any>{});

                this.storeAnonymousSubscription(
                    this.initiated$.pipe(filter(initiated => initiated)).subscribe(() => {
                        this.initForm(true);
                    })
                );
            }
        }
    }

    public override reload() {
        this.initVm(true);
        this.initForm(true);
        this.clearErrorMsg();
    }

    protected initForm(forceReinit: boolean = false) {
        if (this.formConfig && this.form && !forceReinit) return;

        const initialFormConfig = this.initialFormConfig();
        if (initialFormConfig == undefined)
            throw new Error('initialFormConfig must not be undefined or formConfig and form must be input');

        this.formConfig = initialFormConfig;
        this.form = this.buildForm(this.formConfig);

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
                (<FormControl>(<any>this.form.controls)[formControlKey]).valueChanges
                    .pipe(throttleTime(300, asyncScheduler, { leading: true, trailing: true }))
                    .subscribe(value => {
                        this.updateVmOnFormValuesChange(<Partial<TViewModel>>{ [formControlKey]: value });
                        this.processGroupValidation(<keyof TViewModel>formControlKey);
                        this.processDependentValidations(<keyof TViewModel>formControlKey);
                    })
            );
        });

        this.patchVmValuesToForm(this.vm, false);

        if (this.isViewMode) this.form.disable();

        if (this.formConfig.afterInit) this.formConfig.afterInit();

        function buildControlValueChangesSubscriptionKey(formControlKey: string): string {
            return `initForm_${formControlKey}_valueChanges`;
        }
    }

    protected override internalSetVm = (v: TViewModel, shallowCheckDiff: boolean = true): void => {
        if (shallowCheckDiff == false || this._vm != v) {
            this._vm = v;

            if (this.initiated$.value) {
                this.patchVmValuesToForm(v);
                this.detectChanges();
                this.vmChangeEvent.emit(v);
            }
        }
    };

    public isFormValid(): boolean {
        // form or formConfig if it's initiated asynchronous, waiting call api but the component template use isFormValid
        // so that it could be undefined. check to prevent the bug
        return (
            this.form?.valid &&
            (this.formConfig?.childForms == undefined || this.isAllChildFormsValid(this.formConfig.childForms()))
        );
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
        return (
            FormHelpers.isFormValid(this.form) &&
            (this.formConfig.childForms == undefined || this.validateAllChildForms(this.formConfig.childForms()))
        );
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

    public patchVmValuesToForm(vm: TViewModel, runFormValidation: boolean = true): void {
        const vmFormValues: Partial<TViewModel> = this.getFromVmFormValues(vm);
        const currentReactiveFormValues: Partial<TViewModel> = this.getCurrentReactiveFormControlValues();

        keys(vmFormValues).forEach(formKey => {
            const vmFormKeyValue = (<any>vmFormValues)[formKey];
            const formControl = (<any>this.form.controls)[formKey];

            if (isDifferent(vmFormKeyValue, (<any>currentReactiveFormValues)[formKey])) {
                if (
                    formControl instanceof FormArray &&
                    vmFormKeyValue instanceof Array &&
                    formControl.length != vmFormKeyValue.length
                ) {
                    formControl.clear({ emitEvent: false });
                    vmFormKeyValue.forEach((modelItem, index) =>
                        formControl.push(
                            this.buildFromArrayControlItem((<any>this.formConfig.controls)[formKey], modelItem, index),
                            {
                                emitEvent: false
                            }
                        )
                    );
                }

                this.form.patchValue(<any>{ [formKey]: vmFormKeyValue }, { emitEvent: false });

                if (!this.isViewMode && runFormValidation) {
                    this.processGroupValidation(formKey);
                    this.processDependentValidations(formKey);
                }
            }
        });
    }

    public getCurrentReactiveFormControlValues(): Partial<TViewModel> {
        const reactiveFormValues: Partial<TViewModel> = {};

        keys(this.formConfig.controls).forEach(formControlKey => {
            (<any>reactiveFormValues)[formControlKey] = (<any>this.form.controls)[formControlKey].value;
        });

        return reactiveFormValues;
    }

    public getFromVmFormValues(vm: TViewModel): Partial<TViewModel> {
        const vmFormValues: Partial<TViewModel> = {};

        keys(this.formConfig.controls).forEach(formControlKey => {
            (<any>vmFormValues)[formControlKey] = (<any>vm)[formControlKey];
        });

        // To toPlainObj to ensure removing getter/setter which help angular lib can read prop keys and apply data from vm
        // to form
        return toPlainObj(vmFormValues);
    }

    public formControls(key: keyof TViewModel): FormControl {
        return <FormControl>this.form.get(<string>key);
    }

    public formControlsError(
        controlKey: keyof TViewModel,
        errorKey: string,
        onlyWhenTouchedOrDirty: boolean = false
    ): IPlatformFormValidationError | null {
        if (onlyWhenTouchedOrDirty && this.form.touched == false && this.form.dirty == false) return null;
        return this.formControls(controlKey)?.errors?.[errorKey];
    }

    public processGroupValidation(formControlKey: keyof TViewModel) {
        if (this.formConfig.groupValidations == null) return;

        this.cancelStoredSubscription(`processGroupValidation_${formControlKey.toString()}`);

        this.storeSubscription(
            `processGroupValidation_${formControlKey.toString()}`,
            task_delay(() => {
                if (this.formConfig.groupValidations == null) return;

                this.formConfig.groupValidations.forEach(groupValidators => {
                    if (groupValidators.includes(formControlKey))
                        groupValidators.forEach(groupValidatorControlKey => {
                            this.formControls(groupValidatorControlKey).updateValueAndValidity({
                                emitEvent: false,
                                onlySelf: false
                            });
                        });
                });
                this.detectChanges();
            }, 300)
        );
    }

    public processDependentValidations(formControlKey: keyof TViewModel) {
        if (this.formConfig.dependentValidations == null) return;

        this.cancelStoredSubscription(`processDependentValidations_${formControlKey.toString()}`);

        this.storeSubscription(
            `processDependentValidations_${formControlKey.toString()}`,
            task_delay(() => {
                if (this.formConfig.dependentValidations == undefined) return;

                const dependentValidationsConfig = this.formConfig.dependentValidations;

                Object.keys(dependentValidationsConfig).forEach(dependentValidationControlKey => {
                    const dependedOnOtherControlKeys = (<any>dependentValidationsConfig)[dependentValidationControlKey];

                    if (dependedOnOtherControlKeys.includes(formControlKey)) {
                        this.formControls(<keyof TViewModel>dependentValidationControlKey).updateValueAndValidity({
                            emitEvent: false,
                            onlySelf: false
                        });
                        this.detectChanges();
                    }
                });
            }, 300)
        );
    }

    protected formGroupArrayFor<TItemModel>(
        items: TItemModel[],
        formItemGroupControls: (item: TItemModel) => PlatformPartialFormGroupControls<TItemModel>
    ): FormArray<FormGroup<PlatformFormGroupControls<TItemModel>>> {
        return new FormArray(
            items.map(item => new FormGroup(<PlatformFormGroupControls<TItemModel>>formItemGroupControls(item)))
        );
    }

    protected formControlArrayFor<TItemModel>(
        items: TItemModel[],
        formItemControl: (item: TItemModel) => FormControl<TItemModel>
    ): FormArray<FormControl<TItemModel>> {
        return new FormArray(items.map(item => formItemControl(item)));
    }

    protected updateVmOnFormValuesChange(values: Partial<TViewModel>) {
        const newUpdatedVm: TViewModel = immutableUpdate(this.vm, values);

        if (newUpdatedVm != this.vm) {
            this.internalSetVm(newUpdatedVm, false);
        }
    }

    protected buildForm(formConfig: PlatformFormConfig<TViewModel>): FormGroup<PlatformFormGroupControls<TViewModel>> {
        const controls = <PlatformFormGroupControls<TViewModel>>{};

        keys(formConfig.controls).forEach(key => {
            const formConfigControlsConfigItem: PlatformFormGroupControlConfigProp<unknown> = (<any>(
                formConfig.controls
            ))[key];
            const formConfigControlsConfigArrayItem = <PlatformFormGroupControlConfigPropArray<unknown>>(
                (<any>formConfig.controls)[key]
            );

            if (formConfigControlsConfigItem instanceof FormControl) {
                (<any>controls)[key] = formConfigControlsConfigItem;
            } else if (
                formConfigControlsConfigArrayItem.itemControl != undefined &&
                formConfigControlsConfigArrayItem.modelItems != undefined
            ) {
                (<any>controls)[key] = new FormArray(
                    formConfigControlsConfigArrayItem.modelItems().map((modelItem, index) => {
                        return this.buildFromArrayControlItem(formConfigControlsConfigArrayItem, modelItem, index);
                    })
                );
            }
        });

        return new FormGroup(controls);
    }

    protected buildFromArrayControlItem(
        formConfigControlsConfigArrayItem: PlatformFormGroupControlConfigPropArray<unknown>,
        modelItem: unknown,
        modelItemIndex: number
    ) {
        const itemControl = formConfigControlsConfigArrayItem.itemControl(modelItem, modelItemIndex);
        return itemControl instanceof FormControl ? itemControl : new FormGroup(itemControl);
    }
}

export type PlatformFormConfig<TFormModel> = {
    controls: PlatformPartialFormGroupControlsConfig<TFormModel>;
    groupValidations?: (keyof TFormModel)[][];

    /**
     * Used to config that one control key validation is depended on other control values changes.
     *
     * Example:
     * dependentValidations: {
     *    dependentProp: ['dependedOnProp1', 'dependedOnProp2']
     * }
     *
     * This mean that dependentProp will trigger validation when dependedOnProp1 or dependedOnProp2 changed
     */
    dependentValidations?: Partial<Record<keyof TFormModel, (keyof TFormModel)[]>>;
    afterInit?: () => void;
    childForms?: () => (QueryList<IPlatformFormComponent<IPlatformVm>> | IPlatformFormComponent<IPlatformVm>)[];
};

export type PlatformPartialFormGroupControlsConfig<TFormModel> = {
    [P in keyof TFormModel]?: TFormModel[P] extends readonly unknown[]
        ? FormControl<TFormModel[P]> | PlatformFormGroupControlConfigPropArray<ArrayElement<TFormModel[P]>>
        : FormControl<TFormModel[P]>;
};

// Need to be code duplicated used in "export type PlatformPartialFormGroupControlsConfig<TFormModel> = {"
// "[P in keyof TFormModel]?: TFormModel[P] ..." should be equal to PlatformFormGroupControlConfigProp<TFormModel[P]>
// dont know why it will get type errors when using if TFormModel[P] is enum
export type PlatformFormGroupControlConfigProp<TFormModelProp> = TFormModelProp extends readonly unknown[]
    ? FormControl<TFormModelProp> | PlatformFormGroupControlConfigPropArray<ArrayElement<TFormModelProp>>
    : FormControl<TFormModelProp>;

export type PlatformFormGroupControlConfigPropArray<TItemModel> = {
    modelItems: () => TItemModel[];
    itemControl: (
        item: TItemModel,
        itemIndex: number
    ) => PlatformPartialFormGroupControls<TItemModel> | FormControl<TItemModel>;
};

export type PlatformFormGroupControls<TFormModel> = {
    [P in keyof TFormModel]: TFormModel[P] extends readonly unknown[]
        ?
              | FormControl<TFormModel[P]>
              | FormArray<FormControl<ArrayElement<TFormModel[P]>>>
              | FormArray<FormGroup<PlatformFormGroupControls<ArrayElement<TFormModel[P]>>>>
        : FormControl<TFormModel[P]>;
};

export type PlatformPartialFormGroupControls<TFormModel> = {
    [P in keyof TFormModel]?: TFormModel[P] extends readonly unknown[]
        ?
              | FormControl<TFormModel[P]>
              | FormArray<FormControl<ArrayElement<TFormModel[P]>>>
              | FormArray<FormGroup<PlatformFormGroupControls<ArrayElement<TFormModel[P]>>>>
        : FormControl<TFormModel[P]>;
};

// Need to be code duplicated used in "export type PlatformFormGroupControls<TFormModel> = {", "export type
// PlatformPartialFormGroupControls<TFormModel> = {" "[P in keyof TFormModel]: TFormModel[P] ..." should be equal to
// PlatformFormGroupControlProp<TFormModel[P]> dont know why it will get type errors when using if TFormModel[P] is
// enum, boolean
export type PlatformFormGroupControlProp<TFormModelProp> = TFormModelProp extends readonly unknown[]
    ?
          | FormControl<TFormModelProp>
          | FormArray<FormControl<ArrayElement<TFormModelProp>>>
          | FormArray<FormGroup<PlatformFormGroupControls<ArrayElement<TFormModelProp>>>>
    : FormControl<TFormModelProp>;
