import { AbstractControl, AsyncValidatorFn, FormControl, ValidatorFn } from '@angular/forms';
import { of } from 'rxjs';

export function ifValidator(condition: (control: FormControl) => boolean, validatorFn: () => ValidatorFn): ValidatorFn {
    return (control: AbstractControl) => {
        if (!condition(<FormControl>control)) {
            return null;
        }
        return validatorFn()(control);
    };
}

export function ifAsyncValidator(
    condition: (control: FormControl) => boolean,
    validatorFn: AsyncValidatorFn
): AsyncValidatorFn {
    return (control: AbstractControl) => {
        if (!condition(<FormControl>control)) {
            return of(null);
        }
        return validatorFn(control);
    };
}
