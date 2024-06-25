import { Observable, OperatorFunction } from 'rxjs';
import { isDifferent } from '../utils';

/**
 * Custom operator to skip duplicated same value items within a given amount of time.
 *
 * @param milliseconds The time in milliseconds within which duplicate values will be skipped.
 * @returns An operator function that can be used with RxJS `pipe`.
 */
export function skipDuplicates<T>(milliseconds: number): OperatorFunction<T, T> {
    return (source: Observable<T>): Observable<T> => {
        return new Observable<T>(observer => {
            let lastValue: T | undefined;
            let lastEmitTime = 0;

            return source.subscribe({
                next: value => {
                    const currentTime = Date.now();
                    if (isDifferent(value, lastValue) || currentTime - lastEmitTime > milliseconds) {
                        lastValue = value;
                        lastEmitTime = currentTime;
                        observer.next(value);
                    }
                },
                error: err => {
                    observer.error(err);
                },
                complete: () => {
                    observer.complete();
                }
            });
        });
    };
}
