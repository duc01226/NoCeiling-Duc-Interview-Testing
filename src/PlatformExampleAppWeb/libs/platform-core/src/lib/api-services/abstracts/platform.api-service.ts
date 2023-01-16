import { HttpClient, HttpErrorResponse, HttpParams, HttpStatusCode } from '@angular/common/http';
import { Injectable, Optional } from '@angular/core';
import { Observable, of, throwError } from 'rxjs';
import { catchError, switchMap } from 'rxjs/operators';

import { PlatformAuthHttpRequestOptionsAppenderService } from '../../auth-services';
import { HttpClientOptions, PlatformHttpService } from '../../http-services';
import { PlatformCoreModuleConfig } from '../../platform-core.config';
import { removeNullProps, toPlainObj } from '../../utils';
import { IPlatformApiServiceErrorResponse, PlatformApiServiceErrorInfoCode, PlatformApiServiceErrorResponse } from './platform.api-error';

const ERR_CONNECTION_REFUSED_STATUS: HttpStatusCode[] = [0, 504];

@Injectable()
export abstract class PlatformApiService extends PlatformHttpService {
  public constructor(
    http: HttpClient,
    @Optional() moduleConfig: PlatformCoreModuleConfig,
    @Optional() private authHttpRequestOptionsAppender: PlatformAuthHttpRequestOptionsAppenderService
  ) {
    super(moduleConfig, http);
  }

  protected abstract get apiUrl(): string;

  protected override get defaultOptions(): HttpClientOptions {
    const defaultOptions = super.defaultOptions;
    return this.authHttpRequestOptionsAppender?.addAuthorization(defaultOptions) ?? defaultOptions;
  }

  protected appendAdditionalHttpOptions(options: HttpClientOptions): HttpClientOptions {
    return this.authHttpRequestOptionsAppender?.addAuthorization(options);
  }

  protected get<T>(path: string, params: unknown): Observable<T> {
    return super
      .httpGet<T>(this.apiUrl + path, this.getHttpOptions(this.preprocessData(params)))
      .pipe(catchError(err => this.catchHttpError<T>(err)));
  }

  protected post<T>(path: string, body: unknown): Observable<T> {
    return super
      .httpPost<T>(this.apiUrl + path, this.preprocessData(body), this.getHttpOptions())
      .pipe(catchError(err => this.catchHttpError<T>(err)));
  }

  protected put<T>(path: string, body: T): Observable<T> {
    return super
      .httpPut<T>(this.apiUrl + path, <T>this.preprocessData(body), this.getHttpOptions())
      .pipe(catchError(err => this.catchHttpError<T>(err)));
  }

  protected delete<T>(path: string): Observable<T> {
    return super.httpDelete<T>(this.apiUrl + path, this.getHttpOptions()).pipe(catchError(err => this.catchHttpError<T>(err)));
  }

  protected catchHttpError<T>(errorResponse: HttpErrorResponse | Error): Observable<T> {
    if (errorResponse instanceof Error) {
      return this.throwError<T>({ error: { code: errorResponse.name, message: errorResponse.message }, requestId: '' });
    }

    if (ERR_CONNECTION_REFUSED_STATUS.includes(errorResponse.status)) {
      return this.throwError({
        error: {
          code: PlatformApiServiceErrorInfoCode.ConnectionRefused,
          message: 'Your internet connection is not available or the server is temporarily down.'
        },
        requestId: ''
      });
    }

    const apiErrorResponse = <IPlatformApiServiceErrorResponse>errorResponse.error;
    if (apiErrorResponse?.error != null && apiErrorResponse.error.code != null) {
      return this.throwError({
        error: apiErrorResponse.error,
        statusCode: errorResponse.status,
        requestId: apiErrorResponse.requestId
      });
    }

    return this.throwError<T>({
      error: {
        code: PlatformApiServiceErrorInfoCode.Unknown,
        message: errorResponse.message
      },
      statusCode: errorResponse.status,
      requestId: apiErrorResponse.requestId
    });
  }

  protected throwError<T>(error: IPlatformApiServiceErrorResponse): Observable<T> {
    return <Observable<T>>of({}).pipe(
      switchMap(() => {
        return throwError(() => new PlatformApiServiceErrorResponse(error));
      })
    );
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

  private flattenHttpGetParam(inputParams: IApiGetParams | FormData, returnParam: IApiGetParams = {}, prefix?: string): IApiGetParams {
    // eslint-disable-next-line guard-for-in
    for (const paramKey in inputParams || {}) {
      const inputParamValue = inputParams instanceof FormData ? inputParams.get(paramKey) : inputParams[paramKey];
      const inputParamFinalKey = prefix ? `${prefix}.${paramKey}` : paramKey;
      if (inputParamValue instanceof Array) {
        // eslint-disable-next-line no-param-reassign
        returnParam[inputParamFinalKey] = inputParamValue;
      } else if (inputParamValue instanceof Date) {
        returnParam[inputParamFinalKey] = inputParamValue.toISOString();
      } else if (typeof inputParamValue === 'object' && !(inputParamValue instanceof File) && inputParamValue != null) {
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
}

export interface IApiGetParams {
  [param: string]: IApiGetParamItem;
}

declare type IApiGetParamItemSingleValue = string | boolean | number;

declare type IApiGetParamItem = IApiGetParamItemSingleValue | IApiGetParams | IApiGetParamItemSingleValue[];
