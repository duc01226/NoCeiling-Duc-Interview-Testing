import { PlatformEventHandler } from '../../../events';
import { PlatformRepositoryErrorEvent } from '../repository-error.event';

export abstract class PlatformRepositoryErrorEventHandler extends PlatformEventHandler<PlatformRepositoryErrorEvent> {
    public abstract override handle(PlatformRepositoryErrorEvent: PlatformRepositoryErrorEvent): void;
}
