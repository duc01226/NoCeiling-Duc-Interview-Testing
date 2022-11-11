import { ITextSnippetDataModel, TextSnippetDataModel } from '@platform-example-web/apps-domains/text-snippet-domain';
import { Exts, PlatformApiServiceErrorInfo, PlatformViewModel } from '@platform-example-web/platform-core';

export interface IAppTextSnippetDetail {
  toSaveTextSnippet: ITextSnippetDataModel;
  toSaveTextSnippetId?: string;
  loadingTextSnippet: boolean;
  savingTextSnippet: boolean;
  hasSelectedSnippetItemChanged: boolean;
  saveTextSnippetError?: PlatformApiServiceErrorInfo;
}

export class AppTextSnippetDetail extends PlatformViewModel implements IAppTextSnippetDetail {
  public constructor(data?: Partial<IAppTextSnippetDetail>) {
    super();
    this.toSaveTextSnippet = data?.toSaveTextSnippet
      ? new TextSnippetDataModel(data.toSaveTextSnippet)
      : new TextSnippetDataModel();
    this.clonedSelectedSnippetItem = Exts.Object.cloneDeep(this.toSaveTextSnippet);
    this.toSaveTextSnippetId = data?.toSaveTextSnippetId ?? undefined;
    this.loadingTextSnippet = data?.loadingTextSnippet ?? false;
    this.savingTextSnippet = data?.savingTextSnippet ?? false;
    this.hasSelectedSnippetItemChanged = data?.hasSelectedSnippetItemChanged ?? false;
    this.saveTextSnippetError = data?.saveTextSnippetError;
  }

  private _toSaveTextSnippet: TextSnippetDataModel = new TextSnippetDataModel();
  public get toSaveTextSnippet(): TextSnippetDataModel {
    return this._toSaveTextSnippet;
  }
  public set toSaveTextSnippet(v: TextSnippetDataModel) {
    this._toSaveTextSnippet = v;
    this.clonedSelectedSnippetItem = Exts.Object.cloneDeep(v);
    this.updateHasSelectedSnippetItemChanged();
  }

  private _toSaveTextSnippetId: string | undefined;
  public get toSaveTextSnippetId(): string | undefined {
    return this._toSaveTextSnippetId;
  }
  public set toSaveTextSnippetId(v: string | undefined) {
    this._toSaveTextSnippetId = v;
    if (v == undefined) {
      this.toSaveTextSnippet = new TextSnippetDataModel();
    }
  }

  public loadingTextSnippet: boolean;
  public savingTextSnippet: boolean;
  public hasSelectedSnippetItemChanged: boolean;
  public saveTextSnippetError?: PlatformApiServiceErrorInfo;

  private clonedSelectedSnippetItem: TextSnippetDataModel;

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
    this.hasSelectedSnippetItemChanged = Exts.Object.isDifferent(
      this.toSaveTextSnippet,
      this.clonedSelectedSnippetItem
    );
    return this.hasSelectedSnippetItemChanged;
  }

  public resetSelectedSnippetItem() {
    this.toSaveTextSnippet = Exts.Object.cloneDeep(this.clonedSelectedSnippetItem);
  }

  public isCreateNew(): boolean {
    return this.toSaveTextSnippet?.id == null;
  }
}
