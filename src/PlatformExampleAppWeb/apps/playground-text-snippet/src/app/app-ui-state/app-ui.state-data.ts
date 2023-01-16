import { PlatformApiServiceErrorResponse, PlatformVm } from '@libs/platform-core';

export class AppUiStateData extends PlatformVm  {
  public constructor(data?: Partial<AppUiStateData>) {
    super(data);

    this.selectedSnippetTextId = data?.selectedSnippetTextId;
    this.appError = data?.appError;
  }
  public selectedSnippetTextId?: string;
  public appError?: PlatformApiServiceErrorResponse | Error;

  public get appErrorMsg(): string | undefined {
    return this.appError != undefined
      ? PlatformApiServiceErrorResponse.getDefaultFormattedMessage(this.appError)
      : undefined;
  }
}
