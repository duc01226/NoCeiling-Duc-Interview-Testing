import { IPlatformDataModel, PlatformDataModel } from '@libs/platform-core';

export interface ITextSnippetDataModel extends IPlatformDataModel {
  snippetText: string;
  fullText: string;
}

export class TextSnippetDataModel extends PlatformDataModel implements ITextSnippetDataModel {
  public constructor(data?: Partial<ITextSnippetDataModel>) {
    super(data);
    this.snippetText = data?.snippetText ?? '';
    this.fullText = data?.fullText ?? '';
  }

  public snippetText: string;
  public fullText: string;
}
