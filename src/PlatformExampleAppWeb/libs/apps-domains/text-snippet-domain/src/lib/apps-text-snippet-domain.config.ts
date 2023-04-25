export interface IAppsTextSnippetDomainModuleConfig {
    textSnippetApiHost: string;
}

export class AppsTextSnippetDomainModuleConfig implements IAppsTextSnippetDomainModuleConfig {
    public constructor(data?: Partial<IAppsTextSnippetDomainModuleConfig>) {
        this.textSnippetApiHost = data?.textSnippetApiHost ?? '';
    }

    public textSnippetApiHost: string;
}
