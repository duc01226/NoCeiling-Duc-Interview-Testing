import { Inject, Injectable, Injector, Type } from '@angular/core';

import { IPlatformEventManager, PlatformEvent, PlatformEventHandler } from './abstracts';

/**
 * Platform Event Manager service for handling and publishing platform events.
 *
 * @remarks
 * This service is responsible for managing the publication and handling of platform events.
 * It aggregates event handlers from different modules and invokes them when an event is published.
 *
 * @example
 * ```typescript
 * // Inject and use the PlatformEventManager service
 * constructor(private platformEventManager: PlatformEventManager) {}
 *
 * // Publish a platform event
 * this.platformEventManager.publish(new MyPlatformEvent());
 * ```
 */
@Injectable({ providedIn: 'root' })
export class PlatformEventManager implements IPlatformEventManager {
    public constructor(
        private injector: Injector,
        @Inject(PlatformEventManagerSubscriptionsMap)
        private subscriptionsMaps: PlatformEventManagerSubscriptionsMap[]
    ) {
        this.aggregatedSubscriptionsMap = this.buildAggregatedSubscriptionsMap();
    }

    private aggregatedSubscriptionsMap: PlatformEventManagerSubscriptionsMap;

    /**
     * Publishes a platform event, invoking all associated event handlers.
     *
     * @param event - The platform event to be published.
     */
    public publish<TEvent extends PlatformEvent>(event: TEvent): void {
        const currentEventHandlerTypes =
            this.aggregatedSubscriptionsMap.get(<Type<PlatformEvent>>event.constructor) ?? [];
        currentEventHandlerTypes.forEach(currentEventHandlerType => {
            const currentEventHandlerInstance = this.injector.get(currentEventHandlerType);
            if (currentEventHandlerInstance == null) {
                throw new Error(`The event handler ${currentEventHandlerType.name} has not been registered.
          Please register it in providers.`);
            } else {
                currentEventHandlerInstance.handle(event);
            }
        });
    }

    private buildAggregatedSubscriptionsMap(): PlatformEventManagerSubscriptionsMap {
        const finalResult = new PlatformEventManagerSubscriptionsMap();

        this.subscriptionsMaps.forEach(subscriptionsMap => {
            subscriptionsMap.forEach((currentEventHandlerTypes, currentEventType) => {
                const existedEventTypeItemValues = finalResult.get(currentEventType);
                if (existedEventTypeItemValues != null) {
                    const combinedEventHandlerTypes = existedEventTypeItemValues.concat(currentEventHandlerTypes);
                    finalResult.set(currentEventType, combinedEventHandlerTypes);
                } else {
                    finalResult.set(currentEventType, currentEventHandlerTypes);
                }
            });
        });

        return finalResult;
    }
}

/**
 * Map structure that associates platform event types with arrays of associated event handler types.
 *
 * @remarks
 * This map is used by the `PlatformEventManager` to aggregate event handler types from different modules.
 *
 * @example
 * ```typescript
 * const subscriptionsMap = new PlatformEventManagerSubscriptionsMap();
 * subscriptionsMap.set(MyPlatformEvent, [MyPlatformEventHandler]);
 * ```
 */
@Injectable({ providedIn: 'root' })
export class PlatformEventManagerSubscriptionsMap extends Map<
    Type<PlatformEvent>,
    Type<PlatformEventHandler<PlatformEvent>>[]
> {}
