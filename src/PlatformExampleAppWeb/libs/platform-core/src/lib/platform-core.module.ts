import { CommonModule } from '@angular/common';
import { ErrorHandler, ModuleWithProviders, NgModule, Provider, Type } from '@angular/core';
import { FormsModule, ReactiveFormsModule } from '@angular/forms';
import { BrowserModule } from '@angular/platform-browser';
import { BrowserAnimationsModule } from '@angular/platform-browser/animations';
import { RouterModule } from '@angular/router';
import { TranslateModule, TranslateModuleConfig } from '@ngx-translate/core';
import { GlobalConfig, ToastrModule } from 'ngx-toastr';

import {
    DefaultPlatformHttpOptionsConfigService,
    PlatformApiErrorEvent,
    PlatformApiErrorEventHandler,
    PlatformApiService,
    PlatformHttpOptionsConfigService
} from './api-services';
import { PlatformCacheStorageCachingService, PlatformCachingService } from './caching';
import {
    IPlatformEventManager,
    PlatformEvent,
    PlatformEventHandler,
    PlatformEventManager,
    PlatformEventManagerSubscriptionsMap
} from './events';
import { PlatformHighlightSearchTextPipe, PlatformPipe } from './pipes';
import { PLATFORM_CORE_GLOBAL_ENV } from './platform-core-global-environment';
import { PlatformCoreModuleConfig } from './platform-core.config';
import { PlatformGlobalErrorHandler } from './platform-global-error-handler';
import { PlatformServiceWorkerService } from './platform-service-worker';
import { PlatformTranslateConfig } from './translations';
import { list_selectMany } from './utils';

/* eslint-disable @typescript-eslint/no-unused-vars */
type ForRootModules = PlatformCoreModule | BrowserModule | BrowserAnimationsModule;
type ForChildModules = PlatformCoreModule;

/**
 * Angular module for core platform features.
 *
 * @remarks
 * This module includes common Angular modules, services, and configurations required for the platform.
 *
 * @example
 * ```typescript
 * // Import the PlatformCoreModule in your Angular application.
 * import { PlatformCoreModule } from '@your-company/platform-core';
 *
 * @NgModule({
 *   imports: [PlatformCoreModule.forRoot({  configuration options  })],
 *   declarations: [your components, directives, and pipes ],
 *   bootstrap: [your main component ],
 * })
 * export class AppModule { }
 * ```
 *
 * @ngModule PlatformCoreModule
 * @exports PlatformCoreModule
 */
