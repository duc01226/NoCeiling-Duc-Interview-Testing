import { enableProdMode } from '@angular/core';
import { bootstrapApplication } from '@angular/platform-browser';

import { environment } from './environments/environment';

if (environment.production) {
    enableProdMode();
}

Promise.all([import('./app/app.component'), import('./app/app.config')])
    .then(([appComponentModule, appConfigModule]) =>
        bootstrapApplication(appComponentModule.AppComponent, appConfigModule.appConfig)
    )
    .catch((err: Error) => console.error(err));
