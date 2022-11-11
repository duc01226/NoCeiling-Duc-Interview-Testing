import { ChangeDetectionStrategy, ChangeDetectorRef, Component, OnInit, ViewEncapsulation } from '@angular/core';
import { PageEvent } from '@angular/material/paginator';
import {
  SearchTextSnippetQueryDto,
  TextSnippetRepository
} from '@platform-example-web/apps-domains/text-snippet-domain';
import { PlatformApiServiceErrorResponse, PlatformSmartComponent, Utils } from '@platform-example-web/platform-core';

import { AppUiStateData, AppUiStateManager } from './app-ui-state-manager';
import { AppTextSnippetItemViewModel, AppViewModel } from './app.view-model';

@Component({
  selector: 'platform-example-web-root',
  templateUrl: './app.component.html',
  styleUrls: ['./app.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  encapsulation: ViewEncapsulation.None
})
export class AppComponent
  extends PlatformSmartComponent<AppUiStateData, AppUiStateManager, AppViewModel>
  implements OnInit {
  public constructor(
    changeDetector: ChangeDetectorRef,
    appUiState: AppUiStateManager,
    private snippetTextRepo: TextSnippetRepository
  ) {
    super(changeDetector, appUiState);

    this.selectUiStateData(p => p.unexpectedError).subscribe(x => {
      this.updateVm(vm => {
        vm.unexpectedError = x;
      });
    });
  }

  public title = 'Text Snippet';
  public textSnippetsItemGridDisplayedColumns = ['SnippetText', 'FullText'];

  public ngOnInit(): void {
    super.ngOnInit();

    this.loadSnippetTextItems();
  }

  public loadSnippetTextItems(): void {
    this.unsubscribeSubscription('loadSnippetTextItems');
    this.clearAppErrors();

    this.updateVm(vm => {
      vm.loadingTextSnippetItems = true;
      vm.loadingTextSnippetItemsError = undefined;
    });
    let loadSnippetTextItemsSub = this.snippetTextRepo
      .search(
        new SearchTextSnippetQueryDto({
          maxResultCount: this.vm.textSnippetItemsPageSize(),
          skipCount: this.vm.currentTextSnippetItemsSkipCount(),
          searchText: this.vm.searchText
        })
      )
      .pipe(this.untilDestroyed())
      .subscribe(
        data => {
          this.updateVm(vm => {
            vm.textSnippetItems = data.items.map(x => new AppTextSnippetItemViewModel({ data: x }));
            vm.totalTextSnippetItems = data.totalCount;
            vm.loadingTextSnippetItems = false;
          });
        },
        (error: PlatformApiServiceErrorResponse) => {
          this.updateVm(vm => {
            vm.loadingTextSnippetItemsError = error.error;
            vm.loadingTextSnippetItems = false;
          });
        }
      );

    this.storeSubscription('loadSnippetTextItems', loadSnippetTextItemsSub);
  }

  public onSearchTextChange(newValue: string): void {
    this.unsubscribeSubscription('onSearchTextChange');

    let onSearchTextChangeDelay = Utils.TaskRunner.delay(
      () => {
        if (this.vm.searchText == newValue) return;
        this.updateVm(vm => {
          vm.searchText = newValue;
          vm.currentTextSnippetItemsPageNumber = 0;
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

    this.updateVm(vm => {
      vm.currentTextSnippetItemsPageNumber = e.pageIndex;
    });
    this.loadSnippetTextItems();
  }

  public toggleSelectTextSnippedGridRow(row: AppTextSnippetItemViewModel) {
    this.updateVm(vm => {
      vm.selectedSnippetTextId = vm.selectedSnippetTextId != row.data.id ? row.data.id : undefined;
    });
    this.appUiState.updateUiStateData(p => {
      p.selectedSnippetTextId = this.vm.selectedSnippetTextId;
      return p;
    });
  }

  public clearAppErrors(): void {
    this.appUiState.updateUiStateData(p => {
      p.unexpectedError = undefined;
      return p;
    });
  }

  protected initialVm(currentAppUiStateData: AppUiStateData): AppViewModel {
    return new AppViewModel();
  }
}
