import { HttpHeaders, HttpParams } from '@angular/common/http';

export interface HttpClientOptions {
    headers?: HttpHeaders | Record<string, string | string[]>;
    observe?: 'body';
    params?: HttpParams | Record<string, string | string[]>;
    reportProgress?: boolean;
    withCredentials?: boolean;
    timeoutSeconds?: number;
}
