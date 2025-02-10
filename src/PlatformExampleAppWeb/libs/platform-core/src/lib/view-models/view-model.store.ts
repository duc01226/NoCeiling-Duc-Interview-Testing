/* eslint-disable @typescript-eslint/no-explicit-any */
import {
    EnvironmentInjector,
    inject,
    Injectable,
    OnDestroy,
    runInInjectionContext,
    Signal,
    untracked
} from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';

import {
    asyncScheduler,
    BehaviorSubject,
    catchError,
    combineLatest,
    defer,
    delay,
    filter,
    interval,
    isObservable,
    map,
    MonoTypeOperatorFunction,
    Observable,
    Observer,
    of,
    OperatorFunction,
    share,
    shareReplay,
    Subscription,
    switchMap,
    take,
    takeUntil,
    tap,
    throttleTime
} from 'rxjs';
import { PartialDeep } from 'type-fest';

import { ComponentStore, SelectConfig } from '@ngrx/component-store';

import { PlatformApiServiceErrorResponse } from '../api-services';
import { PlatformCachingService } from '../caching';
import { PLATFORM_CORE_GLOBAL_ENV } from '../platform-core-global-environment';
import {
    applyIf,
    distinctUntilObjectValuesChanged,
    onCancel,
    skipDuplicates,
    skipTime,
    subscribeUntil,
    tapLimit,
    tapOnce
} from '../rxjs';
import { immutableUpdate, ImmutableUpdateOptions, list_remove, toPlainObj } from '../utils';
import { PlatformVm } from './generic.view-model';

export const requestStateDefaultKey = 'Default';
const defaultThrottleDurationMs = 300;

declare interface PlatformStoreSelectConfig extends SelectConfig {
    throttleTimeDuration?: number;
}

/**
 * @classdesc
 * Abstract class `PlatformVmStore` is an Angular service designed to be extended for managing the state of view models. It implements the `OnDestroy` interface.
 *
 * @class
 * @abstract
 * @implements OnDestroy
 * @template TViewModel - Generic type extending `PlatformVm`.
 *
 * @constructor
 * @param {TViewModel} defaultState - The default state of the view model.
 *
 * @property {boolean} vmStateInitiating - Indicates whether the view model state is initiating.
 * @property {boolean} vmStateInitiated - Indicates whether the view model state is initiated.
 * @property {ComponentStore<TViewModel>} innerStore - The inner store used for managing the state of the view model.
 * @property {Observable<TViewModel>} vm$ - Observable representing the view model state.
 *
 * @method onInitVm - Abstract method to be implemented by subclasses, called during the initialization of the view model state.
 * @method vmConstructor - Abstract method to be implemented by subclasses, responsible for creating instances of the view model.
 * @method cachedStateKeyName - Abstract method to be implemented by subclasses, providing the key name for caching the state.
 * @method initVmState - Initializes the view model state by triggering `onInitVm` and reloading or initializing data.
 * @method ngOnDestroy - Lifecycle hook method that cleans up subscriptions and resources when the component is destroyed.
 * @method initOrReloadVm - Abstract method to be implemented by subclasses, triggering a reload or initialization of data.
 * @method updateState - Updates the state of the inner store with the provided partial state or updater function.
 * @method setErrorState - Sets the error state of the component with the provided error response or error.
 * @method buildSetErrorPartialState - Builds a partial state object with error details.
 * @method currentState - Gets the current state of the view model.
 * @method loadingRequestsCount - Gets the count of loading requests.
 * @method reloadingRequestsCount - Gets the count of reloading requests.
 * @method observerLoadingErrorState - Observes the loading state of a request and updates the component's state accordingly.
 * @method isForSetReloadingState - Checks if the loading state is for reloading based on options.
 * @method setErrorMsg - Sets the error message for a specific request key in the component's state.
 * @method getErrorMsg$ - Returns the error message observable for a specific request key.
 * @method setLoading - Sets the loading state for a specific request key in the component's state.
 * @method setReloading - Sets the reloading state for a specific request key in the component's state.
 * @method isLoading$ - Returns the loading state observable for a specific request key.
 * @method isReloading$ - Returns the reloading state observable for a specific request key.
 * @method select - Selects a slice of the component's state and returns an observable of the selected slice.
 * @method effect - Creates an effect for loading/updating data, returning a function to trigger the origin observable.
 * @method switchMapVm - Maps the emitted value of the source observable to the current view model state.
 * @method tapResponse - Creates an RxJS operator function that taps into the source observable to handle next, error, and complete events.
 * @method storeSubscription - Stores a subscription using the specified key.
 * @method storeAnonymousSubscription - Stores an anonymous subscription.
 * @method subscribe - Subscribes to the provided observable and stores the subscription.
 * @method cancelStoredSubscription - Cancels and removes a stored subscription identified by the provided key.
 * @method cancelAllStoredSubscriptions - Cancels and removes all stored subscriptions.
 * @method get - Returns the current state of the store.
 * @method subscribeCacheStateOnChanged - Subscribes to changes in the view model state and updates the cached state.
 * @method getCachedStateKey - Generates the key for caching the view model state.
 * @method getCachedState - Retrieves the cached view model state.
 *
 * @typedef {Object} PlatformVmObserverLoadingOptions - Options for observing loading state.
 * @property {Function} onShowLoading - Callback function to be executed when loading is shown.
 * @property {Function} onHideLoading - Callback function to be executed when loading is hidden.
 * @property {boolean} isReloading - Indicates whether the loading state is for reloading.
 */
