import { IPlatformDomainModuleConfig, PlatformDomainModuleConfig } from '@platform-example-web/platform-core';

export interface IAppsTextSnippetDomainModuleConfig extends IPlatformDomainModuleConfig {
  textSnippetApiHost: string;
}

export class AppsTextSnippetDomainModuleConfig
  extends PlatformDomainModuleConfig
  implements IAppsTextSnippetDomainModuleConfig {
  public constructor(data?: Partial<IAppsTextSnippetDomainModuleConfig>) {
    super(data);

    this.textSnippetApiHost = data?.textSnippetApiHost ?? '';
  }

  public textSnippetApiHost: string;
}
