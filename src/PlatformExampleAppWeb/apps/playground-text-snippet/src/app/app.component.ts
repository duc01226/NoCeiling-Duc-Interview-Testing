import { ChangeDetectionStrategy, Component, ViewEncapsulation } from '@angular/core';

import { PlatformCoreModule, PlatformVmStoreComponent } from '@libs/platform-core';

import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialogModule } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTableModule } from '@angular/material/table';
import { TranslateModule } from '@ngx-translate/core';
import { AppStore, AppVm, AppVm_TextSnippetItem } from './app.store';
import { AppTextSnippetDetailComponent } from './shared/components/app-text-snippet-detail';

@Component({
    selector: 'platform-example-web-root',
    templateUrl: './app.component.html',
    styleUrls: ['./app.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    encapsulation: ViewEncapsulation.None,
    standalone: true,
    imports: [
        CommonModule,
        FormsModule,
        ReactiveFormsModule,
        TranslateModule,
        PlatformCoreModule,

        MatTableModule,
        MatInputModule,
        MatFormFieldModule,
        MatPaginatorModule,
        MatProgressSpinnerModule,
        MatButtonModule,
        MatIconModule,
        MatDialogModule,

        AppTextSnippetDetailComponent
    ],
    providers: []
})
export class AppComponent extends PlatformVmStoreComponent<AppVm, AppStore> {
    public title = 'Text Snippet';
    public textSnippetsItemGridDisplayedColumns = ['SnippetText', 'FullText'];

    public constructor(store: AppStore) {
        super(store);
    }

    public onSearchTextChange(newValue: string): void {
        this.store.changeSearchText(newValue);
    }

    public onTextSnippetGridChangePage(e: PageEvent) {
        this.store.changePage(e.pageIndex);
    }

    public loadSnippetTextItems() {
        this.store.loadSnippetTextItems(this.vm()!.currentSearchTextSnippetQuery());
    }

    public toggleSelectTextSnippedGridRow(row: AppVm_TextSnippetItem) {
        this.updateVm({
            selectedSnippetTextId: this.currentVm().selectedSnippetTextId != row.data.id ? row.data.id : undefined
        });
    }
}
