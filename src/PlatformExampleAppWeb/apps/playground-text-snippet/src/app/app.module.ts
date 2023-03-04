import { HttpClient } from '@angular/common/http';
import { NgModule, NgZone } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialogModule } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule } from '@angular/material/paginator';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTableModule } from '@angular/material/table';
import { BrowserModule } from '@angular/platform-browser';
import { BrowserAnimationsModule } from '@angular/platform-browser/animations';
import { AppsTextSnippetDomainModule, AppsTextSnippetDomainModuleConfig } from '@libs/apps-domains/text-snippet-domain';
import { PlatformCoreModule, PlatformTranslateConfig } from '@libs/platform-core';
import { TranslateLoader } from '@ngx-translate/core';
import { TranslateHttpLoader } from '@ngx-translate/http-loader';

import { environment } from '../environments/environment';
import { AppComponent } from './app.component';
import { AppModuleConfig } from './app.module.config';
import { AppUiStateStore } from './app-ui-state';
import { RepositoryErrorEventHandler } from './events';
import { AppTextSnippetDetailComponent } from './smart-components';

export function TranslateHttpLoaderFactory(http: HttpClient) {
  return new TranslateHttpLoader(http, './assets/i18n/', '.json');
}

@NgModule({
  declarations: [AppComponent, AppTextSnippetDetailComponent],
  imports: [
    BrowserModule,
    BrowserAnimationsModule,
    PlatformCoreModule.forRoot({
      moduleConfig: {
        type: AppModuleConfig,
        configFactory: () => new AppModuleConfig({ isDevelopment: !environment.production })
      },
      appRootUiState: AppUiStateStore,
      translate: {
        platformConfig: new PlatformTranslateConfig({ defaultLanguage: 'vi', slowRequestBreakpoint: 500 }),
        config: {
          loader: {
            provide: TranslateLoader,
            useFactory: TranslateHttpLoaderFactory,
            deps: [HttpClient]
          }
        }
      }
    }),
    AppsTextSnippetDomainModule.forRoot({
      moduleConfigFactory: () =>
        new AppsTextSnippetDomainModuleConfig({ textSnippetApiHost: environment.textSnippetApiHost }),
      appRepositoryErrorEventHandlers: [RepositoryErrorEventHandler]
    }),
    MatTableModule,
    MatInputModule,
    MatPaginatorModule,
    MatProgressSpinnerModule,
    MatButtonModule,
    MatIconModule,
    MatDialogModule
  ],
  bootstrap: [AppComponent],
  providers: [
    {
      provide: NgZone,
      // Performance checklist: https://github.com/mgechev/angular-performance-checklist#coalescing-event-change-detections
      useValue: new NgZone({ shouldCoalesceEventChangeDetection: false, shouldCoalesceRunChangeDetection: true })
    }
  ]
})
export class AppModule {}
