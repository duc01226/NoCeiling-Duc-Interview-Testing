import { Directive, EventEmitter, Input, OnInit, Output, signal, WritableSignal } from '@angular/core';

import { cloneDeep } from 'lodash-es';
import { Observable } from 'rxjs';
import { PartialDeep } from 'type-fest';

import { PlatformApiServiceErrorResponse } from '../../api-services';
import { immutableUpdate, isDifferent } from '../../utils';
import { IPlatformVm } from '../../view-models';
import { LoadingState, PlatformComponent } from './platform.component';

/**
 * Abstract class representing a platform view model component.
 * @extends PlatformComponent
 * @abstract
 *
 * Overview:
 *
 * The PlatformVmComponent class is an abstract class that represents a platform view model component in an Angular application. It extends the PlatformComponent class, providing additional functionality specific to view model components. This class is designed to be extended by concrete view model components, and it defines a set of common patterns for handling and managing the view model state.
 */
@Directive()
export abstract class PlatformVmComponent<TViewModel extends IPlatformVm> extends PlatformComponent implements OnInit {
    /**
     * Initializes an instance of the PlatformVmComponent class.
     */
    public constructor() {
        super();
    }

    private _vmSignal?: WritableSignal<TViewModel>;
    /**
     * Get the current view model signal.
     */
    public get vm(): WritableSignal<TViewModel> {
        this._vmSignal ??= signal(this._vm);
        return this._vmSignal;
    }

    protected _vm!: TViewModel;
    /**
     * Sets the view model and performs change detection if the new view model is different.
     */
    @Input('vm')
    public set vmInput(v: TViewModel) {
        if (isDifferent(this._vm, v)) {
            this.internalSetVm(v, false);
        }
    }

    /**
     * The original initialized view model.
     * @public
     */
    public originalInitVm!: TViewModel;

    /**
     * Event emitter for changes in the view model.
     * @public
     */
    @Output('vmChange')
    public vmChangeEvent = new EventEmitter<TViewModel>();

    /**
     * Angular lifecycle hook. Overrides the ngOnInit method to initialize the view model.
     * @public
     */
    public override ngOnInit(): void {
        this.initVm();
    }

    /**
     * Initializes the view model and subscribes to changes.
     * @public
     * @param forceReinit - Forces reinitialization of the view model.
     */
    public initVm(forceReinit: boolean = false, onSuccess?: () => unknown) {
        if (forceReinit) this.cancelStoredSubscription('initVm');

        const initialVm$ = this.onInitVm();

        if ((this.vm() == undefined || forceReinit) && initialVm$ != undefined) {
            if (initialVm$ instanceof Observable) {
                this.storeSubscription(
                    'initVm',
                    initialVm$.subscribe({
                        next: initialVm => {
                            this.internalSetVm(initialVm);
                            this.originalInitVm = cloneDeep(initialVm);
                            super.ngOnInit();

                            executeOnSuccessDelay.bind(this)();
                        },
                        error: (error: PlatformApiServiceErrorResponse | Error) => {
                            this.loadingState$.set(LoadingState.Error);
                            this.setErrorMsg(error);
                        }
                    })
                );
            } else {
                this.internalSetVm(initialVm$);
                this.originalInitVm = cloneDeep(initialVm$);
                super.ngOnInit();

                executeOnSuccessDelay.bind(this)();
            }
        } else {
            super.ngOnInit();

            executeOnSuccessDelay.bind(this)();
        }

        function executeOnSuccessDelay(this: PlatformVmComponent<TViewModel>) {
            // because we are using vm() signal, when internalSetVm => setTimeout to ensure the value
            // in vm signal is updated => then run onSuccess to make sure it works correctly if onSuccess logic is using vm signal value
            if (onSuccess != undefined)
                setTimeout(() => {
                    onSuccess();
                    this.detectChanges();
                });
        }
    }

    /**
     * Reloads the view model.
     * @public
     */
    public reload() {
        this.initVm(true);
        this.clearErrorMsg();
    }

    /**
     * Hook to be implemented by derived classes to provide the initial view model.
     * @protected
     */
    protected abstract onInitVm: () => TViewModel | undefined | Observable<TViewModel>;

    /**
     * Updates the view model with partial state or an updater function.
     * @protected
     * @param partialStateOrUpdaterFn - Partial state or updater function.
     * @returns The updated view model.
     */
    protected updateVm(
        partialStateOrUpdaterFn:
            | PartialDeep<TViewModel>
            | Partial<TViewModel>
            | ((state: TViewModel) => void | PartialDeep<TViewModel>)
    ): TViewModel {
        const newUpdatedVm: TViewModel = immutableUpdate(this._vm, partialStateOrUpdaterFn);

        if (newUpdatedVm != this._vm) {
            this.internalSetVm(newUpdatedVm);
        }

        return this._vm;
    }

    /**
     * Internal method to set the view model, perform change detection, and emit events.
     * @protected
     * @param v - The new view model.
     * @param shallowCheckDiff - Whether to shallow check for differences before updating.
     */
    protected internalSetVm = (v: TViewModel, shallowCheckDiff: boolean = true): void => {
        if (shallowCheckDiff == false || this._vm != v) {
            this._vm = v;
            this.vm.set(v);

            if (this.initiated$.value) this.vmChangeEvent.emit(v);
        }
    };
}
