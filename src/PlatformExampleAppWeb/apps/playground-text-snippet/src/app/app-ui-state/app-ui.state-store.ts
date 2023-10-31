import { Injectable } from '@angular/core';
import { PlatformAppUiStateStore } from '@libs/platform-core';

import { AppUiStateData } from './app-ui.state-data';

@Injectable()
export class AppUiStateStore extends PlatformAppUiStateStore<AppUiStateData> {
    public constructor() {
        super(new AppUiStateData());
    }

    protected onInitVm = () => {};

    public vmConstructor = (data?: Partial<AppUiStateData>) => new AppUiStateData(data);

    protected cachedStateKeyName = () => 'EmployeeAssessmentListStore';

    public reloadOrInitData = () => {};
}
