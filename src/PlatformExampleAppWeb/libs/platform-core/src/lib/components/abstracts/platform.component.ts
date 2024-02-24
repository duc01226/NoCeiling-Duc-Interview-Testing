/* eslint-disable @typescript-eslint/no-explicit-any */
import {
    AfterViewInit,
    ChangeDetectorRef,
    computed,
    Directive,
    inject,
    OnChanges,
    OnDestroy,
    OnInit,
    signal,
    Signal,
    SimpleChanges,
    WritableSignal
} from '@angular/core';

import { ToastrService } from 'ngx-toastr';
import {
    asyncScheduler,
    BehaviorSubject,
    defer,
    MonoTypeOperatorFunction,
    Observable,
    of,
    Subject,
    Subscription
} from 'rxjs';
import { filter, finalize, takeUntil, tap, TapObserver, throttleTime } from 'rxjs/operators';

import { PlatformApiServiceErrorResponse } from '../../api-services';
import { LifeCycleHelper } from '../../helpers';
import { onCancel, tapOnce } from '../../rxjs';
import { PlatformTranslateService } from '../../translations';
import { clone, guid_generate, immutableUpdate, keys, list_remove, task_delay } from '../../utils';
import { requestStateDefaultKey } from '../../view-models';

export const enum LoadingState {
    Error = 'Error',
    Loading = 'Loading',
    Success = 'Success',
    Pending = 'Pending'
}

export const defaultThrottleDurationMs = 500;

/**
 * Abstract class representing a platform component with common functionality.
 * @abstract
 * @directive
 */
@Directive()
export abstract class PlatformComponent implements OnInit, AfterViewInit, OnDestroy, OnChanges {
    public static readonly defaultDetectChangesDelay: number = 0;
    public static readonly defaultDetectChangesThrottleTime: number = defaultThrottleDurationMs;

    public toast: ToastrService = inject(ToastrService);
    public changeDetector: ChangeDetectorRef = inject(ChangeDetectorRef);
    public translateSrv: PlatformTranslateService = inject(PlatformTranslateService);

    public initiated$: BehaviorSubject<boolean> = new BehaviorSubject<boolean>(false);
    public viewInitiated$: BehaviorSubject<boolean> = new BehaviorSubject<boolean>(false);
    public destroyed$: BehaviorSubject<boolean> = new BehaviorSubject<boolean>(false);
    // General loadingState when not specific requestKey, requestKey = requestStateDefaultKey;
    public loadingState$: WritableSignal<LoadingState> = signal(LoadingState.Pending);
    public errorMsgMap$: WritableSignal<Dictionary<string | undefined>> = signal({});
    public loadingMap$: WritableSignal<Dictionary<boolean | null>> = signal({});
    public reloadingMap$: WritableSignal<Dictionary<boolean | null>> = signal({});
    public componentId = guid_generate();

    protected storedSubscriptionsMap: Map<string, Subscription> = new Map();
    protected storedAnonymousSubscriptions: Subscription[] = [];
    protected cachedErrorMsg$: Dictionary<Signal<string | undefined>> = {};
    protected cachedLoading$: Dictionary<Signal<boolean | null>> = {};
    protected cachedReloading$: Dictionary<Signal<boolean | null>> = {};
    protected allErrorMsgs$!: Signal<string | null>;

    protected detectChangesThrottleSource = new Subject<DetectChangesParams>();
    protected detectChangesThrottle$ = this.detectChangesThrottleSource.pipe(
        this.untilDestroyed(),
        throttleTime(PlatformComponent.defaultDetectChangesThrottleTime, asyncScheduler, {
            leading: true,
            trailing: true
        }),
        tap(params => {
            this.doDetectChanges(params);
        })
    );

    protected doDetectChanges(params?: DetectChangesParams) {
        if (this.canDetectChanges) {
            this.changeDetector.detectChanges();
            if (params?.checkParentForHostBinding != undefined) this.changeDetector.markForCheck();
            if (params?.onDone != undefined) params.onDone();
        }
    }

