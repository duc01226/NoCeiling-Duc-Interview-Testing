/* eslint-disable @typescript-eslint/no-explicit-any */

import { inject, Injectable, OnDestroy } from '@angular/core';
import { ComponentStore, SelectConfig } from '@ngrx/component-store';
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

import { PlatformApiServiceErrorResponse } from '../api-services';
import { PlatformCachingService } from '../caching';
import { onCancel, tapOnce } from '../rxjs';
import { immutableUpdate, list_remove } from '../utils';
import { PlatformVm } from './generic.view-model';

export const requestStateDefaultKey = 'Default';
const defaultThrottleDuration = 300;

declare interface PlatformStoreSelectConfig extends SelectConfig {
    throttleTimeDuration?: number;
}

@Injectable()
export abstract class PlatformVmStore<TViewModel extends PlatformVm> implements OnDestroy {
    private storedSubscriptionsMap: Map<string, Subscription> = new Map();
    private storedAnonymousSubscriptions: Subscription[] = [];
    private cachedErrorMsg$: Dictionary<Observable<string | undefined | null>> = {};
    private cachedLoading$: Dictionary<Observable<boolean | undefined | null>> = {};
    private cachedReloading$: Dictionary<Observable<boolean | undefined | null>> = {};
    private cacheService = inject(PlatformCachingService);
    private defaultState?: TViewModel;

    constructor(defaultState: TViewModel) {
        this.defaultState = defaultState;
    }

    public vmStateInitiating: boolean = false;
    public vmStateInitiated: boolean = false;

    private _innerStore?: ComponentStore<TViewModel>;
    public get innerStore(): ComponentStore<TViewModel> {
        if (this._innerStore == undefined) {
            const cachedData = this.getCachedState();

            if (cachedData?.isStateSuccess || cachedData?.isStatePending) {
                this._innerStore = new ComponentStore(cachedData);

                // Clear defaultState to free memory
                this.defaultState = undefined;
            } else {
                this._innerStore = new ComponentStore(this.defaultState);
            }
        }

        return this._innerStore;
    }

    private _vm$?: Observable<TViewModel>;
    public get vm$(): Observable<TViewModel> {
        if (this._vm$ == undefined) {
            this._vm$ = this.select(s => s);
        }

        return this._vm$;
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
        throttleTimeDuration: defaultThrottleDuration
    };

    private _isStatePending$?: Observable<boolean>;
    public get isStatePending$(): Observable<boolean> {
        this._isStatePending$ ??= this.select(_ => _.isStatePending);

        return this._isStatePending$;
    }

