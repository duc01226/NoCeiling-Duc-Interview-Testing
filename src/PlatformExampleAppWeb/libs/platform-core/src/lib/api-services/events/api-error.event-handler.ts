import { PlatformEventHandler } from '../../events';
import { PlatformApiErrorEvent } from './api-error.event';

export abstract class PlatformApiErrorEventHandler extends PlatformEventHandler<PlatformApiErrorEvent> {
    public abstract override handle(event: PlatformApiErrorEvent): void;
}
