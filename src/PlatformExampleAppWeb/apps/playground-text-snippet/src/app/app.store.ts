import { Injectable } from '@angular/core';
import { SearchTextSnippetQuery, TextSnippetApi, TextSnippetDataModel } from '@libs/apps-domains/text-snippet-domain';
import { PlatformVm, PlatformVmStore, distinctUntilObjectValuesChanged } from '@libs/platform-core';
import { Observable, filter, switchMap, throttleTime } from 'rxjs';

export class AppVm extends PlatformVm {
    public static readonly textSnippetItemsPageSize = 10;

    public constructor(data?: Partial<AppVm>) {
        super();
        this.searchText = data?.searchText ?? '';
        this.textSnippetItems = data?.textSnippetItems
            ? data?.textSnippetItems.map(x => new AppVm_TextSnippetItem(x))
            : undefined;
        this.totalTextSnippetItems = data?.totalTextSnippetItems ?? 0;
        this.currentTextSnippetItemsPageNumber = data?.currentTextSnippetItemsPageNumber ?? 0;
        this.selectedSnippetTextId = data?.selectedSnippetTextId ?? undefined;
    }
    public searchText?: string;
    public textSnippetItems?: AppVm_TextSnippetItem[];
    public currentTextSnippetItemsPageNumber: number;
    public totalTextSnippetItems: number;
    public selectedSnippetTextId?: string | null;

    public textSnippetItemsPageSize(): number {
        return AppVm.textSnippetItemsPageSize;
    }

    public currentSearchTextSnippetQuery() {
        return new SearchTextSnippetQuery({
            maxResultCount: this.textSnippetItemsPageSize(),
            skipCount: this.currentTextSnippetItemsSkipCount(),
            searchText: this.searchText
        });
    }

    public currentTextSnippetItemsSkipCount(): number {
        return this.textSnippetItemsPageSize() * this.currentTextSnippetItemsPageNumber;
    }

    // Demo get/set using platform watch decorator
    // Shorthand execute a target function doing something directly if on change only do this logic
    // @Watch('pagedResultWatch')
    // public pagedResult?: PlatformPagedResultDto<LeaveType>;

    // // Full syntax execute a NORMAL FUNCTION
    // @Watch<PlatformPagedQueryDto, LeaveTypesState>((value, change, targetObj) => {
    //   targetObj.updatePageInfo();
    // })
    // public pagedQuery: PlatformPagedQueryDto = new PlatformPagedQueryDto();

    // public pagedResultWatch(
    //   value: PlatformPagedResultDto<LeaveType> | undefined,
    //   change: SimpleChange<PlatformPagedResultDto<LeaveType> | undefined>
    // ) {
    //   this.updatePageInfo();
    // }

    // Demo using validation object
    /**
* return Validation.validateNot(remainingLeave, remainingLeave.totalRemainingLeaveDays <= 0, {
                code: LeaveRequestDetailFormValidationKeys.notEnoughRemainingLeave,
                errorMsg:
                  'The number of remaining leaves is not sufficient for this leave type. Please try another one!'
              })
                .andNextValidate(remainingLeave =>
                  remainingLeave.validateEnoughAvailableRemainingLeaveDays(
                    this.vm.totalDays,
                    this.vm.fromDate,
                    LeaveRequestDetailFormValidationKeys.reachedMaximumTotalDays
                  )
                )
                .match({
                  valid: value => <ValidationErrors | null>null,
                  invalid: errorValidation =>
                    buildFormValidationErrors(errorValidation.error.code, errorValidation.error.errorMsg)
                });
*/
}

export class AppVm_TextSnippetItem {
    public constructor(data?: Partial<AppVm_TextSnippetItem>) {
        this.data = data?.data ?? new TextSnippetDataModel();
    }
    public data: TextSnippetDataModel;
}

@Injectable()
export class AppStore extends PlatformVmStore<AppVm> {
    public query$ = this.select(p => p.currentSearchTextSnippetQuery()).pipe(distinctUntilObjectValuesChanged());

    public constructor(private snippetTextApi: TextSnippetApi) {
        super(new AppVm());
    }

    protected onInitVm = () => {
        this.loadSnippetTextItems(this.query$);
    };

    public vmConstructor = (data?: Partial<AppVm>) => new AppVm(data);

    protected cachedStateKeyName = () => 'AppStore';

    public initOrReloadVm = (isReload: boolean) => {
        this.loadSnippetTextItems(this.currentState().currentSearchTextSnippetQuery(), isReload);
        return null;
    };

    public loadSnippetTextItems = this.effect((query$: Observable<SearchTextSnippetQuery>, isReloading?: boolean) => {
        return query$.pipe(
            switchMap(() =>
                this.snippetTextApi.search(
                    new SearchTextSnippetQuery({
                        maxResultCount: this.currentState().textSnippetItemsPageSize(),
                        skipCount: this.currentState().currentTextSnippetItemsSkipCount(),
                        searchText: this.currentState().searchText
                    })
                )
            ),
            this.observerLoadingErrorState('loadSnippetTextItems', { isReloading: isReloading }),
            this.tapResponse(data => {
                this.updateState({
                    textSnippetItems: data.items.map(x => new AppVm_TextSnippetItem({ data: x })),
                    totalTextSnippetItems: data.totalCount
                });
            })
        );
    });

    public changePage = this.effect((pageIndex$: Observable<number>, isReloading?: boolean) => {
        return pageIndex$.pipe(
            filter(pageIndex => pageIndex != this.currentState().currentTextSnippetItemsPageNumber),
            this.tapResponse(pageIndex => this.updateState({ currentTextSnippetItemsPageNumber: pageIndex }))
        );
    });

    public changeSearchText = this.effect((searchText$: Observable<string>, isReloading?: boolean) => {
        return searchText$.pipe(
            filter(v => v != this.currentState().searchText),
            throttleTime(500),
            this.tapResponse(v => this.updateState({ searchText: v, currentTextSnippetItemsPageNumber: 0 }))
        );
    });
}
