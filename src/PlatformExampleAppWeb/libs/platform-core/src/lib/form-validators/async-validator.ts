import { AbstractControl, AsyncValidatorFn, FormGroup, ValidationErrors } from '@angular/forms';

import { asyncScheduler, catchError, delay, Observable, of, Subject, switchMap, takeUntil } from 'rxjs';

import { PlatformApiServiceErrorResponse } from '../api-services';
import { distinctUntilObjectValuesChanged } from '../rxjs';
import { buildFormValidationErrors } from './models';

/**
 * Creates an asynchronous validator function for Angular forms.
 *
 * @param {string} errorKey - The key used to identify the error message.
 * @param {(control: AbstractControl) => Observable<ValidationErrors | null>} validatorFn - The function that performs asynchronous validation.
 * @param {number} [throttleTimeMs=500] - The time in milliseconds to throttle validation requests.
 * @param {boolean} [onlyThrottleValidForm=false] - Indicates whether to throttle only when the form is valid.
 * @returns {AsyncValidatorFn} An asynchronous validator function.
 */
export function asyncValidator(
    errorKey: string,
    validatorFn: (control: AbstractControl) => Observable<ValidationErrors | null>,
    throttleTimeMs: number = 500,
    onlyThrottleValidForm: boolean = false
): AsyncValidatorFn {
    const cancelPreviousSub: Subject<unknown> = new Subject();

    return (control: AbstractControl): Promise<ValidationErrors | null> | Observable<ValidationErrors | null> => {
        // if control is not FormGroup and root of it equal itself mean that control is just new and not attached to any form group
        // no need to execute form validation
        if (!(control instanceof FormGroup) && control.root == control) return of(null);

        cancelPreviousSub.next(null);

        return of(null).pipe(
            takeUntil(cancelPreviousSub),
            delay(
                !onlyThrottleValidForm || control.root.valid || control.root.status == 'PENDING' ? throttleTimeMs : 0,
                asyncScheduler
            ),
            switchMap(() =>
                validatorFn(control).pipe(
                    takeUntil(cancelPreviousSub),
                    catchError((error: PlatformApiServiceErrorResponse | Error) =>
                        of(
                            buildFormValidationErrors(
                                errorKey,
                                PlatformApiServiceErrorResponse.getDefaultFormattedMessage(error)
                            )
                        )
                    )
                )
            ),
            distinctUntilObjectValuesChanged()
        );
    };
}
