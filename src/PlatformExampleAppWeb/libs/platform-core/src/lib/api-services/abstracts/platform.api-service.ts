import { HttpClient, HttpErrorResponse, HttpHeaders, HttpParams, HttpStatusCode } from '@angular/common/http';
import { Injectable, Optional, inject } from '@angular/core';

import { Observable, throwError } from 'rxjs';
import { catchError, tap } from 'rxjs/operators';

import { PlatformCachingService } from '../../caching';
import { PlatformEventManager } from '../../events';
import { HttpClientOptions, PlatformHttpService } from '../../http-services';
import { PlatformCoreModuleConfig } from '../../platform-core.config';
import { removeNullProps, toPlainObj } from '../../utils';
import { PlatformApiErrorEvent } from '../events/api-error.event';
import {
    IPlatformApiServiceErrorResponse,
    PlatformApiServiceErrorInfoCode,
    PlatformApiServiceErrorResponse
} from './platform.api-error';
import { PlatformHttpOptionsConfigService } from './platform.http-options-config-service';

const ERR_CONNECTION_REFUSED_STATUSES: (HttpStatusCode | number)[] = [0, 504];
const UNAUTHORIZATION_STATUSES: HttpStatusCode[] = [HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden];

@Injectable()
export abstract class PlatformApiService extends PlatformHttpService {
    public static readonly DefaultHeaders: HttpHeaders = new HttpHeaders({
        'Content-Type': 'application/json; charset=utf-8'
    });

    private readonly cacheService = inject(PlatformCachingService);

    public constructor(
        http: HttpClient,
        @Optional() moduleConfig: PlatformCoreModuleConfig,
        @Optional() private httpOptionsConfigService: PlatformHttpOptionsConfigService,
        protected eventManager: PlatformEventManager
    ) {
        super(moduleConfig, http);
    }

    protected abstract get apiUrl(): string;

    protected override get defaultOptions(): HttpClientOptions {
        const defaultOptions = super.defaultOptions;
        return this.httpOptionsConfigService?.configOptions(defaultOptions) ?? defaultOptions;
    }

    protected appendAdditionalHttpOptions(options: HttpClientOptions): HttpClientOptions {
        return this.httpOptionsConfigService?.configOptions(options);
    }

    protected maxNumberOfCacheItemPerRequestPath(): number {
        return 5;
    }

    private cachedRequestDataKeys: Set<string> = new Set();
    private setCachedRequestData<T>(requestCacheKey: string, data: T | undefined) {
        this.cacheService.set(requestCacheKey, data);

        this.cachedRequestDataKeys.add(requestCacheKey);

        this.clearOldestCachedData(requestCacheKey);
    }

    /**
     * Process clear oldest cached data if over maxNumberOfCacheItemPerRequestPath
     */
    private clearOldestCachedData(requestCacheKey: string) {
        const allRequestPathKeys = [...this.cachedRequestDataKeys].filter(key =>
            key.startsWith(this.getRequestPathFromCacheKey(requestCacheKey))
        );

        while (allRequestPathKeys.length > this.maxNumberOfCacheItemPerRequestPath()) {
            const oldestRequestKey = allRequestPathKeys.shift();
            if (oldestRequestKey != null) {
                this.cacheService.delete(requestCacheKey);
                this.cachedRequestDataKeys.delete(oldestRequestKey);
            }
        }
    }

    protected get<T>(
        path: string,
        params: unknown,
        configureOptions?: (option: HttpClientOptions) => HttpClientOptions | void | undefined,
        disableCached?: boolean
    ): Observable<T> {
        if (disableCached) return this.getFromServer<T>(path, params, configureOptions);

        const requestCacheKey = this.buildRequestCacheKey(path, params);
        return this.cacheService.cacheImplicitReloadRequest(
            requestCacheKey,
            () => this.getFromServer<T>(path, params, configureOptions),
            { ttl: this.moduleConfig.apiGetCacheTimeToLiveSeconds },
            (requestCacheKey, data) => {
                this.setCachedRequestData(requestCacheKey, data);
            }
        );
    }

    private getFromServer<T>(
        path: string,
        params: unknown,
        configureOptions: ((option: HttpClientOptions) => HttpClientOptions | void | undefined) | undefined
    ) {
        const options = this.getHttpOptions(this.preprocessData(params));
        const configuredOptions =
            configureOptions != null ? <HttpClientOptions | undefined>configureOptions(options) : options;

        return super.httpGet<T>(this.apiUrl + path, configuredOptions ?? options).pipe(
            tap({
                error: err => {
                    if (err instanceof Error) this.cacheService.clear();
                }
            }),
            catchError(err => this.catchHttpError<T>(err, path, params))
        );
    }

