export const PLATFORM_CORE_GLOBAL_ENV: IPlatformCoreGlobalEnvironment = {
    isLocalDev: false,
    localDevApiDelayMilliseconds: () => (window.location.hostname == 'localhost' ? 1000 : 500)
};

export interface IPlatformCoreGlobalEnvironment {
    isLocalDev: boolean;
    localDevApiDelayMilliseconds: () => number;
}
