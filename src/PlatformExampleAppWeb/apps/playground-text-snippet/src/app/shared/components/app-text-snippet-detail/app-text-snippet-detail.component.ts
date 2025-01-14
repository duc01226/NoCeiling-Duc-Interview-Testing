import { ChangeDetectionStrategy, Component, OnInit, ViewEncapsulation } from '@angular/core';

import { Observable, of, switchMap } from 'rxjs';

import { SaveTextSnippetCommand, SearchTextSnippetQuery, TextSnippetApi } from '@libs/apps-domains/text-snippet-domain';
import { PlatformApiServiceErrorResponse, PlatformVmComponent, cloneDeep } from '@libs/platform-core';

import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule } from '@angular/forms';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { TranslateModule } from '@ngx-translate/core';
import { AppStore } from '../../../app.store';
import { AppTextSnippetDetail } from './app-text-snippet-detail.view-model';

@Component({
    selector: 'platform-example-web-text-snippet-detail',
    templateUrl: './app-text-snippet-detail.component.html',
    styleUrls: ['./app-text-snippet-detail.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    encapsulation: ViewEncapsulation.None,
    imports: [
        CommonModule,
        FormsModule,
        ReactiveFormsModule,
        TranslateModule,
        MatInputModule,
        MatFormFieldModule,
        MatProgressSpinnerModule
    ]
})
export class AppTextSnippetDetailComponent extends PlatformVmComponent<AppTextSnippetDetail> implements OnInit {
    public constructor(
        private appStore: AppStore,
        private snippetTextApi: TextSnippetApi
    ) {
        super();

        this.appStore
            .select(p => p.selectedSnippetTextId)
            .pipe(
                this.subscribeUntilDestroyed(x => {
                    this.updateVm({
                        toSaveTextSnippetId: x
                    });
                    this.loadSelectedTextSnippetItem();
                })
            );
    }

    public override ngOnInit(): void {
        super.ngOnInit();

        this.loadSelectedTextSnippetItem();
    }

    public loadSelectedTextSnippetItem = this.effect((query$: Observable<void>, isReloading?: boolean) => {
        return query$.pipe(
            switchMap(() => {
                this.updateVm({ error: null });

                if (this.currentVm().toSaveTextSnippetId == null) return of(undefined);
                return this.snippetTextApi
                    .search(
                        new SearchTextSnippetQuery({
                            searchId: this.currentVm().toSaveTextSnippetId,
                            skipCount: 0,
                            maxResultCount: 1
                        })
                    )
                    .pipe(
                        this.observerLoadingErrorState('loadSelectedTextSnippetItem'),
                        this.tapResponse(
                            data => {
                                this.updateVm({
                                    toSaveTextSnippet: cloneDeep(data.items[0])
                                });
                            },
                            err => {
                                this.updateVm({
                                    error: PlatformApiServiceErrorResponse.getDefaultFormattedMessage(err)
                                });
                            }
                        )
                    );
            })
        );
    });

    public onSaveSelectedTextSnippetItem = this.effect((query$: Observable<void>, isReloading?: boolean) => {
        return query$.pipe(
            switchMap(() => {
                this.updateVm({ saveTextSnippetError: null });

                return this.snippetTextApi
                    .save(new SaveTextSnippetCommand({ data: this.currentVm().toSaveTextSnippet }))
                    .pipe(
                        this.observerLoadingErrorState('saveTextSnippet'),
                        this.tapResponse(
                            result => {
                                if (this.currentVm().isCreateNew()) {
                                    this.updateVm(vm => vm.resetSelectedSnippetItem());
                                } else {
                                    this.updateVm({
                                        toSaveTextSnippet: result.savedData,
                                        toSaveTextSnippetId: result.savedData.id,
                                        hasSelectedSnippetItemChanged: false,
                                        originalToSaveTextSnippet: cloneDeep(result.savedData)
                                    });
                                    if (
                                        this.appStore.currentState().selectedSnippetTextId !=
                                        this.currentVm().toSaveTextSnippetId
                                    ) {
                                        this.appStore.updateState({
                                            selectedSnippetTextId: this.currentVm().toSaveTextSnippetId
                                        });
                                    }
                                }

                                this.appStore.reload();
                            },
                            err =>
                                this.updateVm({
                                    saveTextSnippetError:
                                        PlatformApiServiceErrorResponse.getDefaultFormattedMessage(err)
                                })
                        )
                    );
            })
        );
    });

    public override initOrReloadVm = (isReload: boolean): Observable<AppTextSnippetDetail> => {
        return of(
            new AppTextSnippetDetail({ toSaveTextSnippetId: this.appStore.currentState().selectedSnippetTextId })
        );
    };
}