    protected _isStatePending?: Signal<boolean>;
    public get isStatePending(): Signal<boolean> {
        this._isStatePending ??= computed(() => this.loadingState$() == 'Pending');
        return this._isStatePending;
    }

    protected _isStateLoading?: Signal<boolean>;
    public get isStateLoading(): Signal<boolean> {
        this._isStateLoading ??= computed(
            () => this.loadingState$() == 'Loading' || this.loadingMap$()[requestStateDefaultKey] == true
        );
        return this._isStateLoading;
    }

    protected _isStateSuccess?: Signal<boolean>;
    public get isStateSuccess(): Signal<boolean> {
        this._isStateSuccess ??= computed(() => this.loadingState$() == 'Success');
        return this._isStateSuccess;
    }

    protected _isStateError?: Signal<boolean>;
    public get isStateError(): Signal<boolean> {
        this._isStateError ??= computed(() => this.loadingState$() == 'Error');
        return this._isStateError;
    }

    /**
     * Returns an Signal that emits the error message associated with the default request key or the first existing error message.
     */
    public get errorMsg$(): Signal<string | undefined> {
        return this.getErrorMsg$();
    }

    public detectChanges(delayTime?: number, onDone?: () => unknown, checkParentForHostBinding: boolean = false): void {
        this.cancelStoredSubscription('detectChangesDelaySubs');

        if (this.canDetectChanges) {
            const finalDelayTime = delayTime ?? PlatformComponent.defaultDetectChangesDelay;

            if (finalDelayTime <= 0) {
                dispatchChangeDetectionSignal.bind(this)();
            } else {
                const detectChangesDelaySubs = task_delay(
                    () => dispatchChangeDetectionSignal.bind(this)(),
                    finalDelayTime
                );

                this.storeSubscription('detectChangesDelaySubs', detectChangesDelaySubs);
            }
        }

        function dispatchChangeDetectionSignal(this: PlatformComponent) {
            this.detectChangesThrottleSource.next({
                onDone: onDone,
                checkParentForHostBinding: checkParentForHostBinding
            });
        }
    }

    /**
     * Creates an RxJS operator function that unsubscribes from the observable when the component is destroyed.
     */
    public untilDestroyed<T>(): MonoTypeOperatorFunction<T> {
        return takeUntil(this.destroyed$.pipe(filter(destroyed => destroyed == true)));
    }

    /**
     * Creates an RxJS operator function that triggers change detection after the observable completes.
     */
    public finalDetectChanges<T>(): MonoTypeOperatorFunction<T> {
        return finalize(() => this.detectChanges());
    }

    public ngOnInit(): void {
        this.detectChangesThrottle$.pipe(this.untilDestroyed()).subscribe();
        this.initiated$.next(true);
    }

    public ngOnChanges(changes: SimpleChanges): void {
        if (this.isInputChanged(changes) && this.initiated$.value) {
            this.ngOnInputChanged(changes);
        }
    }

    public ngOnInputChanged(changes: SimpleChanges): void {
        // Default empty here. Override to implement logic
    }

    public ngAfterViewInit(): void {
        this.viewInitiated$.next(true);
    }

    public ngOnDestroy(): void {
        this.destroyed$.next(true);

        this.destroyAllSubjects();
        this.cancelAllStoredSubscriptions();
    }

    private loadingRequestsCountMap: Dictionary<number> = {};

    /**
     * Returns the total number of active loading requests across all request keys. This method provides a convenient
     * way to track and display the overall loading state of a component by aggregating loading requests from various
     * asynchronous operations.
     *
     * @returns The total number of active loading requests.
     *
     * @usage
     * // Example: Check and display a loading indicator based on the total loading requests count
     * const isLoading = this.loadingRequestsCount() > 0;
     * if (isLoading) {
     *   // Display loading indicator
     * } else {
     *   // Hide loading indicator
     * }
     */
    public loadingRequestsCount() {
        let result = 0;
        Object.keys(this.loadingRequestsCountMap).forEach(key => {
            result += this.loadingRequestsCountMap[key]!;
        });
        return result;
    }

    private reloadingRequestsCountMap: Dictionary<number> = {};

