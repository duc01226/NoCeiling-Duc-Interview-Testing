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
import { onCancel } from '../../rxjs';
import { PlatformTranslateService } from '../../translations';
import { clone, keys, list_remove, task_delay } from '../../utils';
import { ComponentSimpleChanges } from '../simple-changes';

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

    protected storedSubscriptionsMap: Map<string, Subscription> = new Map();
    protected storedAnonymousSubscriptions: Subscription[] = [];
    protected cachedErrorMsg$: Dictionary<Observable<string | null | undefined>> = {};
    protected cachedLoading$: Dictionary<Observable<boolean | null>> = {};
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

    public untilDestroyed<T>(): MonoTypeOperatorFunction<T> {
        return takeUntil(this.destroyed$.pipe(filter(destroyed => destroyed == true)));
    }

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

    public ngOnInputChanged(changes: ComponentSimpleChanges<object>): void {
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
    public loadingRequestsCount() {
        let result = 0;
        Object.keys(this.loadingRequestsCountMap).forEach(key => {
            result += this.loadingRequestsCountMap[key];
        });
        return result;
    }

    /**
     *
     *  Changes state based on observable request
     *
     * @template T Type of observable
     * @returns
     * @usage
     *  ** TS:
     *  apiService.loadData().pipe(this.observerLoadingState()).subscribe()
     */
    public observerLoadingState<T>(
        requestKey: string = requestStateDefaultKey,
        options?: PlatformObserverLoadingStateOptions<T>
    ): (source: Observable<T>) => Observable<T> {
        const setLoadingState = () => {
            this.loadingState$.next(LoadingState.Loading);

            this.setLoading(true, requestKey);
            this.setErrorMsg(null, requestKey);
        };

        return (source: Observable<T>) => {
            return defer(() => {
                setLoadingState();

                return source.pipe(
                    onCancel(() => this.setLoading(false, requestKey)),
                    tap({
                        next: value => {
                            // Timeout to queue this update state success/failed after tapResponse
                            setTimeout(() => {
                                if (this.loadingState$.value != LoadingState.Error && this.loadingRequestsCount() <= 1)
                                    this.loadingState$.next(LoadingState.Success);

                                if (options?.onSuccess != null) options.onSuccess(value);

                                this.setLoading(false, requestKey);
                            });
                        },
                        error: (err: PlatformApiServiceErrorResponse | Error) => {
                            // Timeout to queue this update state success/failed after tapResponse
                            setTimeout(() => {
                                this.setErrorMsg(err, requestKey);
                                this.loadingState$.next(LoadingState.Error);

                                if (options?.onError != null) options.onError(err);

                                this.setLoading(false, requestKey);
                            });
                        }
                    })
                );
            });
        };
    }

    public getErrorMsg$(requestKey: string = requestStateDefaultKey): Observable<string | null | undefined> {
        if (this.cachedErrorMsg$[requestKey] == null) {
            this.cachedErrorMsg$[requestKey] = this.errorMsgMap$.pipe(map(_ => this.getErrorMsg(requestKey)));
        }
        return this.cachedErrorMsg$[requestKey];
    }

    public getErrorMsg(requestKey: string = requestStateDefaultKey): string | undefined | null {
        if (this.errorMsgMap$.getValue()[requestKey] == null && requestKey == requestStateDefaultKey)
            return Object.keys(this.errorMsgMap$.getValue())
                .map(key => this.errorMsgMap$.getValue()[key])
                .find(errorMsg => errorMsg != null);

        return this.errorMsgMap$.getValue()[requestKey];
    }

    public getAllErrorMsgs$(): Observable<string | null> {
        if (this.allErrorMsgs == null) {
            this.allErrorMsgs = this.errorMsgMap$.pipe(
                map(_ =>
                    keys(_)
                        .map(key => _[key] ?? '')
                        .filter(msg => msg != '' && msg != null)
                        .join('; ')
                ),
                filter(_ => _ != '')
            );
        }

        return this.allErrorMsgs;
    }

    public isLoading$(requestKey: string = requestStateDefaultKey): Observable<boolean | null> {
        if (this.cachedLoading$[requestKey] == null) {
            this.cachedLoading$[requestKey] = this.loadingMap$.pipe(map(_ => this.isLoading(requestKey)));
        }
        return this.cachedLoading$[requestKey];
    }

    public isLoading(errorKey: string = requestStateDefaultKey): boolean | null {
        return this.loadingMap$.getValue()[errorKey];
    }

    protected tapResponse<T>(
        nextFn: (next: T) => void,
        errorFn?: (error: PlatformApiServiceErrorResponse | Error) => any,
        completeFn?: () => void
    ): (source: Observable<T>) => Observable<T> {
        // eslint-disable-next-line @typescript-eslint/no-empty-function
        return tapResponse(nextFn, errorFn ?? (() => {}), completeFn);
    }

    /**
     * Creates an effect. Ex: this.effect()
     *
     * This effect is subscribed to throughout the lifecycle of the Component.
     * @param generator A function that takes an origin Observable input and
     *     returns an Observable. The Observable that is returned will be
     *     subscribed to for the life of the component.
     * @return A function that, when called, will trigger the origin Observable.
     */
    public effect<TOrigin, TReturn>(
        generator: (origin: TOrigin | Observable<TOrigin | null> | null) => Observable<TReturn>
    ) {
        let previousEffectSub: Subscription = new Subscription();

        return (observableOrValue: TOrigin | Observable<TOrigin> | null = null) => {
            previousEffectSub.unsubscribe();

            // ThrottleTime explain: Delay to enhance performance
            // { leading: true, trailing: true } <=> emit the first item to ensure not delay, but also ignore the sub-sequence,
            // and still emit the latest item to ensure data is latest
            const newEffectSub: Subscription = generator(observableOrValue)
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

    protected storeSubscription(key: string, subscription: Subscription): void {
        this.storedSubscriptionsMap.set(key, subscription);
    }

    protected storeAnonymousSubscription(subscription: Subscription): void {
        list_remove(this.storedAnonymousSubscriptions, p => p.closed);
        this.storedAnonymousSubscriptions.push(subscription);
    }

    protected cancelStoredSubscription(key: string): void {
        this.storedSubscriptionsMap.get(key)?.unsubscribe();
        this.storedSubscriptionsMap.delete(key);
    }

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

    protected setLoading = (value: boolean | null, requestKey: string = requestStateDefaultKey) => {
        this.loadingMap$.next(
            clone(this.loadingMap$.value, _ => {
                _[requestKey] = value;
            })
        );

        if (this.loadingRequestsCountMap[requestKey] == undefined) this.loadingRequestsCountMap[requestKey] = 0;
        if (value == true) this.loadingRequestsCountMap[requestKey] += 1;
        if (value == false && this.loadingRequestsCountMap[requestKey] > 0)
            this.loadingRequestsCountMap[requestKey] -= 1;

        this.detectChanges();
    };

    protected cancelAllStoredSubscriptions(): void {
        this.storedSubscriptionsMap.forEach((value, key) => this.cancelStoredSubscription(key));
        this.cancelAllStoredAnonymousSubscriptions();
    }

    protected ngForTrackByImmutableList<TItem>(trackTargetList: TItem[]): (index: number, item: TItem) => TItem[] {
        return () => trackTargetList;
    }

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
}

interface DetectChangesParams {
    onDone?: () => any;
    checkParentForHostBinding: boolean;
}
