export interface IPlatformViewModel {}
export abstract class PlatformViewModel implements IPlatformViewModel {
  public constructor(data?: Partial<IPlatformViewModel>) {}
}
