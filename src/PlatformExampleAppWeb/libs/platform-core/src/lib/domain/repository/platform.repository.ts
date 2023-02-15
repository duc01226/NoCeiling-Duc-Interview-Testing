import { BehaviorSubject, defer, Observable, Subject, throwError } from 'rxjs';
import { catchError, distinctUntilChanged, finalize, map, switchMap, take, takeUntil } from 'rxjs/operators';

import { PlatformApiServiceErrorResponse } from '../../api-services';
import { PlatformCommandDto, PlatformQueryDto } from '../../dtos';
import { IPlatformEventManager } from '../../events';
import { PlatformCoreModuleConfig } from '../../platform-core.config';
import { cloneDeep, dictionary_upsert, isDifferent, task_delay } from '../../utils';
import { PlatformRepositoryErrorEvent } from '../events/repository-error.event';
import { PlatformRepositoryContext } from '../platform.repository-context';

/* eslint-disable object-shorthand */
export type RepoLoadStrategy = 'loadOnce' | 'implicitReload' | 'explicitReload';

export abstract class PlatformRepository<TContext extends PlatformRepositoryContext> {
  public constructor(
    protected moduleConfig: PlatformCoreModuleConfig,
    protected context: TContext,
    protected eventManager: IPlatformEventManager
  ) {}

  protected maxCacheRequestDataPerApiRequestName(): number {
    return this.moduleConfig.maxCacheRequestDataPerApiRequestName;
  }

  protected processUpsertData<TModel, TApiResult>(config: {
    repoDataSubject: BehaviorSubject<Dictionary<TModel>>;
    apiRequestFn: (implicitLoad: boolean) => Observable<TApiResult>;
    requestName: string;
    requestPayload: PlatformQueryDto | PlatformCommandDto;
    strategy: RepoLoadStrategy;
    finalResultBuilder: (repoData: Dictionary<TModel>, apiResult: TApiResult) => TApiResult;
    modelDataExtractor: (apiResult: TApiResult) => TModel[];
    modelIdFn: (item: TModel | Partial<TModel>) => string | number | undefined;
    initModelItemFn: (data: TModel | Partial<TModel>) => TModel;
    replaceItem?: boolean;
    asRequest?: boolean;
    refreshRelatedRequests?: { requestName: string; requestPartialPayload: PlatformQueryDto | PlatformCommandDto }[];
    optionalProps?: (keyof TModel)[];
  }): Observable<TApiResult> {
    const {
      repoDataSubject,
      apiRequestFn,
      requestName,
      requestPayload,
      strategy,
      finalResultBuilder,
      modelDataExtractor,
      modelIdFn,
      initModelItemFn,
      asRequest,
      refreshRelatedRequests
    } = config;
    const replaceItem = config.replaceItem ?? true;
    const optionalProps = config.optionalProps ?? [];

    const requestId = this.buildRequestId(requestName, requestPayload);
    const stopRefreshNotifier$ = new Subject();
    const refreshDataFn = () => {
      apiRequestFn(true)
        .pipe(takeUntil(stopRefreshNotifier$))
        .subscribe({
          next: apiResult => {
            this.updateNewRequestData<TModel, TApiResult>({
              requestId,
              apiResult,
              repoDataSubject,
              modelDataExtractor,
              modelIdFn,
              initModelItemFn,
              replaceItem: replaceItem,
              optionalProps: optionalProps
            });
            if (refreshRelatedRequests != null) {
              refreshRelatedRequests.forEach(p =>
                this.processRefreshData({ requestName: p.requestName, requestPayload: p.requestPartialPayload })
              );
            }
          },
          error: error => {
            this.handleApiError(error, requestName, requestPayload);
          }
        });
    };
    const returnDataObsFn = () =>
      defer(() => {
        if (this.context.loadedRequestSubscriberCountDic[requestId] != null) {
          this.context.loadedRequestSubscriberCountDic[requestId] += 1;
        } else {
          this.context.loadedRequestSubscriberCountDic[requestId] = 1;
        }

        let resultObs = repoDataSubject.asObservable().pipe(
          map(repoData => {
            const cachedRequestData = <TApiResult>this.context.loadedRequestDataDic[requestId];
            return finalResultBuilder(repoData, cloneDeep(cachedRequestData));
          }),
          distinctUntilChanged((x, y) => !isDifferent(x, y)),
          map(x => cloneDeep(x))
        );
        if (asRequest) {
          resultObs = resultObs.pipe(take(1));
        }
        return resultObs.pipe(
          finalize(() => {
            stopRefreshNotifier$.next(null);
            this.context.loadedRequestSubscriberCountDic[requestId] -= 1;
            this.clearLoadedRequestDataCacheItem(requestName);
          })
        );
      });

    this.context.loadedRequestRefreshFnDic[requestId] = refreshDataFn;

    const cachedRequestApiResult = this.context.loadedRequestDataDic[requestId];
    if (cachedRequestApiResult == null || strategy === 'explicitReload' || (asRequest && strategy !== 'loadOnce')) {
      return apiRequestFn(false).pipe(
        catchError(error => this.catchApiError(error, requestName, requestPayload)),
        switchMap(apiResult => {
          this.updateNewRequestData<TModel, TApiResult>({
            requestId,
            apiResult,
            repoDataSubject,
            modelDataExtractor,
            modelIdFn,
            initModelItemFn,
            replaceItem: replaceItem,
            optionalProps: optionalProps
          });
          if (refreshRelatedRequests != null) {
            refreshRelatedRequests.forEach(p =>
              this.processRefreshData({ requestName: p.requestName, requestPayload: p.requestPartialPayload })
            );
          }
          return returnDataObsFn();
        })
      );
    }
    if (strategy === 'implicitReload') {
      refreshDataFn();
      return returnDataObsFn();
    }

    return returnDataObsFn();
  }

