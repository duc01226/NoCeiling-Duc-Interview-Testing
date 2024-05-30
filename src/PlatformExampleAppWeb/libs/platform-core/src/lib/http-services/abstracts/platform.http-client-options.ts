import { HttpHeaders, HttpParams } from '@angular/common/http';

export interface HttpClientOptions {
    headers?: HttpHeaders | Record<string, string | string[]>;
    observe?: 'body' | 'response';
    params?: HttpParams | Record<string, string | string[]>;
    reportProgress?: boolean;
    withCredentials?: boolean;
    timeoutSeconds?: number;
}
