import { Observable } from 'rxjs';

export function onCancel<T>(f: () => void): (source: Observable<T>) => Observable<T> {
    return observable =>
        new Observable(observer => {
            let completed = false;
            let errored = false;
            const subscription = observable.subscribe({
                next: v => observer.next(v),
                error: e => {
                    errored = true;
                    observer.error(e);
                },
                complete: () => {
                    completed = true;
                    observer.complete();
                }
            });
            return () => {
                subscription.unsubscribe();
                if (!completed && !errored) f();
            };
        });
}
