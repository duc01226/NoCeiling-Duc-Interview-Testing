import { Inject, Injectable, Injector, Type } from '@angular/core';

import { IPlatformEventManager, PlatformEvent, PlatformEventHandler } from './abstracts';

@Injectable()
export class PlatformEventManager implements IPlatformEventManager {
    public constructor(
        private injector: Injector,
        @Inject(PlatformEventManagerSubscriptionsMap)
        private subscriptionsMaps: PlatformEventManagerSubscriptionsMap[]
    ) {
        this.aggregatedSubscriptionsMap = this.buildAggregatedSubscriptionsMap();
    }

    private aggregatedSubscriptionsMap: PlatformEventManagerSubscriptionsMap;

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

@Injectable()
export class PlatformEventManagerSubscriptionsMap extends Map<
    Type<PlatformEvent>,
    Type<PlatformEventHandler<PlatformEvent>>[]
> {}
