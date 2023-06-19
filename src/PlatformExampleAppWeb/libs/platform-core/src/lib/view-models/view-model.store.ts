import { Injectable, OnDestroy } from '@angular/core';
/* eslint-disable @typescript-eslint/no-explicit-any */
import { ComponentStore, SelectConfig, tapResponse } from '@ngrx/component-store';
import {
    asyncScheduler,
    defer,
    isObservable,
    map,
    Observable,
    of,
    OperatorFunction,
    Subscription,
    switchMap,
    takeUntil,
    throttleTime
} from 'rxjs';
import { PartialDeep } from 'type-fest';

import { PlatformApiServiceErrorResponse } from '../api-services';
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
    private cachedLoading$: Dictionary<Observable<boolean | undefined>> = {};
    private innerStore: ComponentStore<TViewModel>;
    private onInitVmTriggered: boolean = false;

    constructor(defaultState: TViewModel) {
        this.innerStore = new ComponentStore(defaultState);
    }

    private _vm$?: Observable<TViewModel>;
    public get vm$(): Observable<TViewModel> {
        if (this._vm$ == undefined) {
            this._vm$ = this.select(s => s).pipe(tapOnce({ next: s => this.triggerOnInitVm() }));
        }

        return this._vm$;
    }

    public abstract onInitVm: () => Observable<unknown> | void;

    public triggerOnInitVm() {
        if (!this.onInitVmTriggered) {
            this.onInitVmTriggered = true;

            // Process trigger call onInitVm
            const onInitVmObservableOrVoid = this.onInitVm();
            if (onInitVmObservableOrVoid instanceof Observable) {
                this.storeAnonymousSubscription(onInitVmObservableOrVoid.subscribe());
            }
        }
    }

    public ngOnDestroy(): void {
        this.innerStore.ngOnDestroy();
        this.cancelAllStoredSubscriptions();
    }

    public get state$() {
        return this.innerStore.select(p => p);
    }

    public abstract reload: () => void;

    public readonly defaultSelectConfig: PlatformStoreSelectConfig = {
        debounce: false,
        throttleTimeDuration: defaultThrottleDuration
    };

    private _isStatePending$?: Observable<boolean>;
    public get isStatePending$(): Observable<boolean> {
        this._isStatePending$ ??= this.state$.pipe(map(_ => _.isStatePending));

        return this._isStatePending$;
    }

    private _isStateLoading$?: Observable<boolean>;
    public get isStateLoading$(): Observable<boolean> {
        this._isStateLoading$ ??= this.state$.pipe(map(_ => _.isStateLoading));

        return this._isStateLoading$;
    }

    private _isStateSuccess$?: Observable<boolean>;
    public get isStateSuccess$(): Observable<boolean> {
        this._isStateSuccess$ ??= this.state$.pipe(map(_ => _.isStateSuccess));

        return this._isStateSuccess$;
    }

    private _isStateError$?: Observable<boolean>;
    public get isStateError$(): Observable<boolean> {
        this._isStateError$ ??= this.state$.pipe(map(_ => _.isStateError));

        return this._isStateError$;
    }

    public updateState(
        partialStateOrUpdaterFn:
            | PartialDeep<TViewModel>
            | Partial<TViewModel>
            | ((state: TViewModel) => void | PartialDeep<TViewModel> | Partial<TViewModel>)
    ): void {
        this.innerStore.setState(state => {
            try {
                return immutableUpdate(state, partialStateOrUpdaterFn);
            } catch (error) {
                return immutableUpdate(state, this.buildSetErrorPartialState(<Error>error));
            }
        });
    }

    public readonly setErrorState = (errorResponse: PlatformApiServiceErrorResponse | Error) => {
        this.updateState(this.buildSetErrorPartialState(errorResponse));
    };

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
        requestKey: string = PlatformVm.requestStateDefaultKey
    ): (source: Observable<T>) => Observable<T> {
        const setLoadingState = () => {
            this.updateState(<Partial<TViewModel>>{ status: 'Loading' });

            this.setLoading(true, requestKey);
            this.setErrorMsg(null, requestKey);
        };

        return (source: Observable<T>) => {
            return defer(() => {
                setLoadingState();

                return source.pipe(
                    onCancel(() => this.setLoading(false, requestKey)),
                    tapOnce({
                        next: () => {
                            // Timeout to queue this update state success/failed after tapResponse
                            setTimeout(() => {
                                if (this.currentState.status != 'Error' && this.loadingRequestsCount() <= 1)
                                    this.updateState(<Partial<TViewModel>>{ status: 'Success' });

                                this.setLoading(false, requestKey);
                            });
                        },
                        error: (err: PlatformApiServiceErrorResponse | Error) => {
                            // Timeout to queue this update state success/failed after tapResponse
                            setTimeout(() => {
                                this.setErrorMsg(err, requestKey);
                                this.setErrorState(err);

                                this.setLoading(false, requestKey);
                            });
                        }
                    })
                );
            });
        };
    }

    public setErrorMsg = (
        error: string | null | PlatformApiServiceErrorResponse | Error,
        requestKey: string = PlatformVm.requestStateDefaultKey
    ) => {
        const errorMsg =
            typeof error == 'string' || error == null
                ? <string | null>error
                : PlatformApiServiceErrorResponse.getDefaultFormattedMessage(error);

        this.updateState(<Partial<TViewModel>>{ errorMsgMap: { [requestKey]: errorMsg }, error: errorMsg });
    };

    public getErrorMsg$ = (requestKey: string = requestStateDefaultKey) => {
        if (this.cachedErrorMsg$[requestKey] == null) {
            this.cachedErrorMsg$[requestKey] = this.state$.pipe(map(_ => _.getErrorMsg(requestKey)));
        }
        return this.cachedErrorMsg$[requestKey];
    };

    public setLoading = (value: boolean | null, requestKey: string = PlatformVm.requestStateDefaultKey) => {
        this.updateState(<Partial<TViewModel>>{ loadingMap: { [requestKey]: value } });

        if (this.loadingRequestsCountMap[requestKey] == undefined) this.loadingRequestsCountMap[requestKey] = 0;
        if (value == true) this.loadingRequestsCountMap[requestKey] += 1;
        if (value == false && this.loadingRequestsCountMap[requestKey] > 0)
            this.loadingRequestsCountMap[requestKey] -= 1;
    };

    public isLoading$ = (requestKey: string = requestStateDefaultKey) => {
        if (this.cachedLoading$[requestKey] == null) {
            this.cachedLoading$[requestKey] = this.state$.pipe(map(_ => _.isLoading(requestKey)));
        }
        return this.cachedLoading$[requestKey];
    };

    public select<Result>(
        projector: (s: TViewModel) => Result,
        config?: PlatformStoreSelectConfig
    ): Observable<Result> {
        const selectConfig = config ?? this.defaultSelectConfig;

        let selectResult$ = this.innerStore
            .select(projector, selectConfig)
            .pipe(tapOnce({ next: () => this.triggerOnInitVm() }));

        // ThrottleTime explain: Delay to enhance performance
        // { leading: true, trailing: true } <=> emit the first item to ensure not delay, but also ignore the sub-sequence,
        // and still emit the latest item to ensure data is latest
        if (selectConfig.throttleTimeDuration != undefined && selectConfig.throttleTimeDuration > 0)
            selectResult$ = selectResult$.pipe(
                throttleTime(selectConfig.throttleTimeDuration ?? 0, asyncScheduler, { leading: true, trailing: true })
            );

        return selectResult$;
    }

    public effect<
        ProvidedType,
        OriginType extends Observable<ProvidedType> | unknown = Observable<ProvidedType>,
        ObservableType = OriginType extends Observable<infer A> ? A : never,
        ReturnType = ProvidedType | ObservableType extends void
            ? (observableOrValue?: ObservableType | Observable<ObservableType> | undefined) => Subscription
            : (observableOrValue: ObservableType | Observable<ObservableType>) => Subscription
    >(generator: (origin$: OriginType) => Observable<unknown>): ReturnType {
        let previousEffectSub: Subscription = new Subscription();

        return ((observableOrValue?: ObservableType | Observable<ObservableType>): Subscription => {
            previousEffectSub.unsubscribe();

            const observable$ = isObservable(observableOrValue) ? observableOrValue : of(observableOrValue);

            // ThrottleTime explain: Delay to enhance performance
            // { leading: true, trailing: true } <=> emit the first item to ensure not delay, but also ignore the sub-sequence,
            // and still emit the latest item to ensure data is latest
            const newEffectSub: Subscription = generator(
                <OriginType>(
                    (<any>(
                        observable$.pipe(
                            throttleTime(defaultThrottleDuration, asyncScheduler, { leading: true, trailing: true })
                        )
                    ))
                )
            )
                .pipe(takeUntil(this.innerStore.destroy$))
                .subscribe();

            this.storeAnonymousSubscription(newEffectSub);
            previousEffectSub = newEffectSub;

            return newEffectSub;
        }) as unknown as ReturnType;
    }

    public switchMapVm<T>(): OperatorFunction<T, TViewModel> {
        return switchMap(p => this.select(vm => vm));
    }

    protected tapResponse<T, E = any>(
        nextFn: (next: T) => void,
        errorFn?: (error: E) => void,
        completeFn?: () => void
    ): (source: Observable<T>) => Observable<T> {
        return tapResponse(nextFn, errorFn ?? (() => {}), completeFn);
    }

    protected storeSubscription(key: string, subscription: Subscription): void {
        this.storedSubscriptionsMap.set(key, subscription);
    }

    protected storeAnonymousSubscription(subscription: Subscription): void {
        list_remove(this.storedAnonymousSubscriptions, p => p.closed);
        this.storedAnonymousSubscriptions.push(subscription);
    }

    protected subscribe(observable: Observable<unknown>): Subscription {
        const subs = observable.subscribe();

        this.storeAnonymousSubscription(subs);

        return subs;
    }

    protected cancelStoredSubscription(key: string): void {
        this.storedSubscriptionsMap.get(key)?.unsubscribe();
        this.storedSubscriptionsMap.delete(key);
    }

    protected cancelAllStoredSubscriptions(): void {
        this.storedSubscriptionsMap.forEach((sub, key) => this.cancelStoredSubscription(key));
        this.storedAnonymousSubscriptions.forEach(sub => sub.unsubscribe());
    }

    protected get(): TViewModel {
        return this.currentState;
    }

    // public override patchState(
    //     // eslint-disable-next-line @typescript-eslint/no-unused-vars
    //     partialStateOrUpdaterFn:
    //         | Partial<TViewModel>
    //         | Observable<Partial<TViewModel>>
    //         | ((state: TViewModel) => Partial<TViewModel>)
    // ): void {
    //     throw new Error('Do not use this function from the library. Use updateState instead to handle immutable case');
    // }

    // eslint-disable-next-line @typescript-eslint/no-unused-vars
    // public override setState(stateOrUpdaterFn: TViewModel | ((state: TViewModel) => TViewModel)): void {
    //     throw new Error('Do not use this function from the library. Use updateState instead to handle immutable case');
    // }
}
