import {
    computed,
    Directive,
    EnvironmentInjector,
    inject,
    OnInit,
    runInInjectionContext,
    Signal,
    untracked
} from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';

import { combineLatest, map, Observable } from 'rxjs';
import { PartialDeep } from 'type-fest';

import { keys, list_all, list_distinct } from '../../utils';
import { PlatformVm, PlatformVmStore, requestStateDefaultKey } from '../../view-models';
import { PlatformComponent, PlatformObserverLoadingErrorStateOptions } from './platform.component';

/**
 * @classdesc
 * Abstract class `PlatformVmStoreComponent` is designed as a base class for Angular components in TypeScript. It extends the abstract class `PlatformComponent` and implements the `OnInit` interface.
 *
 * @class
 * @abstract
 * @extends PlatformComponent
 * @implements OnInit
 * @template TViewModel - Generic type extending `PlatformVm`.
 * @template TViewModelStore - Generic type extending `PlatformVmStore<TViewModel>`.
 *
 * @constructor
 * @param {TViewModelStore} store - The store of type `TViewModelStore` for initializing the component.
 *
 * @property {PlatformVmStore<PlatformVm>[]} additionalStores - Mechanism to handle additional stores besides the main store.
 * @property {Observable<TViewModel>} state$ - Observable representing the root state of the store.
 * @property {Observable<TViewModel>} vm$ - Observable representing the state of the store with a valid loaded value for UI.
 * @property {Signal<boolean>} isStatePending$ - Signal indicating whether the state is pending.
 * @property {Signal<boolean>} isStateLoading$ - Signal indicating whether the state is loading.
 * @property {Signal<boolean>} isStateSuccess$ - Signal indicating whether the state is successful.
 * @property {Signal<boolean>} isStateError$ - Signal indicating whether an error has occurred in the state.
 * @property {boolean} isStatePending - Getter for checking if the state is pending.
 * @property {boolean} isStateLoading - Getter for checking if the state is loading.
 * @property {boolean} isStateSuccess - Getter for checking if the state is successful.
 * @property {boolean} isStateError - Getter for checking if an error has occurred in the state.
 * @property {TViewModel} currentState - Getter for accessing the current state.
 *
 * @method ngOnInit - Overrides the `ngOnInit` lifecycle hook to perform initialization tasks.
 * @method reload - Initiates a reload of data in the main store and additional stores.
 * @method updateVm - Updates the state of the main store with a partial state or updater function.
 * @method getErrorMsg$ - Returns an observable for retrieving error messages for a specific request key.
 * @method setErrorMsg - Sets the error message for a specific request key in the component's state.
 * @method observerLoadingErrorState - Observes the loading state of a request and updates the component's state accordingly.
 * @method effect - Creates an effect to define loading/updating data methods in the store.
 * @method switchMapVm - Maps the emitted value of the source observable to the current view model state.
 * @method tapResponse - Creates an RxJS operator function to tap into the source observable.
 * @method storeSubscription - Stores a subscription using the specified key.
 * @method storeAnonymousSubscription - Stores an anonymous subscription.
 * @method subscribe - Subscribes to the provided observable and stores the subscription.
 * @method cancelStoredSubscription - Cancels and removes a stored subscription identified by the provided key.
 * @method cancelAllStoredSubscriptions - Cancels and removes all stored subscriptions.
 * @method get - Returns the current state of the store.
 * @method subscribeCacheStateOnChanged - Subscribes to the `vm$` observable to store the component's state in the cache.
 * @method getCachedStateKey - Returns the key used for caching the state.
 * @method getCachedState - Retrieves the cached state of the component.
 */
