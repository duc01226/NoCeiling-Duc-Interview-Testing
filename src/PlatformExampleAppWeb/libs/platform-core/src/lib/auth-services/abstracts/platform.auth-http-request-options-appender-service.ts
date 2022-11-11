import { HttpClientOptions } from '../../http-services';

export abstract class PlatformAuthHttpRequestOptionsAppenderService {
  public abstract addAuthorization(options?: HttpClientOptions): HttpClientOptions;
}
