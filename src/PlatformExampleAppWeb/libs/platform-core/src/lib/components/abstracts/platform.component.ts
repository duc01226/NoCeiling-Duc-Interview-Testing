/* eslint-disable @typescript-eslint/no-explicit-any */
import {
    AfterViewInit,
    ChangeDetectorRef,
    computed,
    Directive,
    effect,
    ElementRef,
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
    isObservable,
    MonoTypeOperatorFunction,
    Observable,
    Observer,
    of,
    Subject,
    Subscription
} from 'rxjs';
import { delay, filter, finalize, share, switchMap, takeUntil, tap, throttleTime } from 'rxjs/operators';

import { PlatformApiServiceErrorResponse } from '../../api-services';
import { LifeCycleHelper } from '../../helpers';
import { PLATFORM_CORE_GLOBAL_ENV } from '../../platform-core-global-environment';
import { onCancel, skipDuplicates, skipTime, subscribeUntil, tapLimit, tapOnce } from '../../rxjs';
import { PlatformTranslateService } from '../../translations';
import { clone, guid_generate, immutableUpdate, keys, list_distinct, list_remove, task_delay } from '../../utils';
import { requestStateDefaultKey } from '../../view-models';

export const enum ComponentStateStatus {
    Error = 'Error',
    Loading = 'Loading',
    Reloading = 'Reloading',
    Success = 'Success',
    Pending = 'Pending'
}

const defaultThrottleDurationMs = 300;

/**
 * Abstract class representing a platform component with common functionality.
 * @abstract
 * @directive
 */
@Directive()
export abstract class PlatformComponent implements OnInit, AfterViewInit, OnDestroy, OnChanges {
    public static readonly defaultDetectChangesDelay: number = 0;
    public static readonly defaultDetectChangesThrottleTime: number = defaultThrottleDurationMs;
    public static readonly checkLoadingDelayTimeMs = 100;

    private devModeCheckLoadingStateElementTimeoutId?: number;

    constructor() {
        // Setup dev mode check has loading
        if (this.devModeCheckLoadingStateElement != undefined && PLATFORM_CORE_GLOBAL_ENV.isLocalDev) {
            effect(() => {
                if (this.devModeCheckLoadingStateElementTimeoutId != undefined)
                    clearTimeout(this.devModeCheckLoadingStateElementTimeoutId);
                if (this.isStateLoading()) {
                    this.devModeCheckLoadingStateElementTimeoutId = setTimeout(() => {
                        if (this.devModeCheckLoadingStateElement == undefined || !this.isStateLoading()) return;

                        const devModeCheckLoadingStateElements =
                            typeof this.devModeCheckLoadingStateElement == 'string'
                                ? [this.devModeCheckLoadingStateElement]
                                : this.devModeCheckLoadingStateElement;
                        const findInRootElement = this.devModeCheckLoadingOrErrorAllowInGlobalDocumentBody
                            ? document
                            : this.elementRef.nativeElement;

                        if (
                            this.isStateLoading() &&
                            this.devModeCheckLoadingStateElementOnlyWhen &&
                            devModeCheckLoadingStateElements.find(
                                elementSelector =>
                                    !this.isStateLoading() || findInRootElement.querySelector(elementSelector) != null
                            ) == null
                        ) {
                            if (!this.isStateLoading() || this.destroyed$.value) return;

                            const msg = `[DEV-ERROR] ${this.elementRef.nativeElement.tagName} Component in loading state but no loading element found`;
                            alert(msg);
                            console.error(new Error(msg));
                        }
                    }, PlatformComponent.checkLoadingDelayTimeMs + 100);
                }
            });
        }

        //Setup dev mode check error has alert
        if (this.devModeCheckErrorStateElement != undefined && PLATFORM_CORE_GLOBAL_ENV.isLocalDev) {
            effect(() => {
                if (this.errorMsg$() != null) {
                    setTimeout(() => {
                        if (this.devModeCheckErrorStateElement == undefined) return;

                        const devModeCheckErrorStateElements =
                            typeof this.devModeCheckErrorStateElement == 'string'
                                ? [this.devModeCheckErrorStateElement]
                                : this.devModeCheckErrorStateElement;
                        const findInRootElement = this.devModeCheckLoadingOrErrorAllowInGlobalDocumentBody
                            ? document
                            : this.elementRef.nativeElement;

                        if (
                            this.errorMsg$() != null &&
                            devModeCheckErrorStateElements.find(
                                elementSelector =>
                                    this.errorMsg$() == null || findInRootElement.querySelector(elementSelector) != null
                            ) == null
                        ) {
                            if (this.errorMsg$() == null || this.destroyed$.value) return;

                            const msg = `[DEV-ERROR] ${this.elementRef.nativeElement.tagName} Component in error state but no error element found`;
                            alert(msg);
                            console.error(new Error(msg));
                        }
                    });
                }
            });
        }
    }

