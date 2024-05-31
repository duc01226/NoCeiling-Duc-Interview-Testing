import { ErrorHandler, Injectable } from '@angular/core';

import { PlatformApiServiceErrorResponse } from './api-services';
import { PlatformCachingService } from './caching';
import { PlatformServiceWorkerService } from './platform-service-worker';

@Injectable()
export class PlatformGlobalErrorHandler extends ErrorHandler {
    constructor(
        private readonly cacheService: PlatformCachingService,
        private readonly servcieWorkerSvc: PlatformServiceWorkerService
    ) {
        super();
    }

    public override handleError(error: unknown) {
        if (!(error instanceof PlatformApiServiceErrorResponse)) {
            super.handleError(error);
            setTimeout(() => {
                this.clearCache();
            });
        }
    }

    public clearCache() {
        this.cacheService.clear();
        this.servcieWorkerSvc.clearCache();
    }
}
