/* eslint-disable @typescript-eslint/no-explicit-any */
export interface SimpleChange<T> {
    previousValue: T;
    currentValue: T;
    isFirstTimeSet: boolean;
}

export type WatchCallBackFunction<T, TTargetObj> = (value: T, change: SimpleChange<T>, targetObj: TTargetObj) => void;

/**
 * Operator used to watch a property when it is set.
 *
 * @template TTargetObj The type of the target object, defaults to object.
 * @template TProp The type of the property being watched, defaults to object.
 *
 * @param {WatchCallBackFunction<TProp, TTargetObj> | keyof TTargetObj} callbackFnOrName
 *        A callback function to be executed when the watched property changes, or the name of a method on the component.
 * @param {(obj: TTargetObj, change: SimpleChange<TProp>) => boolean} [onlyWhen]
 *        An optional function that determines if the callback should be executed based on the change.
 * @param {(target: TTargetObj) => void} [afterCallback]
 *        An optional function to be executed after the main callback.
 *
 * @returns {MethodDecorator} A method decorator to watch the specified property.
 *
 * @example
 * // Shorthand to execute a target function directly on change
 * @Watch('pagedResultWatch')
 * public pagedResult?: PlatformPagedResultDto<LeaveType>;
 *
 * // Full syntax to execute a normal function
 * @Watch<LeaveTypesState, PlatformPagedQueryDto>((value, change, targetObj) => {
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
export function Watch<TTargetObj extends object = object, TProp = object>(
    callbackFnOrName: WatchCallBackFunction<TProp, TTargetObj> | keyof TTargetObj,
    onlyWhen?: (obj: TTargetObj, change: SimpleChange<TProp>) => boolean,
    afterCallback?: (target: TTargetObj) => void
) {
    return (target: TTargetObj, key: keyof TTargetObj) => {
        EnsureNotExistingSetterForKey(target, key);

        const privatePropKey = `_${key.toString()}`;
        let isFirstTimeSet: boolean | undefined;

        Object.defineProperty(target, key, {
            set: function (value: TProp) {
                const oldValue = this[privatePropKey];
                this[privatePropKey] = value;

                isFirstTimeSet = isFirstTimeSet == undefined;

                const simpleChange: SimpleChange<TProp> = {
                    previousValue: oldValue,
                    currentValue: this[privatePropKey],
                    isFirstTimeSet: isFirstTimeSet
                };

                if (onlyWhen != null && !onlyWhen(this, simpleChange)) return;

                if (typeof callbackFnOrName === 'string') {
                    const callBackMethod = (target as any)[callbackFnOrName];
                    if (callBackMethod == null) {
                        throw new Error(`Cannot find method ${callbackFnOrName} in class ${target.constructor.name}`);
                    }

                    callBackMethod.call(this, this[privatePropKey], simpleChange, this);
                } else if (typeof callbackFnOrName == 'function') {
                    callbackFnOrName(this[privatePropKey], simpleChange, this);
                }

                if (afterCallback != null) afterCallback(this);
            },
            get: function () {
                return this[privatePropKey];
            },
            enumerable: true,
            configurable: true
        });
    };

    function EnsureNotExistingSetterForKey<TTargetObj extends object>(target: TTargetObj, key: PropertyKey) {
        const existingTargetKeyProp = Object.getOwnPropertyDescriptors(target)[key.toString()];

        if (existingTargetKeyProp?.set != null || existingTargetKeyProp?.get != null)
            throw Error(
                'Could not use watch decorator on a existing get/set property. Should only use one solution, either get/set property or @Watch decorator'
            );
    }
}
