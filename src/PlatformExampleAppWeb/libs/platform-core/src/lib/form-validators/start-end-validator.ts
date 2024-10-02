import { AbstractControl, FormControl, ValidatorFn } from '@angular/forms';

import { date_compareOnlyDay, date_compareOnlyTime, date_format, number_IsNumber } from '../utils';
import { IPlatformFormValidationError } from './models';
import { validator } from './validator';

/**
 * Validator function to check if the start date/time is before the end date/time in a form control.
 *
 * @remarks
 * This validator compares the values of two form controls representing start and end dates or times.
 * It validates whether the start date/time is chronologically before the end date/time, with options
 * to allow or disallow equal values and to check only the date part, time part, or both.
 *
 * @param errorKey - The key to identify the validation error in the form control.
 * @param startFn - A function that extracts the start date/time value from the form control.
 * @param endFn - A function that extracts the end date/time value from the form control.
 * @param options - Additional options for the validator.
 *
 * @returns A validator function that checks the validity of the start and end date/time values.
 *
 * @example
 * ```typescript
 * const form = new FormGroup({
 *   startDate: new FormControl(new Date()),
 *   endDate: new FormControl(new Date())
 * }, { validators: startEndValidator('dateRange', control => control.value.startDate, control => control.value.endDate) });
 * ```
 */
export function startEndValidator<T extends number | Date>(
    errorKey: string,
    startFn: (control: FormControl<T>) => T,
    endFn: (control: FormControl<T>) => T,
    options: {
        allowEqual?: boolean;
        checkDatePart?: 'default' | 'dateOnly' | 'timeOnly';
        condition?: (control: FormControl) => boolean;
        errorMsg?: string;
    } | null = null
): ValidatorFn {
    return validator((control: AbstractControl) => {
        const allowEqual = options?.allowEqual ?? true;
        const checkDatePart = options?.checkDatePart ?? 'default';
        const condition = options?.condition;

        if (condition != null && !condition(<FormControl>control)) {
            return null;
        }

        const start = convertToDateOrNumber(startFn(<FormControl>control));
        const end = convertToDateOrNumber(endFn(<FormControl>control));

        if (typeof start === 'number' && typeof end === 'number') {
            if ((allowEqual && start > end) || (!allowEqual && start >= end)) {
                return {
                    [errorKey]: buildValidatorError(start, end, options?.errorMsg)
                };
            }
        } else if (start instanceof Date && end instanceof Date) {
            if (checkDatePart === 'default') {
                if ((allowEqual && start > end) || (!allowEqual && start >= end)) {
                    return {
                        [errorKey]: buildValidatorError(start, end, options?.errorMsg)
                    };
                }
            } else if (checkDatePart === 'dateOnly') {
                if (
                    (allowEqual && date_compareOnlyDay(start, end) > 0) ||
                    (!allowEqual && date_compareOnlyDay(start, end) >= 0)
                ) {
                    return {
                        [errorKey]: buildValidatorError(start, end, options?.errorMsg)
                    };
                }
            } else if (
                checkDatePart === 'timeOnly' &&
                ((allowEqual && date_compareOnlyTime(start, end) > 0) ||
                    (!allowEqual && date_compareOnlyTime(start, end) >= 0))
            ) {
                return {
                    [errorKey]: buildValidatorError(start, end, options?.errorMsg)
                };
            }
        }

        return null;
    });
}

/**
 * Parses a value to either a Date or a number.
 *
 * @param value - The value to be parsed.
 * @returns The parsed Date or number.
 *
 * @internal
 */
function convertToDateOrNumber(value: Date | number | string) {
    return number_IsNumber(value) ? Number(value) : new Date(value);
}

/**
 * Formats a date value as a string in the 'YYYY/MM/DD' format.
 *
 * @param value - The date value to be formatted.
 * @returns A string representing the formatted date.
 *
 * @internal
 */
function formatDate(value: Date | number): string {
    return date_format(new Date(value), 'YYYY/MM/DD');
}

/**
 * Builds a validation error object for the start-end date/time validator.
 *
 * @param start - The start date/time value.
 * @param end - The end date/time value.
 * @param errorMsg - Custom error message for the validation error.
 * @returns A validation error object with error message and parameters.
 *
 * @internal
 */
function buildValidatorError(
    start: Date | number,
    end: Date | number,
    errorMsg: string = `Value must be in range ${formatDateOrNumber(start)} and ${formatDateOrNumber(end)}`
): IPlatformFormValidationError {
    return { errorMsg: errorMsg, params: { startDate: start, endDate: end } };
}

/**
 * Formats a date or number value as a string.
 *
 * @param value - The value to be formatted.
 * @returns A string representing the formatted value.
 */
function formatDateOrNumber(value: Date | number): string {
    return typeof value === 'number' ? value.toString() : formatDate(value);
}
