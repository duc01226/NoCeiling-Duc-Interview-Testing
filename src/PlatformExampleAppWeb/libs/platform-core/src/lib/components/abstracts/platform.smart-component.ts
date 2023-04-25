import { Directive, OnInit } from '@angular/core';
import { Observable } from 'rxjs';

import { PlatformAppUiStateData, PlatformAppUiStateStore } from '../../app-ui-state';
import { PlatformVm } from '../../view-models';
import { PlatformVmComponent } from './platform.vm-component';

@Directive()
export abstract class PlatformSmartComponent<
        TAppUiStateData extends PlatformAppUiStateData,
        TAppUiStateStore extends PlatformAppUiStateStore<TAppUiStateData>,
        TViewModel extends PlatformVm
    >
    extends PlatformVmComponent<TViewModel>
    implements OnInit
{
    public constructor(protected appUiStateStore: TAppUiStateStore) {
        super();
    }

    protected selectAppUiState<T>(selector: (uiStateData: TAppUiStateData) => T): Observable<T> {
        return this.appUiStateStore.select(selector).pipe(this.untilDestroyed());
    }
}
