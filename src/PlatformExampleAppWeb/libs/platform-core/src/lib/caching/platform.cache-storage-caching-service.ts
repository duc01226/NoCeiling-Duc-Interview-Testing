import { task_debounce, toPlainObj } from '../utils';
import {
    PlatformCachingItem,
    PlatformCachingService,
    PlatformCachingServiceOptions,
    PlatformCachingServiceSetCacheOptions,
    DefaultPlatformCachingServiceOptions as defaultPlatformCachingServiceOptions
} from './platform.caching-service';

/**
 * CacheStorage caching service implementation.
 *
 * @remarks
 * This class extends the {@link PlatformCachingService} abstract class and provides a caching service
 * that utilizes the browser's CacheStorage for storing cached data.
 *
 * @example
 * ```typescript
 * // Create an instance of PlatformCacheStorageCachingService
 * const cacheStorageCacheService = new PlatformCacheStorageCachingService();
 *
 * // Use caching methods such as get, set, delete, etc.
 * const cachedData = cacheStorageCacheService.get<MyData>('myDataCacheKey');
 * ```
 */
export class PlatformCacheStorageCachingService extends PlatformCachingService {
    protected cacheKeyPrefix: string = '__PlatformCacheStorageCaching__';
    protected cacheName: string = 'platform-cache-storage';
    protected cache: Map<string, PlatformCachingItem> = new Map();

    constructor(options?: PlatformCachingServiceOptions) {
        super(options ?? defaultPlatformCachingServiceOptions());
        this.loadCache();
    }

    /**
     * Loads cached data from CacheStorage.
     */
    public override async loadCache() {
        const cache = await caches.open(this.cacheName);
        const keys = await cache.keys();

        const cacheMap = new Map();
        for (const request of keys) {
            const response = await cache.match(request);
            if (response) {
                const cacheDataItem = await response.json();
                cacheMap.set(request.url, cacheDataItem);
            }
        }

        this.cache = cacheMap;
        this.removeExpiredItems();

        this.cacheLoaded$.next(true);
    }

    /**
     * Removes expired items from the cache.
     */
    private async removeExpiredItems() {
        const cache = await caches.open(this.cacheName);
        for (const [key, value] of this.cache.entries()) {
            if (this.isItemExpired(value)) {
                this.cache.delete(key);
                await cache.delete(new Request(key));
            }
        }
    }

    /**
     * Saves the cache to CacheStorage.
     *
     * @param debounceSaveCache - Determines whether to debounce saving the cache.
     */
    private async saveCache(debounceSaveCache?: boolean) {
        if (debounceSaveCache == false) await this.doSaveCache();
        else await this.doSaveCacheDebounce();
    }

    private doSaveCache = async () => {
        try {
            const cache = await caches.open(this.cacheName);
            const keys = await cache.keys();
            for (const request of keys) {
                await cache.delete(request);
            }
            for (const [key, cacheItem] of this.cache.entries()) {
                await cache.put(new Request(key), new Response(JSON.stringify(toPlainObj(cacheItem))));
            }
        } catch (error) {
            console.warn('CacheStorage is full, Please empty data', error);
            await this.clear();
        }
    };

    private doSaveCacheDebounce = task_debounce(() => this.doSaveCache(), this.options.defaultDebounceSaveCacheMs);

    /**
     * Checks if a cached item is expired.
     *
     * @param item - The cached item to check.
     * @returns True if the item is expired, otherwise false.
     */
    public isItemExpired(item: PlatformCachingItem) {
        const ttl = item.ttl ?? this.options.ttl;
        return Date.now() - item.timestamp >= ttl * 1000;
    }

    /**
     * Gets cached data for a given key.
     *
     * @param key - The key for which to retrieve the cached data.
     * @param objectConstuctor - Optional constructor function to create an object from the cached data.
     * @returns The cached data or undefined if not found.
     */
    public override get<T>(key: string, objectConstuctor?: (data?: Partial<T>) => T): T | undefined {
        try {
            const cachedItem = this.cache.get(this.buildFinalCacheKey(key));

            if (cachedItem != null) {
                if (this.isItemExpired(cachedItem)) {
                    this.delete(key);
                    return undefined;
                }
                const data = cachedItem.data != null ? JSON.parse(<string>cachedItem.data) : null;

                return objectConstuctor != null ? objectConstuctor(data) : data;
            }
            return undefined;
        } catch (error) {
            console.error(error);
            this.clear();
            return undefined;
        }
    }

    /**
     * Sets cached data for a given key.
     *
     * @param key - The key for which to set the cached data.
     * @param data - The data to be cached.
     * @param options - Additional options for caching, such as time to live (TTL).
     */
    public override set<T>(key: string, data: T | undefined, options?: PlatformCachingServiceSetCacheOptions): void {
        if (data == undefined) this.delete(key);
        else this.doSetData<T>(data, options, key);
    }

    private doSetData<T>(
        data: NonNullable<T>,
        options: PlatformCachingServiceSetCacheOptions | undefined,
        key: string
    ) {
        const serializedData = JSON.stringify(toPlainObj(data));
        const debounceSaveCache =
            options?.debounceSaveCache != undefined
                ? options?.debounceSaveCache
                : this.options.debounceSaveCache ?? true;

        const newItem: PlatformCachingItem = {
            data: serializedData,
            timestamp: Date.now(),
            ttl: options?.ttl
        };

        // If cache is full, delete the oldest item
        if (this.cache.size >= this.options.maxSize) {
            const oldestKey = this.findOldestKey();
            if (oldestKey != null) this.cache.delete(oldestKey);
        }

        this.cache.set(this.buildFinalCacheKey(key), newItem);
        this.saveCache(debounceSaveCache);
    }

    private buildFinalCacheKey(key: string): string {
        return `${location.origin}/${encodeURI(this.cacheKeyPrefix + key)}`;
    }

    public override delete(key: string): void {
        this.cache.delete(this.buildFinalCacheKey(key));
        this.saveCache();
    }

    public async clear(): Promise<void> {
        try {
            this.cache.clear();
            const cache = await caches.open(this.cacheName);
            const keys = await cache.keys();
            for (const request of keys) {
                await cache.delete(request);
            }
        } catch (error) {
            console.error(error);
        }
    }

    protected findOldestKey() {
        let oldestKey = null;
        let oldestTimestamp = Infinity;

        for (const [key, value] of this.cache.entries()) {
            if (value.timestamp < oldestTimestamp) {
                oldestKey = key;
                oldestTimestamp = value.timestamp;
            }
        }

        return oldestKey;
    }
}
