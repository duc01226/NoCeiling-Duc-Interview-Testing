import { CommonModule } from '@angular/common';
import { ModuleWithProviders, NgModule, Provider, Type } from '@angular/core';
import { FormsModule, ReactiveFormsModule } from '@angular/forms';
import { BrowserModule } from '@angular/platform-browser';
import { BrowserAnimationsModule } from '@angular/platform-browser/animations';

import { PlatformApiService } from './api-services';
import { PlatformAppUiStateManager } from './app-ui-state-manager';
import {
  DefaultPlatformAuthHttpRequestOptionsAppenderService,
  PlatformAuthHttpRequestOptionsAppenderService,
} from './auth-services';
import {
  IPlatformEventManager,
  PlatformEvent,
  PlatformEventHandler,
  PlatformEventManager,
  PlatformEventManagerSubscriptionsMap,
} from './events';
import { Exts } from './extensions';
import { PlatformHighlightSearchTextPipe, PlatformPipe } from './pipes';
import { PlatformCoreModuleConfig } from './platform-core.config';

/* eslint-disable @typescript-eslint/no-unused-vars */
type ForRootModules = PlatformCoreModule | BrowserModule | BrowserAnimationsModule;
type ForChildModules = PlatformCoreModule;

@NgModule({
  declarations: [PlatformHighlightSearchTextPipe],
  exports: [CommonModule, FormsModule, ReactiveFormsModule, PlatformHighlightSearchTextPipe]
})
export class PlatformCoreModule {
  public static forRoot<TAppUiStateData>(config: {
    moduleConfig: {
      type: Type<PlatformCoreModuleConfig>;
      configFactory: () => PlatformCoreModuleConfig;
    };
    eventManager?: Type<IPlatformEventManager>;
    eventHandlerMaps?: [Type<PlatformEvent>, Type<PlatformEventHandler<PlatformEvent>>[]][];

    appRootUiState: Type<PlatformAppUiStateManager<TAppUiStateData>>;
    apiServices?: Type<PlatformApiService>[];
    authHttpRequestOptionsAppender?: Type<PlatformAuthHttpRequestOptionsAppenderService>;
  }): ModuleWithProviders<ForRootModules>[] {
    return [
      {
        ngModule: PlatformCoreModule,
        providers: [
          { provide: config.moduleConfig.type, useFactory: () => config.moduleConfig.configFactory() },
          { provide: PlatformCoreModuleConfig, useExisting: config.moduleConfig.type },

          {
            provide: PlatformEventManagerSubscriptionsMap,
            useValue: new PlatformEventManagerSubscriptionsMap(config.eventHandlerMaps ?? []),
            multi: true
          },
          // Register all eventHandlers from eventHandlerMaps
          ...(config.eventHandlerMaps != null
            ? Exts.List.selectMany(config.eventHandlerMaps, ([event, eventHandlers]) => eventHandlers)
            : []),
          PlatformEventManager,
          ...(config.eventManager != null ? [config.eventManager] : []),
          {
            provide: 'IPlatformEventManager',
            useExisting: config.eventManager ?? PlatformEventManager
          },

          ...this.buildCanBeInChildModuleProviders({
            apiServices: config.apiServices,
            authHttpRequestOptionsAppender: config.authHttpRequestOptionsAppender,
            moduleUiState: config.appRootUiState
          })
        ]
      },
      {
        ngModule: BrowserModule
      },
      {
        ngModule: BrowserAnimationsModule
      }
    ];
  }

  public static forChild<TAppUiStateData>(config: {
    appModuleState?: Type<PlatformAppUiStateManager<TAppUiStateData>>;
    apiServices?: Type<PlatformApiService>[];
    authHttpRequestOptionsAppender?: Type<PlatformAuthHttpRequestOptionsAppenderService>;
  }): ModuleWithProviders<ForChildModules>[] {
    return [
      {
        ngModule: PlatformCoreModule,
        providers: [
          ...this.buildCanBeInChildModuleProviders({
            apiServices: config.apiServices,
            authHttpRequestOptionsAppender: config.authHttpRequestOptionsAppender,
            moduleUiState: config.appModuleState
          })
        ]
      }
    ];
  }

  private static buildCanBeInChildModuleProviders<TAppUiStateData>(config: {
    moduleUiState?: Type<PlatformAppUiStateManager<TAppUiStateData>>;
    apiServices?: Type<PlatformApiService>[];
    authHttpRequestOptionsAppender?: Type<PlatformAuthHttpRequestOptionsAppenderService>;
    pipes?: Type<PlatformPipe<unknown, unknown, unknown>>[];
  }): Provider[] {
    return [
      DefaultPlatformAuthHttpRequestOptionsAppenderService,
      ...(config.authHttpRequestOptionsAppender != null ? [config.authHttpRequestOptionsAppender] : []),
      {
        provide: PlatformAuthHttpRequestOptionsAppenderService,
        useExisting: config.authHttpRequestOptionsAppender ?? DefaultPlatformAuthHttpRequestOptionsAppenderService
      },
      ...(config.apiServices ?? []),
      ...(config.moduleUiState != null ? [config.moduleUiState] : []),
      ...(config.pipes ?? [])
    ];
  }
}
