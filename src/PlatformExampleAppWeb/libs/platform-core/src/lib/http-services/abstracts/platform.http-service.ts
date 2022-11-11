import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Observable, OperatorFunction } from 'rxjs';
import { timeout } from 'rxjs/operators';

import { Exts } from '../../extensions';
import { PlatformCoreModuleConfig } from '../../platform-core.config';
import { HttpClientOptions } from './platform.http-client-options';

export abstract class PlatformHttpService {
  public constructor(protected moduleConfig: PlatformCoreModuleConfig, protected http: HttpClient) {}

  protected get requestTimeoutInMs(): number {
    return this.moduleConfig.httpRequestTimeoutInSeconds * 1000;
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

  protected httpPost<TResult>(url: string, body: unknown, options?: HttpClientOptions | (() => HttpClientOptions)) {
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

    let headerContentType =
      finalOptions.headers instanceof HttpHeaders
        ? finalOptions.headers.get('Content-type')
        : finalOptions.headers['Content-type'];

    if (headerContentType == 'application/x-www-form-urlencoded') return this.buildUrlEncodedFormData(body);

    if (headerContentType == 'application/json') return JSON.stringify(Exts.Object.toJsonObj(body));

    return body;
  }

  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  protected buildUrlEncodedFormData(data: any): string {
    const formData = new URLSearchParams();
    if (data == undefined) return '';
    if (typeof data == 'object') {
      Exts.Object.keys(data).map(key => formData.append(key, data[key]));
    } else {
      formData.append('value', data);
    }
    return formData.toString();
  }

  private getFinalOptions(options?: HttpClientOptions | (() => HttpClientOptions)): HttpClientOptions {
    const finalOptions = options == undefined ? {} : typeof options == 'function' ? options() : options;
    return Exts.Object.assignDeep(this.defaultOptions, this.appendAdditionalHttpOptions(finalOptions));
  }
}
