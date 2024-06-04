import { HttpClient } from '@angular/common/http';
import {
    ApplicationConfig,
    LOCALE_ID,
    importProvidersFrom,
    provideExperimentalZonelessChangeDetection
} from '@angular/core';
import { BrowserAnimationsModule } from '@angular/platform-browser/animations';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { ServiceWorkerModule } from '@angular/service-worker';

import { PlatformCoreModule, PlatformCoreModuleConfig, PlatformTranslateConfig } from '@libs/platform-core';
import { TranslateLoader } from '@ngx-translate/core';
import { TranslateHttpLoader } from '@ngx-translate/http-loader';

import { AppsTextSnippetDomainModule, AppsTextSnippetDomainModuleConfig } from '@libs/apps-domains/text-snippet-domain';
import { environment } from '../environments/environment';
import { appRoutes } from './app.routes';
import { AppStore } from './app.store';
import { AppApiErrorEventHandler } from './events/app.api-error-event-handler';

export class AppModuleConfig extends PlatformCoreModuleConfig {}

export function TranslateHttpLoaderFactory(http: HttpClient) {
    return new TranslateHttpLoader(http, './assets/i18n/', '.json');
}

export const appConfig: ApplicationConfig = {
    providers: [
        provideExperimentalZonelessChangeDetection(),
        provideRouter(appRoutes, withComponentInputBinding()),
        { provide: LOCALE_ID, useValue: 'en-GB' },
        importProvidersFrom(
            BrowserAnimationsModule,

            ServiceWorkerModule.register('ngsw-worker.js', {
                enabled: environment.production
            }),
            PlatformCoreModule.forRoot({
                moduleConfig: {
                    type: AppModuleConfig,
                    configFactory: () => new AppModuleConfig({ isDevelopment: !environment.production })
                },
                translate: {
                    platformConfig: new PlatformTranslateConfig({ defaultLanguage: 'vi', slowRequestBreakpoint: 500 }),
                    config: {
                        loader: {
                            provide: TranslateLoader,
                            useFactory: TranslateHttpLoaderFactory,
                            deps: [HttpClient]
                        }
                    }
                },
                appApiErrorEventHandlers: [AppApiErrorEventHandler]
            }),
            AppsTextSnippetDomainModule.forRoot({
                moduleConfigFactory: () =>
                    new AppsTextSnippetDomainModuleConfig({ textSnippetApiHost: environment.textSnippetApiHost })
            })
        ),
        AppStore
    ]
};
