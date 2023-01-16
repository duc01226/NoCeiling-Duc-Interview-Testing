/* eslint-disable @typescript-eslint/no-explicit-any */
import { Observable, of, pipe, Subscription } from 'rxjs';
import { delay as rxjs_delay, filter as rxjs_filter, takeUntil as rxjs_takeUntil } from 'rxjs/operators';

export function task_delay(callback: () => void, delayTime?: number, cancelOnFirstTrueValue$?: Observable<boolean>): Subscription {
  if (typeof delayTime === 'number' && delayTime <= 0) {
    callback();
    return new Subscription();
  }

  const delayObs = pipe(
    cancelOnFirstTrueValue$ != null
      ? rxjs_takeUntil(cancelOnFirstTrueValue$?.pipe(rxjs_filter(x => x == true)))
      : (obs: Observable<unknown>) => obs,
    rxjs_delay(delayTime == null ? 10 : delayTime)
  );
  return delayObs(of({})).subscribe(() => {
    callback();
  });
}

export function task_debounce(func: (...args: any[]) => void, wait: number): (...args: any[]) => void {
  if (wait <= 0) {
    return func;
  }

  let timeout: number;
  return (...args: any[]) => {
    clearTimeout(timeout);
    timeout = <number>(<any>setTimeout(() => func(args), wait));
  };
}
