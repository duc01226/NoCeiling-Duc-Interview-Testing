/* eslint-disable @typescript-eslint/no-explicit-any */
export interface SimpleChange<T> {
  firstChange: boolean;
  previousValue: T;
  currentValue: T;
  isFirstChange: () => boolean;
}

export type WatchCallBackFunction<T> = (value: T, change?: SimpleChange<T>) => void;

export function Watch<T = object>(callbackFnOrName: WatchCallBackFunction<T> | string) {
  const cachedValueKey = Symbol();
  const isFirstChangeKey = Symbol();

  return (target: object, key: PropertyKey) => {
    Object.defineProperty(target, key, {
      set: function (value: object) {
        this[isFirstChangeKey] = this[isFirstChangeKey] === undefined;
        if (!this[isFirstChangeKey] && this[cachedValueKey] === value) {
          return;
        }

        const oldValue = this[cachedValueKey];
        this[cachedValueKey] = value;
        const simpleChange: SimpleChange<T> = {
          firstChange: this[isFirstChangeKey],
          previousValue: oldValue,
          currentValue: this[cachedValueKey],
          isFirstChange: () => this[isFirstChangeKey]
        };

        const callBackFn: WatchCallBackFunction<T> =
          typeof callbackFnOrName === 'string' ? (target as any)[callbackFnOrName] : callbackFnOrName;
        if (!callBackFn) {
          throw new Error(`Cannot find method ${callbackFnOrName} in class ${target.constructor.name}`);
        }

        callBackFn.call(this, this[cachedValueKey], simpleChange);
      },
      get: function () {
        return this[cachedValueKey];
      },
      enumerable: true,
      configurable: true
    });
  };
}
