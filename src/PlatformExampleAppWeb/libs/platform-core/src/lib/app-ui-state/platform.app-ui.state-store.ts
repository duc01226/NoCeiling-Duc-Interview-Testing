import { Injectable } from '@angular/core';

import { PlatformApiServiceErrorResponse } from '../api-services';
import { PlatformVm, PlatformVmStore } from '../view-models';

@Injectable()
export abstract class PlatformAppUiStateStore<
  TAppUiStateData extends PlatformAppUiStateData
> extends PlatformVmStore<TAppUiStateData> {
  public constructor(defaultState: TAppUiStateData) {
    super(defaultState);
  }

  public clearAppGlobalError(): void {
    this.updateState(<Partial<TAppUiStateData>>{
      appError: undefined
    });
  }

  public setAppGlobalError(error: PlatformApiServiceErrorResponse | Error | undefined): void {
    this.updateState(<Partial<TAppUiStateData>>{
      appError: error
    });
  }
}

export class PlatformAppUiStateData extends PlatformVm {
  constructor(data?: Partial<PlatformAppUiStateData>) {
    super(data);
    if (data == null) return;

    if (data.appError !== undefined) this.appError = data.appError;
  }

  public appError?: PlatformApiServiceErrorResponse | Error;
}
