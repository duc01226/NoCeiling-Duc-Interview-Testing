/* eslint-disable @typescript-eslint/no-explicit-any */
import {
    AfterViewInit,
    ChangeDetectorRef,
    Directive,
    inject,
    OnChanges,
    OnDestroy,
    OnInit,
    SimpleChanges
} from '@angular/core';
import { tapResponse } from '@ngrx/component-store';
import { ToastrService } from 'ngx-toastr';
import {
    asyncScheduler,
    BehaviorSubject,
    defer,
    MonoTypeOperatorFunction,
    Observable,
    Subject,
    Subscription
} from 'rxjs';
import { filter, finalize, map, takeUntil, tap, throttleTime } from 'rxjs/operators';

import { PlatformApiServiceErrorResponse } from '../../api-services';
import { LifeCycleHelper } from '../../helpers';
import { distinctUntilObjectValuesChanged, onCancel, tapOnce } from '../../rxjs';
import { PlatformTranslateService } from '../../translations';
import { clone, keys, list_remove, task_delay } from '../../utils';

export const enum LoadingState {
    Error = 'Error',
    Loading = 'Loading',
    Success = 'Success',
    Pending = 'Pending'
}

const requestStateDefaultKey = 'Default';

@Directive()
export abstract class PlatformComponent implements OnInit, AfterViewInit, OnDestroy, OnChanges {
    public toast: ToastrService = inject(ToastrService);
    public changeDetector: ChangeDetectorRef = inject(ChangeDetectorRef);
    public translateSrv: PlatformTranslateService = inject(PlatformTranslateService);

    public static get defaultDetectChangesDelay(): number {
        return 20;
    }

    public initiated$: BehaviorSubject<boolean> = new BehaviorSubject<boolean>(false);
    public viewInitiated$: BehaviorSubject<boolean> = new BehaviorSubject<boolean>(false);
    public destroyed$: BehaviorSubject<boolean> = new BehaviorSubject<boolean>(false);
    // General loadingState when not specific requestKey, requestKey = requestStateDefaultKey;
    public loadingState$: BehaviorSubject<LoadingState> = new BehaviorSubject<LoadingState>(LoadingState.Pending);
    public errorMsgMap$: BehaviorSubject<Dictionary<string | undefined | null>> = new BehaviorSubject({});
    public loadingMap$: BehaviorSubject<Dictionary<boolean | null>> = new BehaviorSubject<Dictionary<boolean | null>>(
        {}
    );
    public reloadingMap$: BehaviorSubject<Dictionary<boolean | null>> = new BehaviorSubject<Dictionary<boolean | null>>(
        {}
    );

    protected storedSubscriptionsMap: Map<string, Subscription> = new Map();
    protected storedAnonymousSubscriptions: Subscription[] = [];
    protected cachedErrorMsg$: Dictionary<Observable<string | null | undefined>> = {};
    protected cachedLoading$: Dictionary<Observable<boolean | null>> = {};
    protected cachedReloading$: Dictionary<Observable<boolean | null>> = {};
    protected allErrorMsgs!: Observable<string | null>;

    protected detectChangesThrottleSource = new Subject<DetectChangesParams>();
    protected detectChangesThrottle$ = this.detectChangesThrottleSource.pipe(
        throttleTime(300, asyncScheduler, { leading: true, trailing: true }),
        tap(params => {
            this.doDetectChanges(params);
        })
    );

    protected doDetectChanges(params: DetectChangesParams) {
        if (this.canDetectChanges) {
            this.changeDetector.detectChanges();
            if (params.checkParentForHostBinding) this.changeDetector.markForCheck();
            if (params.onDone != undefined) params.onDone();
        }
    }

