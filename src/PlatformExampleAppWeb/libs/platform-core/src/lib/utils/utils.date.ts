import * as moment from 'moment';

export function date_setHours(date: Date, hours: number): Date {
    return new Date(new Date(date).setHours(hours));
}

export function date_setToEndOfDay(date: Date): Date {
    return new Date(new Date(date).setHours(23, 59, 59, 0));
}

export function date_getStartOfMonth(date: Date): Date {
    return new Date(date.getFullYear(), date.getMonth(), 1);
}

export function date_getEndOfMonth(date: Date): Date {
    return new Date(date.getFullYear(), date.getMonth() + 1, 0);
}

export function date_setToStartOfDay(date: Date): Date {
    return new Date(new Date(date).setHours(0, 0, 0, 0));
}

export function date_getEndOfYear(date: Date): Date {
    return new Date(date.getFullYear(), 11, 31);
}

export function date_getStartOfYear(date: Date): Date {
    return new Date(date.getFullYear(), 0, 1);
}

export function date_daysInRange(startDate: Date, stopDate: Date): Date[] {
    const dateArray: Date[] = [];
    let currentDate: Date = new Date(startDate);
    while (currentDate <= stopDate) {
        dateArray.push(new Date(currentDate));
        currentDate = moment(currentDate).add(1, 'days').toDate();
    }

    return dateArray;
}

export function date_countDaysToNow(value: Date): number {
    const diff: number = Date.now() - new Date(value).getTime();
    return Math.floor(diff / (60 * 60 * 24 * 1000));
}

export function date_countDaysFromNow(value: Date): number {
    const diff: number = new Date(value).getTime() - Date.now();
    return Math.floor(diff / (60 * 60 * 24 * 1000));
}

export function date_countWeeksFromNow(value: Date): number {
    const present = new Date();
    const date = new Date(value);
    const diff = moment(date).diff(moment(present), 'weeks', true);
    return Math.floor(diff);
}

export function date_countMonthsFromNow(value: Date): number {
    const present = new Date();
    const date = new Date(value);
    const diff = moment(date).diff(moment(present), 'months', true);
    return Math.floor(diff);
}

export function date_addYear(date: Date, year: number): Date {
    date.setFullYear(date.getFullYear() + year);
    return new Date(date);
}

export function date_getDurationInfo(miliseconds: number): {
    days: number;
    hours: number;
    minutes: number;
    seconds: number;
} {
    const secondsForOneDay = 60 * 60 * 24;
    const secondsForOneHour = 60 * 60;
    const secondsForOneMinute = 60;

    const totalSecondDiff = Math.round(miliseconds / 1000);
    const days = miliseconds <= 0 ? 0 : Math.floor(totalSecondDiff / secondsForOneDay);

    const totalHoursSecondDiff = totalSecondDiff - days * secondsForOneDay;
    const hours = miliseconds <= 0 ? 0 : Math.floor(totalHoursSecondDiff / secondsForOneHour);

    const totalMinutesSecondDiff = totalHoursSecondDiff - hours * secondsForOneHour;
    const minutes = miliseconds <= 0 ? 0 : Math.floor(totalMinutesSecondDiff / secondsForOneMinute);

    const seconds = miliseconds <= 0 ? 0 : totalMinutesSecondDiff - minutes * secondsForOneMinute;
    return { days: days, hours: hours, minutes: minutes, seconds: seconds };
}

export function date_removeTime(date: Date | null): Date {
    if (date == null) {
        date = new Date();
    }
    return new Date(new Date(date).toDateString());
}

export function date_compareDate(firstDate: Date, secondDate: Date, includeTime: boolean = true): number {
    const toCompareFirstDate = includeTime ? firstDate : date_removeTime(firstDate);
    const toCompareSecondDate = includeTime ? secondDate : date_removeTime(secondDate);
    if (toCompareFirstDate.getTime() === toCompareSecondDate.getTime()) {
        return 0;
    }
    if (toCompareFirstDate.getTime() > toCompareSecondDate.getTime()) {
        return 1;
    }
    return -1;
}

export function date_compareOnlyDay(firstDate: Date, secondDate: Date): number {
    const toCompareFirstDate = date_setToStartOfDay(firstDate).getTime();
    const toCompareSecondDate = date_setToStartOfDay(secondDate).getTime();
    if (toCompareFirstDate === toCompareSecondDate) {
        return 0;
    }
    if (toCompareFirstDate > toCompareSecondDate) {
        return 1;
    }
    return -1;
}

/**
 * Compare two dates without times and return diff in days.
 * @param startDate first date
 * @param endDate second date
 * @param floor round down or not
 */
