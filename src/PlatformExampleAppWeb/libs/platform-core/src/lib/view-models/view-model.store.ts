/* eslint-disable @typescript-eslint/no-explicit-any */
import { ComponentStore, tapResponse } from '@ngrx/component-store';
import { defer, isObservable, Observable, of, Subscription, takeUntil, tap } from 'rxjs';
import { PartialDeep } from 'type-fest';

import { PlatformApiServiceErrorResponse } from '../api-services';
import { immutableUpdate } from '../utils';
import { PlatformVm } from './generic.view-model';

export abstract class PlatformVmStore<TViewModel extends PlatformVm> extends ComponentStore<TViewModel> {
  private storedSubscriptionsMap: Map<string, Subscription> = new Map();
  private storedAnonymousSubscriptions: Subscription[] = [];

  constructor(defaultState?: TViewModel) {
    super(defaultState);
    this.destroy$.subscribe(() => this.cancelAllStoredSubscriptions());
  }

  public abstract readonly vm$: Observable<TViewModel>;

  public updateState(
    partialStateOrUpdaterFn:
      | PartialDeep<TViewModel>
      | Partial<TViewModel>
      | ((state: TViewModel) => void | PartialDeep<TViewModel> | Partial<TViewModel>)
  ): void {
    super.setState(state => {
      return immutableUpdate(state, partialStateOrUpdaterFn);
    });
  }

  public readonly updateApiErrorState = (errorResponse: PlatformApiServiceErrorResponse | Error) => {
    this.updateState(<PartialDeep<TViewModel>>{
      status: 'Error',
      error: PlatformApiServiceErrorResponse.getDefaultFormattedMessage(errorResponse)
    });
  };

  public get currentVm(): TViewModel {
    return this.get();
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
    requestKey: string = PlatformVm.requestStateDefaultKey,
    deferSetLoading: boolean = false
  ): (source: Observable<T>) => Observable<T> {
    const setLoadingState = () => {
      this.updateState(<Partial<TViewModel>>{ status: 'Loading' });

      this.setLoading(true, requestKey);
      this.setErrorMsg(null, requestKey);
    };

    return (source: Observable<T>) => {
      if (!deferSetLoading) setLoadingState();

      return defer(() => {
        if (deferSetLoading) setLoadingState();

        return source.pipe(
          tap({
            next: () => {
              if (this.get().status != 'Error') this.updateState(<Partial<TViewModel>>{ status: 'Success' });
              this.setLoading(false, requestKey);
            },
            error: (err: PlatformApiServiceErrorResponse | Error) => {
              this.updateApiErrorState(err);

              this.setLoading(false, requestKey);
              this.setErrorMsg(err, requestKey);
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

    this.updateState(<Partial<TViewModel>>{ errorMsgMap: { [requestKey]: errorMsg } });
  };

  public setLoading = (value: boolean | null, requestKey: string = PlatformVm.requestStateDefaultKey) => {
    this.updateState(<Partial<TViewModel>>{ loadingMap: { [requestKey]: value } });
  };

  public override effect<
    ProvidedType,
    OriginType extends
    | Observable<ProvidedType>
    | unknown = Observable<ProvidedType>,
    ObservableType = OriginType extends Observable<infer A> ? A : never,
    ReturnType = ProvidedType | ObservableType extends void
      ? (observableOrValue?: ObservableType | Observable<ObservableType> | undefined) => Subscription
      : (observableOrValue: ObservableType | Observable<ObservableType>) => Subscription
  >(generator: (origin$: OriginType) => Observable<unknown>): ReturnType {
    let previousEffectSub: Subscription = new Subscription();

    return ((observableOrValue?: ObservableType | Observable<ObservableType>): Subscription => {
      previousEffectSub.unsubscribe();

      const observable$ = isObservable(observableOrValue) ? observableOrValue : of(observableOrValue);

      const newEffectSub: Subscription = generator(<OriginType><any>observable$).pipe(takeUntil(this.destroy$)).subscribe();

      this.storeAnonymousSubscription(newEffectSub);
      previousEffectSub = newEffectSub;

      return newEffectSub;
    }) as unknown as ReturnType;
  }

  protected tapResponse<T>(
    nextFn: (next: T) => void,
    errorFn?: (error: any) => void,
    completeFn?: () => void
  ): (source: Observable<T>) => Observable<T> {
    // eslint-disable-next-line @typescript-eslint/no-empty-function
    return tapResponse(nextFn, errorFn ?? (() => {}), completeFn);
  }

  protected storeSubscription(key: string, subscription: Subscription): void {
    this.storedSubscriptionsMap.set(key, subscription);
  }

  protected storeAnonymousSubscription(subscription: Subscription): void {
    this.storedAnonymousSubscriptions.push(subscription);
  }

  protected cancelStoredSubscription(key: string): void {
    this.storedSubscriptionsMap.get(key)?.unsubscribe();
    this.storedSubscriptionsMap.delete(key);
  }

  protected cancelAllStoredSubscriptions(): void {
    this.storedSubscriptionsMap.forEach((value, key) => this.cancelStoredSubscription(key));
    this.storedAnonymousSubscriptions.forEach((value, key) => value.unsubscribe());
  }

  public override patchState(
    // eslint-disable-next-line @typescript-eslint/no-unused-vars
    partialStateOrUpdaterFn:
      | Partial<TViewModel>
      | Observable<Partial<TViewModel>>
      | ((state: TViewModel) => Partial<TViewModel>)
  ): void {
    throw new Error('Do not use this function from the library. Use updateState instead to handle immutable case');
  }

  // eslint-disable-next-line @typescript-eslint/no-unused-vars
  public override setState(stateOrUpdaterFn: TViewModel | ((state: TViewModel) => TViewModel)): void {
    throw new Error('Do not use this function from the library. Use updateState instead to handle immutable case');
  }
}
