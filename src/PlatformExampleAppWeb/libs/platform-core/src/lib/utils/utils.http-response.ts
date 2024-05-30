import { HttpResponse } from '@angular/common/http';

export function httpResponse_getFileName(response: HttpResponse<Blob>): string {
    const contentDisposition = response.headers.get('content-disposition') ?? '';
    const matches = /filename="([^;]+)"/.exec(contentDisposition);
    return matches && matches[1] ? matches[1] : 'downloadedFile';
}
