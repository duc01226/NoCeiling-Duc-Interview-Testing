import { MonoTypeOperatorFunction, Observable, tap } from 'rxjs';
import { TapObserver } from 'rxjs/internal/operators/tap';

export function tapOnce<T>(observer: Partial<TapObserver<T>>): MonoTypeOperatorFunction<T> {
    let tappedNext = false;
    let tappedError = false;
    let tappedComplete = false;

    return (source: Observable<T>): Observable<T> =>
        source.pipe(
            tap({
                next: value => {
                    if (!tappedNext) {
                        if (observer.next != undefined) observer.next(value);
                        tappedNext = true;
                    }
                },
                error: error => {
                    if (!tappedError) {
                        if (observer.error != undefined) observer.error(error);
                        tappedError = true;
                    }
                },
                complete: () => {
                    if (!tappedComplete) {
                        if (observer.complete != undefined) observer.complete();
                        tappedComplete = true;
                    }
                }
            })
        );
}
