import { AbstractControl, AsyncValidatorFn, FormControl, FormGroup, ValidatorFn } from '@angular/forms';

import { of } from 'rxjs';

import { validator } from './validator';

/**
 * Conditional synchronous validator function.
 *
 * @remarks
 * This function applies a validator function conditionally based on the specified condition.
 * If the condition is met, it invokes the provided validator function; otherwise, it returns null.
 *
 * @param condition - The condition to determine whether to apply the validator.
 * @param validatorFn - The validator function to apply when the condition is true.
 *
 * @returns A validator function that is applied based on the specified condition.
 *
 * @example
 * ```typescript
 * const form = new FormGroup({
 *   username: new FormControl('', [
 *     ifValidator(control => control.value !== '', Validators.required),
 *     // other validators...
 *   ]),
 * });
 * ```
 */
export function ifValidator(condition: (control: FormControl) => boolean, validatorFn: () => ValidatorFn): ValidatorFn {
    return validator((control: AbstractControl) => {
        if (!condition(<FormControl>control)) {
            return null;
        }
        return validatorFn()(control);
    });
}

/**
 * Conditional asynchronous validator function.
 *
 * @remarks
 * This function applies an asynchronous validator function conditionally based on the specified condition.
 * If the condition is met, it invokes the provided asynchronous validator function; otherwise, it returns an observable of null.
 *
 * @param condition - The condition to determine whether to apply the asynchronous validator.
 * @param validatorFn - The asynchronous validator function to apply when the condition is true.
 *
 * @returns An asynchronous validator function that is applied based on the specified condition.
 *
 * @example
 * ```typescript
 * const form = new FormGroup({
 *   email: new FormControl('', [
 *     ifAsyncValidator(control => control.value !== '', asyncValidators.emailAvailability),
 *     // other asynchronous validators...
 *   ]),
 * });
 * ```
 */
export function ifAsyncValidator(
    condition: (control: FormControl) => boolean,
    validatorFn: AsyncValidatorFn
): AsyncValidatorFn {
    return (control: AbstractControl) => {
        // if control is not FormGroup and root of it equal itself mean that control is just new and not attached to any form group
        // no need to execute form validation or check if condition
        if (!(control instanceof FormGroup) && control.root == control) return of(null);

        if (!condition(<FormControl>control)) {
            return of(null);
        }
        return validatorFn(control);
    };
}
