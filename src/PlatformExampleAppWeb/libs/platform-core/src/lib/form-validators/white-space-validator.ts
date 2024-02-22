import { FormControl, ValidationErrors } from '@angular/forms';

export function noWhitespaceValidator(control: FormControl): ValidationErrors | null {
    const isWhitespace = (control.value ?? '').trim().length === 0;
    return isWhitespace ? { required: true } : null;
}