    /**
     * Returns the total number of active reloading requests.
     */
    public reloadingRequestsCount() {
        let result = 0;
        Object.keys(this.reloadingRequestsCountMap).forEach(key => {
            result += this.reloadingRequestsCountMap[key]!;
        });
        return result;
    }

    /**
     * Creates an RxJS operator function that observes and manages the loading state and error state of an observable
     * request. It is designed to be used with Angular components to simplify the handling of loading and error states,
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
        requestKey: string = requestStateDefaultKey,
        options?: PlatformObserverLoadingStateOptions<T>
    ): (source: Observable<T>) => Observable<T> {
        const setLoadingState = () => {
            if (!this.isForSetReloadingState(options) && this.loadingState$() != LoadingState.Loading)
                this.loadingState$.set(LoadingState.Loading);

            if (this.isForSetReloadingState(options)) this.setReloading(true, requestKey);
            else this.setLoading(true, requestKey);

            this.setErrorMsg(undefined, requestKey);
        };

        return (source: Observable<T>) => {
            return defer(() => {
                const previousLoadingState = this.loadingState$();

                setLoadingState();

                return source.pipe(
                    this.untilDestroyed(),
                    onCancel(() => {
                        if (this.isForSetReloadingState(options)) this.setReloading(false, requestKey);
                        else this.setLoading(false, requestKey);

                        if (
                            this.loadingState$() == 'Loading' &&
                            this.loadingRequestsCount() <= 0 &&
                            previousLoadingState == 'Success'
                        )
                            this.loadingState$.set(LoadingState.Success);
                    }),

                    tapOnce({
                        next: value => {
                            if (this.isForSetReloadingState(options)) this.setReloading(false, requestKey);
                            else this.setLoading(false, requestKey);

                            if (options?.onSuccess != null) options.onSuccess(value);
                        },
                        error: (err: PlatformApiServiceErrorResponse | Error) => {
                            if (this.isForSetReloadingState(options)) this.setReloading(false, requestKey);
                            else this.setLoading(false, requestKey);

                            if (options?.onError != null) options.onError(err);
                        }
                    }),
                    tap({
                        next: value => {
                            if (
                                this.loadingState$() != LoadingState.Error &&
                                this.loadingState$() != LoadingState.Success &&
                                this.loadingRequestsCount() <= 0
                            )
                                this.loadingState$.set(LoadingState.Success);
                        },
                        error: (err: PlatformApiServiceErrorResponse | Error) => {
                            this.setErrorMsg(err, requestKey);
                            this.loadingState$.set(LoadingState.Error);
                        }
                    })
                );
            });
        };
    }

    private isForSetReloadingState<T>(options: PlatformObserverLoadingStateOptions<T> | undefined) {
        return options?.isReloading && this.getErrorMsg() == null && !this.isStateLoading();
    }

    /**
     * Returns an Signal that emits the error message associated with the specified request key or the first existing error message if requestKey is default key if error message with default key is null.
     * * @param [requestKey=requestStateDefaultKey] (optional): A key to identify the request. Default is
     * requestStateDefaultKey.
     */
    public getErrorMsg$(requestKey: string = requestStateDefaultKey): Signal<string | undefined> {
        if (this.cachedErrorMsg$[requestKey] == null) {
            this.cachedErrorMsg$[requestKey] = computed(() => {
                return this.getErrorMsg(requestKey);
            });
        }
        return this.cachedErrorMsg$[requestKey]!;
    }

    /**
     * Returns the error message associated with the specified request key or the first existing error message if requestKey is default key if error message with default key is null.
     * * @param [requestKey=requestStateDefaultKey] (optional): A key to identify the request. Default is
     * requestStateDefaultKey.
     */
    public getErrorMsg(requestKey: string = requestStateDefaultKey): string | undefined {
        if (this.errorMsgMap$()[requestKey] == null && requestKey == requestStateDefaultKey)
            return Object.keys(this.errorMsgMap$())
                .map(key => this.errorMsgMap$()[key])
                .find(errorMsg => errorMsg != null);

        return this.errorMsgMap$()[requestKey];
    }