@Injectable()
export abstract class PlatformVmStore<TViewModel extends PlatformVm> implements OnDestroy {
    private storedSubscriptionsMap: Map<string, Subscription> = new Map();
    private storedAnonymousSubscriptions: Subscription[] = [];
    private cachedErrorMsg$: Dictionary<Signal<string | undefined>> = {};
    private cachedErrorMsgObservable$: Dictionary<Observable<string | undefined>> = {};
    private cachedLoading$: Dictionary<Signal<boolean | undefined>> = {};
    private cachedReloading$: Dictionary<Signal<boolean | undefined>> = {};
    private defaultState?: TViewModel;
    private environmentInjector = inject(EnvironmentInjector);

    constructor(defaultState: TViewModel) {
        this.defaultState = defaultState;

        this.setClonedDeepStateToCheckDataMutation(defaultState);
    }

    public vmStateInitiating: boolean = false;
    public vmStateInitiated: boolean = false;
    public vmStateDataLoaded: boolean = false;
    public cacheService = inject(PlatformCachingService);

    public get enableCache(): boolean {
        return true;
    }

    private _innerStore?: ComponentStore<TViewModel>;
    public get innerStore(): ComponentStore<TViewModel> {
        this.initInnerStore();

        return <ComponentStore<TViewModel>>this._innerStore;
    }

