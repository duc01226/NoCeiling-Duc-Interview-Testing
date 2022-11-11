import { ITextSnippetDataModel, TextSnippetDataModel } from '@platform-example-web/apps-domains/text-snippet-domain';
import {
  PlatformApiServiceErrorInfo,
  PlatformApiServiceErrorResponse,
  PlatformViewModel,
} from '@platform-example-web/platform-core';

export interface IAppViewModel {
  searchText?: string;
  textSnippetItems?: IAppTextSnippetItemViewModel[];
  selectedSnippetTextId?: string;
  totalTextSnippetItems: number;
  currentTextSnippetItemsPageNumber: number;
  loadingTextSnippetItems: boolean;
  loadingTextSnippetItemsError?: PlatformApiServiceErrorInfo;
  unexpectedError?: PlatformApiServiceErrorResponse;
}

export class AppViewModel extends PlatformViewModel implements IAppViewModel {
  public static readonly textSnippetItemsPageSize = 10;

  public constructor(data?: Partial<IAppViewModel>) {
    super();
    this.searchText = data?.searchText ?? '';
    this.textSnippetItems = data?.textSnippetItems
      ? data?.textSnippetItems.map(x => new AppTextSnippetItemViewModel(x))
      : undefined;
    this.totalTextSnippetItems = data?.totalTextSnippetItems ?? 0;
    this.currentTextSnippetItemsPageNumber = data?.currentTextSnippetItemsPageNumber ?? 0;
    this.selectedSnippetTextId = data?.selectedSnippetTextId ?? undefined;
    this.loadingTextSnippetItems = data?.loadingTextSnippetItems ?? false;
    this.loadingTextSnippetItemsError = data?.loadingTextSnippetItemsError;
    this.unexpectedError = data?.unexpectedError;
  }
  public searchText?: string;
  public textSnippetItems?: AppTextSnippetItemViewModel[];
  public currentTextSnippetItemsPageNumber: number;
  public totalTextSnippetItems: number;
  public selectedSnippetTextId?: string;
  public loadingTextSnippetItems: boolean;
  public loadingTextSnippetItemsError?: PlatformApiServiceErrorInfo;
  public unexpectedError?: PlatformApiServiceErrorResponse;

  public textSnippetItemsPageSize(): number {
    return AppViewModel.textSnippetItemsPageSize;
  }

  public currentTextSnippetItemsSkipCount(): number {
    return this.textSnippetItemsPageSize() * this.currentTextSnippetItemsPageNumber;
  }
}

export interface IAppTextSnippetItemViewModel {
  data: ITextSnippetDataModel;
}
export class AppTextSnippetItemViewModel {
  public constructor(data?: Partial<IAppTextSnippetItemViewModel>) {
    this.data = data?.data ?? new TextSnippetDataModel();
  }
  public data: TextSnippetDataModel;
}
