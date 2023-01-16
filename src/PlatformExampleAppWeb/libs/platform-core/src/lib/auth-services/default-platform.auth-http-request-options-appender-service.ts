import { HttpClientOptions } from '../http-services';
import { PlatformAuthHttpRequestOptionsAppenderService } from './abstracts/platform.auth-http-request-options-appender-service';

export class DefaultPlatformAuthHttpRequestOptionsAppenderService extends PlatformAuthHttpRequestOptionsAppenderService {
  public addAuthorization(options?: HttpClientOptions): HttpClientOptions {
    return options ?? {};
  }
}
