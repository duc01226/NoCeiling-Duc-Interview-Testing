import { HttpClientOptions } from '../http-services';
import { PlatformAuthHttpRequestOptionsAppenderService } from './abstracts';

export class DefaultPlatformAuthHttpRequestOptionsAppenderService extends PlatformAuthHttpRequestOptionsAppenderService {
  public addAuthorization(options?: HttpClientOptions): HttpClientOptions {
    return options ?? {};
  }
}