export function date_dayDiffs(startDate: Date, endDate: Date, floor: boolean = true): number {
    const firstDate = startDate ? new Date(startDate) : new Date();
    const secondDate = endDate ? new Date(endDate) : new Date();
    firstDate.setHours(0, 0, 0, 0);
    secondDate.setHours(0, 0, 0, 0);
    const diffDays: number = (secondDate.getTime() - firstDate.getTime()) / (1000 * 3600 * 24);
    return floor ? Math.floor(diffDays) : diffDays;
}

export function date_compareOnlyTime(firstDate: Date, secondDate: Date): number {
    if (secondDate && firstDate) {
        const secondHour = secondDate.getHours();
        const firstHour = firstDate.getHours();
        if (secondHour < firstHour) {
            return 1;
        } else if (secondHour === firstHour && secondDate.getMinutes() < firstDate.getMinutes()) {
            return 1;
        } else if (secondHour === firstHour && secondDate.getMinutes() === firstDate.getMinutes()) {
            return 0;
        }
    }
    return -1;
}

export function date_isInRange(start: Date, end: Date, date: Date, includeTime: boolean = true): boolean {
    return date_compareDate(start, date, includeTime) <= 0 && date_compareDate(date, end, includeTime) <= 0;
}

export function date_addMinutes(date: Date, minutes: number): Date {
    return new Date(date.getTime() + minutes * 60000);
}

export function date_addDays(date: Date, days: number): Date {
    return new Date(date.getTime() + days * 24 * 60 * 60 * 1000);
}

export function date_addMonths(date: Date, months: number): Date {
    return new Date(date.setMonth(date.getMonth() + months));
}

export function date_now(): Date {
    return new Date();
}

export function date_startOfToday(): Date {
    const now = date_now();
    return new Date(now.getFullYear(), now.getMonth(), now.getDate(), 0, 0, 0);
}

export function date_endOfToday(): Date {
    const now = date_now();
    return new Date(now.getFullYear(), now.getMonth(), now.getDate(), 23, 59, 59);
}

export function date_startOfYear(year?: number): Date {
    if (year == null) {
        return new Date(date_now().getFullYear(), 0, 1, 0, 0, 1, 0);
    }
    return new Date(year, 0, 1, 0, 0, 0, 0);
}

export function date_endOfYear(year?: number): Date {
    if (year == null) {
        return new Date(date_now().getFullYear(), 11, 31, 23, 59, 59);
    }
    return new Date(year, 11, 31, 23, 59, 59);
}

export function date_endOfMonth(date: Date): Date {
    return new Date(date.getFullYear(), date.getMonth() + 1, 0, 23, 59, 59);
}

export function date_startOfMonth(date: Date): Date {
    return new Date(date.getFullYear(), date.getMonth(), 1, 0, 0, 0);
}

export function date_MondayOfWeek(date: Date): Date {
    const lessDays = date.getDay() === 0 ? 6 : date.getDay() - 1;
    return new Date(new Date(date).setDate(date.getDate() - lessDays));
}

export function date_SundayOfWeek(date: Date): Date {
    const moreDays = date.getDay() === 0 ? 0 : 7 - date.getDay();
    return new Date(new Date(date).setDate(date.getDate() + moreDays));
}

export function date_format(date: Date, format: string): string {
    return moment(date).format(format);
}

export function date_timeDiff(value1: Date, value2: Date): number {
    return value1.getTime() - value2.getTime();
}

export function date_startOfQuarter(date: Date): Date {
    return moment(date).startOf('quarter').toDate();
}

export function date_endOfQuarter(date: Date): Date {
    return moment(date).endOf('quarter').toDate();
}

export function date_startOfWeek(date: Date): Date {
    return moment(date).startOf('isoWeek').toDate();
}

export function date_endOfWeek(date: Date): Date {
    return moment(date).endOf('isoWeek').toDate();
}

export function date_addQuarters(currentDate: Date, numberOfQuarters: number): Date {
    return moment(currentDate).add(numberOfQuarters, 'quarter').toDate();
}

export function date_addWeeks(currentDate: Date, numberOfWeeks: number): Date {
    return moment(currentDate).add(numberOfWeeks, 'week').toDate();
}

export function date_getNextWeekday(
    date: Date,
    dayToFind: 'Monday' | 'Tuesday' | 'Wednesday' | 'Thusday' | 'Friday' | 'Sartuday' | 'Sunday'
): Date {
    const days = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thusday', 'Friday', 'Sartuday'];
    const dayIndex = days.findIndex(v => v === dayToFind);
    const dateCopy = new Date(date.getTime());
    return new Date(dateCopy.setDate(dateCopy.getDate() + ((7 - dateCopy.getDay() + dayIndex) % 7 || 7)));
}