    private _vm$?: Observable<TViewModel>;
    public get vm$(): Observable<TViewModel> {
        if (this._vm$ == undefined) {
            this._vm$ = <Observable<TViewModel>>combineLatest([this.initVmState(), this.internalSelect(s => s)]).pipe(
                map(([_, vm]) => (this.vmStateInitiated || this.vmStateDataLoaded ? vm : undefined)),
                filter(vm => vm != null),
                shareReplay({ bufferSize: 1, refCount: true })
            );

            this.subscribeCacheStateOnChanged();
        }

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
                });
            });
        }

        return this._vm!;
    }

    public destroyed$: BehaviorSubject<boolean> = new BehaviorSubject<boolean>(false);

    /**
     * Run when first time select data from store or use $vm to get data.
     * Use this to set up subscription to listen to data change if needed
     */
    protected abstract beforeInitVm: () => void;

    public abstract vmConstructor(data?: Partial<TViewModel>): TViewModel;

    protected abstract cachedStateKeyName(): string;

    /**
     * Triggers the onInitVm function and initializes the store's view model state.
     */
    public initVmState(): Observable<boolean> {
        if (!this.vmStateInitiating && !this.vmStateInitiated) {
            this.vmStateInitiating = true;

            this.beforeInitVm();

            const initOrReloadVm$ = this.initOrReloadVm(this.vmStateDataLoaded).pipe(take(2)) ?? of(null); //take(2) support api cache and implicit reload

            return initOrReloadVm$.pipe(
                delay(1, asyncScheduler), // Mimic real async incase observable is not async
                map(_ => {
                    if (!this.vmStateInitiated) {
                        this.vmStateInitiating = false;
                        this.vmStateInitiated = true;
                        this.vmStateDataLoaded = true;

                        this.setupIntervalCheckDataMutation();

                        if (this.currentState().status == 'Pending') {
                            this.updateState(<Partial<TViewModel>>{ status: 'Success' });
                        }
                    }

                    return this.vmStateInitiated;
                }),
                catchError(err => of(false)),
                distinctUntilObjectValuesChanged()
            );
        }

        return of(this.vmStateInitiated);
    }

    public setupIntervalCheckDataMutation() {
        if (!PLATFORM_CORE_GLOBAL_ENV.isLocalDev) return;

        interval(5000).pipe(
            this.subscribeUntilDestroyed(v => {
                this.ensureStateNotMutated();
            })
        );
    }

    public initInnerStore(forceReinit: boolean = false) {
        if (this._innerStore == undefined || forceReinit) {
            const cachedData = this.getCachedState();

            // Reset state to Pending if lastime caching it's loading state
            if (cachedData?.isStateReloading || cachedData?.isStateLoading) {
                cachedData.status = 'Pending';
                cachedData.reloadingMap = {};
                cachedData.loadingMap = {};
            }

            if (cachedData?.isStateSuccess || cachedData?.isStatePending) {
                this._innerStore = new ComponentStore(cachedData);
                this.setClonedDeepStateToCheckDataMutation(cachedData);

                if (cachedData.isStateSuccess) this.vmStateDataLoaded = true;

                // Clear defaultState to free memory
                this.defaultState = undefined;
            } else {
                this._innerStore = new ComponentStore(this.defaultState);
                this.setClonedDeepStateToCheckDataMutation(this.defaultState);
            }
        }
    }

    public ngOnDestroy(): void {
        this.innerStore.ngOnDestroy();
        this.destroyed$.next(true);
        this.cancelAllStoredSubscriptions();
    }

    /**
     * Returns the state observable of the store.
     */
    public get state$() {
        return this.vm$;
    }

    /**
     * Called to reload or initialize the data for the view model state.
     * Return observable to wait for the observable finished to ensure vm is not null only when first time load data successfully.
     * Return null to ignore wait for first-time loading to return vm not null with default initiated value from constructor.
     */
    public abstract initOrReloadVm: (isReload: boolean) => Observable<unknown>;

    public reload() {
        this.clearAllErrorMsgs();
        return this.initOrReloadVm(this.currentState().isStateSuccessOrReloading).pipe(take(2)); //take(2) support api cache and implicit reload
    }

    public clearAllErrorMsgs() {
        this.updateState(vm => vm.clearAllErrorMsgs(), { updaterNotDeepMutate: true });
    }

    public readonly defaultSelectConfig: PlatformStoreSelectConfig = {
        debounce: false,
        throttleTimeDuration: defaultThrottleDurationMs
    };

    private _isStatePending?: Signal<boolean>;
    public get isStatePending(): Signal<boolean> {
        if (this._isStatePending == null) {
            //untracked to fix NG0602: A disallowed function is called inside a reactive context
            untracked(() => {
                // toSignal must be used in an injection context
                runInInjectionContext(this.environmentInjector, () => {
                    this._isStatePending = toSignal(
                        this.select(_ => _.isStatePending),
                        { initialValue: true }
                    );
                });
            });
        }
        return this._isStatePending!;
    }

    private _isStateLoading?: Signal<boolean>;
    public get isStateLoading(): Signal<boolean> {
        if (this._isStateLoading == null) {
            //untracked to fix NG0602: A disallowed function is called inside a reactive context
            untracked(() => {
                // toSignal must be used in an injection context
                runInInjectionContext(this.environmentInjector, () => {
                    this._isStateLoading = toSignal(
                        this.select(_ => _.isStateLoading || this.currentState().isLoading()),
                        { initialValue: false }
                    );
                });
            });
        }
        return this._isStateLoading!;
    }

    private _isStateReloading?: Signal<boolean>;
    public get isStateReloading(): Signal<boolean> {
        if (this._isStateReloading == null) {
            //untracked to fix NG0602: A disallowed function is called inside a reactive context
            untracked(() => {
                // toSignal must be used in an injection context
                runInInjectionContext(this.environmentInjector, () => {
                    this._isStateReloading = toSignal(
                        this.select(_ => _.isStateReloading || this.currentState().isReloading()),
                        { initialValue: false }
                    );
                });
            });
        }
        return this._isStateReloading!;
    }

    private _isStateSuccess?: Signal<boolean>;
    public get isStateSuccess(): Signal<boolean> {
        if (this._isStateSuccess == null) {
            //untracked to fix NG0602: A disallowed function is called inside a reactive context
            untracked(() => {
                // toSignal must be used in an injection context
                runInInjectionContext(this.environmentInjector, () => {
                    this._isStateSuccess = toSignal(
                        this.select(_ => _.isStateSuccess),
                        { initialValue: false }
                    );
                });
            });
        }
        return this._isStateSuccess!;
    }

    private _isStateSuccessOrReloading?: Signal<boolean>;
    public get isStateSuccessOrReloading(): Signal<boolean> {
        if (this._isStateSuccessOrReloading == null) {
            //untracked to fix NG0602: A disallowed function is called inside a reactive context
            untracked(() => {
                // toSignal must be used in an injection context
                runInInjectionContext(this.environmentInjector, () => {
                    this._isStateSuccessOrReloading = toSignal(
                        this.select(_ => _.isStateSuccessOrReloading),
                        { initialValue: false }
                    );
                });
            });
        }
        return this._isStateSuccessOrReloading!;
    }

    private _isStateError?: Signal<boolean>;
    public get isStateError(): Signal<boolean> {
        if (this._isStateError == null) {
            //untracked to fix NG0602: A disallowed function is called inside a reactive context
            untracked(() => {
                // toSignal must be used in an injection context
                runInInjectionContext(this.environmentInjector, () => {
                    this._isStateError = toSignal(
                        this.select(_ => _.isStateError),
                        { initialValue: false }
                    );
                });
            });
        }
        return this._isStateError!;
    }

    private _isStatePending$?: Observable<boolean>;
    public get isStatePending$(): Observable<boolean> {
        this._isStatePending$ ??= this.select(_ => _.isStatePending);

        return this._isStatePending$;
    }

    private _isStateLoading$?: Observable<boolean>;
    public get isStateLoading$(): Observable<boolean> {
        this._isStateLoading$ ??= this.select(
            _ => _.isStateLoading || this.currentState().isAnyLoadingRequest() == true
        );

        return this._isStateLoading$;
    }

    private _isStateReloading$?: Observable<boolean>;
    public get isStateReloading$(): Observable<boolean> {
        this._isStateReloading$ ??= this.select(
            _ => _.isStateReloading || this.currentState().isAnyReloadingRequest() == true
        );

        return this._isStateReloading$;
    }

    private _isStateSuccess$?: Observable<boolean>;
    public get isStateSuccess$(): Observable<boolean> {
        this._isStateSuccess$ ??= this.select(_ => _.isStateSuccess);

        return this._isStateSuccess$;
    }

    private _isStateSuccessOrReloading$?: Observable<boolean>;
    public get isStateSuccessOrReloading$(): Observable<boolean> {
        this._isStateSuccessOrReloading$ ??= this.select(_ => _.isStateSuccess || _.isStateReloading);

        return this._isStateSuccessOrReloading$;
    }

    private _isStateError$?: Observable<boolean>;
    public get isStateError$(): Observable<boolean> {
        this._isStateError$ ??= this.select(_ => _.isStateError);

        return this._isStateError$;
    }

    private clonedDeepStateToCheckDataMutationJson?: string;

    /**
     * Updates the state of the innerStore of the component with the provided partial state or updater function.
     *
     * @param partialStateOrUpdaterFn - Partial state or updater function to update the component's state.
     * @param assignDeepLevel - Level of deep assignment for the state update (default is 1).
     */
    public updateState(
        partialStateOrUpdaterFn:
            | PartialDeep<TViewModel>
            | Partial<TViewModel>
            | ((state: TViewModel) => void | PartialDeep<TViewModel> | Partial<TViewModel>),
        options?: {
            updaterNotDeepMutate?: boolean;
            assignDeepLevel?: number;
            onDone?: (newState: TViewModel) => unknown;
        }
    ): void {
        this.setupSetClonedDeepStateToCheckDataMutation();
        this.ensureStateNotMutated();

        const processedOptions: ImmutableUpdateOptions = {
            checkDiff: 'deepCheck',
            maxDeepLevel: options?.assignDeepLevel ?? 1,
            updaterNotDeepMutate: options?.updaterNotDeepMutate
        };

        this.innerStore.setState(state => {
            try {
                const newState = immutableUpdate(state, partialStateOrUpdaterFn, processedOptions);

                if (this.currentState() != newState && options?.onDone) {
                    // Delay to ensure the state is updated before the onDone callback is executed
                    setTimeout(() => {
                        options.onDone!(newState);
                    });
                }

                return newState;
            } catch (error) {
                console.error(error);
                return immutableUpdate(state, this.buildSetErrorPartialState(<Error>error), processedOptions);
            }
        });
    }

    public ensureStateNotMutated() {
        if (!PLATFORM_CORE_GLOBAL_ENV.isLocalDev) return;

        // toPlainObj before check different to avoid case object has get property auto update value
        const currentStatePlainObj = toPlainObj(this.currentState());

        if (
            this.clonedDeepStateToCheckDataMutationJson != undefined &&
            JSON.stringify(currentStatePlainObj) !== this.clonedDeepStateToCheckDataMutationJson
        ) {
            const msg =
                '[DEV_ERROR] Data State mutated by dev in some placed, please check. Do not allow to mutate state directly. See CONSOLE LOG for more detail.';

            alert(msg);
            console.error(
                `[ClonedDeepStateToCheckDataMutationJson Original Value]:\n${
                    this.clonedDeepStateToCheckDataMutationJson
                }\n###\n[CurrentState Mutated Value]:\n${JSON.stringify(currentStatePlainObj)}`
            );
            console.error(new Error(msg));
        }
    }

    /**
     * Sets the error state of the component with the provided error response or error.
     *
     * @param errorResponse - Error response or error to set the component's error state.
     */
    public readonly setErrorState = (errorResponse: PlatformApiServiceErrorResponse | Error) => {
        this.updateState(this.buildSetErrorPartialState(errorResponse));
    };

    /**
     * Builds a partial state object with the error details.
     *
     * @param errorResponse - Error response or error to build the partial state with error details.
     * @returns Partial state object with error details.
     */
    public buildSetErrorPartialState(
        errorResponse: PlatformApiServiceErrorResponse | Error
    ):
        | PartialDeep<TViewModel>
        | Partial<TViewModel>
        | ((state: TViewModel) => void | PartialDeep<TViewModel> | Partial<TViewModel>) {
        return <PartialDeep<TViewModel>>{
            status: 'Error',
            error: PlatformApiServiceErrorResponse.getDefaultFormattedMessage(errorResponse)
        };
    }

    public currentState(): TViewModel {
        // force use protected function to return current state
        return (<any>this.innerStore).get();
    }

    private loadingRequestsCountMap: Dictionary<number> = {};

    public loadingRequestsCount() {
        let result = 0;
        Object.keys(this.loadingRequestsCountMap).forEach(key => {
            result += this.loadingRequestsCountMap[key]!;
        });
        return result;
    }

    private reloadingRequestsCountMap: Dictionary<number> = {};

    public reloadingRequestsCount() {
        let result = 0;
        Object.keys(this.reloadingRequestsCountMap).forEach(key => {
            result += this.reloadingRequestsCountMap[key]!;
        });
        return result;
    }

    /**
     * Creates an RxJS operator function that observes and manages the loading state and error state of an observable
     * request. It is designed to be used to simplify the handling of loading and error states,
     * providing a convenient way to manage asynchronous operations and their associated UI states.
     *
     * @template T The type emitted by the source observable.
     *
     * @param requestKey A key to identify the request. Defaults to `requestStateDefaultKey` if not specified.
     * @param options Additional options for handling success and error states.
     *
     * @returns An RxJS operator function that can be used with the `pipe` operator on an observable.
     *
     * @usage
     * // Example: Subscribe to an API request, managing loading and error states
     * apiService.loadData()
     *   .pipe(observerLoadingErrorState())
     *   .subscribe(
     *     data => {
     *       // Handle successful response
     *     },
     *     error => {
     *       // Handle error
     *     }
     *   );
     */
    public observerLoadingErrorState<T>(
        requestKey?: string | null,
        options?: PlatformVmObserverLoadingOptions
    ): (source: Observable<T>) => Observable<T> {
        if (requestKey == undefined) requestKey = PlatformVm.requestStateDefaultKey;

        const setLoadingState = () => {
            if (options?.isReloading) this.setReloading(true, requestKey);
            else this.setLoading(true, requestKey);

            this.setErrorMsg(undefined, requestKey);

            checkSetStatus.bind(this)();
        };

        return (source: Observable<T>) => {
            return defer(() => {
                setLoadingState();

                return source.pipe(
                    onCancel(() => {
                        if (!options?.isReloading) this.setLoading(false, requestKey);
                        this.setReloading(false, requestKey);

                        checkSetStatus.bind(this)();
                    }),

                    tapOnce({
                        next: result => {
                            // Set to reloading if is loading after first successful item
                            // So that if observable is not completed yet, support get case get api
                            // service has implicit caching reload, the observable return 2 items.
                            // First item from cache, second item is from server
                            // Set reloading for waiting second item to be completed then set to success
                            if (!options?.isReloading) {
                                this.setLoading(false, requestKey);
                                this.setReloading(true, requestKey);

                                checkSetStatus.bind(this)();
                            }
                        },
                        error: (err: PlatformApiServiceErrorResponse | Error) => {
                            if (!options?.isReloading) this.setLoading(false, requestKey);
                            this.setReloading(false, requestKey);

                            checkSetStatus.bind(this)();
                        }
                    }),
                    tap({
                        error: (err: PlatformApiServiceErrorResponse | Error) => {
                            this.setErrorMsg(err, requestKey);
                            this.setErrorState(err);
                        }
                    }),
                    tapLimit(
                        {
                            complete: () => {
                                this.setReloading(false, requestKey);
                                checkSetStatus.bind(this)();
                            }
                        },
                        2 // limit first 2 item for complete reloading. The second item is the item implicit load if api cached
                    )
                );
            });
        };

        function checkSetStatus(this: PlatformVmStore<TViewModel>) {
            if (this.loadingRequestsCount() > 0) {
                if (this.currentState().status != 'Loading')
                    this.updateState(<Partial<TViewModel>>{ status: 'Loading' });
            } else if (this.reloadingRequestsCount() > 0) {
                if (this.currentState().status != 'Reloading')
                    this.updateState(<Partial<TViewModel>>{ status: 'Reloading' });
            } else if (this.currentState().error != null && this.currentState().error?.trim() != '') {
                if (this.currentState().status != 'Error') this.updateState(<Partial<TViewModel>>{ status: 'Error' });
            } else if (this.currentState().status != 'Success')
                this.updateState(<Partial<TViewModel>>{ status: 'Success' });
        }
    }

    /**
     * Sets the error message for a specific request key in the component's state.
     *
     * @param error - Error message or null.
     * @param requestKey - Key to identify the request.
     */
    public setErrorMsg = (
        error: string | undefined | PlatformApiServiceErrorResponse | Error,
        requestKey: string = PlatformVm.requestStateDefaultKey
    ) => {
        const errorMsg =
            typeof error == 'string' || error == null
                ? error
                : PlatformApiServiceErrorResponse.getDefaultFormattedMessage(error);

        this.updateState(<Partial<TViewModel>>{
            errorMsgMap: immutableUpdate(this.currentState().errorMsgMap, { [requestKey]: errorMsg }),
            error: errorMsg ?? null
        });

        if (error instanceof Error) {
            console.error(error);
            this.cacheService.clear();
        }
    };

    /**
     * Returns the error message Signal for a specific request key.
     *
     * @param requestKey - Key to identify the request.
     * @returns Error message Signal.
     */
    public getErrorMsg$ = (requestKey?: string) => {
        requestKey ??= requestStateDefaultKey;

        if (this.cachedErrorMsg$[requestKey] == null) {
            //untracked to fix NG0602: A disallowed function is called inside a reactive context
            untracked(() => {
                // toSignal must be used in an injection context
                runInInjectionContext(this.environmentInjector, () => {
                    this.cachedErrorMsg$[requestKey] = toSignal(this.getErrorMsgObservable$(requestKey));
                });
            });
        }
        return this.cachedErrorMsg$[requestKey];
    };

    /**
     * Returns the error message observable for a specific request key.
     *
     * @param requestKey - Key to identify the request.
     * @returns Error message observable.
     */
    public getErrorMsgObservable$ = (requestKey?: string) => {
        requestKey ??= requestStateDefaultKey;

        if (this.cachedErrorMsgObservable$[requestKey] == null) {
            this.cachedErrorMsgObservable$[requestKey] = this.select(_ => _.getErrorMsg(requestKey));
        }
        return this.cachedErrorMsgObservable$[requestKey]!;
    };

    public getAllErrorMsgObservable$ = (requestKeys?: string[], excludeKeys?: string[]) => {
        const combinedCacheRequestKey = `${requestKeys != null ? JSON.stringify(requestKeys) : 'All'}_excludeKeys:${
            excludeKeys != null ? JSON.stringify(excludeKeys) : 'null'
        }`;

        if (this.cachedErrorMsgObservable$[combinedCacheRequestKey] == null) {
            this.cachedErrorMsgObservable$[combinedCacheRequestKey] = this.select(_ =>
                _.getAllErrorMsgs(requestKeys, excludeKeys)
            );
        }
        return this.cachedErrorMsgObservable$[combinedCacheRequestKey]!;
    };

    /**
     * Sets the loading state for a specific request key in the component's state.
     *
     * @param value - Loading state value (true, false, or null).
     * @param requestKey - Key to identify the request.
     */
    public setLoading = (value: boolean | null, requestKey: string = PlatformVm.requestStateDefaultKey) => {
        if (this.loadingRequestsCountMap[requestKey] == undefined) this.loadingRequestsCountMap[requestKey] = 0;

        if (value == true) this.loadingRequestsCountMap[requestKey] += 1;
        if (value == false && this.loadingRequestsCountMap[requestKey]! > 0)
            this.loadingRequestsCountMap[requestKey] -= 1;

        this.updateState(<Partial<TViewModel>>{
            loadingMap: immutableUpdate(this.currentState().loadingMap, {
                [requestKey]: this.loadingRequestsCountMap[requestKey]! > 0
            })
        });
    };

    /**
     * Sets the reloading state for a specific request key in the component's state.
     *
     * @param value - Reloading state value (true, false, or null).
     * @param requestKey - Key to identify the request.
     */
    public setReloading = (value: boolean | null, requestKey: string = PlatformVm.requestStateDefaultKey) => {
        if (this.reloadingRequestsCountMap[requestKey] == undefined) this.reloadingRequestsCountMap[requestKey] = 0;

        if (value == true) this.reloadingRequestsCountMap[requestKey] += 1;
        if (value == false && this.reloadingRequestsCountMap[requestKey]! > 0)
            this.reloadingRequestsCountMap[requestKey] -= 1;

        this.updateState(<Partial<TViewModel>>{
            reloadingMap: immutableUpdate(this.currentState().reloadingMap, {
                [requestKey]: this.reloadingRequestsCountMap[requestKey]! > 0
            })
        });
    };

    /**
     * Returns the loading state Signal for a specific request key.
     *
     * @param requestKey - Key to identify the request.
     * @returns Loading state Signal.
     */
    public isLoading$ = (requestKey: string = requestStateDefaultKey) => {
        if (this.cachedLoading$[requestKey] == null) {
            //untracked to fix NG0602: A disallowed function is called inside a reactive context
            untracked(() => {
                // toSignal must be used in an injection context
                runInInjectionContext(this.environmentInjector, () => {
                    this.cachedLoading$[requestKey] = toSignal(this.select(_ => _.isLoading(requestKey)));
                });
            });
        }
        return this.cachedLoading$[requestKey]!;
    };

    /**
     * Returns the reloading state Signal for a specific request key.
     *
     * @param requestKey - Key to identify the request.
     * @returns Reloading state Signal.
     */
    public isReloading$ = (requestKey: string = requestStateDefaultKey) => {
        if (this.cachedReloading$[requestKey] == null) {
            //untracked to fix NG0602: A disallowed function is called inside a reactive context
            untracked(() => {
                // toSignal must be used in an injection context
                runInInjectionContext(this.environmentInjector, () => {
                    this.cachedReloading$[requestKey] = toSignal(this.select(_ => _.isReloading(requestKey)));
                });
            });
        }
        return this.cachedReloading$[requestKey]!;
    };

    /**
     * Selects a slice of the component's state and returns an observable of the selected slice.
     *
     * @template Result - Type of the selected slice.
     * @param projector - Function to select the desired slice of the state.
     * @param config - Select configuration options (optional).
     * @returns Observable of the selected slice of the state.
     */
    public select<Result>(
        projector: (s: TViewModel) => Result,
        config?: PlatformStoreSelectConfig
    ): Observable<Result> {
        // autoInitVmState for case using store select only but not use vm$ in template
        return this.internalSelect<Result>(projector, config).pipe(tapOnce({ next: () => this.autoInitVmState() }));
    }

    private setupSetClonedDeepStateToCheckDataMutationHasDone: boolean = false;
    private setupSetClonedDeepStateToCheckDataMutation() {
        if (PLATFORM_CORE_GLOBAL_ENV.isLocalDev && !this.setupSetClonedDeepStateToCheckDataMutationHasDone) {
            this.setupSetClonedDeepStateToCheckDataMutationHasDone = true;
            this.innerStore
                .select(v => v)
                .pipe(
                    this.subscribeUntilDestroyed(v => {
                        this.setClonedDeepStateToCheckDataMutation(this.currentState());
                    })
                );
        }
    }

    protected internalSelect<Result>(
        projector: (s: TViewModel) => Result,
        config?: PlatformStoreSelectConfig
    ): Observable<Result> {
        return defer(() => {
            this.setupSetClonedDeepStateToCheckDataMutation();

            const selectConfig = config ?? this.defaultSelectConfig;

            let selectResult$ = this.innerStore.select(projector, selectConfig);

            // ThrottleTime explain: Delay to enhance performance
            // { leading: true, trailing: true } <=> emit the first item to ensure not delay, but also ignore the sub-sequence,
            // and still emit the latest item to ensure data is latest
            if (selectConfig.throttleTimeDuration != undefined && selectConfig.throttleTimeDuration > 0)
                selectResult$ = selectResult$.pipe(
                    throttleTime(selectConfig.throttleTimeDuration ?? 0, asyncScheduler, {
                        leading: true,
                        trailing: true
                    })
                );

            return <Observable<Result>>selectResult$.pipe(
                map(result => {
                    setTimeout(() => {
                        this.ensureStateNotMutated();
                    }, 500);
                    return result;
                })
            );
        });
    }

    protected autoInitVmState() {
        if (this._vm$ == undefined) {
            this.vm$.pipe(take(1)).subscribe();
        }
    }

    /**
     * This method is a higher-order function that creates and manages side effects in an application.
     * Side effects are actions that interact with the outside world, such as making API calls or updating the UI.
     *
     * @template ProvidedType - The type of value provided to the effect.
     * @template OriginType - The type of the origin observable.
     * @template ObservableType - The inferred type of the origin observable.
     * @template ReturnObservableType - The type of the observable returned by the generator function.
     * @template ReturnType - The type of the return value of the `effect` method.
     *
     * @param {function(origin$: OriginType, isReloading?: boolean): Observable<ReturnObservableType>} generator - The generator function that defines the effect.
     * @param { throttleTimeMs?: number} [options] - An optional object that can contain a throttle time in milliseconds for the effect and a function to handle the effect subscription.
     *
     * @returns {ReturnType} - The function that can be used to trigger the effect. The function params including: observableOrValue, isReloading, otherOptions. In otherOptions including: effectSubscriptionHandleFn - a function to handle the effect subscription.
     *
     * @example this.effect((query$: Observable<(any type here, can be void too)>) => { return $query.pipe(switchMap(query => callApi(query)), this.tapResponse(...), this.observerLoadingState('key', {isReloading:isReloading})) }, {throttleTimeMs: 300}).
     * The returned function could be used like: effectFunc(query, isLoading, {effectSubscriptionHandleFn: sub => this.storeSubscription('key', sub)})
     */
    public effect<
        ProvidedType,
        OriginType extends Observable<ProvidedType> = Observable<ProvidedType>,
        ObservableType = OriginType extends Observable<infer A> ? A : never,
        ReturnObservableType = unknown,
        ReturnType = [ObservableType] extends [void]
            ? (
                  observableOrValue?: null | undefined | void | Observable<null | undefined | void>,
                  isReloading?: boolean,
                  options?: { effectSubscriptionHandleFn?: (sub: Subscription) => unknown }
              ) => Observable<ReturnObservableType>
            : (
                  observableOrValue: ObservableType | Observable<ObservableType>,
                  isReloading?: boolean,
                  options?: { effectSubscriptionHandleFn?: (sub: Subscription) => unknown }
              ) => Observable<ReturnObservableType>
    >(
        generator: (origin$: OriginType, isReloading?: boolean) => Observable<ReturnObservableType>,
        requestKey?: string | null,
        options?: {
            effectSubscriptionHandleFn?: (sub: Subscription) => unknown;
            notAutoObserveErrorLoadingState?: boolean;
        }
    ): ReturnType {
        if (requestKey == undefined) requestKey = PlatformVm.requestStateDefaultKey;

        const returnFunc = (
            observableOrValue?: ObservableType | Observable<ObservableType> | null,
            isReloading?: boolean,
            otherOptions?: { effectSubscriptionHandleFn?: (sub: Subscription) => unknown }
        ) => {
            // observableOrValue.pipe(skipTime(100)) => skipTime(100) to skip first item of observable
            // within first 100ms to prevent if obserable is from an behavior subject
            // only start subscribe for item emit after subscribe listen for some thing changed new
            // in the future
            const request$ = isObservable(observableOrValue)
                ? observableOrValue.pipe(skipTime(100))
                : of(observableOrValue);

            // (III)
            // Delay to make the next api call asynchronous. When call an effect => loading. Call again => previousEffectSub.unsubscribe => cancel => back to success => call next api (async) => set loading again correctly.
            // If not delay => call next api is sync => set loading is sync but previous cancel is not activated successfully yet, which status is not updated back to Success => which this new effect call skip set status to loading => but then the previous api cancel executing => update status to Success but actually it's loading => create incorrectly status
            // (IV)
            // Share so that later subscriber can receive the result, this will help component call
            // effect could subscribe to do some action like show loading, hide loading, etc.
            return request$.pipe(
                skipDuplicates(500),
                switchMap(request =>
                    generator(<OriginType>of(request), isReloading).pipe(
                        delay(1, asyncScheduler), // (III)
                        applyIf(
                            !options?.notAutoObserveErrorLoadingState,
                            this.observerLoadingErrorState(requestKey, { isReloading: isReloading })
                        )
                    )
                ),
                this.untilDestroyed(),
                share(), // (IV)
                this.subscribeUntilDestroyed(undefined, sub => {
                    if (options?.effectSubscriptionHandleFn != null) options?.effectSubscriptionHandleFn(sub);
                    if (otherOptions?.effectSubscriptionHandleFn != null) otherOptions?.effectSubscriptionHandleFn(sub);
                })
            );
        };

        return returnFunc as unknown as ReturnType;
    }

    public effectSimple<
        ProvidedType = void,
        ReturnObservableType = unknown,
        ReturnType = [ProvidedType] extends [void]
            ? (
                  observableOrValue?: null | undefined | void | Observable<null | undefined | void>,
                  isReloading?: boolean,
                  options?: { effectSubscriptionHandleFn?: (sub: Subscription) => unknown }
              ) => Observable<ReturnObservableType>
            : (
                  observableOrValue: ProvidedType | Observable<ProvidedType>,
                  isReloading?: boolean,
                  options?: { effectSubscriptionHandleFn?: (sub: Subscription) => unknown }
              ) => Observable<ReturnObservableType>
    >(
        generator: (origin: ProvidedType, isReloading?: boolean) => Observable<ReturnObservableType> | void,
        requestKey?: string | null,
        options?: {
            effectSubscriptionHandleFn?: (sub: Subscription) => unknown;
            notAutoObserveErrorLoadingState?: boolean;
        }
    ): ReturnType {
        return this.effect(
            (origin$: Observable<ProvidedType>, isReloading?: boolean) => {
                return origin$.pipe(
                    switchMap(origin => {
                        const returnObservableOrVoid = generator(origin, isReloading);
                        return returnObservableOrVoid instanceof Observable ? returnObservableOrVoid : of(undefined);
                    })
                );
            },
            requestKey,
            options
        );
    }

    /**
     * Maps the emitted value of the source observable to the current view model state.
     *
     * @template T - Type emitted by the source observable.
     * @returns Operator function to map the emitted value to the current view model state.
     */
    public switchMapVm<T>(): OperatorFunction<T, TViewModel> {
        return switchMap(p => this.select(vm => vm));
    }

    /**
     * Creates an RxJS operator function that taps into the source observable to handle next, error, and complete events.
     * @param nextFn A function to handle the next value emitted by the source observable.
     * @param errorFn  (optional): A function to handle errors emitted by the source observable.
     * @param completeFn (optional): A function to handle the complete event emitted by the source observable.
     */
    protected tapResponse<T, E = string | PlatformApiServiceErrorResponse | Error>(
        nextFn?: (next: T) => void,
        errorFn?: (error: E) => void,
        completeFn?: () => void
    ): (source: Observable<T>) => Observable<T> {
        return tap({
            next: data => {
                try {
                    if (nextFn) nextFn(data);
                } catch (error) {
                    console.error(error);
                    throw error;
                }
            },
            error: errorFn,
            complete: completeFn
        });
    }

    /**
     * Stores a subscription using the specified key. The subscription will be unsubscribed when the store is destroyed.
     */
    protected storeSubscription(key: string, subscription: Subscription): void {
        this.storedSubscriptionsMap.set(key, subscription);
    }

    /**
     * Stores a subscription. The subscription will be unsubscribed when the store is destroyed.
     */
    protected storeAnonymousSubscription(subscription: Subscription): void {
        list_remove(this.storedAnonymousSubscriptions, p => p.closed);
        this.storedAnonymousSubscriptions.push(subscription);
    }

    /**
     * Subscribes to the provided observable and stores the subscription in the storedAnonymousSubscriptions array.
     */
    protected subscribe<T>(observable: Observable<T>): Subscription {
        const subs = observable.subscribe();

        this.storeAnonymousSubscription(subs);

        return subs;
    }

    /**
     * Cancels and removes a stored subscription identified by the provided key from the
     */
    protected cancelStoredSubscription(key: string): void {
        this.storedSubscriptionsMap.get(key)?.unsubscribe();
        this.storedSubscriptionsMap.delete(key);
    }

    /**
     * Cancels and removes all stored subscriptions from both the storedSubscriptionsMap and storedAnonymousSubscriptions.
     */
    protected cancelAllStoredSubscriptions(): void {
        this.storedSubscriptionsMap.forEach((sub, key) => this.cancelStoredSubscription(key));
        this.storedAnonymousSubscriptions.forEach(sub => sub.unsubscribe());
    }

    /**
     * Returns the current state of the store.
     */
    protected get(): TViewModel {
        return this.currentState();
    }

    /**
     * Subscribes to changes in the view model state and updates the cached state.
     * This method ensures that the cached state is updated after the view model state changes,
     * but it throttles the updates to avoid excessive storage writes.
     *
     * @protected
     * @method
     * @returns {void}
     */
    protected subscribeCacheStateOnChanged() {
        if (this.vm$ == undefined) return;

        if (this.enableCache)
            this.storeAnonymousSubscription(
                this.vm$.pipe(throttleTime(1000, asyncScheduler, { leading: true, trailing: true })).subscribe(vm => {
                    if (!vm.isStateLoading && !vm.isStateReloading) this.cacheService.set(this.getCachedStateKey(), vm);
                })
            );
    }

    private getCachedStateKey(): string {
        return 'PlatformViewModelState_' + this.cachedStateKeyName();
    }

    /**
     * Retrieves the cached view model state from the caching service.
     *
     * @protected
     * @method
     * @returns {TViewModel | undefined} The cached view model state or undefined if not found.
     */
    protected getCachedState(): TViewModel | undefined {
        const cachedData = this.cacheService.get(this.getCachedStateKey(), (data?: Partial<TViewModel>) =>
            this.vmConstructor(data)
        );

        return cachedData;
    }

    protected setClonedDeepStateToCheckDataMutation(newState: TViewModel | undefined) {
        // toPlainObj before check different to avoid case object has get property auto update value
        this.clonedDeepStateToCheckDataMutationJson = PLATFORM_CORE_GLOBAL_ENV.isLocalDev
            ? JSON.stringify(toPlainObj(newState))
            : undefined;
    }

    /**
     * Creates an RxJS operator function that unsubscribes from the observable when the component is destroyed.
     */
    public untilDestroyed<T>(): MonoTypeOperatorFunction<T> {
        return takeUntil(this.destroyed$.pipe(filter(destroyed => destroyed == true)));
    }

    /**
     * Creates an RxJS operator function that subscribes to the observable until the component is destroyed.
     */
    public subscribeUntilDestroyed<T>(
        observerOrNext?: Partial<Observer<T>> | ((value: T) => void),
        outSubscriptionFn?: (subscription: Subscription) => void
    ): MonoTypeOperatorFunction<T> {
        return subscribeUntil(
            this.destroyed$.pipe(filter(destroyed => destroyed == true)),
            observerOrNext,
            outSubscriptionFn
        );
    }
}

export type PlatformVmObserverLoadingOptions = {
    isReloading?: boolean;
};
