import { PlatformDataModel } from '@libs/platform-core';

export class TextSnippetDataModel extends PlatformDataModel {
    public constructor(data?: Partial<TextSnippetDataModel>) {
        super(data);
        this.snippetText = data?.snippetText ?? '';
        this.fullText = data?.fullText ?? '';
    }

    public snippetText: string = '';
    public fullText: string = '';
}
