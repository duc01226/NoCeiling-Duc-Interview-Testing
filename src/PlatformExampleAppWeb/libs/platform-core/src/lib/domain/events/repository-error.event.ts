import { PlatformApiServiceErrorResponse } from '../../api-services';
import { PlatformEvent } from '../../events';

export class PlatformRepositoryErrorEvent extends PlatformEvent {
  public constructor(
    public repositoryRequestName: string,
    public repositoryRequestPayload: unknown,
    public apiError: PlatformApiServiceErrorResponse
  ) {
    super(repositoryRequestName);
  }
}