    public toast: ToastrService = inject(ToastrService);
    public changeDetector: ChangeDetectorRef = inject(ChangeDetectorRef);
    public translateSrv: PlatformTranslateService = inject(PlatformTranslateService);
    public elementRef: ElementRef<HTMLElement> = inject(ElementRef);

    public initiated$: BehaviorSubject<boolean> = new BehaviorSubject<boolean>(false);
    public ngOnInitCalled$: BehaviorSubject<boolean> = new BehaviorSubject<boolean>(false);
    public viewInitiated$: BehaviorSubject<boolean> = new BehaviorSubject<boolean>(false);
    public destroyed$: BehaviorSubject<boolean> = new BehaviorSubject<boolean>(false);
    public status$: WritableSignal<ComponentStateStatus> = signal(ComponentStateStatus.Pending);
    public errorMsgMap$: WritableSignal<Dictionary<string | undefined>> = signal({});
    public loadingMap$: WritableSignal<Dictionary<boolean | null>> = signal({});
    public reloadingMap$: WritableSignal<Dictionary<boolean | null>> = signal({});
    public componentId = guid_generate();

    protected storedSubscriptionsMap: Map<string, Subscription> = new Map();
    protected storedAnonymousSubscriptions: Subscription[] = [];
    protected cachedErrorMsg$: Dictionary<Signal<string | undefined>> = {};
    protected cachedLoading$: Dictionary<Signal<boolean | null>> = {};
    protected cachedReloading$: Dictionary<Signal<boolean | null>> = {};
    protected cachedAllErrorMsgs$: Dictionary<Signal<string | null | undefined>> = {};

