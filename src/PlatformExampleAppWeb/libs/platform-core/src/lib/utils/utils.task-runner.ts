/* eslint-disable @typescript-eslint/no-explicit-any */
import { Observable, of, pipe, Subscription } from 'rxjs';
import { delay, filter, takeUntil } from 'rxjs/operators';

export class TaskRunner {
  public static delay(
    callback: () => void,
    delayTime?: number,
    cancelOnFirstTrueValue$?: Observable<boolean>
  ): Subscription {
    if (typeof delayTime === 'number' && delayTime <= 0) {
      callback();
      return new Subscription();
    }

    const delayObs = pipe(
      cancelOnFirstTrueValue$ != null
        ? takeUntil(cancelOnFirstTrueValue$?.pipe(filter(x => x == true)))
        : (obs: Observable<unknown>) => obs,
      delay(delayTime == null ? 10 : delayTime)
    );
    return delayObs(of({})).subscribe(() => {
      callback();
    });
  }

  public static debounce(func: (...args: any[]) => void, wait: number): (...args: any[]) => void {
    if (wait <= 0) {
      return func;
    }

    let timeout: number;
    return (...args: any[]) => {
      clearTimeout(timeout);
      timeout = <number>(<any>setTimeout(() => func(args), wait));
    };
  }
}
