import { Injectable } from '@angular/core';
import { PlatformEventHandler, PlatformRepositoryErrorEvent } from '@libs/platform-core';

import { AppUiStateStore } from '../app-ui-state';

@Injectable()
export class RepositoryErrorEventHandler extends PlatformEventHandler<PlatformRepositoryErrorEvent> {
  public constructor(public uiState: AppUiStateStore) {
    super();
  }

  public handle(event: PlatformRepositoryErrorEvent): void {
    if (!event.apiError.error.isApplicationError()) {
      this.uiState.updateState({
        appError : event.apiError
      });
    }
  }
}
