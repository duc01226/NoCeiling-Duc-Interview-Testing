import { Injectable } from '@angular/core';
import { PlatformApiErrorEvent, PlatformApiErrorEventHandler } from '@libs/platform-core';
import { AppStore } from '../app.store';

@Injectable()
export class AppApiErrorEventHandler extends PlatformApiErrorEventHandler {
    public constructor(public appStore: AppStore) {
        super();
    }

    public handle(event: PlatformApiErrorEvent): void {
        if (!event.apiError.error.isApplicationError()) {
            console.log('Demo using PlatformApiErrorEventHandler error event handler', event.apiError);
        }
    }
}
