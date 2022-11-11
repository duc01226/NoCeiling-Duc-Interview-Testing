import { Injectable } from '@angular/core';
import { PlatformAppUiStateManager } from '@platform-example-web/platform-core';

import { AppModuleConfig } from '../app.module.config';
import { AppUiStateData } from './app-ui.state-data';

@Injectable()
export class AppUiStateManager extends PlatformAppUiStateManager<AppUiStateData> {
  public constructor(moduleConfig: AppModuleConfig) {
    super(moduleConfig);
  }

  protected initialUiState(): AppUiStateData {
    return new AppUiStateData();
  }
}
