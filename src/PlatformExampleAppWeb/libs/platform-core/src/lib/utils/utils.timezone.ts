import moment from 'moment-timezone';

export function timezone_getCurrentTimezone(): string {
    return Intl.DateTimeFormat().resolvedOptions().timeZone || moment.tz.guess();
}

export function timezone_getCurrentTimezoneWithLocalTime() {
    return `${moment.tz(timezone_getCurrentTimezone()).format('Z')} ${timezone_getCurrentTimezone()}`;
}