  protected handleApiError(
    error: PlatformApiServiceErrorResponse,
    requestName: string,
    requestPayload: PlatformQueryDto | PlatformCommandDto
  ) {
    if (error instanceof PlatformApiServiceErrorResponse) {
      this.eventManager.publish(new PlatformRepositoryErrorEvent(requestName, requestPayload, error));
    }
  }

  protected catchApiError(
    error: PlatformApiServiceErrorResponse,
    requestName: string,
    requestPayload: PlatformQueryDto | PlatformCommandDto
  ) {
    this.handleApiError(error, requestName, requestPayload);
    return throwError(() => error);
  }

  /**
   * Refresh cached request data, filtered by requestName and requestPayload.
   * @param options.delay Delay time. Default is 1000
   */
  protected processRefreshData(options: {
    requestName: string;
    requestPayload?: PlatformQueryDto | PlatformCommandDto;
    delayTime?: number;
  }): void {
    const delayTime = options.delayTime ?? 500;

    task_delay(() => {
      const requestId = this.buildRequestId(options.requestName, options.requestPayload);
      const requestIdPrefix = requestId.endsWith(']') ? requestId.slice(0, requestId.length - 1) : requestId;
      Object.keys(this.context.loadedRequestRefreshFnDic).forEach(key => {
        if (key.startsWith(requestIdPrefix)) {
          this.context.loadedRequestRefreshFnDic[key]();
        }
      });
    }, delayTime);
  }

  protected processClearRefreshDataRequest(
    requestName: string,
    requestPartialPayload?: PlatformQueryDto | PlatformCommandDto
  ): void {
    const requestId = this.buildRequestId(requestName, requestPartialPayload);
    const requestIdPrefix = requestId.endsWith(']') ? requestId.slice(0, requestId.length - 1) : requestId;
    Object.keys(this.context.loadedRequestRefreshFnDic).forEach(key => {
      if (key.startsWith(requestIdPrefix)) {
        delete this.context.loadedRequestRefreshFnDic[key];
      }
    });
  }

  protected upsertData<TModel>(
    dataSubject: BehaviorSubject<Dictionary<TModel>>,
    data: (TModel | Partial<TModel>)[],
    modelIdFn: (item: TModel | Partial<TModel>) => string | number | undefined,
    initModelItemFn: (data: TModel | Partial<TModel>) => TModel,
    replaceItem: boolean = false,
    onDataChanged?: (newState: Dictionary<TModel>) => void,
    optionalProps: (keyof TModel)[] = []
  ): Dictionary<TModel> {
    return dictionary_upsert(
      dataSubject.getValue(),
      data,
      item => modelIdFn(item) ?? '',
      x => initModelItemFn(x),
      undefined,
      undefined,
      replaceItem,
      onDataChanged ?? (x => dataSubject.next(x)),
      optionalProps
    );
  }

  private updateCachedRequestData<TApiResult>(requestId: string, apiResult: TApiResult): boolean {
    if (isDifferent(this.context.loadedRequestDataDic[requestId], apiResult)) {
      this.context.loadedRequestDataDic[requestId] = cloneDeep(apiResult);
      return true;
    }
    return false;
  }

  private buildRequestId(requestName: string, requestPayload?: PlatformQueryDto | PlatformCommandDto): string {
    return `${requestName}${requestPayload != null ? `_${JSON.stringify(requestPayload)}` : ''}`;
  }

  private updateNewRequestData<TModel, TApiResult>(config: {
    requestId: string;
    apiResult: TApiResult;
    repoDataSubject: BehaviorSubject<Dictionary<TModel>>;
    modelDataExtractor: (apiResult: TApiResult) => TModel[];
    modelIdFn: (item: TModel | Partial<TModel>) => string | number | undefined;
    initModelItemFn: (data: TModel | Partial<TModel>) => TModel;
    replaceItem: boolean;
    optionalProps: (keyof TModel)[];
  }): void {
    const {
      requestId,
      apiResult,
      repoDataSubject,
      modelDataExtractor,
      modelIdFn,
      initModelItemFn,
      replaceItem,
      optionalProps
    } = config;

    let hasChanged = this.updateCachedRequestData<TApiResult>(requestId, apiResult);
    const newData = this.upsertData(
      repoDataSubject,
      modelDataExtractor(apiResult),
      modelIdFn,
      initModelItemFn,
      replaceItem,
      () => {
        hasChanged = true;
      },
      optionalProps
    );
    if (hasChanged) {
      repoDataSubject.next(newData);
    }
  }

  private clearLoadedRequestDataCacheItem(startWithRequestName: string): void {
    const noSubscriberRequests = Object.keys(this.context.loadedRequestDataDic).filter(
      key => key.startsWith(startWithRequestName) && this.context.loadedRequestSubscriberCountDic[key] <= 0
    );

    while (noSubscriberRequests.length > this.maxCacheRequestDataPerApiRequestName()) {
      const oldestRequestKey = <string>noSubscriberRequests.shift();
      this.context.clearLoadedRequestInfo(oldestRequestKey);
    }
  }
}
