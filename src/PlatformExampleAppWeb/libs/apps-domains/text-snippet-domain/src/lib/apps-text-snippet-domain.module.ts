import { ModuleWithProviders, NgModule, Type } from '@angular/core';
import { PlatformDomainModule, PlatformRepositoryErrorEventHandler } from '@platform-example-web/platform-core';

import { TextSnippetApi } from './apis';
import { AppsTextSnippetDomainModuleConfig } from './apps-text-snippet-domain.config';
import { TextSnippetRepositoryContext } from './apps-text-snippet.repository-context';
import { TextSnippetRepository } from './repositories';

@NgModule({
  imports: []
})
export class AppsTextSnippetDomainModule {
  public static forRoot(config: {
    moduleConfigFactory: () => AppsTextSnippetDomainModuleConfig;
    appRepositoryErrorEventHandlers?: Type<PlatformRepositoryErrorEventHandler>[];
  }): ModuleWithProviders<PlatformDomainModule>[] {
    return [
      ...PlatformDomainModule.forRoot({
        moduleConfig: {
          type: AppsTextSnippetDomainModuleConfig,
          configFactory: () => config.moduleConfigFactory()
        },
        appRepositoryContext: TextSnippetRepositoryContext,
        appRepositories: [TextSnippetRepository],
        appApis: [TextSnippetApi],
        appRepositoryErrorEventHandlers: config.appRepositoryErrorEventHandlers
      })
    ];
  }

  public static forChild(): ModuleWithProviders<PlatformDomainModule>[] {
    return [
      ...PlatformDomainModule.forChild({
        appModuleRepositoryContext: TextSnippetRepositoryContext,
        appModuleRepositories: [TextSnippetRepository],
        appModuleApis: [TextSnippetApi]
      })
    ];
  }
}
