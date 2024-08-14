import { asyncScheduler, BehaviorSubject, concat, delay, Observable, of, tap } from 'rxjs';
import { TapObserver } from 'rxjs/internal/operators/tap';
import { distinctUntilObjectValuesChanged } from '../rxjs';

/**
 * Abstract class for platform caching service.
 *
 * @remarks
 * This class provides a base implementation for caching data in a platform.
 * It includes methods for getting, setting, deleting, and clearing cached data.
 *
 * @example
 * ```typescript
 * // Create a custom caching service by extending PlatformCachingService
 * class CustomCachingService extends PlatformCachingService {
 *   // Implement the abstract methods
 *   public get<T>(key: string, objectConstuctor?: (data?: Partial<T>) => T): T | undefined {
 *     // Custom implementation here
 *   }
 *
 *   public set<T>(key: string, value: T | undefined, options?: PlatformCachingServiceSetCacheOptions): void {
 *     // Custom implementation here
 *   }
 *
 *   public delete(key: string): void {
 *     // Custom implementation here
 *   }
 *
 *   public clear(): void {
 *     // Custom implementation here
 *   }
 * }
 * ```
 */
export abstract class PlatformCachingService {
    protected readonly options: PlatformCachingServiceOptions;

    constructor(options: PlatformCachingServiceOptions) {
        this.options = options;
    }

    public abstract get<T>(key: string, objectConstuctor?: (data?: Partial<T>) => T): T | undefined;

    public abstract set<T>(key: string, value: T | undefined, options?: PlatformCachingServiceSetCacheOptions): void;

    public abstract delete(key: string): void;

    public abstract clear(): void;

    public abstract loadCache(): Promise<void>;

    public cacheLoaded$: BehaviorSubject<boolean> = new BehaviorSubject(false);

    /**
     * Caches data with implicit reload request.
     *
     * @param requestCacheKey - The key associated with the cached data.
     * @param request - The function that returns an observable for the reload request.
     * @param options - Additional options for caching, such as time to live (TTL).
     * @param customSetCachedRequestDataFn - Custom function to handle setting cached data.
     * @returns An observable that emits the cached data and triggers a reload request.
     */
    public cacheImplicitReloadRequest<T>(
        requestCacheKey: string,
        request: () => Observable<T>,
        options?: PlatformCachingServiceSetCacheOptions,
        customSetCachedRequestDataFn?: (requestCacheKey: string, data: T | undefined) => unknown
    ): Observable<T> {
        const cachedData = this.get<T>(requestCacheKey);

        if (cachedData == null) {
            return request().pipe(
                tap(this.tapCacheDataObserver<T>(customSetCachedRequestDataFn, requestCacheKey, options))
            );
        } else {
            // delay(1ms) a little to mimic the real async rxjs observable => the next will be async => the flow is corrected if before call api
            // do update something in store
            return concat(
                of(cachedData).pipe(delay(1, asyncScheduler)),
                request().pipe(
                    tap(this.tapCacheDataObserver<T>(customSetCachedRequestDataFn, requestCacheKey, options))
                )
            ).pipe(distinctUntilObjectValuesChanged());
        }
    }

    private tapCacheDataObserver<T>(
        customSetCachedRequestDataFn: ((requestCacheKey: string, data: T | undefined) => unknown) | undefined,
        requestCacheKey: string,
        options: PlatformCachingServiceSetCacheOptions | undefined
    ): Partial<TapObserver<T>> {
        return {
            next: result => {
                if (customSetCachedRequestDataFn != null) customSetCachedRequestDataFn(requestCacheKey, result);
                else this.set(requestCacheKey, result, options);
            },
            error: err => {
                if (customSetCachedRequestDataFn != null) customSetCachedRequestDataFn(requestCacheKey, undefined);
                else this.delete(requestCacheKey);
            }
        };
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
