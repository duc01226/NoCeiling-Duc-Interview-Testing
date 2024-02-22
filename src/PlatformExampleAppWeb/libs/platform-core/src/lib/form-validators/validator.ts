import { AbstractControl, FormGroup, ValidationErrors, ValidatorFn } from '@angular/forms';

/**
 * Creates an validator function for Angular forms.
 *
 * @param {(control: AbstractControl) => Observable<ValidationErrors | null>} validatorFn - The function that performs asynchronous validation.
 * @returns {ValidatorFn} An validator function.
 */
export function validator(validatorFn: (control: AbstractControl) => ValidationErrors | null): ValidatorFn {
    return (control: AbstractControl): ValidationErrors | null => {
        // if control is not FormGroup and root of it equal itself mean that control is just new and not attached to any form group
        // no need to execute form validation
        if (!(control instanceof FormGroup) && control.root == control) return null;

        return validatorFn(control);
    };
}
