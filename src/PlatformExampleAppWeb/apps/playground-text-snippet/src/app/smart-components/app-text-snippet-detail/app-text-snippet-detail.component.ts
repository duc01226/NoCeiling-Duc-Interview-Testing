import { ChangeDetectionStrategy, Component, OnInit, ViewEncapsulation } from '@angular/core';
import {
    SaveTextSnippetCommand,
    SearchTextSnippetQuery,
    TextSnippetRepository
} from '@libs/apps-domains/text-snippet-domain';
import { cloneDeep, PlatformApiServiceErrorResponse, PlatformSmartComponent } from '@libs/platform-core';
import { of } from 'rxjs';

import { AppUiStateData, AppUiStateStore } from '../../app-ui-state';
import { AppTextSnippetDetail } from './app-text-snippet-detail.view-model';

@Component({
    selector: 'platform-example-web-text-snippet-detail',
    templateUrl: './app-text-snippet-detail.component.html',
    styleUrls: ['./app-text-snippet-detail.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    encapsulation: ViewEncapsulation.None
})
export class AppTextSnippetDetailComponent
    extends PlatformSmartComponent<AppUiStateData, AppUiStateStore, AppTextSnippetDetail>
    implements OnInit
{
    public constructor(appUiState: AppUiStateStore, private snippetTextRepo: TextSnippetRepository) {
        super(appUiState);
        this.vm = new AppTextSnippetDetail();

        this.selectAppUiState(p => p.selectedSnippetTextId).subscribe(x => {
            this.updateVm({
                toSaveTextSnippetId: x
            });
            this.loadSelectedTextSnippetItem();
        });
    }

    public ngOnInit(): void {
        super.ngOnInit();

        this.loadSelectedTextSnippetItem();
    }

    public loadSelectedTextSnippetItem = this.effect(() => {
        this.updateVm({ error: null });

        if (this.vm.toSaveTextSnippetId == null) return of();
        return this.snippetTextRepo
            .search(
                new SearchTextSnippetQuery({
                    searchId: this.vm.toSaveTextSnippetId,
                    skipCount: 0,
                    maxResultCount: 1
                })
            )
            .pipe(
                this.observerLoadingState('loadSelectedTextSnippetItem', {
                    onError: err =>
                        this.updateVm({ error: PlatformApiServiceErrorResponse.getDefaultFormattedMessage(err) })
                }),
                this.tapResponse(data => {
                    this.updateVm({
                        toSaveTextSnippet: cloneDeep(data.items[0])
                    });
                })
            );
    });

    public onSaveSelectedTextSnippetItem = this.effect(() => {
        this.updateVm({ saveTextSnippetError: null });

        return this.snippetTextRepo.save(new SaveTextSnippetCommand({ data: this.vm.toSaveTextSnippet })).pipe(
            this.observerLoadingState('saveTextSnippet'),
            this.tapResponse(
                result => {
                    if (this.vm.isCreateNew()) {
                        this.updateVm(vm => vm.resetSelectedSnippetItem());
                    } else {
                        this.updateVm({
                            toSaveTextSnippet: result.savedData,
                            toSaveTextSnippetId: result.savedData.id,
                            hasSelectedSnippetItemChanged: false,
                            originalToSaveTextSnippet: cloneDeep(result.savedData)
                        });
                        if (this.appUiStateStore.currentVm.selectedSnippetTextId != this.vm.toSaveTextSnippetId) {
                            this.appUiStateStore.updateState({
                                selectedSnippetTextId: this.vm.toSaveTextSnippetId
                            });
                        }
                    }
                },
                err =>
                    this.updateVm({
                        saveTextSnippetError: PlatformApiServiceErrorResponse.getDefaultFormattedMessage(err)
                    })
            )
        );
    });

    protected override onInitVm: () => AppTextSnippetDetail = () =>
        new AppTextSnippetDetail({ toSaveTextSnippetId: this.appUiStateStore.currentVm.selectedSnippetTextId });
}
