import { ITextSnippetDataModel, TextSnippetDataModel } from '@libs/apps-domains/text-snippet-domain';
import { PlatformApiServiceErrorResponse, PlatformVm } from '@libs/platform-core';

export interface IAppViewModel {
    searchText?: string;
    textSnippetItems?: IAppTextSnippetItemViewModel[];
    selectedSnippetTextId?: string;
    totalTextSnippetItems: number;
    currentTextSnippetItemsPageNumber: number;
    loadTextSnippetItemsErrorMsg?: string;
    appError?: PlatformApiServiceErrorResponse | Error;
}

export class AppViewModel extends PlatformVm implements IAppViewModel {
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
        this.loadTextSnippetItemsErrorMsg = data?.loadTextSnippetItemsErrorMsg;
        this.appError = data?.appError;
    }
    public searchText?: string;
    public textSnippetItems?: AppTextSnippetItemViewModel[];
    public currentTextSnippetItemsPageNumber: number;
    public totalTextSnippetItems: number;
    public selectedSnippetTextId?: string;
    public loadTextSnippetItemsErrorMsg?: string;
    public appError?: PlatformApiServiceErrorResponse | Error;
    public get appErrorMsg(): string | undefined {
        return this.appError != undefined
            ? PlatformApiServiceErrorResponse.getDefaultFormattedMessage(this.appError)
            : undefined;
    }

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
