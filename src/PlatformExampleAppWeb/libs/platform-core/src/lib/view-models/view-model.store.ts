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
    defer,
    delay,
    filter,
    isObservable,
    Observable,
    of,
    OperatorFunction,
    Subscription,
    switchMap,
    takeUntil,
    tap,
    throttleTime
} from 'rxjs';
import { PartialDeep } from 'type-fest';

import { ComponentStore, SelectConfig } from '@ngrx/component-store';

import { PlatformApiServiceErrorResponse } from '../api-services';
import { PlatformCachingService } from '../caching';
import { onCancel, tapOnce } from '../rxjs';
import { immutableUpdate, list_remove } from '../utils';
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
 * @method reloadOrInitData - Abstract method to be implemented by subclasses, triggering a reload or initialization of data.
 * @method updateState - Updates the state of the inner store with the provided partial state or updater function.
 * @method setErrorState - Sets the error state of the component with the provided error response or error.
 * @method buildSetErrorPartialState - Builds a partial state object with error details.
 * @method currentState - Gets the current state of the view model.
 * @method loadingRequestsCount - Gets the count of loading requests.
 * @method reloadingRequestsCount - Gets the count of reloading requests.
 * @method observerLoadingState - Observes the loading state of a request and updates the component's state accordingly.
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
    private cacheService = inject(PlatformCachingService);
    private defaultState?: TViewModel;
    private environmentInjector = inject(EnvironmentInjector);

    constructor(defaultState: TViewModel) {
        this.defaultState = defaultState;
    }

    public vmStateInitiating: boolean = false;
    public vmStateInitiated: boolean = false;
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
            this._vm$ = this.select(s => s);
        }

        return this._vm$;
    }

    private _vm?: Signal<TViewModel>;
    /**
     * Vm signal from vm$
     */
    public get vm(): Signal<TViewModel> {
        if (this._vm == undefined) {
            //untracked to fix NG0602: A disallowed function is called inside a reactive context
            untracked(() => {
                // toSignal must be used in an injection context
                runInInjectionContext(this.environmentInjector, () => {
                    this._vm = toSignal(this.vm$, { initialValue: this.currentState });
                });
            });
        }

        return this._vm!;
    }

    protected abstract onInitVm: () => void;

    public abstract vmConstructor(data?: Partial<TViewModel>): TViewModel;

    protected abstract cachedStateKeyName(): string;

    /**
     * Triggers the onInitVm function and initializes the store's view model state.
     */
    public initVmState() {
        if (!this.vmStateInitiating && !this.vmStateInitiated) {
            this.vmStateInitiating = true;

            this.subscribeCacheStateOnChanged();

            this.onInitVm();

            this.reloadOrInitData();

            this.vmStateInitiated = true;
        }
    }

    public initInnerStore(forceReinit: boolean = false) {
        if (this._innerStore == undefined || forceReinit) {
            const cachedData = this.getCachedState();

            if (cachedData?.isStateSuccess || cachedData?.isStatePending) {
                this._innerStore = new ComponentStore(cachedData);

                // Clear defaultState to free memory
                this.defaultState = undefined;
            } else {
                this._innerStore = new ComponentStore(this.defaultState);
            }
        }
    }

    public ngOnDestroy(): void {
        this.innerStore.ngOnDestroy();
        this.cancelAllStoredSubscriptions();
    }

    /**
     * Returns the state observable of the store.
     */
    public get state$() {
        return this.vm$;
    }

    public abstract reloadOrInitData: () => void;

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
                        this.select(_ => _.isStateLoading || this.currentState.isLoading()),
                        { initialValue: false }
                    );
                });
            });
        }
        return this._isStateLoading!;
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
        this._isStateLoading$ ??= this.select(_ => _.isStateLoading || this.currentState.isLoading());

        return this._isStateLoading$;
    }

    private _isStateSuccess$?: Observable<boolean>;
    public get isStateSuccess$(): Observable<boolean> {
        this._isStateSuccess$ ??= this.select(_ => _.isStateSuccess);

        return this._isStateSuccess$;
    }

    private _isStateError$?: Observable<boolean>;
    public get isStateError$(): Observable<boolean> {
        this._isStateError$ ??= this.select(_ => _.isStateError);

        return this._isStateError$;
    }

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
        assignDeepLevel: number = 1
    ): void {
        this.innerStore.setState(state => {
            try {
                return immutableUpdate(state, partialStateOrUpdaterFn, 'deepCheck', assignDeepLevel);
            } catch (error) {
                console.error(error);
                return immutableUpdate(
                    state,
                    this.buildSetErrorPartialState(<Error>error),
                    'deepCheck',
                    assignDeepLevel
                );
            }
        });
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

    public get currentState(): TViewModel {
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
     *   .pipe(observerLoadingState())
     *   .subscribe(
     *     data => {
     *       // Handle successful response
     *     },
     *     error => {
     *       // Handle error
     *     }
     *   );
     */
    public observerLoadingState<T>(
        requestKey?: string | null,
        options?: PlatformVmObserverLoadingOptions
    ): (source: Observable<T>) => Observable<T> {
        if (requestKey == undefined) requestKey = PlatformVm.requestStateDefaultKey;

        const setLoadingState = () => {
            if (!this.isForSetReloadingState(requestKey, options) && this.currentState.status != 'Loading')
                this.updateState(<Partial<TViewModel>>{
                    status: 'Loading'
                });

            if (this.isForSetReloadingState(requestKey, options)) this.setReloading(true, requestKey!);
            else this.setLoading(true, requestKey!);

            this.setErrorMsg(undefined, requestKey!);

            if (options?.onShowLoading != null && !this.isForSetReloadingState(requestKey, options))
                options.onShowLoading();
        };

        return (source: Observable<T>) => {
            return defer(() => {
                const previousStatus = this.currentState.status;

                setLoadingState();

                return source.pipe(
                    onCancel(() => {
                        if (this.isForSetReloadingState(<string>requestKey, options))
                            this.setReloading(false, requestKey!);
                        else this.setLoading(false, requestKey!);

                        if (
                            this.currentState.status == 'Loading' &&
                            this.loadingRequestsCount() <= 0 &&
                            previousStatus == 'Success'
                        )
                            this.updateState(<Partial<TViewModel>>{ status: 'Success' });

                        if (options?.onHideLoading != null && !this.isForSetReloadingState(<string>requestKey, options))
                            options.onHideLoading();
                    }),

                    tapOnce({
                        next: result => {
                            if (this.isForSetReloadingState(<string>requestKey, options))
                                this.setReloading(false, requestKey!);
                            else this.setLoading(false, requestKey!);

                            if (
                                options?.onHideLoading != null &&
                                !this.isForSetReloadingState(<string>requestKey, options)
                            )
                                options.onHideLoading();
                        },
                        error: (err: PlatformApiServiceErrorResponse | Error) => {
                            if (this.isForSetReloadingState(<string>requestKey, options))
                                this.setReloading(false, requestKey!);
                            else this.setLoading(false, requestKey!);

                            if (
                                options?.onHideLoading != null &&
                                !this.isForSetReloadingState(<string>requestKey, options)
                            )
                                options.onHideLoading();
                        }
                    }),
                    tap({
                        next: result => {
                            if (
                                this.currentState.status != 'Error' &&
                                this.currentState.status != 'Success' &&
                                this.loadingRequestsCount() <= 0
                            )
                                this.updateState(<Partial<TViewModel>>{ status: 'Success' });
                        },
                        error: (err: PlatformApiServiceErrorResponse | Error) => {
                            this.setErrorMsg(err, requestKey!);
                            this.setErrorState(err);
                        }
                    })
                );
            });
        };
    }

    private isForSetReloadingState(
        requestKey: string | undefined | null,
        options: PlatformVmObserverLoadingOptions | undefined
    ) {
        return (
            options?.isReloading &&
            this.currentState.isStateSuccess &&
            this.currentState.error == null &&
            !this.isLoading$(requestKey ?? undefined)()
        );
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
            errorMsgMap: immutableUpdate(this.currentState.errorMsgMap, { [requestKey]: errorMsg }),
            error: errorMsg === undefined ? null : errorMsg
        });
    };

    /**
     * Returns the error message Signal for a specific request key.
     *
     * @param requestKey - Key to identify the request.
     * @returns Error message Signal.
     */
    public getErrorMsg$ = (requestKey: string = requestStateDefaultKey) => {
        if (this.cachedErrorMsg$[requestKey] == null) {
            //untracked to fix NG0602: A disallowed function is called inside a reactive context
            untracked(() => {
                // toSignal must be used in an injection context
                runInInjectionContext(this.environmentInjector, () => {
                    this.cachedErrorMsg$[requestKey] = toSignal(this.select(_ => _.getErrorMsg(requestKey)));
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
    public getErrorMsgObservable$ = (requestKey: string = requestStateDefaultKey) => {
        if (this.cachedErrorMsgObservable$[requestKey] == null) {
            this.cachedErrorMsgObservable$[requestKey] = this.select(_ => _.getErrorMsg(requestKey));
        }
        return this.cachedErrorMsgObservable$[requestKey];
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
            loadingMap: immutableUpdate(this.currentState.loadingMap, {
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
            reloadingMap: immutableUpdate(this.currentState.reloadingMap, {
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
        return defer(() => {
            const selectConfig = config ?? this.defaultSelectConfig;

            let selectResult$ = this.innerStore
                .select(projector, selectConfig)
                .pipe(tapOnce({ next: () => this.initVmState() }));

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

            return <Observable<Result>>selectResult$;
        });
    }

    /**
     * Creates an effect, which return a function when called will subscribe to the RXJS observable returned
     * by the given generator. This used to the define loading/updating data method in the store.
     *
     * This effect is subscribed to throughout the lifecycle of the store.
     * @param generator A function that takes an origin Observable input and
     *     returns an Observable. The Observable that is returned will be
     *     subscribed to for the life of the store.
     * @return A function that, when called, will trigger the origin Observable.
     * @usage
     * Ex1: public loadData = this.effect(() => this.someApi.load()); Use function: this.loadData();
     *
     * Ex2: public loadData = this.effect((query$: Observable<TQuery>, isReloading?: boolean) => query$.pipe(switchMap(query => this.someApi.load(query)))); Use function: this.loadData(<TQuery>query param here);
     */
    public effect<
        ProvidedType,
        OriginType extends Observable<ProvidedType> | unknown = Observable<ProvidedType>,
        ObservableType = OriginType extends Observable<infer A> ? A : never,
        ReturnType = ObservableType extends void
            ? (
                  observableOrValue: ObservableType | Observable<ObservableType> | null | undefined,
                  isReloading?: boolean
              ) => Subscription
            : (observableOrValue: ObservableType | Observable<ObservableType>, isReloading?: boolean) => Subscription
    >(
        generator: (origin$: OriginType, isReloading?: boolean) => Observable<unknown> | unknown,
        throttleTimeMs?: number
    ): ReturnType {
        let previousEffectSub: Subscription = new Subscription();

        return ((
            observableOrValue?: ObservableType | Observable<ObservableType> | null,
            isReloading?: boolean
        ): Subscription => {
            previousEffectSub.unsubscribe();

            const observable$ = isObservable(observableOrValue) ? observableOrValue : of(observableOrValue);

            // ThrottleTime explain: Delay to enhance performance
            // { leading: true, trailing: true } <=> emit the first item to ensure not delay, but also ignore the sub-sequence,
            // and still emit the latest item to ensure data is latest
            const newEffectSub: Subscription = of(null)
                .pipe(
                    delay(1), // (III)
                    switchMap(() => {
                        const generatorResult = generator(
                            <OriginType>(<unknown>observable$.pipe(
                                throttleTime(throttleTimeMs ?? defaultThrottleDurationMs, asyncScheduler, {
                                    leading: true,
                                    trailing: true
                                })
                            )),
                            isReloading
                        );

                        return (generatorResult instanceof Observable ? generatorResult : of(<unknown>null)).pipe(
                            takeUntil(this.innerStore.destroy$)
                        );
                    })
                )
                .subscribe();

            // (III)
            // Delay to make the next api call asynchronous. When call an effect1 => loading. Call again => previousEffectSub.unsubscribe => cancel => back to success => call next api (async) => set loading again correctly.
            // If not delay => call next api is sync => set loading is sync but previous cancel is not activated successfully yet, which status is not updated back to Success => which this new effect call skip set status to loading => but then the previous api cancel executing => update status to Success but actually it's loading => create incorrectly status

            this.storeAnonymousSubscription(newEffectSub);
            previousEffectSub = newEffectSub;

            return newEffectSub;
        }) as unknown as ReturnType;
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
        nextFn: (next: T) => void,
        errorFn?: (error: E) => void,
        completeFn?: () => void
    ): (source: Observable<T>) => Observable<T> {
        return tap({
            next: data => {
                try {
                    nextFn(data);
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
        return this.currentState;
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
                this.vm$
                    .pipe(
                        throttleTime(1000, asyncScheduler, { leading: true, trailing: true }),
                        filter(x => !x.isStateLoading && !x.isAnyLoadingRequest() && !x.isAnyReloadingRequest())
                    )
                    .subscribe(vm => {
                        setTimeout(() => {
                            this.cacheService.set(this.getCachedStateKey(), vm);
                        }, 0);
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
}

export type PlatformVmObserverLoadingOptions = {
    onShowLoading?: () => unknown;
    onHideLoading?: () => unknown;
    isReloading?: boolean;
};
