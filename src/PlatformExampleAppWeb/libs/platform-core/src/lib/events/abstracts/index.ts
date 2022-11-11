export interface IPlatformEventManager {
  publish<TEvent extends PlatformEvent>(event: TEvent): void;
}

export abstract class PlatformEvent {
  public constructor(public name: string) {}
}

export abstract class PlatformEventHandler<TEvent extends PlatformEvent> {
  public abstract handle(event: TEvent): void;
}
