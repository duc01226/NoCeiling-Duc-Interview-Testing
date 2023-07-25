import { concat, delay, Observable, of, tap } from 'rxjs';

import { distinctUntilObjectValuesChanged } from '../rxjs';

export abstract class PlatformCachingService {
    protected readonly options: PlatformCachingServiceOptions;

    constructor(options: PlatformCachingServiceOptions) {
        this.options = options;
    }

    public abstract get<T>(key: string, objectConstuctor?: (data?: Partial<T>) => T): T | undefined;

    public abstract set<T>(key: string, value: T | undefined, options?: PlatformCachingServiceSetCacheOptions): void;

    public abstract delete(key: string): void;

    public abstract clear(): void;

    public cacheImplicitReloadRequest<T>(
        requestCacheKey: string,
        request: () => Observable<T>,
        options?: PlatformCachingServiceSetCacheOptions,
        customSetCachedRequestDataFn?: (requestCacheKey: string, data: T | undefined) => unknown
    ): Observable<T> {
        const cachedData = this.get<T>(requestCacheKey);

        if (cachedData == null) {
            return request();
        } else {
            // delay(10ms) a little to mimic the real async rxjs observable => the next will be async => the flow is corrected if before call api
            // do update something in store
            return concat(
                of(cachedData).pipe(delay(10)),
                request().pipe(
                    tap({
                        next: result => {
                            if (customSetCachedRequestDataFn != null)
                                customSetCachedRequestDataFn(requestCacheKey, result);
                            else this.set(requestCacheKey, result, options);
                        },
                        error: err => {
                            if (customSetCachedRequestDataFn != null)
                                customSetCachedRequestDataFn(requestCacheKey, undefined);
                            else this.delete(requestCacheKey);
                        }
                    })
                )
            ).pipe(distinctUntilObjectValuesChanged());
        }
    }
}

export interface PlatformCachingServiceSetCacheOptions {
    /** Time to leave of a cache item in seconds */
    ttl: number;

    /** Determine the cache will be saved immediately or debounced for performance */
    debounceSaveCache?: boolean;
}

export interface PlatformCachingServiceOptions extends PlatformCachingServiceSetCacheOptions {
    /** Max number of cached items */
    maxSize: number;

    /** Determine the cache will be saved immediately or debounced in Ms for performance */
    defaultDebounceSaveCacheMs: number;
}

export function DefaultPlatformCachingServiceOptions(): PlatformCachingServiceOptions {
    return { ttl: 3600 * 48, maxSize: 500, defaultDebounceSaveCacheMs: 500, debounceSaveCache: true };
}

export interface PlatformCachingItem {
    data: unknown;
    /** Like Date.Now() => Returns the number of milliseconds elapsed since midnight, January 1, 1970 Universal Coordinated Time (UTC). */
    timestamp: number;

    /** Individual time to live of the cache item */
    ttl?: number;
}