    protected post<T>(
        path: string,
        body: unknown,
        configureOptions?: (option: HttpClientOptions) => HttpClientOptions | void | undefined,
        enableCache: boolean = false
    ): Observable<T> {
        if (enableCache) {
            const requestCacheKey = this.buildRequestCacheKey(path, body);
            return this.cacheService.cacheImplicitReloadRequest(
                requestCacheKey,
                () => this.postFromServer<T>(path, body, configureOptions),
                { ttl: this.moduleConfig.apiGetCacheTimeToLiveSeconds },
                (requestCacheKey, data) => {
                    this.setCachedRequestData(requestCacheKey, data);
                }
            );
        }
        return this.postFromServer(path, body, configureOptions);
    }

    private postFromServer<T>(
        path: string,
        body: unknown,
        configureOptions?: (option: HttpClientOptions) => HttpClientOptions | void | undefined
    ): Observable<T> {
        const options = this.getHttpOptions();
        const configuredOptions =
            configureOptions != null ? <HttpClientOptions | undefined>configureOptions(options) : options;

        return super
            .httpPost<T>(this.apiUrl + path, this.preprocessData(body), configuredOptions ?? options)
            .pipe(catchError(err => this.catchHttpError<T>(err, path, body)));
    }

    protected postFileMultiPartForm<T>(
        path: string,
        body: unknown,
        configureOptions?: (option: HttpClientOptions) => HttpClientOptions | void | undefined
    ): Observable<T> {
        const options = this.getHttpOptions();
        const configuredOptions =
            configureOptions != null ? <HttpClientOptions | undefined>configureOptions(options) : options;

        return super
            .httpPostFileMultiPartForm<T>(this.apiUrl + path, <object>body, configuredOptions ?? options)
            .pipe(catchError(err => this.catchHttpError<T>(err, path, body)));
    }

    protected put<T>(
        path: string,
        body: T,
        configureOptions?: (option: HttpClientOptions) => HttpClientOptions | void | undefined
    ): Observable<T> {
        const options = this.getHttpOptions();
        const configuredOptions =
            configureOptions != null ? <HttpClientOptions | undefined>configureOptions(options) : options;

        return super
            .httpPut<T>(this.apiUrl + path, <T>this.preprocessData(body), configuredOptions ?? options)
            .pipe(catchError(err => this.catchHttpError<T>(err, path, body)));
    }

    protected delete<T>(
        path: string,
        configureOptions?: (option: HttpClientOptions) => HttpClientOptions | void | undefined
    ): Observable<T> {
        const options = this.getHttpOptions();
        const configuredOptions =
            configureOptions != null ? <HttpClientOptions | undefined>configureOptions(options) : options;

        return super
            .httpDelete<T>(this.apiUrl + path, configuredOptions ?? options)
            .pipe(catchError(err => this.catchHttpError<T>(err, path, null)));
    }

    protected catchHttpError<T>(
        errorResponse: HttpErrorResponse | Error,
        apiRequestPath: string,
        apiRequestPayload: unknown
    ): Observable<T> {
        if (errorResponse instanceof Error) {
            console.error(errorResponse);
            return this.throwError<T>(
                {
                    error: { code: errorResponse.name, message: errorResponse.message },
                    requestId: ''
                },
                apiRequestPath,
                apiRequestPayload
            );
        }

        if (ERR_CONNECTION_REFUSED_STATUSES.includes(errorResponse.status)) {
            return this.throwError(
                {
                    error: {
                        code: PlatformApiServiceErrorInfoCode.ConnectionRefused,
                        message: 'Your internet connection is not available or the server is temporarily down.'
                    },
                    statusCode: errorResponse.status,
                    requestId: ''
                },
                apiRequestPath,
                apiRequestPayload
            );
        }

        const apiErrorResponse = <IPlatformApiServiceErrorResponse | null>errorResponse.error;
        if (apiErrorResponse?.error?.code != null) {
            return this.throwError(
                {
                    error: apiErrorResponse.error,
                    statusCode: errorResponse.status,
                    requestId: apiErrorResponse.requestId
                },
                apiRequestPath,
                apiRequestPayload
            );
        }

        if (UNAUTHORIZATION_STATUSES.includes(errorResponse.status)) {
            return this.throwError(
                {
                    error: {
                        code: PlatformApiServiceErrorInfoCode.PlatformPermissionException,
                        message: errorResponse.message ?? 'You are unauthorized or forbidden'
                    },
                    statusCode: errorResponse.status,
                    requestId: apiErrorResponse?.requestId ?? ''
                },
                apiRequestPath,
                apiRequestPayload
            );
        }

        return this.throwError<T>(
            {
                error: {
                    code: PlatformApiServiceErrorInfoCode.Unknown,
                    message: errorResponse.message
                },
                statusCode: errorResponse.status,
                requestId: apiErrorResponse?.requestId ?? ''
            },
            apiRequestPath,
            apiRequestPayload
        );
    }