    public detectChanges(delayTime?: number, onDone?: () => unknown, checkParentForHostBinding: boolean = false): void {
        this.cancelStoredSubscription('detectChangesDelaySubs');

        if (this.canDetectChanges) {
            const finalDelayTime = delayTime == null ? PlatformComponent.defaultDetectChangesDelay : delayTime;
            const detectChangesDelaySubs = task_delay(
                () =>
                    this.detectChangesThrottleSource.next({
                        onDone: onDone,
                        checkParentForHostBinding: checkParentForHostBinding
                    }),
                finalDelayTime
            );

            this.storeSubscription('detectChangesDelaySubs', detectChangesDelaySubs);
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
     * Returns the total number of active loading requests.
     */
    public loadingRequestsCount() {
        let result = 0;
        Object.keys(this.loadingRequestsCountMap).forEach(key => {
            result += this.loadingRequestsCountMap[key];
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
            result += this.reloadingRequestsCountMap[key];
        });
        return result;
    }

    /**
     *  Creates an RxJS operator function that changes the loading state based on an observable request.
     * @param [requestKey=requestStateDefaultKey]  (optional): A key to identify the request. Default is requestStateDefaultKey
     * @param options (optional): Additional options for handling success and error states.
     * @usage
     *  Example:
     *  apiService.loadData().pipe(this.observerLoadingState()).subscribe()
     */
    public observerLoadingState<T>(
        requestKey: string = requestStateDefaultKey,
        options?: PlatformObserverLoadingStateOptions<T>
    ): (source: Observable<T>) => Observable<T> {
        const setLoadingState = () => {
            if (!this.isForSetReloadingState(options) && this.loadingState$.getValue() != LoadingState.Loading)
                this.loadingState$.next(LoadingState.Loading);

            if (this.isForSetReloadingState(options)) this.setReloading(true, requestKey);
            else this.setLoading(true, requestKey);

            this.setErrorMsg(null, requestKey);
        };

        return (source: Observable<T>) => {
            return defer(() => {
                const previousLoadingState = this.loadingState$.getValue();

                setLoadingState();

                return source.pipe(
                    onCancel(() => {
                        if (this.isForSetReloadingState(options)) this.setReloading(false, requestKey);
                        else this.setLoading(false, requestKey);

                        if (
                            this.loadingState$.getValue() == 'Loading' &&
                            this.loadingRequestsCount() <= 0 &&
                            previousLoadingState == 'Success'
                        )
                            this.loadingState$.next(LoadingState.Success);
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
                                this.loadingState$.value != LoadingState.Error &&
                                this.loadingState$.value != LoadingState.Success &&
                                this.loadingRequestsCount() <= 0
                            )
                                this.loadingState$.next(LoadingState.Success);
                        },
                        error: (err: PlatformApiServiceErrorResponse | Error) => {
                            this.setErrorMsg(err, requestKey);
                            this.loadingState$.next(LoadingState.Error);
                        }
                    })
                );
            });
        };
    }

    private isForSetReloadingState<T>(options: PlatformObserverLoadingStateOptions<T> | undefined) {
        return options?.isReloading && this.getErrorMsg() == null && !this.isLoading;
    }

    /**
     * Returns an observable that emits the error message associated with the specified request key.
     */
    public getErrorMsg$(requestKey: string = requestStateDefaultKey): Observable<string | null | undefined> {
        if (this.cachedErrorMsg$[requestKey] == null) {
            this.cachedErrorMsg$[requestKey] = this.errorMsgMap$.pipe(
                map(_ => this.getErrorMsg(requestKey)),
                distinctUntilObjectValuesChanged()
            );
        }
        return this.cachedErrorMsg$[requestKey];
    }

    /**
     * Returns the error message associated with the specified request key.
     * * @param [requestKey=requestStateDefaultKey] (optional): A key to identify the request. Default is requestStateDefaultKey.
     */
    public getErrorMsg(requestKey: string = requestStateDefaultKey): string | undefined | null {
        if (this.errorMsgMap$.getValue()[requestKey] == null && requestKey == requestStateDefaultKey)
            return Object.keys(this.errorMsgMap$.getValue())
                .map(key => this.errorMsgMap$.getValue()[key])
                .find(errorMsg => errorMsg != null);

        return this.errorMsgMap$.getValue()[requestKey];
    }

    /**
     * Returns an observable that emits all error messages combined into a single string.
     */
    public getAllErrorMsgs$(): Observable<string | null> {
        if (this.allErrorMsgs == null) {
            this.allErrorMsgs = this.errorMsgMap$.pipe(
                map(_ =>
                    keys(_)
                        .map(key => _[key] ?? '')
                        .filter(msg => msg != '' && msg != null)
                        .join('; ')
                ),
                filter(_ => _ != ''),
                distinctUntilObjectValuesChanged()
            );
        }

        return this.allErrorMsgs;
    }

    /**
     * Returns an obseravble that emits the loading state (true or false) associated with the specified request key.
     * @param [requestKey=requestStateDefaultKey] (optional): A key to identify the request. Default is requestStateDefaultKey.
     */
    public isLoading$(requestKey: string = requestStateDefaultKey): Observable<boolean | null> {
        if (this.cachedLoading$[requestKey] == null) {
            this.cachedLoading$[requestKey] = this.loadingMap$.pipe(
                map(_ => this.isLoading(requestKey)),
                distinctUntilObjectValuesChanged()
            );
        }
        return this.cachedLoading$[requestKey];
    }

    /**
     * Returns the loading state (true or false) associated with the specified request key.
     * @param errorKey (optional): A key to identify the request. Default is requestStateDefaultKey.
     */
    public isLoading(errorKey: string = requestStateDefaultKey): boolean | null {
        return this.loadingMap$.getValue()[errorKey];
    }

