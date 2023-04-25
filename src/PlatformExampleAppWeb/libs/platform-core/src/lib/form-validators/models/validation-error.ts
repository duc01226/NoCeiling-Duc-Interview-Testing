import { ValidationErrors } from '@angular/forms';

export interface IPlatformFormValidationError {
    errorMsg: string;
    params?: Dictionary<string | number | Date>;
}

export function buildFormValidationError(
    validationErrorOrMsg: IPlatformFormValidationError | string
): IPlatformFormValidationError {
    if (typeof validationErrorOrMsg == 'string') return { errorMsg: validationErrorOrMsg };
    return validationErrorOrMsg;
}

export function buildFormValidationErrors(
    key: string,
    validationErrorOrMsg: IPlatformFormValidationError | string
): ValidationErrors {
    return { [key]: buildFormValidationError(validationErrorOrMsg) };
}
