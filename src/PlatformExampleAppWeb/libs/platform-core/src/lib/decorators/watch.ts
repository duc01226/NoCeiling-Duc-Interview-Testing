/* eslint-disable @typescript-eslint/no-explicit-any */
export interface SimpleChange<T> {
  firstChange: boolean;
  previousValue: T;
  currentValue: T;
  isFirstChange: () => boolean;
}

export type WatchCallBackFunction<T, TTargetObj> = (value: T, change: SimpleChange<T>, targetObj: TTargetObj) => void;

/**
 * Operator used to watch a property when it is set
 *
 * Example:
 *
 * // Shorthand execute a target function doing something directly if on change only do this logic
 * @Watch('pagedResultWatch')
 * public pagedResult?: PlatformPagedResultDto<LeaveType>;
 *
 * // Full syntax execute a NORMAL FUNCTION
 * @Watch<PlatformPagedQueryDto, LeaveTypesState>((value, change, targetObj) => {
 *   targetObj.updatePageInfo();
 * })
 * public pagedQuery: PlatformPagedQueryDto = new PlatformPagedQueryDto();
 *
 * public pagedResultWatch(
 *   value: PlatformPagedResultDto<LeaveType> | undefined,
 *   change: SimpleChange<PlatformPagedResultDto<LeaveType> | undefined>
 * ) {
 *   this.updatePageInfo();
 * }
 */
export function Watch<TProp = object, TTargetObj extends object = object>(
  callbackFnOrName: WatchCallBackFunction<TProp, TTargetObj> | string
) {
  const cachedValueKey = Symbol();
  const isFirstChangeKey = Symbol();

  return (target: TTargetObj, key: PropertyKey) => {
    Object.defineProperty(target, key, {
      set: function (value: object) {
        this[isFirstChangeKey] = this[isFirstChangeKey] === undefined;
        if (!this[isFirstChangeKey] && this[cachedValueKey] === value) {
          return;
        }

        const oldValue = this[cachedValueKey];
        this[cachedValueKey] = value;
        const simpleChange: SimpleChange<TProp> = {
          firstChange: this[isFirstChangeKey],
          previousValue: oldValue,
          currentValue: this[cachedValueKey],
          isFirstChange: () => this[isFirstChangeKey]
        };

        if (typeof callbackFnOrName === 'string') {
          const callBackMethod = (target as any)[callbackFnOrName];
          if (callBackMethod == null) {
            throw new Error(`Cannot find method ${callbackFnOrName} in class ${target.constructor.name}`);
          }

          callBackMethod.call(this, this[cachedValueKey], simpleChange);
        } else {
          callbackFnOrName(this[cachedValueKey], simpleChange, this);
        }
      },
      get: function () {
        return this[cachedValueKey];
      },
      enumerable: true,
      configurable: true
    });
  };
}
