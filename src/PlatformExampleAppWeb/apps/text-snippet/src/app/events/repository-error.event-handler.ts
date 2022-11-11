import { Injectable } from '@angular/core';
import { PlatformEventHandler, PlatformRepositoryErrorEvent } from '@platform-example-web/platform-core';

import { AppUiStateManager } from '../app-ui-state-manager';

@Injectable()
export class RepositoryErrorEventHandler extends PlatformEventHandler<PlatformRepositoryErrorEvent> {
  public constructor(public uiState: AppUiStateManager) {
    super();
  }

  public handle(event: PlatformRepositoryErrorEvent): void {
    if (!event.apiError.error.isApplicationError()) {
      this.uiState.updateUiStateData(current => {
        current.unexpectedError = event.apiError;
        return current;
      });
    }
  }
}
