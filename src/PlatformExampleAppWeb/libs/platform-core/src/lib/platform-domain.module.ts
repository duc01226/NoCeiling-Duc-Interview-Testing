import { CommonModule } from '@angular/common';
import { HttpClientModule } from '@angular/common/http';
import { EnvironmentProviders, ModuleWithProviders, NgModule, Provider, Type } from '@angular/core';
import { FormsModule, ReactiveFormsModule } from '@angular/forms';

import { PlatformApiService } from './api-services';
import {
  PlatformRepository,
  PlatformRepositoryContext,
  PlatformRepositoryErrorEvent,
  PlatformRepositoryErrorEventHandler
} from './domain';
import { PlatformEventManagerSubscriptionsMap } from './events';

@NgModule({
  imports: [],
  exports: [CommonModule, FormsModule, ReactiveFormsModule, HttpClientModule]
})
export class PlatformDomainModule {
  public static forRoot(config: {
    appRepositoryContext?: Type<PlatformRepositoryContext>;
    appRepositories?: Type<PlatformRepository<PlatformRepositoryContext>>[];
    appApis?: Type<PlatformApiService>[];
    appRepositoryErrorEventHandlers?: Type<PlatformRepositoryErrorEventHandler>[];
    additionalProviders?: Array<Provider | EnvironmentProviders>;
  }): ModuleWithProviders<PlatformDomainModule>[] {
    return [
      {
        ngModule: PlatformDomainModule,
        providers: [
          ...this.buildRepositoryRelatedProviders({
            repositoryContext: config.appRepositoryContext,
            repositories: config.appRepositories,
            apis: config.appApis,
            repositoryErrorEventHandlers: config.appRepositoryErrorEventHandlers
          }),
          ...(config.additionalProviders != null ? config.additionalProviders : [])
        ]
      }
    ];
  }

  public static forChild(config: {
    appModuleRepositoryContext?: Type<PlatformRepositoryContext>;
    appModuleRepositories?: Type<PlatformRepository<PlatformRepositoryContext>>[];
    appModuleApis?: Type<PlatformApiService>[];
    appRepositoryErrorEventHandlers?: Type<PlatformRepositoryErrorEventHandler>[];
  }): ModuleWithProviders<PlatformDomainModule>[] {
    return [
      {
        ngModule: PlatformDomainModule,
        providers: [
          ...this.buildRepositoryRelatedProviders({
            repositoryContext: config.appModuleRepositoryContext,
            repositories: config.appModuleRepositories,
            apis: config.appModuleApis,
            repositoryErrorEventHandlers: config.appRepositoryErrorEventHandlers
          })
        ]
      }
    ];
  }

  private static buildRepositoryRelatedProviders(config: {
    repositoryContext?: Type<PlatformRepositoryContext>;
    repositories?: Type<PlatformRepository<PlatformRepositoryContext>>[];
    apis?: Type<PlatformApiService>[];
    repositoryErrorEventHandlers?: Type<PlatformRepositoryErrorEventHandler>[];
  }): Provider[] {
    return [
      ...(config.repositoryContext ? [config.repositoryContext] : []),
      ...(config.repositories ? config.repositories : []),
      ...(config.apis ? config.apis : []),

      ...(config.repositoryErrorEventHandlers ?? []),
      {
        provide: PlatformEventManagerSubscriptionsMap,
        useValue: new PlatformEventManagerSubscriptionsMap(
          config.repositoryErrorEventHandlers
            ? [[PlatformRepositoryErrorEvent, config.repositoryErrorEventHandlers]]
            : []
        ),
        multi: true
      }
    ];
  }
}
