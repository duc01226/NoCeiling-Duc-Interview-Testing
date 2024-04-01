import { Observable, ObservableInput, Observer, Subscription, takeUntil } from 'rxjs';

export function subscribe<T>(
    outSubscriptionFn?: (subscription: Subscription) => void
): (source: Observable<T>) => Observable<T> {
    return sourceObservable => {
        if (outSubscriptionFn != undefined) outSubscriptionFn(sourceObservable.subscribe());
        else sourceObservable.subscribe();

        return sourceObservable;
    };
}

export function subscribeUntil<T>(
    notifier: ObservableInput<unknown>,
    observerOrNext?: Partial<Observer<T>> | ((value: T) => void),
    outSubscriptionFn?: (subscription: Subscription) => void
): (source: Observable<T>) => Observable<T> {
    return sourceObservable => {
        const resultObservable = sourceObservable.pipe(takeUntil(notifier));
        if (outSubscriptionFn != undefined) outSubscriptionFn(resultObservable.subscribe(observerOrNext));
        else resultObservable.subscribe(observerOrNext);

        return resultObservable;
    };
}
