/* eslint-disable @typescript-eslint/ban-types */
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Optional } from '@angular/core';
import { Observable, OperatorFunction } from 'rxjs';
import { timeout } from 'rxjs/operators';

import { PlatformCoreModuleConfig } from '../../platform-core.config';
import { clone, immutableUpdate, keys, toPlainObj } from '../../utils';
import { HttpClientOptions } from './platform.http-client-options';

export abstract class PlatformHttpService {
  public DEFAULT_TIMEOUT = 60;
  public constructor(@Optional() protected moduleConfig: PlatformCoreModuleConfig, protected http: HttpClient) {}

  protected get requestTimeoutInMs(): number {
    return (this.moduleConfig?.httpRequestTimeoutInSeconds ?? this.DEFAULT_TIMEOUT) * 1000;
  }
  protected get defaultOptions(): HttpClientOptions {
    return {
      headers: {
        Accept: 'application/json',
        'Content-type': 'application/json'
      }
    };
  }

  protected abstract appendAdditionalHttpOptions(options: HttpClientOptions): HttpClientOptions;

  protected httpGet<T>(url: string, options?: HttpClientOptions | (() => HttpClientOptions)): Observable<T> {
    return this.http
      .get(url, this.getFinalOptions(options))
      .pipe(<OperatorFunction<Object, T>>timeout(this.requestTimeoutInMs));
  }

  protected httpPost<TResult>(url: string, body: object, options?: HttpClientOptions | (() => HttpClientOptions)) {
    const finalBody = this.buildHttpBody(body, this.getFinalOptions(options));
    return this.http
      .post(url, finalBody, this.getFinalOptions(options))
      .pipe(<OperatorFunction<Object, TResult>>timeout(this.requestTimeoutInMs));
  }

  protected httpPut<T>(url: string, body: T, options?: HttpClientOptions | (() => HttpClientOptions)) {
    const finalBody = this.buildHttpBody(body, this.getFinalOptions(options));
    return this.http
      .put(url, finalBody, this.getFinalOptions(options))
      .pipe(<OperatorFunction<Object, T>>timeout(this.requestTimeoutInMs));
  }

  protected httpDelete<T>(url: string, options?: HttpClientOptions | (() => HttpClientOptions)) {
    return this.http
      .delete(url, this.getFinalOptions(options))
      .pipe(<OperatorFunction<Object, T>>timeout(this.requestTimeoutInMs));
  }

  protected buildHttpBody<T>(body: T, options: HttpClientOptions | (() => HttpClientOptions)) {
    const finalOptions = this.getFinalOptions(options);
    if (finalOptions.headers == undefined) return body;

    const headerContentType =
      finalOptions.headers instanceof HttpHeaders
        ? finalOptions.headers.get('Content-type')
        : finalOptions.headers['Content-type'];

    if (headerContentType == 'application/x-www-form-urlencoded') return this.buildUrlEncodedFormData(body);

    if (headerContentType == 'application/json') return JSON.stringify(toPlainObj(body));

    return body;
  }

  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  protected buildUrlEncodedFormData(data: any): string {
    const formData = new URLSearchParams();
    if (data == undefined) return '';
    if (typeof data == 'object') {
      keys(data).forEach(key => formData.append(key, data[key]));
    } else {
      formData.append('value', data);
    }
    return formData.toString();
  }

  private getFinalOptions(options?: HttpClientOptions | (() => HttpClientOptions)): HttpClientOptions {
    const finalOptions = options == undefined ? {} : typeof options == 'function' ? options() : options;
    return immutableUpdate(clone(this.defaultOptions), this.appendAdditionalHttpOptions(finalOptions) ?? finalOptions);
  }
}

export const ErrorCodeConstant: Record<string, number> = {
  RequestCanceller: 0,
  NotModified: 304,
  // Client error codes with 4**
  BadRequest: 400,
  Unauthorized: 401,
  PaymentRequired: 402,
  Forbidden: 403,
  NotFound: 404,
  MethodNotAllowed: 405,
  RequestTimeout: 408,
  PreconditionFailed: 412,
  LoginTimeout: 440,
  UnprocessableEntity: 422,
  // Server error codes with 5**
  InternalServerError: 500,
  NotImplemented: 501,
  GatewayTimeout: 504,
  NetworkConnectTimeout: 599
};
