import { HttpClientOptions } from '../../http-services';

export abstract class PlatformHttpOptionsConfigService {
  public abstract configOptions(options?: HttpClientOptions): HttpClientOptions;
}

export class DefaultPlatformHttpOptionsConfigService extends PlatformHttpOptionsConfigService {
  public configOptions(options?: HttpClientOptions | undefined): HttpClientOptions {
    return options ?? {};
  }
}
