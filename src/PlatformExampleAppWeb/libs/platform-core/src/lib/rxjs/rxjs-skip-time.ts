import { Observable, OperatorFunction, timer } from 'rxjs';
import { skipUntil } from 'rxjs/operators';

/**
 * Custom operator to skip all items emitted by the source observable
 * for a given amount of time.
 *
 * @param milliseconds The time in milliseconds to skip items.
 * @returns An operator function that can be used with RxJS `pipe`.
 */
export function skipTime<T>(milliseconds: number): OperatorFunction<T, T> {
    return (source: Observable<T>): Observable<T> => {
        return source.pipe(skipUntil(timer(milliseconds)));
    };
}