    /**
     * Element selectors. If return not null and any, will check element exist when is loading
     */
    protected get devModeCheckLoadingStateElement(): string | string[] | undefined {
        return undefined;
    }
    /**
     * Default is True. Custom condition for dev-mode when to check loading element
     */
    protected get devModeCheckLoadingStateElementOnlyWhen(): boolean {
        return true;
    }
    /**
     * Element selectors. If return not null and any, will check element exist when has error
     */
    protected get devModeCheckErrorStateElement(): string | string[] | undefined {
        return undefined;
    }
    /**
     * Default return false. If true, search check loading or error element in whole document body
     */
    protected get devModeCheckLoadingOrErrorAllowInGlobalDocumentBody(): boolean {
        return false;
    }

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
        this._isStatePending ??= computed(() => this.status$() == 'Pending');
        return this._isStatePending;
    }

    protected _isStateLoading?: Signal<boolean>;
    public get isStateLoading(): Signal<boolean> {
        this._isStateLoading ??= computed(() => this.status$() == 'Loading' || this.isAnyLoadingRequest() == true);
        return this._isStateLoading;
    }

    protected _isStateReloading?: Signal<boolean>;
    public get isStateReloading(): Signal<boolean> {
        this._isStateReloading ??= computed(
            () => this.status$() == 'Reloading' || this.isAnyReloadingRequest() == true
        );
        return this._isStateReloading;
    }

    public isAnyLoadingRequest = computed(() => {
        return keys(this.loadingMap$()).find(requestKey => this.loadingMap$()[requestKey]) != undefined;
    });

    public isAnyReloadingRequest = computed(() => {
        return keys(this.reloadingMap$()).find(requestKey => this.reloadingMap$()[requestKey]) != undefined;
    });

    protected _isStateInitVmLoading?: Signal<boolean>;
    public get isStateInitVmLoading(): Signal<boolean> {
        this._isStateInitVmLoading ??= computed(() => false);
        return this._isStateInitVmLoading;
    }

    protected _isStateSuccess?: Signal<boolean>;
    public get isStateSuccess(): Signal<boolean> {
        this._isStateSuccess ??= computed(() => this.status$() == 'Success');
        return this._isStateSuccess;
    }

    protected _isStateError?: Signal<boolean>;
    public get isStateError(): Signal<boolean> {
        this._isStateError ??= computed(() => this.status$() == 'Error');
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
     * Creates an RxJS operator function that subscribes to the observable until the component is destroyed.
     */
    public subscribeUntilDestroyed<T>(
        observerOrNext?: Partial<Observer<T>> | ((value: T) => void),
        outSubscriptionFn?: (subscription: Subscription) => void
    ): MonoTypeOperatorFunction<T> {
        const next = typeof observerOrNext === 'function' ? observerOrNext : observerOrNext?.next;
        const error = typeof observerOrNext === 'function' ? undefined : observerOrNext?.error;
        const complete = typeof observerOrNext === 'function' ? undefined : observerOrNext?.complete;

        return subscribeUntil(
            this.destroyed$.pipe(filter(destroyed => destroyed == true)),
            {
                next: v => {
                    if (next) {
                        next(v);
                        this.detectChanges();
                    }
                },
                error: e => {
                    if (error) {
                        error(e);
                        this.detectChanges();
                    }
                },
                complete: () => {
                    if (complete) {
                        complete();
                        this.detectChanges();
                    }
                }
            },
            outSubscriptionFn
        );
    }

    /**
     * Creates an RxJS operator function that triggers change detection after the observable completes.
     */
    public finalDetectChanges<T>(): MonoTypeOperatorFunction<T> {
        return finalize(() => this.detectChanges());
    }

    public ngOnInit(): void {
        this.detectChangesThrottle$.pipe(this.subscribeUntilDestroyed());
        this.initiated$.next(true);
        if (PLATFORM_CORE_GLOBAL_ENV.isLocalDev) this.ngOnInitCalled$.next(true);
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
        // Handle for case parent input ngTemplate for child onPush component. Child activate change detection on init, then parent init ngTemplate view later
        // but template rendered inside child component => need to trigger change detection again for the template from parent to render
        this.detectChanges();

        if (PLATFORM_CORE_GLOBAL_ENV.isLocalDev && this.ngOnInitCalled$.getValue() == false) {
            const msg = `[DEV-ERROR] Component ${this.elementRef.nativeElement.tagName}: Base Platform Component ngOnInit is not called. Please call super.ngOnInit() in the child component ngOnInit() method or manually ngOnInitCalled$.next(true) in the child component ngOnInit() method`;

            if (PLATFORM_CORE_GLOBAL_ENV.isLocalDev) {
                alert(msg);
                console.error(new Error(msg));
            }
        }
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
        options?: PlatformObserverLoadingErrorStateOptions
    ): (source: Observable<T>) => Observable<T> {
        if (requestKey == undefined) requestKey = requestStateDefaultKey;

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
                    this.untilDestroyed(),
                    onCancel(() => {
                        if (!options?.isReloading) this.setLoading(false, requestKey);
                        this.setReloading(false, requestKey);

                        checkSetStatus.bind(this)();
                    }),

                    tapOnce({
                        next: value => {
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
                            this.status$.set(ComponentStateStatus.Error);
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

        function checkSetStatus(this: PlatformComponent) {
            if (this.loadingRequestsCount() > 0) {
                if (this.status$() != ComponentStateStatus.Loading) this.status$.set(ComponentStateStatus.Loading);
            } else if (this.reloadingRequestsCount() > 0) {
                if (this.status$() != ComponentStateStatus.Reloading) this.status$.set(ComponentStateStatus.Reloading);
            } else if (this.getErrorMsg() != null && this.getErrorMsg()?.trim() != '') {
                if (this.status$() != ComponentStateStatus.Error) this.status$.set(ComponentStateStatus.Error);
            } else if (this.status$() != ComponentStateStatus.Success) this.status$.set(ComponentStateStatus.Success);
        }
    }

    /**
     * Returns an Signal that emits the error message associated with the specified request key or the first existing error message if requestKey is default key if error message with default key is null.
     * * @param [requestKey=requestStateDefaultKey] (optional): A key to identify the request. Default is
     * requestStateDefaultKey.
     */
    public getErrorMsg$(requestKey?: string, excludeKeys?: string[]): Signal<string | undefined> {
        requestKey ??= requestStateDefaultKey;

        const combinedCacheRequestKey = `${requestKey}_excludeKeys:${JSON.stringify(excludeKeys ?? '')}`;

        if (this.cachedErrorMsg$[combinedCacheRequestKey] == null) {
            this.cachedErrorMsg$[combinedCacheRequestKey] = computed(() => {
                return this.getErrorMsg(requestKey, excludeKeys);
            });
        }
        return this.cachedErrorMsg$[combinedCacheRequestKey]!;
    }

    /**
     * Returns the error message associated with the specified request key or the first existing error message if requestKey is default key if error message with default key is null.
     * * @param [requestKey=requestStateDefaultKey] (optional): A key to identify the request. Default is
     * requestStateDefaultKey.
     */
    public getErrorMsg(requestKey?: string, excludeKeys?: string[]): string | undefined {
        requestKey ??= requestStateDefaultKey;
        const excludeKeysSet = excludeKeys != undefined ? new Set(excludeKeys) : undefined;

        if (this.errorMsgMap$()[requestKey] == null && requestKey == requestStateDefaultKey)
            return Object.keys(this.errorMsgMap$())
                .filter(key => excludeKeysSet?.has(key) != true)
                .map(key => this.errorMsgMap$()[key])
                .find(errorMsg => errorMsg != null);

        return this.errorMsgMap$()[requestKey];
    }

    /**
     * Returns an Signal that emits all error messages combined into a single string.
     */
    public getAllErrorMsgs$(requestKeys?: string[], excludeKeys?: string[]): Signal<string | undefined> {
        const combinedCacheRequestKey = `${requestKeys != null ? JSON.stringify(requestKeys) : 'All'}_excludeKeys:${
            excludeKeys != null ? JSON.stringify(excludeKeys) : 'null'
        }`;

        if (this.cachedAllErrorMsgs$[combinedCacheRequestKey] == null) {
            this.cachedAllErrorMsgs$[combinedCacheRequestKey] = computed(() => {
                const errorMsgMap = this.errorMsgMap$();
                return list_distinct(
                    keys(errorMsgMap)
                        .map(key => {
                            if (
                                (requestKeys != undefined && !requestKeys.includes(key)) ||
                                excludeKeys?.includes(key) == true
                            )
                                return '';
                            return errorMsgMap[key] ?? '';
                        })
                        .filter(msg => msg != null && msg.trim() != '')
                ).join('; ');
            });
        }

        return <Signal<string | undefined>>this.cachedAllErrorMsgs$[combinedCacheRequestKey];
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
     * Returns the loading state (true or false) associated with the specified request key.
     * @param requestKey (optional): A key to identify the request. Default is requestStateDefaultKey.
     */
    public isLoading(requestKey: string = requestStateDefaultKey): boolean | null {
        return this.loadingMap$()[requestKey]!;
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
     * @param requestKey (optional): A key to identify the request. Default is requestStateDefaultKey.
     */
    public isReloading(requestKey: string = requestStateDefaultKey): boolean | null {
        return this.reloadingMap$()[requestKey]!;
    }

    /**
     * Creates an RxJS operator function that taps into the source observable to handle next, error, and complete
     * events.
     * @param nextFn A function to handle the next value emitted by the source observable.
     * @param errorFn  (optional): A function to handle errors emitted by the source observable.
     * @param completeFn (optional): A function to handle the complete event emitted by the source observable.
     */
    protected tapResponse<T>(
        nextFn?: (next: T) => void,
        errorFn?: (error: PlatformApiServiceErrorResponse | Error) => any,
        completeFn?: () => void
    ): (source: Observable<T>) => Observable<T> {
        // eslint-disable-next-line @typescript-eslint/no-empty-function
        return tap({
            next: data => {
                try {
                    if (nextFn) {
                        nextFn(data);
                        this.detectChanges();
                    }
                } catch (error) {
                    console.error(error);
                    throw error;
                }
            },
            error: err => {
                if (errorFn) {
                    errorFn(err);
                    this.detectChanges();
                }
            },
            complete: () => {
                if (completeFn) {
                    completeFn();
                    this.detectChanges();
                }
            }
        });
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
     * @param {function(origin$: OriginType): Observable<ReturnObservableType>} generator - The generator function that defines the effect.
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
        options?: { effectSubscriptionHandleFn?: (sub: Subscription) => unknown }
    ): ReturnType {
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
                        this.observerLoadingErrorState(requestKey, { isReloading: isReloading })
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
        options?: { effectSubscriptionHandleFn?: (sub: Subscription) => unknown }
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
    public clearErrorMsg = (requestKey: string = requestStateDefaultKey) => {
        const currentErrorMsgMap = this.errorMsgMap$();

        this.errorMsgMap$.set(
            immutableUpdate(
                currentErrorMsgMap,
                p => {
                    delete p[requestKey];
                },
                { updaterNotDeepMutate: true }
            )
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
     * Reloads data
     * @public
     */
    public reload() {}

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

export interface PlatformObserverLoadingErrorStateOptions {
    isReloading?: boolean;
}

interface DetectChangesParams {
    onDone?: () => any;
    checkParentForHostBinding: boolean;
}
