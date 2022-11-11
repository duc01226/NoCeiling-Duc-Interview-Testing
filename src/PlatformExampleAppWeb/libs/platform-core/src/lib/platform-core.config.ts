export interface IPlatformCoreModuleConfig {
  isDevelopment: boolean;
  httpRequestTimeoutInSeconds: number;
  multiThemeConfig: IPlatformMultiThemeModuleConfig;
  maxCacheRequestDataPerApiRequestName: number;
}

export class PlatformCoreModuleConfig implements IPlatformCoreModuleConfig {
  public static readonly defaultMaxCacheRequestDataPerApiRequestName: number = 1;

  public constructor(data?: Partial<IPlatformCoreModuleConfig>) {
    this.isDevelopment = data?.isDevelopment ?? true;
    this.httpRequestTimeoutInSeconds = data?.httpRequestTimeoutInSeconds ?? 60;
    this.multiThemeConfig = data?.multiThemeConfig
      ? new PlatformMultiThemeModuleConfig(data.multiThemeConfig)
      : new PlatformMultiThemeModuleConfig();
    this.maxCacheRequestDataPerApiRequestName =
      data?.maxCacheRequestDataPerApiRequestName ??
      PlatformCoreModuleConfig.defaultMaxCacheRequestDataPerApiRequestName;
  }

  public isDevelopment: boolean;
  public httpRequestTimeoutInSeconds: number;
  public multiThemeConfig: PlatformMultiThemeModuleConfig;
  public maxCacheRequestDataPerApiRequestName: number;
}

export interface IPlatformMultiThemeModuleConfig {
  isActivated: boolean;
  defaultThemeName: string;
  themeQueryParamName: string;
}

export class PlatformMultiThemeModuleConfig implements IPlatformMultiThemeModuleConfig {
  public constructor(data?: Partial<IPlatformMultiThemeModuleConfig>) {
    this.isActivated = data?.isActivated ?? false;
    this.defaultThemeName = data?.defaultThemeName ?? 'default-theme';
    this.themeQueryParamName = data?.themeQueryParamName ?? 'theme';
  }

  public isActivated: boolean;
  public defaultThemeName: string;
  public themeQueryParamName: string;
}