    private _isStateLoading$?: Observable<boolean>;
    public get isStateLoading$(): Observable<boolean> {
        this._isStateLoading$ ??= this.select(_ => _.isStateLoading);

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
            result += this.loadingRequestsCountMap[key];
        });
        return result;
    }

    private reloadingRequestsCountMap: Dictionary<number> = {};
    public reloadingRequestsCount() {
        let result = 0;
        Object.keys(this.reloadingRequestsCountMap).forEach(key => {
            result += this.reloadingRequestsCountMap[key];
        });
        return result;
    }

    /**
     * Observes the loading state of a request and updates the component's state accordingly.
     *
     * @template T - Type of observable.
     * @param requestKey - Key to identify the request.
     * @param options - Custom loading observer configuration.
     * @returns Operator function to observe the loading state.
     *
     * @usage
     *  ** TS:
     *  apiService.loadData().pipe(this.observerLoadingState()).subscribe()
     */
    public observerLoadingState<T>(
        requestKey?: string,
        options?: PlatformVmObserverLoadingOptions
    ): (source: Observable<T>) => Observable<T> {
        if (requestKey == undefined) requestKey = PlatformVm.requestStateDefaultKey;

        const setLoadingState = () => {
            if (!this.isForSetReloadingState(<string>requestKey, options) && this.currentState.status != 'Loading')
                this.updateState(<Partial<TViewModel>>{
                    status: 'Loading'
                });

            if (this.isForSetReloadingState(<string>requestKey, options)) this.setReloading(true, requestKey);
            else this.setLoading(true, requestKey);

            this.setErrorMsg(null, requestKey);

            if (options?.onShowLoading != null && !this.isForSetReloadingState(<string>requestKey, options))
                options.onShowLoading();
        };

        return (source: Observable<T>) => {
            return defer(() => {
                const previousStatus = this.currentState.status;

                setLoadingState();

                return source.pipe(
                    onCancel(() => {
                        if (this.isForSetReloadingState(<string>requestKey, options))
                            this.setReloading(false, requestKey);
                        else this.setLoading(false, requestKey);

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
                                this.setReloading(false, requestKey);
                            else this.setLoading(false, requestKey);

                            if (
                                options?.onHideLoading != null &&
                                !this.isForSetReloadingState(<string>requestKey, options)
                            )
                                options.onHideLoading();
                        },
                        error: (err: PlatformApiServiceErrorResponse | Error) => {
                            if (this.isForSetReloadingState(<string>requestKey, options))
                                this.setReloading(false, requestKey);
                            else this.setLoading(false, requestKey);

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
                            this.setErrorMsg(err, requestKey);
                            this.setErrorState(err);
                        }
                    })
                );
            });
        };
    }

    private isForSetReloadingState(requestKey: string, options: PlatformVmObserverLoadingOptions | undefined) {
        return options?.isReloading && this.currentState.isStateSuccess;
    }

    /**
     * Sets the error message for a specific request key in the component's state.
     *
     * @param error - Error message or null.
     * @param requestKey - Key to identify the request.
     */
    public setErrorMsg = (
        error: string | null | PlatformApiServiceErrorResponse | Error,
        requestKey: string = PlatformVm.requestStateDefaultKey
    ) => {
        const errorMsg =
            typeof error == 'string' || error == null
                ? <string | null>error
                : PlatformApiServiceErrorResponse.getDefaultFormattedMessage(error);

        this.updateState(<Partial<TViewModel>>{
            errorMsgMap: immutableUpdate(this.currentState.errorMsgMap, { [requestKey]: errorMsg }),
            error: errorMsg
        });
    };

    /**
     * Returns the error message observable for a specific request key.
     *
     * @param requestKey - Key to identify the request.
     * @returns Error message observable.
     */
    public getErrorMsg$ = (requestKey: string = requestStateDefaultKey) => {
        if (this.cachedErrorMsg$[requestKey] == null) {
            this.cachedErrorMsg$[requestKey] = this.select(_ => _.getErrorMsg(requestKey));
        }
        return this.cachedErrorMsg$[requestKey];
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
        if (value == false && this.loadingRequestsCountMap[requestKey] > 0)
            this.loadingRequestsCountMap[requestKey] -= 1;

        this.updateState(<Partial<TViewModel>>{
            loadingMap: immutableUpdate(this.currentState.loadingMap, {
                [requestKey]: this.loadingRequestsCountMap[requestKey] > 0
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
        if (value == false && this.reloadingRequestsCountMap[requestKey] > 0)
            this.reloadingRequestsCountMap[requestKey] -= 1;

        this.updateState(<Partial<TViewModel>>{
            reloadingMap: immutableUpdate(this.currentState.reloadingMap, {
                [requestKey]: this.reloadingRequestsCountMap[requestKey] > 0
            })
        });
    };

    /**
     * Returns the loading state observable for a specific request key.
     *
     * @param requestKey - Key to identify the request.
     * @returns Loading state observable.
     */
    public isLoading$ = (requestKey: string = requestStateDefaultKey) => {
        if (this.cachedLoading$[requestKey] == null) {
            this.cachedLoading$[requestKey] = this.select(_ => _.isLoading(requestKey));
        }
        return this.cachedLoading$[requestKey];
    };

    /**
     * Returns the reloading state observable for a specific request key.
     *
     * @param requestKey - Key to identify the request.
     * @returns Reloading state observable.
     */
    public isReloading$ = (requestKey: string = requestStateDefaultKey) => {
        if (this.cachedReloading$[requestKey] == null) {
            this.cachedReloading$[requestKey] = this.select(_ => _.isReloading(requestKey));
        }
        return this.cachedReloading$[requestKey];
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

            return selectResult$;
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
        ReturnType = ProvidedType | ObservableType extends void
            ? (
                  observableOrValue?: ObservableType | Observable<ObservableType> | undefined,
                  isReloading?: boolean
              ) => Subscription
            : (observableOrValue: ObservableType | Observable<ObservableType>, isReloading?: boolean) => Subscription
    >(generator: (origin$: OriginType, isReloading?: boolean) => Observable<unknown>): ReturnType {
        let previousEffectSub: Subscription = new Subscription();

        return ((
            observableOrValue?: ObservableType | Observable<ObservableType>,
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
                        return generator(
                            <OriginType>(<any>observable$.pipe(
                                throttleTime(defaultThrottleDuration, asyncScheduler, {
                                    leading: true,
                                    trailing: true
                                })
                            )),
                            isReloading
                        ).pipe(takeUntil(this.innerStore.destroy$));
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
    protected tapResponse<T, E = any>(
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

    protected subscribeCacheStateOnChanged() {
        if (this.vm$ == undefined) return;

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

    protected getCachedState() {
        const cachedData = <TViewModel | undefined>(
            this.cacheService.get(this.getCachedStateKey(), data => this.vmConstructor(data))
        );

        return cachedData;
    }
}

export type PlatformVmObserverLoadingOptions = {
    onShowLoading?: () => unknown;
    onHideLoading?: () => unknown;
    isReloading?: boolean;
};
