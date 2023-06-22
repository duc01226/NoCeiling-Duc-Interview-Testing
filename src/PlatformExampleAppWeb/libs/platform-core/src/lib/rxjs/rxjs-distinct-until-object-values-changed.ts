import { distinctUntilChanged, MonoTypeOperatorFunction } from 'rxjs';

import { isDifferent } from '../utils';

export function distinctUntilObjectValuesChanged<T>(): MonoTypeOperatorFunction<T> {
    return distinctUntilChanged((prev, current) => !isDifferent(prev, current));
}