    /**
     * Returns an Signal that emits all error messages combined into a single string.
     */
    public getAllErrorMsgs$(): Signal<string | null> {
        if (this.allErrorMsgs$ == null) {
            this.allErrorMsgs$ = computed(() => {
                const errorMsgMap = this.errorMsgMap$();
                return keys(errorMsgMap)
                    .map(key => errorMsgMap[key] ?? '')
                    .filter(msg => msg != '' && msg != null)
                    .join('; ');
            });
        }

        return this.allErrorMsgs$;
    }

    /**
     * Returns an Signal that emits the loading state (true or false) associated with the specified request key.
     * @param [requestKey=requestStateDefaultKey] (optional): A key to identify the request. Default is requestStateDefaultKey.
     */
    public isLoading$(requestKey: string = requestStateDefaultKey): Signal<boolean | null> {
        if (this.cachedLoading$[requestKey] == null) {
            this.cachedLoading$[requestKey] = computed(() => this.loadingMap$()[requestKey]!);
        }
        return this.cachedLoading$[requestKey]!;
    }

    /**
     * Returns an Signal that emits the reloading state (true or false) associated with the specified request key.
     * @param [requestKey=requestStateDefaultKey] (optional): A key to identify the request. Default is
     *     requestStateDefaultKey.
     */
    public isReloading$(requestKey: string = requestStateDefaultKey): Signal<boolean | null> {
        if (this.cachedReloading$[requestKey] == null) {
            this.cachedReloading$[requestKey] = computed(() => this.isReloading(requestKey));
        }
        return this.cachedReloading$[requestKey]!;
    }

    /**
     * Returns the reloading state (true or false) associated with the specified request key.
     * @param errorKey (optional): A key to identify the request. Default is requestStateDefaultKey.
     */
    public isReloading(errorKey: string = requestStateDefaultKey): boolean | null {
        return this.reloadingMap$()[errorKey]!;
    }

