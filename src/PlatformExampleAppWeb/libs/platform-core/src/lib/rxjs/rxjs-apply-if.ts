import { MonoTypeOperatorFunction } from 'rxjs';

export function applyIf<T>(
    condition: boolean,
    applyOperator: MonoTypeOperatorFunction<T>
): MonoTypeOperatorFunction<T> {
    if (!condition) return o => o;
    return applyOperator;
}
