import { MonoTypeOperatorFunction, Observable, tap } from 'rxjs';
import { TapObserver } from 'rxjs/internal/operators/tap';

export function tapSkipFirst<T>(
    observerOrNext: Partial<TapObserver<T>> | ((value: T) => void)
): MonoTypeOperatorFunction<T> {
    let tappedOnce = false;

    return (source: Observable<T>): Observable<T> =>
        source.pipe(
            tap({
                next: value => {
                    if (tappedOnce) {
                        if (typeof observerOrNext == 'function') observerOrNext(value);
                        else if (observerOrNext.next != undefined) observerOrNext.next(value);
                    }
                    tappedOnce = true;
                },
                error: error => {
                    if (tappedOnce && typeof observerOrNext != 'function' && observerOrNext.error != undefined)
                        observerOrNext.error(error);
                    tappedOnce = true;
                },
                complete: () => {
                    if (tappedOnce && typeof observerOrNext != 'function' && observerOrNext.complete != undefined)
                        observerOrNext.complete();
                    tappedOnce = true;
                }
            })
        );
}
