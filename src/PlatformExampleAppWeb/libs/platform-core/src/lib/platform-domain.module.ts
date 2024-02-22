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

/**
 * Angular module for platform domain-related features.
 *
 * @remarks
 * This module includes common Angular modules, services, and configurations required for the platform domain.
 * It provides functionality related to repositories, APIs, and repository error handling.
 *
 * @example
 * ```typescript
 * // Import the PlatformDomainModule in your Angular application.
 * import { PlatformDomainModule } from '@your-company/platform-domain';
 *
 * @NgModule({
 *   imports: [PlatformDomainModule.forRoot({ configuration options })],
 *   declarations: [ your components, directives, and pipes ],
 *   bootstrap: [ your main component ],
 * })
 * export class AppModule { }
 * ```
 *
 * @ngModule PlatformDomainModule
 * @exports PlatformDomainModule
 */
@NgModule({
    imports: [],
    exports: [CommonModule, FormsModule, ReactiveFormsModule, HttpClientModule]
})
export class PlatformDomainModule {
    /**
     * Creates and returns an Angular module with platform domain features for the root module.
     *
     * @param config - Configuration options for the platform domain module.
     * @returns An array of providers and configuration objects for the root module.
     *
     * @example
     * ```typescript
     * // Import the PlatformDomainModule in your Angular application.
     * import { PlatformDomainModule } from '@your-company/platform-domain';
     *
     * @NgModule({
     *   imports: [PlatformDomainModule.forRoot({ configuration options })],
     *   declarations: [ your components, directives, and pipes ],
     *   bootstrap: [ your main component ],
     * })
     * export class AppModule { }
     * ```
     */
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

    /**
     * Creates and returns an Angular module with platform domain features for child modules.
     *
     * @param config - Configuration options for the platform domain module.
     * @returns An array of providers and configuration objects for child modules.
     *
     * @example
     * ```typescript
     * // Import the PlatformDomainModule in your Angular feature module.
     * import { PlatformDomainModule } from '@your-company/platform-domain';
     *
     * @NgModule({
     *   imports: [PlatformDomainModule.forChild({  configuration options  })],
     *   declarations: [ your feature components, directives, and pipes ],
     * })
     * export class FeatureModule { }
     * ```
     */
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

    /**
     * Builds an array of providers related to repositories, APIs, and repository error handling.
     *
     * @param config - Configuration options for the platform domain module.
     * @returns An array of providers for the platform domain module.
     *
     * @internal
     */
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
