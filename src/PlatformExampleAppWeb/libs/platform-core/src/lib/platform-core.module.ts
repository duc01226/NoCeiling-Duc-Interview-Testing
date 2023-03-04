import { CommonModule } from '@angular/common';
import { ModuleWithProviders, NgModule, Provider, Type } from '@angular/core';
import { FormsModule, ReactiveFormsModule } from '@angular/forms';
import { BrowserModule } from '@angular/platform-browser';
import { BrowserAnimationsModule } from '@angular/platform-browser/animations';
import { TranslateModule, TranslateModuleConfig } from '@ngx-translate/core';
import { GlobalConfig, ToastrModule } from 'ngx-toastr';

import {
  DefaultPlatformHttpOptionsConfigService,
  PlatformApiService,
  PlatformHttpOptionsConfigService
} from './api-services';
import { PlatformAppUiStateData, PlatformAppUiStateStore } from './app-ui-state';
import {
  IPlatformEventManager,
  PlatformEvent,
  PlatformEventHandler,
  PlatformEventManager,
  PlatformEventManagerSubscriptionsMap
} from './events';
import { PlatformHighlightSearchTextPipe, PlatformPipe } from './pipes';
import { PlatformCoreModuleConfig } from './platform-core.config';
import { PlatformTranslateConfig } from './translations';
import { list_selectMany } from './utils';

/* eslint-disable @typescript-eslint/no-unused-vars */
type ForRootModules = PlatformCoreModule | BrowserModule | BrowserAnimationsModule;
type ForChildModules = PlatformCoreModule;

@NgModule({
  declarations: [PlatformHighlightSearchTextPipe],
  exports: [CommonModule, FormsModule, ReactiveFormsModule, PlatformHighlightSearchTextPipe]
})
export class PlatformCoreModule {
  public static forRoot<TAppUiStateData extends PlatformAppUiStateData>(config: {
    moduleConfig?: {
      type: Type<PlatformCoreModuleConfig>;
      configFactory: () => PlatformCoreModuleConfig;
    };
    eventManager?: Type<IPlatformEventManager>;
    eventHandlerMaps?: [Type<PlatformEvent>, Type<PlatformEventHandler<PlatformEvent>>[]][];

    appRootUiState?: Type<PlatformAppUiStateStore<TAppUiStateData>>;
    apiServices?: Type<PlatformApiService>[];
    httpOptionsConfigService?: Type<PlatformHttpOptionsConfigService>;
    translate?: { platformConfig?: PlatformTranslateConfig; config?: TranslateModuleConfig };
    toastConfig?: Partial<GlobalConfig>;
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
          PlatformEventManager,
          ...(config.eventManager != null ? [config.eventManager] : []),
          {
            provide: 'IPlatformEventManager',
            useExisting: config.eventManager ?? PlatformEventManager
          },

          ...this.buildCanBeInChildModuleProviders({
            apiServices: config.apiServices,
            httpOptionsConfigService: config.httpOptionsConfigService,
            moduleUiState: config.appRootUiState
          }),
          {
            provide: PlatformTranslateConfig,
            useValue:
              config.translate?.platformConfig != null
                ? config.translate.platformConfig
                : PlatformTranslateConfig.defaultConfig()
          }
        ]
      },
      {
        ngModule: BrowserModule
      },
      {
        ngModule: BrowserAnimationsModule
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

  public static forChild<TAppUiStateData extends PlatformAppUiStateData>(config: {
    appModuleState?: Type<PlatformAppUiStateStore<TAppUiStateData>>;
    apiServices?: Type<PlatformApiService>[];
    httpOptionsConfigService?: Type<PlatformHttpOptionsConfigService>;
  }): ModuleWithProviders<ForChildModules>[] {
    return [
      {
        ngModule: PlatformCoreModule,
        providers: [
          ...this.buildCanBeInChildModuleProviders({
            apiServices: config.apiServices,
            httpOptionsConfigService: config.httpOptionsConfigService,
            moduleUiState: config.appModuleState
          })
        ]
      }
    ];
  }

  private static buildCanBeInChildModuleProviders<TAppUiStateData extends PlatformAppUiStateData>(config: {
    moduleUiState?: Type<PlatformAppUiStateStore<TAppUiStateData>>;
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
      ...(config.moduleUiState != null ? [config.moduleUiState] : []),
      ...(config.pipes ?? [])
    ];
  }
}
