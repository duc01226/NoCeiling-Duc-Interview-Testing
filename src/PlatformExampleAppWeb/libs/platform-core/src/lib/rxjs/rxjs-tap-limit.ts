import { MonoTypeOperatorFunction, Observable, tap } from 'rxjs';
import { TapObserver } from 'rxjs/internal/operators/tap';

export function tapLimit<T>(observer: Partial<TapObserver<T>>, itemsLimit: number): MonoTypeOperatorFunction<T> {
    let tappedNextCount = 0;
    let tappedError = false;
    let tappedComplete = false;

    return (source: Observable<T>): Observable<T> =>
        source.pipe(
            tap({
                next: value => {
                    if (tappedNextCount < itemsLimit) {
                        if (observer.next != undefined) observer.next(value);

                        tappedNextCount++;
                    }

                    if (tappedNextCount == itemsLimit) setTimeout(() => complete());
                },
                error: error => {
                    if (!tappedError) {
                        if (observer.error != undefined) observer.error(error);
                        tappedError = true;
                    }
                },
                complete: () => {
                    complete();
                }
            })
        );

    function complete() {
        if (!tappedComplete) {
            if (observer.complete != undefined) observer.complete();
            tappedComplete = true;
        }
    }
}
