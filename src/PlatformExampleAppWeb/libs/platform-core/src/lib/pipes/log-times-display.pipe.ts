import { Pipe, PipeTransform } from '@angular/core';

import { Time } from '../common-types/time';

@Pipe({
    name: 'logTimesDisplay',
    standalone: true,
    pure: true
})
export class LogTimesDisplayPipe implements PipeTransform {
    public transform(logTimes?: Time[]): string {
        if (logTimes == undefined) return '';

        if (logTimes.length > 2) {
            return Time.parse(logTimes[0]!)
                .hourMinuteDisplay()
                .concat('; ... ; ')
                .concat(Time.parse(logTimes[logTimes.length - 1]!).hourMinuteDisplay());
        } else {
            let result = '';
            logTimes.forEach(
                (item, index) =>
                    (result = result
                        .concat(Time.parse(item).hourMinuteDisplay())
                        .concat(index === logTimes.length - 1 ? '' : '; '))
            );
            return result;
        }
    }
}
