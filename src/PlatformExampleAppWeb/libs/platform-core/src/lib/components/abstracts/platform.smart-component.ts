import { ChangeDetectorRef, Directive } from '@angular/core';
import { Observable, Subscription } from 'rxjs';

import { PlatformAppUiStateManager } from '../../app-ui-state-manager';
import { Exts } from '../../extensions';
import { PlatformViewModel } from '../../view-models';
import { PlatformComponent } from './platform.component';

@Directive()
export abstract class PlatformSmartComponent<
  TAppUiStateData,
  TAppUiState extends PlatformAppUiStateManager<TAppUiStateData>,
  TViewModel extends PlatformViewModel
> extends PlatformComponent {
  public constructor(changeDetector: ChangeDetectorRef, protected appUiState: TAppUiState) {
    super(changeDetector);
    this._vm = this.initialVm(appUiState.currentData());
  }

  private _vm: TViewModel;
  public get vm(): TViewModel {
    return this._vm;
  }
  public set vm(v: TViewModel) {
    if (this._vm != v) {
      this._vm = v;
      this.detectChanges();
    }
  }

  private subscriptionMap: Map<string, Subscription> = new Map();

  protected abstract initialVm(currentAppUiStateData: TAppUiStateData): TViewModel;

  protected storeSubscription(key: string, subscription: Subscription): void {
    this.subscriptionMap.set(key, subscription);
  }

  protected unsubscribeSubscription(key: string): void {
    this.subscriptionMap.get(key)?.unsubscribe();
    this.subscriptionMap.delete(key);
  }

  protected updateVm(updateFn: (currentVm: TViewModel) => void) {
    this.vm = Exts.Object.clone(this.vm, clonedVm => {
      updateFn(clonedVm);
    });
  }

  protected selectUiStateData<T>(selector: (uiStateData: TAppUiStateData) => T): Observable<T> {
    return this.appUiState.selectUiStateData(selector).pipe(this.untilDestroyed());
  }
}
