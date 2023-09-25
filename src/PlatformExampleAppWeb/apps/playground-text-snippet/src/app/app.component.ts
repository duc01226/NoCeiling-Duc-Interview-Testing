import { ChangeDetectionStrategy, Component, OnInit, ViewEncapsulation } from '@angular/core';
import { PageEvent } from '@angular/material/paginator';
import { SearchTextSnippetQuery, TextSnippetRepository } from '@libs/apps-domains/text-snippet-domain';
import { PlatformApiServiceErrorResponse, PlatformSmartComponent, task_delay } from '@libs/platform-core';

import { AppTextSnippetItemViewModel, AppViewModel } from './app.view-model';
import { AppUiStateData, AppUiStateStore } from './app-ui-state';

@Component({
  selector: 'platform-example-web-root',
  templateUrl: './app.component.html',
  styleUrls: ['./app.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  encapsulation: ViewEncapsulation.None
})
export class AppComponent
  extends PlatformSmartComponent<AppUiStateData, AppUiStateStore, AppViewModel>
  implements OnInit
{
  public constructor(appUiState: AppUiStateStore, private snippetTextRepo: TextSnippetRepository) {
    super(appUiState);
    this.vm = new AppViewModel();

    this.selectAppUiState(p => p.appError).subscribe(x => {
      this.updateVm({
        appError: x
      });
    });
  }

  public title = 'Text Snippet';
  public textSnippetsItemGridDisplayedColumns = ['SnippetText', 'FullText'];

  public ngOnInit(): void {
    super.ngOnInit();

    this.loadSnippetTextItems();
  }

  public loadSnippetTextItems = this.effect(() => {
    this.appUiStateStore.clearAppGlobalError();
    this.updateVm({ loadTextSnippetItemsErrorMsg: undefined });

    return this.snippetTextRepo
      .search(
        new SearchTextSnippetQuery({
          maxResultCount: this.vm.textSnippetItemsPageSize(),
          skipCount: this.vm.currentTextSnippetItemsSkipCount(),
          searchText: this.vm.searchText
        })
      )
      .pipe(
        this.observerLoadingState('loadSnippetTextItems', {
          onError: error => {
            this.updateVm({
              loadTextSnippetItemsErrorMsg: PlatformApiServiceErrorResponse.getDefaultFormattedMessage(error)
            });
          }
        }),
        this.tapResponse(data => {
          this.updateVm({
            textSnippetItems: data.items.map(x => new AppTextSnippetItemViewModel({ data: x })),
            totalTextSnippetItems: data.totalCount
          });
        })
      );
  });

  public onSearchTextChange(newValue: string): void {
    this.cancelStoredSubscription('onSearchTextChange');

    const onSearchTextChangeDelay = task_delay(
      () => {
        if (this.vm.searchText == newValue) return;

        this.updateVm({
          searchText: newValue,
          currentTextSnippetItemsPageNumber: 0
        });

        this.loadSnippetTextItems();
      },
      500,
      this.destroyed$
    );

    this.storeSubscription('onSearchTextChange', onSearchTextChangeDelay);
  }

  public onTextSnippetGridChangePage(e: PageEvent) {
    if (this.vm.currentTextSnippetItemsPageNumber == e.pageIndex) return;

    this.updateVm({
      currentTextSnippetItemsPageNumber: e.pageIndex
    });
    this.loadSnippetTextItems();
  }

  public toggleSelectTextSnippedGridRow(row: AppTextSnippetItemViewModel) {
    this.updateVm({
      selectedSnippetTextId: this.vm.selectedSnippetTextId != row.data.id ? row.data.id : undefined
    });
    this.appUiStateStore.updateState({
      selectedSnippetTextId: this.vm.selectedSnippetTextId
    });
  }

  protected override onInitVm: () => AppViewModel = () => new AppViewModel();
}
