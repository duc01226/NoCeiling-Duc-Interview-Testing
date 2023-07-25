import { HttpClient, HttpErrorResponse, HttpHeaders, HttpParams, HttpStatusCode } from '@angular/common/http';
import { inject, Injectable, Optional } from '@angular/core';
import { Observable, of, throwError } from 'rxjs';
import { catchError, switchMap, tap } from 'rxjs/operators';

import { PlatformCachingService } from '../../caching';
import { HttpClientOptions, PlatformHttpService } from '../../http-services';
import { PlatformCoreModuleConfig } from '../../platform-core.config';
import { removeNullProps, toPlainObj } from '../../utils';
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
    public static DefaultHeaders: HttpHeaders = new HttpHeaders({ 'Content-Type': 'application/json; charset=utf-8' });

    private readonly cacheService = inject(PlatformCachingService);

    public constructor(
        http: HttpClient,
        @Optional() moduleConfig: PlatformCoreModuleConfig,
        @Optional() private httpOptionsConfigService: PlatformHttpOptionsConfigService
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
        return 10;
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
        configureOptions?: (option: HttpClientOptions) => HttpClientOptions | void | undefined
    ): Observable<T> {
        const requestCacheKey = this.buildRequestCacheKey(path, params);

        return this.cacheService.cacheImplicitReloadRequest(
            requestCacheKey,
            () => this.getFromServer<T>(path, params, configureOptions),
            undefined,
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
            catchError(err => this.catchHttpError<T>(err))
        );
    }

    protected post<T>(
        path: string,
        body: unknown,
        configureOptions?: (option: HttpClientOptions) => HttpClientOptions | void | undefined
    ): Observable<T> {
        const options = this.getHttpOptions();
        const configuredOptions =
            configureOptions != null ? <HttpClientOptions | undefined>configureOptions(options) : options;

        return super
            .httpPost<T>(this.apiUrl + path, this.preprocessData(body), configuredOptions ?? options)
            .pipe(catchError(err => this.catchHttpError<T>(err)));
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
            .pipe(catchError(err => this.catchHttpError<T>(err)));
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
            .pipe(catchError(err => this.catchHttpError<T>(err)));
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
            .pipe(catchError(err => this.catchHttpError<T>(err)));
    }

    protected catchHttpError<T>(errorResponse: HttpErrorResponse | Error): Observable<T> {
        if (errorResponse instanceof Error) {
            console.error(errorResponse);
            return this.throwError<T>({
                error: { code: errorResponse.name, message: errorResponse.message },
                requestId: ''
            });
        }

        if (ERR_CONNECTION_REFUSED_STATUSES.includes(errorResponse.status)) {
            return this.throwError({
                error: {
                    code: PlatformApiServiceErrorInfoCode.ConnectionRefused,
                    message: 'Your internet connection is not available or the server is temporarily down.'
                },
                requestId: ''
            });
        }

        const apiErrorResponse = <IPlatformApiServiceErrorResponse | null>errorResponse.error;
        if (apiErrorResponse?.error != null && apiErrorResponse.error.code != null) {
            return this.throwError({
                error: apiErrorResponse.error,
                statusCode: errorResponse.status,
                requestId: apiErrorResponse.requestId
            });
        }

        if (UNAUTHORIZATION_STATUSES.includes(errorResponse.status)) {
            return this.throwError({
                error: {
                    code: PlatformApiServiceErrorInfoCode.PlatformPermissionException,
                    message: errorResponse.message ?? 'You are unauthorized or forbidden'
                },
                requestId: apiErrorResponse?.requestId ?? ''
            });
        }

        return this.throwError<T>({
            error: {
                code: PlatformApiServiceErrorInfoCode.Unknown,
                message: errorResponse.message
            },
            statusCode: errorResponse.status,
            requestId: apiErrorResponse?.requestId ?? ''
        });
    }

    protected throwError<T>(errorResponse: IPlatformApiServiceErrorResponse): Observable<T> {
        if (errorResponse.error.developerExceptionMessage != null)
            console.error(errorResponse.error.developerExceptionMessage);

        return <Observable<T>>of({}).pipe(
            switchMap(() => {
                return throwError(() => new PlatformApiServiceErrorResponse(errorResponse));
            })
        );
    }

    protected setDefaultOptions(options?: HttpClientOptions): HttpClientOptions {
        options = options ? options : {};
        if (!options.headers) {
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
        returnParam: IApiGetParams = {},
        prefix?: string
    ): IApiGetParams {
        // eslint-disable-next-line guard-for-in
        for (const paramKey in inputParams || {}) {
            const inputParamValue = inputParams instanceof FormData ? inputParams.get(paramKey) : inputParams[paramKey];
            const inputParamFinalKey = prefix ? `${prefix}.${paramKey}` : paramKey;
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
            } else if (inputParamValue != null) {
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
            if (Object.prototype.hasOwnProperty.call(flattenedInputParams, paramKey)) {
                const inputParamValue = flattenedInputParams[paramKey];
                if (inputParamValue instanceof Array) {
                    inputParamValue.forEach((p: IApiGetParamItemSingleValue) => {
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
        return `${this.apiUrl}${requestPath}${
            requestPayload != null ? this.requestCacheKeySeperator + JSON.stringify(requestPayload) : ''
        }`;
    }

    private getRequestPathFromCacheKey(requestCacheKey: string): string {
        return requestCacheKey.split(this.requestCacheKeySeperator)[0];
    }
}

export interface IApiGetParams {
    [param: string]: IApiGetParamItem;
}

declare type IApiGetParamItemSingleValue = string | boolean | number;

declare type IApiGetParamItem = IApiGetParamItemSingleValue | IApiGetParams | IApiGetParamItemSingleValue[];
