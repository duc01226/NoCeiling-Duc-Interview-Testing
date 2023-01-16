import { ITextSnippetDataModel, TextSnippetDataModel } from '@libs/apps-domains/text-snippet-domain';
import { cloneDeep, IPlatformVm, isDifferent, PlatformVm } from '@libs/platform-core';

export interface IAppTextSnippetDetail extends IPlatformVm {
  toSaveTextSnippet: ITextSnippetDataModel;
  toSaveTextSnippetId?: string;
  hasSelectedSnippetItemChanged: boolean;
  saveTextSnippetError?: string | null;
}

export class AppTextSnippetDetail extends PlatformVm implements IAppTextSnippetDetail {
  public constructor(data?: Partial<IAppTextSnippetDetail>) {
    super();
    this.toSaveTextSnippet = data?.toSaveTextSnippet
      ? new TextSnippetDataModel(data.toSaveTextSnippet)
      : new TextSnippetDataModel();
    this.clonedSelectedSnippetItem = cloneDeep(this.toSaveTextSnippet);
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
    this.clonedSelectedSnippetItem = cloneDeep(v);
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

  public hasSelectedSnippetItemChanged: boolean;
  public saveTextSnippetError?: string | null;

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
    this.hasSelectedSnippetItemChanged = isDifferent(this.toSaveTextSnippet, this.clonedSelectedSnippetItem);
    return this.hasSelectedSnippetItemChanged;
  }

  public resetSelectedSnippetItem() {
    this.toSaveTextSnippet = cloneDeep(this.clonedSelectedSnippetItem);
  }

  public isCreateNew(): boolean {
    return this.toSaveTextSnippet?.id == null;
  }
}
