export interface IPlatformCoreModuleConfig {
    isDevelopment: boolean;
    httpRequestTimeoutInSeconds: number;
    multiThemeConfig: IPlatformMultiThemeModuleConfig;
    maxCacheRequestDataPerApiRequestName: number;
}

export class PlatformCoreModuleConfig implements IPlatformCoreModuleConfig {
    public DEFAULT_TIMEOUT_SECONDS = 3600 * 24;
    public static readonly defaultMaxCacheRequestDataPerApiRequestName: number = 1;

    public constructor(data?: Partial<IPlatformCoreModuleConfig>) {
        this.isDevelopment = data?.isDevelopment ?? false;
        this.httpRequestTimeoutInSeconds = data?.httpRequestTimeoutInSeconds ?? this.DEFAULT_TIMEOUT_SECONDS;
        this.multiThemeConfig = data?.multiThemeConfig
            ? new PlatformMultiThemeModuleConfig(data.multiThemeConfig)
            : new PlatformMultiThemeModuleConfig();
        this.maxCacheRequestDataPerApiRequestName =
            data?.maxCacheRequestDataPerApiRequestName ??
            PlatformCoreModuleConfig.defaultMaxCacheRequestDataPerApiRequestName;
    }

    public isDevelopment: boolean = false;
    public httpRequestTimeoutInSeconds: number = this.DEFAULT_TIMEOUT_SECONDS;
    public multiThemeConfig: PlatformMultiThemeModuleConfig = new PlatformMultiThemeModuleConfig();
    public maxCacheRequestDataPerApiRequestName: number =
        PlatformCoreModuleConfig.defaultMaxCacheRequestDataPerApiRequestName;
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
