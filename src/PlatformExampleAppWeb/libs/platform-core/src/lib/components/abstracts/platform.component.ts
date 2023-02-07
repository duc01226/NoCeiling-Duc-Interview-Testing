/* eslint-disable @typescript-eslint/no-explicit-any */
import { AfterViewInit, ChangeDetectorRef, Directive, inject, OnDestroy, OnInit } from '@angular/core';
import { tapResponse } from '@ngrx/component-store';
import { ToastrService } from 'ngx-toastr';
import { BehaviorSubject, defer, MonoTypeOperatorFunction, Observable, of, Subscription } from 'rxjs';
import { filter, finalize, map, takeUntil, tap } from 'rxjs/operators';

import { PlatformApiServiceErrorResponse } from '../../api-services';
import { PlatformTranslateService } from '../../translations';
import { clone, keys, task_delay } from '../../utils';

export const enum LoadingState {
  Error = 'Error',
  Loading = 'Loading',
  Success = 'Success',
  Pending = 'Pending'
}

const requestStateDefaultKey = 'Default';

@Directive()
export abstract class PlatformComponent implements OnInit, AfterViewInit, OnDestroy {
  public toast: ToastrService = inject(ToastrService);
  public changeDetector: ChangeDetectorRef = inject(ChangeDetectorRef);
  public translateSrv: PlatformTranslateService = inject(PlatformTranslateService);

  public static get defaultDetectChangesDelay(): number {
    return 100;
  }

  public initiated$: BehaviorSubject<boolean> = new BehaviorSubject<boolean>(false);
  public viewInitiated$: BehaviorSubject<boolean> = new BehaviorSubject<boolean>(false);
  public destroyed$: BehaviorSubject<boolean> = new BehaviorSubject<boolean>(false);
  // General loadingState when not specific requestKey, requestKey = requestStateDefaultKey;
  public loadingState$: BehaviorSubject<LoadingState> = new BehaviorSubject<LoadingState>(LoadingState.Pending);
  public errorMsgMap$: BehaviorSubject<Dictionary<string | null>> = new BehaviorSubject<Dictionary<string | null>>({});
  public loadingMap$: BehaviorSubject<Dictionary<boolean | null>> = new BehaviorSubject<Dictionary<boolean | null>>({});

  private storedSubscriptionsMap: Map<string, Subscription> = new Map();
  private storedAnonymousSubscriptions: Subscription[] = [];
  private cachedErrorMsg$: Dictionary<Observable<string | null>> = {};
  private cachedLoading$: Dictionary<Observable<boolean | null>> = {};
  private allErrorMsgs!: Observable<string | null>;

  public detectChanges(delayTime?: number, onDone?: () => unknown, checkParentForHostBinding: boolean = false): void {
    this.cancelStoredSubscription('detectChangesDelaySubs');

    if (this.canDetectChanges) {
      const finalDelayTime = delayTime == null ? PlatformComponent.defaultDetectChangesDelay : delayTime;
      const detectChangesDelaySubs = task_delay(() => {
        if (this.canDetectChanges) {
          this.changeDetector.detectChanges();
          if (checkParentForHostBinding) {
            this.changeDetector.markForCheck();
          }
          if (onDone != null) {
            onDone();
          }
        }
      }, finalDelayTime);

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
    this.initiated$.next(true);
  }

  public ngAfterViewInit(): void {
    this.viewInitiated$.next(true);
  }

  public ngOnDestroy(): void {
    this.destroyed$.next(true);

    this.destroyAllSubjects();
    this.cancelAllStoredSubscriptions();
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
      if (options?.deferSetLoading != true) setLoadingState();

      return defer(() => {
        if (options?.deferSetLoading == true) setLoadingState();

        return source.pipe(
          tap({
            next: value => {
              if (this.loadingState$.value != LoadingState.Error) this.loadingState$.next(LoadingState.Success);
              this.setLoading(false, requestKey);

              if (options?.onSuccess != null) options.onSuccess(value);
            },
            error: (err: PlatformApiServiceErrorResponse | Error) => {
              this.loadingState$.next(LoadingState.Error);
              this.setLoading(false, requestKey);
              this.setErrorMsg(err, requestKey);

              if (options?.onError != null) options.onError(err);
            }
          })
        );
      });
    };
  }

  public getErrorMsg$(errorKey: string = requestStateDefaultKey): Observable<string | null> {
    if (this.cachedErrorMsg$[errorKey] == null) {
      this.cachedErrorMsg$[errorKey] = this.errorMsgMap$.pipe(map(_ => _[errorKey]));
    }
    return this.cachedErrorMsg$[errorKey];
  }

  public getErrorMsg(errorKey: string = requestStateDefaultKey): string | null {
    return this.errorMsgMap$.getValue()[errorKey];
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

  public getLoading$(errorKey: string = requestStateDefaultKey): Observable<boolean | null> {
    if (this.cachedLoading$[errorKey] == null) {
      this.cachedLoading$[errorKey] = this.loadingMap$.pipe(map(_ => _[errorKey]));
    }
    return this.cachedLoading$[errorKey];
  }

  public getLoading(errorKey: string = requestStateDefaultKey): boolean | null {
    return this.loadingMap$.getValue()[errorKey];
  }

  protected tapResponse<T>(
    nextFn: (next: T) => void,
    errorFn?: (error: any) => void,
    completeFn?: () => void
  ): (source: Observable<T>) => Observable<T> {
    // eslint-disable-next-line @typescript-eslint/no-empty-function
    return tapResponse(nextFn, errorFn ?? (() => {}), completeFn);
  }

  /**
   * Creates an effect.
   *
   * This effect is subscribed to throughout the lifecycle of the Component.
   * @param generator A function that takes an origin Observable input and
   *     returns an Observable. The Observable that is returned will be
   *     subscribed to for the life of the component.
   * @return A function that, when called, will trigger the origin Observable.
   */
  protected effect<TOrigin, TReturn>(
    generator: (origin: TOrigin | Observable<TOrigin | null> | null) => Observable<TReturn>
  ) {
    let previousEffectSub: Subscription = new Subscription();

    return (observableOrValue: TOrigin | Observable<TOrigin> | null = null) => {
      previousEffectSub.unsubscribe();

      const newEffectSub: Subscription = generator(observableOrValue)
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

  protected setLoading = (value: boolean | null, requestKey: string = requestStateDefaultKey) => {
    this.loadingMap$.next(
      clone(this.loadingMap$.value, _ => {
        _[requestKey] = value;
      })
    );
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
  deferSetLoading?: boolean;
  onSuccess?: (value: T) => any;
  onError?: (err: PlatformApiServiceErrorResponse | Error) => any;
}
