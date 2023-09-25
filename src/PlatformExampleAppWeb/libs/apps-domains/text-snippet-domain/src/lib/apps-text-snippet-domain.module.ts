import { ModuleWithProviders, NgModule, Type } from '@angular/core';
import { PlatformDomainModule, PlatformRepositoryErrorEventHandler } from '@libs/platform-core';

import { TextSnippetApi } from './apis';
import { TextSnippetRepositoryContext } from './apps-text-snippet.repository-context';
import { AppsTextSnippetDomainModuleConfig } from './apps-text-snippet-domain.config';
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
        appRepositoryContext: TextSnippetRepositoryContext,
        appRepositories: [TextSnippetRepository],
        appApis: [TextSnippetApi],
        appRepositoryErrorEventHandlers: config.appRepositoryErrorEventHandlers,
        additionalProviders: [
          {
            provide: AppsTextSnippetDomainModuleConfig,
            useFactory: () => config.moduleConfigFactory()
          }
        ]
      })
    ];
  }
}