    protected throwError<T>(
        errorResponse: IPlatformApiServiceErrorResponse,
        apiRequestPath: string,
        apiRequestPayload: unknown
    ): Observable<T> {
        if (errorResponse.error.developerExceptionMessage != null)
            console.error(errorResponse.error.developerExceptionMessage);

        return throwError(() => {
            const errorResponseInstance = new PlatformApiServiceErrorResponse(errorResponse);

            this.eventManager.publish(
                new PlatformApiErrorEvent(apiRequestPath, apiRequestPayload, errorResponseInstance)
            );

            return errorResponseInstance;
        });
    }

    protected setDefaultOptions(options?: HttpClientOptions): HttpClientOptions {
        options = options ?? {};
        if (options.headers == null) {
            const httpHeaders = PlatformApiService.DefaultHeaders;
            options.headers = httpHeaders;
        }

        return options;
    }

    /**
     * We remove all null props because it's not necessary. And in server dotnet core, if the data is nullable => default value is null
     * so that do not need to submit null. If data is not nullable, then if submit null can raise exception.
     */
    private preprocessData<T>(data: T): IApiGetParams | Record<string, string> | FormData {
        if (data instanceof FormData) {
            return data;
        }
        return toPlainObj(removeNullProps(data));
    }

    private getHttpOptions(params?: IApiGetParams | Record<string, string> | FormData): HttpClientOptions {
        if (params == null) return this.defaultOptions;
        const finalOptions = this.defaultOptions;
        finalOptions.params = this.parseHttpGetParam(params);
        return finalOptions;
    }

    private flattenHttpGetParam(
        inputParams: IApiGetParams | FormData,
        returnParam: Dictionary<ApiGetParamItemSingleValue | ApiGetParamItemSingleValue[]> = {},
        prefix?: string
    ): Dictionary<ApiGetParamItemSingleValue | ApiGetParamItemSingleValue[]> {
        // eslint-disable-next-line guard-for-in
        for (const paramKey in inputParams ?? {}) {
            const inputParamValue = inputParams instanceof FormData ? inputParams.get(paramKey) : inputParams[paramKey];
            const inputParamFinalKey = prefix != null ? `${prefix}.${paramKey}` : paramKey;

            if (inputParamValue instanceof Array) {
                // eslint-disable-next-line no-param-reassign
                returnParam[inputParamFinalKey] = inputParamValue;
            } else if (inputParamValue instanceof Date) {
                returnParam[inputParamFinalKey] = inputParamValue.toISOString();
            } else if (
                typeof inputParamValue === 'object' &&
                !(inputParamValue instanceof File) &&
                inputParamValue != null
            ) {
                this.flattenHttpGetParam(inputParamValue, returnParam, paramKey);
            } else if (inputParamValue != null && !(inputParamValue instanceof File)) {
                // eslint-disable-next-line no-param-reassign
                returnParam[inputParamFinalKey] = inputParamValue.toString();
            }
        }

        return returnParam;
    }

    private parseHttpGetParam(inputParams: IApiGetParams | Record<string, string> | FormData): HttpParams {
        let returnParam = new HttpParams();
        const flattenedInputParams = this.flattenHttpGetParam(inputParams);
        for (const paramKey in flattenedInputParams) {
            if (Object.hasOwn(flattenedInputParams, paramKey)) {
                const inputParamValue = flattenedInputParams[paramKey]!;
                if (inputParamValue instanceof Array) {
                    inputParamValue.forEach((p: ApiGetParamItemSingleValue) => {
                        returnParam = returnParam.append(paramKey, p);
                    });
                } else {
                    returnParam = returnParam.append(paramKey, inputParamValue.toString());
                }
            }
        }
        return returnParam;
    }

    private requestCacheKeySeperator: string = '___';
    private buildRequestCacheKey(requestPath: string, requestPayload: unknown): string {
        const requestPayloadCacheKeyPart =
            requestPayload != null ? this.requestCacheKeySeperator + JSON.stringify(requestPayload) : '';

        return `${this.apiUrl}${requestPath}${requestPayloadCacheKeyPart}`;
    }

    private getRequestPathFromCacheKey(requestCacheKey: string): string {
        return requestCacheKey.split(this.requestCacheKeySeperator)[0]!;
    }
}

export interface IApiGetParams {
    [param: string]: ApiGetParamItem;
}

declare type ApiGetParamItemSingleValue = string | boolean | number;

declare type ApiGetParamItem = ApiGetParamItemSingleValue | IApiGetParams | ApiGetParamItemSingleValue[];
