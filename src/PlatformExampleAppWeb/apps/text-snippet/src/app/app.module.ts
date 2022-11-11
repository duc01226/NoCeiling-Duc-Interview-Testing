import { NgModule } from '@angular/core';
import { FlexLayoutModule } from '@angular/flex-layout';
import { MatButtonModule } from '@angular/material/button';
import { MatDialogModule } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule } from '@angular/material/paginator';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTableModule } from '@angular/material/table';
import { BrowserModule } from '@angular/platform-browser';
import { BrowserAnimationsModule } from '@angular/platform-browser/animations';
import {
  AppsTextSnippetDomainModule,
  AppsTextSnippetDomainModuleConfig,
} from '@platform-example-web/apps-domains/text-snippet-domain';
import { PlatformCoreModule } from '@platform-example-web/platform-core';

import { environment } from '../environments/environment';
import { AppUiStateManager } from './app-ui-state-manager';
import { AppComponent } from './app.component';
import { AppModuleConfig } from './app.module.config';
import { RepositoryErrorEventHandler } from './events';
import { AppTextSnippetDetailComponent } from './smart-components';

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
      appRootUiState: AppUiStateManager
    }),
    AppsTextSnippetDomainModule.forRoot({
      moduleConfigFactory: () =>
        new AppsTextSnippetDomainModuleConfig({
          isDevelopment: !environment.production,
          textSnippetApiHost: environment.textSnippetApiHost
        }),
      appRepositoryErrorEventHandlers: [RepositoryErrorEventHandler]
    }),

    MatTableModule,
    MatInputModule,
    MatPaginatorModule,
    MatProgressSpinnerModule,
    MatButtonModule,
    MatIconModule,
    MatDialogModule,

    FlexLayoutModule
  ],
  providers: [],
  bootstrap: [AppComponent]
})
export class AppModule {}
