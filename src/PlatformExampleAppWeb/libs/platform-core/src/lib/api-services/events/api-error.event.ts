import { PlatformEvent } from '../../events';
import { PlatformApiServiceErrorResponse } from '../abstracts/platform.api-error';

export class PlatformApiErrorEvent extends PlatformEvent {
    public constructor(
        public apiRequestPath: string,
        public apiRequestPayload: unknown,
        public apiError: PlatformApiServiceErrorResponse
    ) {
        super(apiRequestPath);
    }
}
