import { inject, Injectable, NgZone } from '@angular/core';
import { SwUpdate } from '@angular/service-worker';
import { TranslateService } from '@ngx-translate/core';
import { map, Observable, of, tap } from 'rxjs';

import { PlatformCachingService } from './caching';
import { date_timeDiff } from './utils';

@Injectable()
export class PlatformServiceWorkerService {
    public static maxWaitingForAutoReloadNewVersionSeconds = 10;
    public static checkNewVersionInterval = 60000;

    private readonly translateSvc = inject(TranslateService);

    constructor(private swUpdate: SwUpdate, private cacheService: PlatformCachingService, private ngZone: NgZone) {}

    public reloadConfirmed: boolean = false;
    public showingReloadDialog: boolean = false;
    public startCheckNewAppVersionTime: Date = new Date();

    protected getTranslate(key: string): Observable<string> {
        if (this.translateSvc == null) return of(key);
        return this.translateSvc.get(key);
    }

    public setupCheckNewAppVersionProcess(
        onNewAppVersionAvailableFn?: () => unknown,
        handleNewVersionMsgFn?: (msg: string) => void
    ) {
        if (!this.swUpdate.isEnabled) return;

        this.swUpdate.versionUpdates
            .pipe(
                map(e => {
                    if (e.type != 'VERSION_READY') return;

                    this.ngZone.run(() => {
                        console.log('New app version available.');

                        this.clearCache();

                        if (onNewAppVersionAvailableFn != undefined) onNewAppVersionAvailableFn();
                        if (
                            date_timeDiff(new Date(), this.startCheckNewAppVersionTime) / 1000 <=
                            PlatformServiceWorkerService.maxWaitingForAutoReloadNewVersionSeconds
                        ) {
                            console.log('Auto reload to get new version.');
                            this.reloadPageWithNewVersion();
                        } else {
                            this.swUpdate.activateUpdate().then(() => {
                                this.getTranslate('New version available. Load New Version?').subscribe(
                                    (msg: string) => {
                                        if (handleNewVersionMsgFn != undefined) {
                                            handleNewVersionMsgFn(msg);
                                        } else {
                                            this.showReloadNewVersionAlert(msg);
                                        }
                                    }
                                );
                            });
                        }
                    });
                })
            )
            .subscribe();

        this.swUpdate.unrecoverable
            .pipe(
                tap(e => {
                    this.getTranslate('This current version is broken. Reload New Version?').subscribe(
                        (msg: string) => {
                            if (confirm(msg)) {
                                this.clearCacheReload(this.allNgswCacheKeysRegex);
                            }
                        }
                    );
                })
            )
            .subscribe();

        this.startCheckAppVersionInterval();
    }

    public startCheckAppVersionInterval() {
        this.ngZone.runOutsideAngular(() => {
            this.startCheckNewAppVersionTime = new Date();
            this.swUpdate.checkForUpdate();
            if (PlatformServiceWorkerService.checkNewVersionInterval > 0) {
                console.log('Start check app version interval.');
                setInterval(() => {
                    this.swUpdate.checkForUpdate();
                }, PlatformServiceWorkerService.checkNewVersionInterval);
            }
        });
    }

    public showReloadNewVersionAlert(msg: string) {
        if (!this.reloadConfirmed && !this.showingReloadDialog) {
            this.showingReloadDialog = true;
            if (confirm(msg)) {
                this.reloadConfirmed = true;
                this.reloadPageWithNewVersion();
            } else {
                this.reloadConfirmed = false;
                this.showingReloadDialog = false;
            }
        }
    }

    public reloadPageWithNewVersion() {
        this.swUpdate.activateUpdate().then(
            () => {
                this.clearCacheReload();
            },
            err => {
                this.clearCacheReload();
            }
        );
    }

    public clearCacheReload(cacheKeysRegex?: RegExp) {
        this.clearCache(cacheKeysRegex).then(
            () => window.location.reload(),
            () => window.location.reload()
        );
    }

    public clearCache(cacheKeysRegex?: RegExp) {
        this.cacheService.clear();
        return PlatformServiceWorkerService.clearNgswCacheStorage(cacheKeysRegex ?? this.apiDataCacheKeysRegex);
    }

    public allNgswCacheKeysRegex = /^ngsw:.*/;
    public apiDataCacheKeysRegex = /^ngsw:.*:data:/;
    public static async clearNgswCacheStorage(cacheKeysRegex: RegExp, name?: string): Promise<boolean[]> {
        const allCacheKeys = await window.caches.keys();
        const ngswCacheKeys = allCacheKeys.filter(
            p => cacheKeysRegex.test(p) && (name == undefined || p.indexOf(name) > -1)
        );
        // Cast any because of angular cli bug build prod failed
        return Promise.all(
            ngswCacheKeys.map(async p => {
                const cache = await window.caches.open(p);
                const cacheKeys = await cache.keys();
                const deleteResults = await Promise.all(cacheKeys.map(cacheKey => cache.delete(cacheKey)));
                return deleteResults.indexOf(false) < 0 && deleteResults.length == cacheKeys.length;
            })
        );
    }
}
