import * as moment from 'moment-timezone';

export function timezone_getCurrentTimezone(): string {
    return Intl.DateTimeFormat().resolvedOptions().timeZone || moment.tz.guess();
}
