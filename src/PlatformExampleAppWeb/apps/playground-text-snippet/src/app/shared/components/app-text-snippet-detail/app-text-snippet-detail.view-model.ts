import { TextSnippetDataModel } from '@libs/apps-domains/text-snippet-domain';
import { cloneDeep, isDifferent, PlatformVm } from '@libs/platform-core';

export class AppTextSnippetDetail extends PlatformVm {
    public constructor(data?: Partial<AppTextSnippetDetail>) {
        super();
        this.toSaveTextSnippet =
            data?.toSaveTextSnippet != null
                ? new TextSnippetDataModel(data.toSaveTextSnippet)
                : new TextSnippetDataModel();
        this.originalToSaveTextSnippet = cloneDeep(this.toSaveTextSnippet);
        this.toSaveTextSnippetId = data?.toSaveTextSnippetId ?? undefined;
        this.hasSelectedSnippetItemChanged = data?.hasSelectedSnippetItemChanged ?? false;
        this.saveTextSnippetError = data?.saveTextSnippetError;
    }

    private _toSaveTextSnippet: TextSnippetDataModel = new TextSnippetDataModel();
    public get toSaveTextSnippet(): TextSnippetDataModel {
        return this._toSaveTextSnippet;
    }
    public set toSaveTextSnippet(v: TextSnippetDataModel) {
        this._toSaveTextSnippet = v;
        this.originalToSaveTextSnippet = cloneDeep(v);
        this.updateHasSelectedSnippetItemChanged();
    }

    private _toSaveTextSnippetId: string | undefined | null;
    public get toSaveTextSnippetId(): string | undefined | null {
        return this._toSaveTextSnippetId;
    }
    public set toSaveTextSnippetId(v: string | undefined | null) {
        this._toSaveTextSnippetId = v;
        if (v == undefined) {
            this.toSaveTextSnippet = new TextSnippetDataModel();
        }
    }

    public hasSelectedSnippetItemChanged: boolean;
    public saveTextSnippetError?: string | null;

    public originalToSaveTextSnippet: TextSnippetDataModel;

    public get toSaveTextSnippetSnippetText(): string {
        return this.toSaveTextSnippet?.snippetText ?? '';
    }
    public set toSaveTextSnippetSnippetText(v: string) {
        if (this.toSaveTextSnippet != null) this.toSaveTextSnippet.snippetText = v;
        this.updateHasSelectedSnippetItemChanged();
    }
    public get toSaveTextSnippetFullText(): string {
        return this.toSaveTextSnippet?.fullText ?? '';
    }
    public set toSaveTextSnippetFullText(v: string) {
        if (this.toSaveTextSnippet != null) this.toSaveTextSnippet.fullText = v;
        this.updateHasSelectedSnippetItemChanged();
    }

    public updateHasSelectedSnippetItemChanged(): boolean {
        this.hasSelectedSnippetItemChanged = isDifferent(this.toSaveTextSnippet, this.originalToSaveTextSnippet);
        return this.hasSelectedSnippetItemChanged;
    }

    public resetSelectedSnippetItem() {
        this.toSaveTextSnippet = cloneDeep(this.originalToSaveTextSnippet);
    }

    public isCreateNew(): boolean {
        return this.toSaveTextSnippet?.id == null;
    }
}
