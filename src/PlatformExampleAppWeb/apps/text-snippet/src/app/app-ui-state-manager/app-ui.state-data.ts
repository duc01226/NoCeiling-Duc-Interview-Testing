import { PlatformApiServiceErrorResponse } from '@platform-example-web/platform-core';

export interface IAppUiStateData {
  selectedSnippetTextId?: string;
  unexpectedError?: PlatformApiServiceErrorResponse;
}
export class AppUiStateData implements IAppUiStateData {
  public constructor(data?: Partial<IAppUiStateData>) {
    this.selectedSnippetTextId = data?.selectedSnippetTextId;
    this.unexpectedError = data?.unexpectedError;
  }
  public selectedSnippetTextId?: string;
  public unexpectedError?: PlatformApiServiceErrorResponse;
}
