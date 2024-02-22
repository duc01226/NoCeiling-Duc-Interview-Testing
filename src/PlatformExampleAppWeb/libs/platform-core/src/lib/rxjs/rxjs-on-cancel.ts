import { Observable } from 'rxjs';

export function onCancel<T>(f: () => void): (source: Observable<T>) => Observable<T> {
    return sourceObservable =>
        new Observable(observer => {
            let completed = false;
            let errored = false;
            let succeeded = false;

            const subscription = sourceObservable.subscribe({
                next: v => {
                    succeeded = true;
                    observer.next(v);
                },
                error: e => {
                    errored = true;
                    observer.error(e);
                },
                complete: () => {
                    completed = true;
                    if (!succeeded && !errored) f();

                    observer.complete();
                }
            });

            return () => {
                subscription.unsubscribe();
                if (!completed && !errored) f();
            };
        });
}