@Directive()
export abstract class PlatformVmStoreComponent<
        TViewModel extends PlatformVm,
        TViewModelStore extends PlatformVmStore<TViewModel>
    >
    extends PlatformComponent
    implements OnInit
{
    private environmentInjector = inject(EnvironmentInjector);

    public constructor(public store: TViewModelStore) {
        super();
    }

    private _additionalStores?: PlatformVmStore<PlatformVm>[];
    public get additionalStores(): PlatformVmStore<PlatformVm>[] {
        if (this._additionalStores == null) {
            const mainStoreKey: keyof PlatformVmStoreComponent<PlatformVm, PlatformVmStore<PlatformVm>> = 'store';

            // ignore ['additionalStores', 'isStateError$', 'isStateLoading$', 'errorMsg$'] to prevent maximum call stack error
            // use keys => access to properies => trigger it self a gain. isStateError, isStateLoading are using additionalStores
            // is also affected
            this._additionalStores = keys(this, true, [
                'additionalStores',
                'isStateError',
                'isStateLoading',
                'isStateReloading',
                'isStateSuccess',
                'isStatePending',
                'errorMsg$',
                'state$',
                'vm',
                'vm$'
            ])
                .filter(key => this[key] instanceof PlatformVmStore && key != mainStoreKey)
                .map(key => <PlatformVmStore<PlatformVm>>this[key]);
        }

        return this._additionalStores;
    }

    public override ngOnInit(): void {
        super.ngOnInit();
        this.store.initInnerStore();

        if (
            this.store.vmStateInitiated &&
            (this.store.currentState().isStateSuccess || this.store.currentState().isStateError) &&
            list_all(this.additionalStores, p => p.currentState().isStateSuccess || p.currentState().isStateError)
        )
            this.reload();

        this.ngOnInitCalled$.next(true);
    }

    private _state$?: Observable<TViewModel>;
    /**
     * State is the root state of the store. Use this to check anything without need to wait the "vm" loaded.
     * Vm are actually still a state but it's a state with valid loaded value to show
     */
    public get state$(): Observable<TViewModel> {
        if (this._state$ == undefined) this._state$ = this.store.state$.pipe(this.untilDestroyed());

        return this._state$;
    }

    private _vm$?: Observable<TViewModel>;
    /**
     * Vm State is the state of the store, but it's a state with valid loaded value to show on UI.
     * Subscribe to this observable could also trigger load data. So should only subscribe it once on UI
     */
    public get vm$(): Observable<TViewModel> {
        if (this._vm$ == undefined) this._vm$ = this.store.vm$.pipe(this.untilDestroyed());

        return this._vm$;
    }

    private _vm?: Signal<TViewModel | undefined>;
    /**
     * Vm signal from vm$
     */
    public get vm(): Signal<TViewModel | undefined> {
        if (this._vm == undefined) {
            //untracked to fix NG0602: A disallowed function is called inside a reactive context
            untracked(() => {
                // toSignal must be used in an injection context
                runInInjectionContext(this.environmentInjector, () => {
                    this._vm = toSignal(this.vm$);

                    // Init all additionalStores signal by accessing it
                    this.additionalStores.forEach(p => p.vm());
                });
            });
        }

        return this._vm!;
    }

    public override get isStatePending(): Signal<boolean> {
        return this.store.isStatePending;
    }

    public override get isStateLoading(): Signal<boolean> {
        if (this._isStateLoading == null) {
            //untracked to fix NG0602: A disallowed function is called inside a reactive context
            untracked(() => {
                // toSignal must be used in an injection context
                runInInjectionContext(this.environmentInjector, () => {
                    this._isStateLoading = toSignal(
                        combineLatest(
                            this.additionalStores
                                .concat([<PlatformVmStore<PlatformVm>>(<unknown>this.store)])
                                .map(store => store.isStateLoading$)
                        ).pipe(
                            this.untilDestroyed(),
                            map(isLoadings => isLoadings.find(isLoading => isLoading) != undefined)
                        ),
                        { initialValue: false }
                    );
                });
            });
        }

        return this._isStateLoading!;
    }

    public override get isStateReloading(): Signal<boolean> {
        if (this._isStateReloading == null) {
            //untracked to fix NG0602: A disallowed function is called inside a reactive context
            untracked(() => {
                // toSignal must be used in an injection context
                runInInjectionContext(this.environmentInjector, () => {
                    this._isStateReloading = toSignal(
                        combineLatest(
                            this.additionalStores
                                .concat([<PlatformVmStore<PlatformVm>>(<unknown>this.store)])
                                .map(store => store.isStateReloading$)
                        ).pipe(
                            this.untilDestroyed(),
                            map(isReloadings => isReloadings.find(isReloading => isReloading) != undefined)
                        ),
                        { initialValue: false }
                    );
                });
            });
        }

        return this._isStateReloading!;
    }

    public override get isStateInitVmLoading(): Signal<boolean> {
        this._isStateInitVmLoading ??= computed(() => this.isStateLoading() == true && this.vm() == undefined);
        return this._isStateInitVmLoading;
    }

    public override get isStateSuccess(): Signal<boolean> {
        return this.store.isStateSuccess;
    }

    public override get isStateError(): Signal<boolean> {
        if (this._isStateError == null) {
            //untracked to fix NG0602: A disallowed function is called inside a reactive context
            untracked(() => {
                // toSignal must be used in an injection context
                runInInjectionContext(this.environmentInjector, () => {
                    this._isStateError = toSignal(
                        combineLatest(
                            this.additionalStores
                                .concat([<PlatformVmStore<PlatformVm>>(<unknown>this.store)])
                                .map(store => store.isStateError$)
                        ).pipe(
                            this.untilDestroyed(),
                            map(isErrors => isErrors.find(isError => isError) == true)
                        ),
                        { initialValue: false }
                    );
                });
            });
        }
        return this._isStateError!;
    }

    public currentVm(): TViewModel {
        return this.store.currentState();
    }

    /**
     * Returns a Signal for retrieving error messages for a specific request key.
     *
     * @remarks
     * This method retrieves an observable that emits error messages associated with a particular request key from the main store and additional stores. It is part of the error handling mechanism in the `PlatformVmStoreComponent`.
     *
     * @example
     * // Example usage in a derived class:
     * const errorSignal$ = this.getErrorMsg$('fetchData');
     * errorSignal$.subscribe(errorMessage => {
     *    if (errorMessage) {
     *        console.error(`Error fetching data: ${errorMessage}`);
     *    }
     * });
     *
     * @param {string} requestKey - The request key for which error messages are retrieved. Defaults to the default request state key.
     * @returns {Signal<string | undefined>} - A Signal observable emitting the error message associated with the specified request key.
     *
     * @method
     * @public
     * @override
     * @throws {Error} Throws an error if the observable for the specified request key is not found.
     *
     * @typeparam {string} requestKey - The key associated with a specific request for error messages.
     */
    public override getErrorMsg$(requestKey: string = requestStateDefaultKey): Signal<string | undefined> {
        if (this.cachedErrorMsg$[requestKey] == null) {
            //untracked to fix NG0602: A disallowed function is called inside a reactive context
            untracked(() => {
                // toSignal must be used in an injection context
                runInInjectionContext(this.environmentInjector, () => {
                    this.cachedErrorMsg$[requestKey] = toSignal(
                        combineLatest(
                            this.additionalStores
                                .concat([<PlatformVmStore<PlatformVm>>(<unknown>this.store)])
                                .map(store => store.getErrorMsgObservable$(requestKey))
                        ).pipe(
                            this.untilDestroyed(),
                            map(errors => errors.find(p => p != null))
                        )
                    );
                });
            });
        }

        return this.cachedErrorMsg$[requestKey]!;
    }

    public override getAllErrorMsgs$(requestKeys?: string[], excludeKeys?: string[]): Signal<string | undefined> {
        const combinedCacheRequestKey = `${requestKeys != null ? JSON.stringify(requestKeys) : 'All'}_excludeKeys:${
            excludeKeys != null ? JSON.stringify(excludeKeys) : 'null'
        }`;

        if (this.cachedAllErrorMsgs$[combinedCacheRequestKey] == null) {
            //untracked to fix NG0602: A disallowed function is called inside a reactive context
            untracked(() => {
                // toSignal must be used in an injection context
                runInInjectionContext(this.environmentInjector, () => {
                    this.cachedAllErrorMsgs$[combinedCacheRequestKey] = toSignal(
                        combineLatest(
                            this.additionalStores
                                .concat([<PlatformVmStore<PlatformVm>>(<unknown>this.store)])
                                .map(store => store.getAllErrorMsgObservable$(requestKeys, excludeKeys))
                        ).pipe(
                            this.untilDestroyed(),
                            map(errors =>
                                list_distinct(errors.filter(msg => msg != null && msg.trim() != '')).join('; ')
                            )
                        )
                    );
                });
            });
        }

        return <Signal<string | undefined>>this.cachedAllErrorMsgs$[combinedCacheRequestKey];
    }

    /**
     * Updates the state of the main store with a partial state or updater function.
     * @method
     * @param {Partial<TViewModel> | ((prevState: TViewModel) => Partial<TViewModel>)} partialStateOrUpdater - The partial state or updater function.
     * @returns {void}
     */
    public updateVm(
        partialStateOrUpdaterFn:
            | PartialDeep<TViewModel>
            | Partial<TViewModel>
            | ((state: TViewModel) => void | PartialDeep<TViewModel> | Partial<TViewModel>),
        options?: { updaterNotDeepMutate?: boolean; assignDeepLevel?: number }
    ): void {
        this.store.updateState(partialStateOrUpdaterFn, options);
    }

    public override isLoading$(requestKey: string = requestStateDefaultKey): Signal<boolean | null> {
        if (this.cachedLoading$[requestKey] == null) {
            this.cachedLoading$[requestKey] = <Signal<boolean | null>>computed(() => {
                return (
                    this.store.isLoading$(requestKey)() ||
                    this.additionalStores.find(s => s.isLoading$(requestKey)()) != undefined
                );
            });
        }
        return this.cachedLoading$[requestKey]!;
    }

    public override isReloading$(requestKey: string = requestStateDefaultKey): Signal<boolean | null> {
        if (this.cachedReloading$[requestKey] == null) {
            this.cachedReloading$[requestKey] = <Signal<boolean | null>>computed(() => {
                return (
                    this.store.isReloading$(requestKey)() ||
                    this.additionalStores.find(s => s.isReloading$(requestKey)()) != undefined
                );
            });
        }
        return this.cachedReloading$[requestKey]!;
    }

    /**
     * Initiates a reload of data in the main store and additional stores.
     * @method
     * @returns {void}
     */
    public override reload(): void {
        this.store.reload();
        this.additionalStores.forEach(p => p.reload());
    }

    public override observerLoadingErrorState<T>(
        requestKey?: string,
        options?: PlatformObserverLoadingErrorStateOptions | undefined
    ): (source: Observable<T>) => Observable<T> {
        return this.store.observerLoadingErrorState(requestKey, options);
    }
}