    /**
     * Returns an obseravble that emits the reloading state (true or false) associated with the specified request key.
     * @param [requestKey=requestStateDefaultKey] (optional): A key to identify the request. Default is requestStateDefaultKey.
     */
    public isReloading$(requestKey: string = requestStateDefaultKey): Observable<boolean | null> {
        if (this.cachedReloading$[requestKey] == null) {
            this.cachedReloading$[requestKey] = this.reloadingMap$.pipe(
                map(_ => this.isReloading(requestKey)),
                distinctUntilObjectValuesChanged()
            );
        }
        return this.cachedReloading$[requestKey];
    }

    /**
     * Returns the reloading state (true or false) associated with the specified request key.
     * @param errorKey (optional): A key to identify the request. Default is requestStateDefaultKey.
     */
    public isReloading(errorKey: string = requestStateDefaultKey): boolean | null {
        return this.reloadingMap$.getValue()[errorKey];
    }

    /**
     * Creates an RxJS operator function that taps into the source observable to handle next, error, and complete events.
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
        return tapResponse(nextFn, errorFn ?? (() => {}), completeFn);
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
     * Ex2: public loadData = this.effect((query$: Observable<TQuery>) => query$.pipe(switchMap(query => this.someApi.load(query)))); Use function: this.loadData(<TQuery>query param here);
     */
    public effect<TOrigin, TReturn>(
        generator: (origin: TOrigin | Observable<TOrigin | null> | null, isReloading?: boolean) => Observable<TReturn>
    ) {
        let previousEffectSub: Subscription = new Subscription();

        return (observableOrValue: TOrigin | Observable<TOrigin> | null = null, isReloading?: boolean) => {
            previousEffectSub.unsubscribe();

            // ThrottleTime explain: Delay to enhance performance
            // { leading: true, trailing: true } <=> emit the first item to ensure not delay, but also ignore the sub-sequence,
            // and still emit the latest item to ensure data is latest
            const newEffectSub: Subscription = generator(observableOrValue, isReloading)
                .pipe(throttleTime(300, asyncScheduler, { leading: true, trailing: true }))
                .pipe(
                    this.untilDestroyed(),
                    finalize(() => {
                        this.detectChanges();
                    })
                )
                .subscribe();

            this.storeAnonymousSubscription(newEffectSub);
            previousEffectSub = newEffectSub;

            this.detectChanges();

            return newEffectSub;
        };
    }

    protected get canDetectChanges(): boolean {
        return this.initiated$.value && !this.destroyed$.value;
    }

    /**
     * Stores a subscription using the specified key. The subscription will be unsubscribed when the component is destroyed.
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
     * Sets the error message for the specified request key.
     */
    protected setErrorMsg = (
        error: string | null | PlatformApiServiceErrorResponse | Error,
        requestKey: string = requestStateDefaultKey
    ) => {
        if (typeof error == 'string' || error == null)
            this.errorMsgMap$.next(
                clone(this.errorMsgMap$.value, _ => {
                    _[requestKey] = <string | null>error;
                })
            );
        else
            this.errorMsgMap$.next(
                clone(this.errorMsgMap$.value, _ => {
                    _[requestKey] = PlatformApiServiceErrorResponse.getDefaultFormattedMessage(error);
                })
            );

        this.detectChanges();
    };

    protected clearErrorMsg = () => {
        this.errorMsgMap$.next({});
    };

    /**
     * Sets the loading state for the specified request key.
     */
    protected setLoading = (value: boolean | null, requestKey: string = requestStateDefaultKey) => {
        if (this.loadingRequestsCountMap[requestKey] == undefined) this.loadingRequestsCountMap[requestKey] = 0;

        if (value == true) this.loadingRequestsCountMap[requestKey] += 1;
        if (value == false && this.loadingRequestsCountMap[requestKey] > 0)
            this.loadingRequestsCountMap[requestKey] -= 1;

        this.loadingMap$.next(
            clone(this.loadingMap$.value, _ => {
                _[requestKey] = this.loadingRequestsCountMap[requestKey] > 0;
            })
        );

        this.detectChanges();
    };

    /**
     * Sets the loading state for the specified request key.
     */
    protected setReloading = (value: boolean | null, requestKey: string = requestStateDefaultKey) => {
        if (this.reloadingRequestsCountMap[requestKey] == undefined) this.reloadingRequestsCountMap[requestKey] = 0;

        if (value == true) this.reloadingRequestsCountMap[requestKey] += 1;
        if (value == false && this.reloadingRequestsCountMap[requestKey] > 0)
            this.reloadingRequestsCountMap[requestKey] -= 1;

        this.reloadingMap$.next(
            clone(this.reloadingMap$.value, _ => {
                _[requestKey] = this.reloadingRequestsCountMap[requestKey] > 0;
            })
        );

        this.detectChanges();
    };

    protected cancelAllStoredSubscriptions(): void {
        this.storedSubscriptionsMap.forEach((value, key) => this.cancelStoredSubscription(key));
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
        this.errorMsgMap$.complete();
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
