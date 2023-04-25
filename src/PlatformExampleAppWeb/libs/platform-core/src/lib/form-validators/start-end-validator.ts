import { AbstractControl, FormControl, ValidatorFn } from '@angular/forms';

import { date_compareOnlyDay, date_compareOnlyTime, date_format } from '../utils';
import { IPlatformFormValidationError } from './models';

export function startEndValidator<T extends number | Date>(
    errorKey: string,
    startFn: (control: FormControl<T>) => T,
    endFn: (control: FormControl<T>) => T,
    options: {
        allowEqual: boolean;
        checkDatePart: 'default' | 'dateOnly' | 'timeOnly';
        condition?: (control: FormControl) => T;
        errorMsg?: string;
    } | null = null
): ValidatorFn {
    return (control: AbstractControl) => {
        const allowEqual = options?.allowEqual ?? true;
        const checkDatePart = options?.checkDatePart ?? 'default';
        const condition = options?.condition;

        if (condition != null && !condition(<FormControl>control)) {
            return null;
        }
        const start = new Date(startFn(<FormControl>control));
        const end = new Date(endFn(<FormControl>control));

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
    };
}

function formatDate(value: Date | number): string {
    return date_format(new Date(value), 'YYYY/MM/DD');
}

function buildValidatorError(start: Date, end: Date, errorMsg?: string): IPlatformFormValidationError {
    errorMsg = errorMsg ?? `Date must be in range ${formatDate(start)} and ${formatDate(end)}`;

    return { errorMsg: errorMsg, params: { startDate: start, endDate: end } };
}
