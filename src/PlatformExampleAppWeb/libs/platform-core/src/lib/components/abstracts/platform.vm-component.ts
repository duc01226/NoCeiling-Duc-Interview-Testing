import {
    computed,
    Directive,
    EventEmitter,
    Input,
    OnInit,
    Output,
    Signal,
    signal,
    WritableSignal
} from '@angular/core';

import { cloneDeep } from 'lodash-es';
import { filter, map, Observable, share, take } from 'rxjs';
import { PartialDeep } from 'type-fest';

import { toObservable } from '@angular/core/rxjs-interop';
import { PlatformApiServiceErrorResponse } from '../../api-services';
import { distinctUntilObjectValuesChanged } from '../../rxjs';
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

    private _vmSignal?: WritableSignal<TViewModel | undefined>;
    /**
     * Get the current view model signal.
     */
    public get vm(): WritableSignal<TViewModel | undefined> {
        this._vmSignal ??= signal(this._vm);
        return this._vmSignal;
    }

    protected _vm?: TViewModel;
    /**
     * Sets the view model and performs change detection if the new view model is different.
     */
    @Input('vm')
    public set vmInput(v: TViewModel | undefined) {
        if (isDifferent(this._vm, v)) {
            this.internalSetVm(v, false);
        }
    }

    private _vm$?: Observable<TViewModel>;
    public vm$(): Observable<TViewModel> {
        this._vm$ ??= <Observable<TViewModel>>toObservable(this.vm).pipe(
            filter(p => p != undefined),
            this.untilDestroyed(),
            share()
        );

        return this._vm$;
    }

    public selectVm<T>(selector: (vm: TViewModel) => T): Observable<T> {
        return this.vm$().pipe(map(selector), distinctUntilObjectValuesChanged());
    }

    public currentVm() {
        if (this._vm == undefined) throw new Error('Vm is not initiated');
        return this._vm;
    }

    public override get isStateInitVmLoading(): Signal<boolean> {
        this._isStateInitVmLoading ??= computed(() => this.isStateLoading() == true && this.vm() == undefined);
        return this._isStateInitVmLoading;
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
        this.ngOnInitCalled$.next(true);
    }

    /**
     * Initializes the view model and subscribes to changes.
     * @public
     * @param forceReinit - Forces reinitialization of the view model.
     */
    public initVm(forceReinit: boolean = false, onSuccess?: () => unknown) {
        if (forceReinit) this.cancelStoredSubscription('initVm');

        const isReload = forceReinit && this._vm?.status == 'Success';
        const initialVm$ = this.initOrReloadVm(isReload);

        if ((this.vm() == undefined || forceReinit) && initialVm$ != undefined) {
            if (initialVm$ instanceof Observable) {
                this.storeSubscription(
                    'initVm',
                    initialVm$
                        .pipe(take(1), this.observerLoadingErrorState(undefined, { isReloading: isReload }))
                        .subscribe({
                            next: initialVm => {
                                if (initialVm) {
                                    autoInitVmStatus.bind(this)(initialVm);

                                    this.internalSetVm(initialVm);
                                    this.originalInitVm = cloneDeep(initialVm);
                                    super.ngOnInit();

                                    executeOnSuccessDelay.bind(this)();
                                } else {
                                    super.ngOnInit();

                                    executeOnSuccessDelay.bind(this)();
                                }
                            },
                            error: (error: PlatformApiServiceErrorResponse | Error) => {
                                this.loadingState$.set(LoadingState.Error);
                                this.setErrorMsg(error);
                            }
                        })
                );
            } else {
                autoInitVmStatus.bind(this)(initialVm$);

                this.internalSetVm(initialVm$);
                this.originalInitVm = cloneDeep(initialVm$);
                super.ngOnInit();

                executeOnSuccessDelay.bind(this)();
            }
        } else {
            super.ngOnInit();

            executeOnSuccessDelay.bind(this)();
        }

        function autoInitVmStatus(this: PlatformVmComponent<TViewModel>, initialVm: TViewModel) {
            // Init status auto default Success if first time init and status is Pending
            if (initialVm.status == 'Pending') {
                if (this._vm == undefined) initialVm.status = 'Success';
                else this.updateVm(<Partial<TViewModel>>{ status: 'Success' });
            }
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
    public override reload() {
        this.initVm(true);
        this.clearErrorMsg();
    }

    /**
     * Hook to be implemented by derived classes to provide the initial view model.
     * @protected
     */
    protected abstract initOrReloadVm: (isReload: boolean) => Observable<TViewModel | undefined>;

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
            | ((state: TViewModel) => void | PartialDeep<TViewModel>),
        onVmChanged?: (vm: TViewModel) => unknown
    ): TViewModel {
        if (this._vm == undefined) return this._vm!;

        const newUpdatedVm: TViewModel = immutableUpdate(this._vm, partialStateOrUpdaterFn);

        if (newUpdatedVm != this._vm) {
            this.internalSetVm(newUpdatedVm, true, onVmChanged);
        }

        return this._vm;
    }

    /**
     * Internal method to set the view model, perform change detection, and emit events.
     * @protected
     * @param v - The new view model.
     * @param shallowCheckDiff - Whether to shallow check for differences before updating.
     */
    protected internalSetVm = (
        v: TViewModel | undefined,
        shallowCheckDiff: boolean = true,
        onVmChanged?: (vm: TViewModel | undefined) => unknown
    ): boolean => {
        if (shallowCheckDiff == false || this._vm != v) {
            const prevVm = this._vm;

            this._vm = v;
            this.vm.set(v);

            if (this.initiated$.value || prevVm == undefined) this.vmChangeEvent.emit(v);

            if (onVmChanged != undefined) onVmChanged(this._vm);

            return true;
        }

        return false;
    };
}