@NgModule({
    declarations: [PlatformHighlightSearchTextPipe],
    exports: [CommonModule, FormsModule, ReactiveFormsModule, PlatformHighlightSearchTextPipe]
})
export class PlatformCoreModule {
    /**
     * Creates and returns an Angular module with platform core features for the root module.
     *
     * @param config - Configuration options for the platform core module.
     * @returns An array of providers and configuration objects for the root module.
     *
     * @example
     * ```typescript
     * // Import the PlatformCoreModule in your Angular application.
     * import { PlatformCoreModule } from '@your-company/platform-core';
     *
     * @NgModule({
     *   imports: [PlatformCoreModule.forRoot({ configuration options })],
     *   declarations: [your components, directives, and pipes],
     *   bootstrap: [your main component],
     * })
     * export class AppModule { }
     * ```
     */
    public static forRoot<TModuleConfig extends PlatformCoreModuleConfig = PlatformCoreModuleConfig>(config: {
        moduleConfig?: {
            type: Type<TModuleConfig>;
            configFactory: () => TModuleConfig;
        };
        eventManager?: Type<IPlatformEventManager>;
        eventHandlerMaps?: [Type<PlatformEvent>, Type<PlatformEventHandler<PlatformEvent>>[]][];

        apiServices?: Type<PlatformApiService>[];
        httpOptionsConfigService?: Type<PlatformHttpOptionsConfigService>;
        translate?: { platformConfig?: PlatformTranslateConfig; config?: TranslateModuleConfig };
        toastConfig?: Partial<GlobalConfig>;
        cachingServiceFactory?: () => PlatformCachingService;
        appApiErrorEventHandlers?: Type<PlatformApiErrorEventHandler>[];
    }): ModuleWithProviders<ForRootModules>[] {
        return [
            {
                ngModule: PlatformCoreModule,
                providers: [
                    ...(config.moduleConfig != null
                        ? [
                              { provide: PlatformCoreModuleConfig, useExisting: config.moduleConfig.type },
                              {
                                  provide: config.moduleConfig.type,
                                  useFactory: () => config.moduleConfig?.configFactory()
                              }
                          ]
                        : [
                              {
                                  provide: PlatformCoreModuleConfig,
                                  useFactory: () => new PlatformCoreModuleConfig()
                              }
                          ]),

                    {
                        provide: PlatformEventManagerSubscriptionsMap,
                        useValue: new PlatformEventManagerSubscriptionsMap(config.eventHandlerMaps ?? []),
                        multi: true
                    },
                    // Register all eventHandlers from eventHandlerMaps
                    ...(config.eventHandlerMaps != null
                        ? list_selectMany(config.eventHandlerMaps, ([event, eventHandlers]) => eventHandlers)
                        : []),
                    {
                        provide: PlatformEventManager,
                        useClass: config.eventManager ?? PlatformEventManager
                    },

                    ...this.buildCanBeInChildModuleProviders({
                        apiServices: config.apiServices,
                        httpOptionsConfigService: config.httpOptionsConfigService
                    }),
                    {
                        provide: PlatformTranslateConfig,
                        useValue:
                            config.translate?.platformConfig != null
                                ? config.translate.platformConfig
                                : PlatformTranslateConfig.defaultConfig()
                    },
                    {
                        provide: PlatformCachingService,
                        useFactory: () =>
                            config.cachingServiceFactory != null
                                ? config.cachingServiceFactory()
                                : new PlatformCacheStorageCachingService()
                    },
                    PlatformServiceWorkerService,
                    { provide: ErrorHandler, useClass: PlatformGlobalErrorHandler },

                    ...(config.appApiErrorEventHandlers ?? []),
                    {
                        provide: PlatformEventManagerSubscriptionsMap,
                        useValue: new PlatformEventManagerSubscriptionsMap(
                            config.appApiErrorEventHandlers
                                ? [[PlatformApiErrorEvent, config.appApiErrorEventHandlers]]
                                : []
                        ),
                        multi: true
                    }
                ]
            },
            {
                ngModule: BrowserModule
            },
            {
                ngModule: BrowserAnimationsModule
            },
            {
                ngModule: RouterModule
            },
            TranslateModule.forRoot(config.translate?.config),
            ToastrModule.forRoot(
                config.toastConfig ?? {
                    newestOnTop: true,
                    positionClass: 'toast-bottom-right',
                    preventDuplicates: true,
                    enableHtml: true
                }
            )
        ];
    }

    /**
     * Creates and returns an Angular module with platform core features for child modules.
     *
     * @param config - Configuration options for the platform core module.
     * @returns An array of providers and configuration objects for child modules.
     *
     * @example
     * ```typescript
     * // Import the PlatformCoreModule in your Angular feature module.
     * import { PlatformCoreModule } from '@your-company/platform-core';
     *
     * @NgModule({
     *   imports: [PlatformCoreModule.forChild({ configuration options })],
     *   declarations: [ your feature components, directives, and pipes ],
     * })
     * export class FeatureModule { }
     * ```
     */
    public static forChild(config: {
        apiServices?: Type<PlatformApiService>[];
        httpOptionsConfigService?: Type<PlatformHttpOptionsConfigService>;
    }): ModuleWithProviders<ForChildModules>[] {
        return [
            {
                ngModule: PlatformCoreModule,
                providers: [
                    ...this.buildCanBeInChildModuleProviders({
                        apiServices: config.apiServices,
                        httpOptionsConfigService: config.httpOptionsConfigService
                    })
                ]
            }
        ];
    }

    /**
     * Builds an array of providers that can be used in child modules.
     *
     * @param config - Configuration options for the platform core module in child modules.
     * @returns An array of providers for child modules.
     *
     * @internal
     */
    private static buildCanBeInChildModuleProviders(config: {
        apiServices?: Type<PlatformApiService>[];
        httpOptionsConfigService?: Type<PlatformHttpOptionsConfigService>;
        pipes?: Type<PlatformPipe<unknown, unknown, unknown>>[];
    }): Provider[] {
        return [
            DefaultPlatformHttpOptionsConfigService,
            ...(config.httpOptionsConfigService != null ? [config.httpOptionsConfigService] : []),
            {
                provide: PlatformHttpOptionsConfigService,
                useExisting: config.httpOptionsConfigService ?? DefaultPlatformHttpOptionsConfigService
            },
            ...(config.apiServices ?? []),
            ...(config.pipes ?? [])
        ];
    }

    constructor(
        public moduleConfig: PlatformCoreModuleConfig,
        public cachingService: PlatformCachingService // (I)
    ) {
        PLATFORM_CORE_GLOBAL_ENV.isLocalDev = moduleConfig.isDevelopment;

        // (I) Inject PlatformCachingService to make it init asap
    }
}
