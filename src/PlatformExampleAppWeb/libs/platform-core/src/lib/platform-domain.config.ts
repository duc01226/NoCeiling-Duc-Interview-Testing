export interface IPlatformDomainModuleConfig {
  isDevelopment: boolean;
}

export class PlatformDomainModuleConfig implements IPlatformDomainModuleConfig {
  public constructor(data?: Partial<IPlatformDomainModuleConfig>) {
    this.isDevelopment = data?.isDevelopment ?? true;
  }

  public isDevelopment: boolean;
}
