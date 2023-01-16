import { Injectable } from '@angular/core';
import { PlatformAppUiStateStore } from '@libs/platform-core';
import { Observable } from 'rxjs';

import { AppUiStateData } from './app-ui.state-data';

@Injectable()
export class AppUiStateStore extends PlatformAppUiStateStore<AppUiStateData> {
  public vm$: Observable<AppUiStateData> = this.select(state => state);
  public constructor() {
    super(new AppUiStateData());
  }
}