    /**
     * Creates an RxJS operator function that taps into the source observable to handle next, error, and complete
     * events.
     * @param nextFn A function to handle the next value emitted by the source observable.
     * @param errorFn  (optional): A function to handle errors emitted by the source observable.
     * @param completeFn (optional): A function to handle the complete event emitted by the source observable.
     */
    protected tapResponse<T>(
        nextFn: (next: T) => void,
        errorFn?: (error: PlatformApiServiceErrorResponse | Error) => any,
        completeFn?: () => void
    ): (source: Observable<T>) => Observable<T> {
        // eslint-disable-next-line @typescript-eslint/no-empty-function
        return tap({
            next: data => {
                try {
                    nextFn(data);
                    this.detectChanges();
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
     * Creates an effect, which return a function when called will subscribe to the RXJS observable returned
     * by the given generator. This used to the define loading/updating data method in the component.
     *
     * This effect is subscribed to throughout the lifecycle of the Component.
     * @param generator A function that takes an origin Observable input and
     *     returns an Observable. The Observable that is returned will be
     *     subscribed to for the life of the component.
     * @return A function that, when called, will trigger the origin Observable.
     * @usage
     * Ex1: public loadData = this.effect(() => this.someApi.load()); Use function: this.loadData();
     *
     * Ex2: public loadData = this.effect((query$: Observable<TQuery>) => query$.pipe(switchMap(query =>
     *     this.someApi.load(query)))); Use function: this.loadData(<TQuery>query param here);
     */
    public effect<TOrigin, TReturn>(
        generator: (
            origin: TOrigin | Observable<TOrigin | null> | null,
            isReloading?: boolean,
            tapRequestObserverOrNext?:
                | Partial<TapObserver<TOrigin | null | undefined>>
                | ((value: TOrigin | null | undefined) => void)
        ) => Observable<TReturn> | TReturn | unknown,
        throttleTimeMs?: number
    ) {
        let previousEffectSub: Subscription = new Subscription();

        return (
            observableOrValue: TOrigin | Observable<TOrigin> | null = null,
            isReloading?: boolean,
            tapRequestObserverOrNext?:
                | Partial<TapObserver<TOrigin | null | undefined>>
                | ((value: TOrigin | null | undefined) => void)
        ) => {
            previousEffectSub.unsubscribe();

            // ThrottleTime explain: Delay to enhance performance
            // { leading: true, trailing: true } <=> emit the first item to ensure not delay, but also ignore the
            // sub-sequence, and still emit the latest item to ensure data is latest
            const generatorResult = generator(observableOrValue, isReloading);
            const newEffectSub: Subscription = (
                generatorResult instanceof Observable ? generatorResult : of(<TReturn>null)
            )
                .pipe(
                    throttleTime(throttleTimeMs ?? defaultThrottleDurationMs, asyncScheduler, {
                        leading: true,
                        trailing: true
                    }),
                    this.untilDestroyed(),
                    tap(tapRequestObserverOrNext ?? {}),
                    finalize(() => this.detectChanges())
                )
                .subscribe();

            this.storeAnonymousSubscription(newEffectSub);
            previousEffectSub = newEffectSub;

            return newEffectSub;
        };
    }

    protected get canDetectChanges(): boolean {
        return this.initiated$.value && !this.destroyed$.value;
    }

    /**
     * Stores a subscription using the specified key. The subscription will be unsubscribed when the component is
     * destroyed.
     */
    protected storeSubscription(key: string, subscription: Subscription): void {
        this.storedSubscriptionsMap.set(key, subscription);
    }

    /**
     * Stores a subscription. The subscription will be unsubscribed when the component is destroyed.
     */
    protected storeAnonymousSubscription(subscription: Subscription): void {
        list_remove(this.storedAnonymousSubscriptions, p => p.closed);
        this.storedAnonymousSubscriptions.push(subscription);
    }

    protected cancelStoredSubscription(key: string): void {
        this.storedSubscriptionsMap.get(key)?.unsubscribe();
        this.storedSubscriptionsMap.delete(key);
    }

    /**
     * Sets the error message for a specific request key in the component. This method is commonly used in conjunction
     * with API requests to update the error state associated with a particular request. If the error is a string or
     * `undefined`, it directly updates the error message for the specified request key. If the error is an instance of
     * `PlatformApiServiceErrorResponse` or `Error`, it formats the error message using
     * `PlatformApiServiceErrorResponse.getDefaultFormattedMessage` before updating the error state.
     *
     * @param error The error message, `undefined`, or an instance of `PlatformApiServiceErrorResponse` or `Error`.
     * @param requestKey The key identifying the request. Defaults to `requestStateDefaultKey` if not specified.
     *
     * @example
     * // Set an error message for the default request key
     * setErrorMsg("An error occurred!");
     *
     * // Set an error message for a specific request key
     * setErrorMsg("Custom error message", "customRequestKey");
     *
     * // Set an error message using an instance of PlatformApiServiceErrorResponse
     * const apiError = new PlatformApiServiceErrorResponse(500, "Internal Server Error");
     * setErrorMsg(apiError, "apiRequest");
     *
     * // Set an error message using an instance of Error
     * const genericError = new Error("An unexpected error");
     * setErrorMsg(genericError, "genericRequest");
     */
    protected setErrorMsg = (
        error: string | undefined | PlatformApiServiceErrorResponse | Error,
        requestKey: string = requestStateDefaultKey
    ) => {
        if (typeof error == 'string' || error == undefined)
            this.errorMsgMap$.set(
                clone(this.errorMsgMap$(), _ => {
                    _[requestKey] = error;
                })
            );
        else
            this.errorMsgMap$.set(
                clone(this.errorMsgMap$(), _ => {
                    _[requestKey] = PlatformApiServiceErrorResponse.getDefaultFormattedMessage(error);
                })
            );
    };

    /**
     * Clears the error message associated with a specific request key in the component. This method is useful when you
     * want to reset or clear the error state for a particular request, making it useful in scenarios where you want to
     * retry an action or clear errors upon successful completion of a related operation.
     *
     * @param requestKey The key identifying the request. Defaults to `requestStateDefaultKey` if not specified.
     *
     * @example
     * // Clear the error message for the default request key
     * clearErrorMsg();
     *
     * // Clear the error message for a specific request key
     * clearErrorMsg("customRequestKey");
     */
    protected clearErrorMsg = (requestKey: string = requestStateDefaultKey) => {
        const currentErrorMsgMap = this.errorMsgMap$();

        this.errorMsgMap$.set(
            immutableUpdate(currentErrorMsgMap, p => {
                delete p[requestKey];
            })
        );
    };

    protected clearAllErrorMsgs = () => {
        this.errorMsgMap$.set({});
    };

    /**
     * Sets the loading state for the specified request key.
     */
    protected setLoading = (value: boolean | null, requestKey: string = requestStateDefaultKey) => {
        if (this.loadingRequestsCountMap[requestKey] == undefined) this.loadingRequestsCountMap[requestKey] = 0;

        if (value == true) this.loadingRequestsCountMap[requestKey] += 1;
        if (value == false && this.loadingRequestsCountMap[requestKey]! > 0)
            this.loadingRequestsCountMap[requestKey] -= 1;

        this.loadingMap$.set(
            clone(this.loadingMap$(), _ => {
                _[requestKey] = this.loadingRequestsCountMap[requestKey]! > 0;
            })
        );
    };

    /**
     * Sets the loading state for the specified request key.
     */
    protected setReloading = (value: boolean | null, requestKey: string = requestStateDefaultKey) => {
        if (this.reloadingRequestsCountMap[requestKey] == undefined) this.reloadingRequestsCountMap[requestKey] = 0;

        if (value == true) this.reloadingRequestsCountMap[requestKey] += 1;
        if (value == false && this.reloadingRequestsCountMap[requestKey]! > 0)
            this.reloadingRequestsCountMap[requestKey] -= 1;

        this.reloadingMap$.set(
            clone(this.reloadingMap$(), _ => {
                _[requestKey] = this.reloadingRequestsCountMap[requestKey]! > 0;
            })
        );
    };

    /**
     * Cancels all stored subscriptions, unsubscribing from each one. This method should be called in the component's
     * ngOnDestroy lifecycle hook to ensure that all subscriptions are properly cleaned up when the component is destroyed.
     * This includes both named subscriptions stored using the `storeSubscription` method and anonymous subscriptions
     * stored using the `storeAnonymousSubscription` method.
     */
    public cancelAllStoredSubscriptions(): void {
        // Unsubscribe from all named subscriptions
        this.storedSubscriptionsMap.forEach((value, key) => this.cancelStoredSubscription(key));

        // Unsubscribe from all anonymous subscriptions
        this.cancelAllStoredAnonymousSubscriptions();
    }

    /**
     * Track-by function for ngFor that uses an immutable list as the tracking target. Use this to improve performance
     * if we know that the list is immutable
     */
    protected ngForTrackByImmutableList<TItem>(trackTargetList: TItem[]): (index: number, item: TItem) => TItem[] {
        return () => trackTargetList;
    }

    /**
     * Track-by function for ngFor that uses a specific property of the item as the tracking key.
     * @param itemPropKey The property key of the item to use as the tracking key.
     */
    protected ngForTrackByItemProp<TItem extends object>(
        itemPropKey: keyof TItem
    ): (index: number, item: TItem) => unknown {
        // eslint-disable-next-line @typescript-eslint/no-explicit-any
        return (index, item) => (<any>item)[itemPropKey];
    }

    protected isInputChanged(changes: SimpleChanges): boolean {
        return LifeCycleHelper.isInputChanged(changes);
    }

    private cancelAllStoredAnonymousSubscriptions() {
        this.storedAnonymousSubscriptions.forEach(sub => sub.unsubscribe());
        this.storedAnonymousSubscriptions = [];
    }

    private destroyAllSubjects(): void {
        this.initiated$.complete();
        this.viewInitiated$.complete();
        this.destroyed$.complete();
        this.detectChangesThrottleSource.complete();
    }
}

export interface PlatformObserverLoadingStateOptions<T> {
    onSuccess?: (value: T) => any;
    onError?: (err: PlatformApiServiceErrorResponse | Error) => any;
    isReloading?: boolean;
}

interface DetectChangesParams {
    onDone?: () => any;
    checkParentForHostBinding: boolean;
}
