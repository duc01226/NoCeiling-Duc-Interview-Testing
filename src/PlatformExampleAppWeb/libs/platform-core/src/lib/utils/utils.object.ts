/* eslint-disable @typescript-eslint/no-unnecessary-type-constraint */
/* eslint-disable @typescript-eslint/no-explicit-any */
import {
    filter,
    clone as lodashClone,
    cloneDeep as lodashCloneDeep,
    keys as lodashKeys,
    values as lodashValues,
    union
} from 'lodash-es';

import { PartialDeep } from 'type-fest';
import { Time } from '../common-types';
import { PLATFORM_CORE_GLOBAL_ENV } from '../platform-core-global-environment';
import { any } from './_common-functions';
import { list_distinct } from './utils.list';

export function keys<T extends object>(
    source: T,
    ignorePrivate: boolean = true,
    excludeKeys?: (keyof T)[]
): (keyof T & string)[] {
    if (typeof source != 'object' || source == null) return [];

    const objectOwnProps: (keyof T & string)[] = [];
    for (const key in source) {
        if (
            typeof (<any>source)[key] != 'function' &&
            (ignorePrivate == false || !key.startsWith('_')) &&
            !excludeKeys?.includes(key)
        ) {
            if (key.startsWith('_')) {
                const publicKey = <keyof T & string>key.substring(1);
                if (!ignorePrivate) objectOwnProps.push(key);
                if ((<any>source)[key] === (<any>source)[publicKey]) {
                    objectOwnProps.push(publicKey);
                }
            } else {
                objectOwnProps.push(key);
            }
        }
    }

    const objectPrototypeProps = getObjectPrototypeProps(source, Object.getPrototypeOf(source));

    return list_distinct(objectOwnProps.concat(objectPrototypeProps));

    function getObjectPrototypeProps(source: any, sourceCurrentAncestorPrototype: any): (keyof T & string)[] {
        let result: string[] = [];

        if (sourceCurrentAncestorPrototype != Object.prototype) {
            result = result.concat(
                Object.keys(Object.getOwnPropertyDescriptors(sourceCurrentAncestorPrototype)).filter(
                    key => !excludeKeys?.includes(<keyof T>key) && typeof source[key] != 'function'
                )
            );

            if (Object.getPrototypeOf(sourceCurrentAncestorPrototype) != Object.prototype) {
                result = result.concat(
                    getObjectPrototypeProps(source, Object.getPrototypeOf(sourceCurrentAncestorPrototype))
                );
            }
        }

        return <(keyof T & string)[]>result;
    }
}

export function writableKeys<T extends object>(
    source: T,
    ignorePrivate: boolean = true,
    excludeKeys?: (keyof T)[]
): (keyof T & string)[] {
    return keys(source, ignorePrivate, excludeKeys).filter(key => {
        return isWritableKey<T>(source, key);
    });
}

export function isWritableKey<T extends object>(source: T, key: string) {
    const targetKeyPropertyDescriptor = getPropertyDescriptor(source, key);
    return targetKeyPropertyDescriptor == null || targetKeyPropertyDescriptor?.writable == true;
}

export function dictionaryMapTo<TSource, TTarget>(
    source: Dictionary<TSource>,
    mapCallback: (item: TSource) => TTarget
): Dictionary<TTarget> {
    const result: Dictionary<TTarget> = {};
    Object.keys(source).forEach(key => {
        result[key] = mapCallback((<any>source)[key]);
    });
    return result;
}

/**
 * Convert an instance object of a class to a pure object. All getter/setter become a normal property
 */
export function toPlainObj<T>(source: T, ignorePrivate: boolean = true, onlyKeysExistInPartialObject?: object): any {
    if (source == undefined) return undefined;
    if (typeof source != 'object' || source instanceof Date || source instanceof Time || source instanceof File)
        return source;
    if (source instanceof Array) {
        return source.map(p => toPlainObj(p, ignorePrivate));
    }
    const objResult: Dictionary<any> = {};
    keys(source, ignorePrivate).forEach(key => {
        if (onlyKeysExistInPartialObject == undefined || (<any>onlyKeysExistInPartialObject)[key] !== undefined)
            objResult[key] = toPlainObj((<any>source)[key], ignorePrivate);
    });
    return objResult;
}

export function clone<T>(value: T, updateClonedValueAction?: (clonedValue: T) => undefined | T | void): T {
    if (value == undefined) return value;

    let clonedValue = lodashClone(value);

    if (updateClonedValueAction != undefined) {
        const updatedClonedValue = updateClonedValueAction(clonedValue);
        if (updatedClonedValue != undefined) {
            clonedValue = updatedClonedValue as NonNullable<T>;
        }
    }
    return clonedValue;
}

export type ImmutableUpdateOptions = {
    updaterNotDeepMutate?: boolean;
    checkDiff?: false | true | 'deepCheck';
    maxDeepLevel?: number;
};

export type ImmutableUpdateUpdaterFn<TObject extends object> = (
    state: TObject
) => void | PartialDeep<TObject> | Partial<TObject>;

export function immutableUpdate<TObject extends object>(
    targetObj: TObject,
    partialStateOrUpdaterFn: PartialDeep<TObject> | Partial<TObject> | ImmutableUpdateUpdaterFn<TObject>,
    options?: ImmutableUpdateOptions
): TObject {
    const checkDiff = options?.checkDiff == undefined ? true : options.checkDiff;
    const maxDeepLevel = options?.maxDeepLevel;

    const clonedObj = clone(targetObj);
    let stateChanged: boolean | undefined;

    if (typeof partialStateOrUpdaterFn == 'object') {
        stateChanged = assignDeep(clonedObj, <object>partialStateOrUpdaterFn, checkDiff, maxDeepLevel);
    }

    if (typeof partialStateOrUpdaterFn == 'function') {
        const clonedDeepState = options?.updaterNotDeepMutate == true ? undefined : cloneDeep(targetObj);

        const finalChooseCloneObjToUpdateData = clonedDeepState ?? clonedObj;

        // Explain: To check the function has deep mutated the state object or not, we need to clone the state object to allow mutation at only first level.
        // Clone deep object to compare with the cloned object to check the deep mutation
        const clonedLocalDevToCheckStateMutation =
            options?.updaterNotDeepMutate == true && PLATFORM_CORE_GLOBAL_ENV.isLocalDev
                ? clone(finalChooseCloneObjToUpdateData)
                : undefined;
        const clonedLocalDevToCheckStateMutationJson =
            options?.updaterNotDeepMutate == true && PLATFORM_CORE_GLOBAL_ENV.isLocalDev
                ? JSON.stringify(toPlainObj(clonedLocalDevToCheckStateMutation))
                : undefined;

        const updatedStateResult = (<ImmutableUpdateUpdaterFn<TObject>>partialStateOrUpdaterFn)(
            finalChooseCloneObjToUpdateData
        );

        // toPlainObj before check different to avoid case object has get property auto update value
        if (
            PLATFORM_CORE_GLOBAL_ENV.isLocalDev &&
            clonedLocalDevToCheckStateMutationJson != JSON.stringify(toPlainObj(clonedLocalDevToCheckStateMutation))
        ) {
            const msg =
                '[DEV_ERROR] Your function has deep mutated the state object. Please use immutable update function to update the state object. See CONSOLE LOG for more detail.';

            alert(msg);
            console.error(
                `[ClonedLocalDevToCheckStateMutation Mutated Value]:\n${JSON.stringify(
                    clonedLocalDevToCheckStateMutation
                )}\n###\n[ClonedLocalDevToCheckStateMutationJson Original Value]:\n${clonedLocalDevToCheckStateMutationJson}`
            );
            throw new Error(msg);
        }

        // Case immutable update function return new state object, then use it as the updated state
        if (options?.updaterNotDeepMutate == true) {
            return updatedStateResult != undefined &&
                updatedStateResult instanceof Object &&
                (<object>updatedStateResult).constructor == clonedObj.constructor
                ? <TObject>updatedStateResult
                : clonedObj;
        } else if (clonedDeepState != undefined) {
            if (updatedStateResult != undefined) {
                // Case the partialStateOrUpdaterFn return partial updated props object
                stateChanged = assignDeep(
                    clonedObj,
                    <object>updatedStateResult,
                    checkDiff == true ? 'deepCheck' : checkDiff, // Should deep check for case partialStateOrUpdaterFn is function because of clone deep
                    maxDeepLevel
                );
            } else {
                // Case the partialStateOrUpdaterFn edit the object state directly.
                // Then the clonnedDeepState is actual an updated result, use it to update the clonedState
                stateChanged = assignDeep(
                    clonedObj,
                    <object>clonedDeepState,
                    checkDiff == true ? 'deepCheck' : checkDiff, // Should deep check for case partialStateOrUpdaterFn is function because of clone deep
                    maxDeepLevel
                );
            }
        }
    }

    return stateChanged != false ? clonedObj : targetObj;
}

export function cloneWithNewValues<T extends object>(value: T, newValues: T | Partial<T>): T {
    if (value == undefined) return value;
    const clonedValue = lodashClone(value);
    Object.keys(newValues).forEach(newValueKey => {
        (<any>clonedValue)[newValueKey] = (<any>newValues)[newValueKey];
    });
    return clonedValue;
}

export function cloneDeep<T>(
    value: T,
    deepLevel?: number,
    updateClonedValueAction?: (clonedValue: T) => undefined | T | void
): T {
    if (value == undefined || typeof value != 'object') return value;

    let clonedValue = value;

    if (deepLevel == undefined) clonedValue = lodashCloneDeep(value);
    else {
        clonedValue = clone(value);
        cloneInsideRecursively(clonedValue, deepLevel);
    }

    if (updateClonedValueAction != undefined) {
        const updatedClonedValue = updateClonedValueAction(clonedValue);
        if (updatedClonedValue != undefined) {
            clonedValue = <any>updatedClonedValue;
        }
    }

    return clonedValue;

    function cloneInsideRecursively(source: any, deepLevel: number, currentDeepLevel: number = 1) {
        if (typeof source != 'object' || currentDeepLevel > deepLevel) return;
        keys(source).forEach(key => {
            source[key] = lodashClone(source[key]);
            cloneInsideRecursively(source[key], deepLevel, currentDeepLevel + 1);
        });
    }
}

export function getDictionaryKeys<T extends string | number>(object?: Dictionary<any>): T[] {
    return lodashKeys(object).map((key: any) => <T>(!isNaN(key) ? parseInt(key) : key));
}

export function values<T>(object?: Dictionary<T> | ArrayLike<T> | undefined): T[] {
    return lodashValues(object);
}

export function isDifferent<T>(value1: T, value2: T, shallowCheckFirstLevel: boolean = false) {
    if (value1 == undefined && value2 == undefined) return false;
    if (value1 == undefined && value2 != undefined) return true;
    if (value1 != undefined && value2 == undefined) return true;
    if (typeof value1 != 'object' && typeof value2 != 'object') {
        return value1 != value2;
    }
    if (value1 instanceof Array && value2 instanceof Array && value1.length != value2.length) {
        return true;
    }
    if (value1 instanceof Date && value2 instanceof Date) {
        return value1.getTime() != value2.getTime();
    }
    if (value1 instanceof Set && value2 instanceof Set) {
        return JSON.stringify(Array.from(value1)) != JSON.stringify(Array.from(value2));
    }

    const value1Keys = keys(<any>value1);
    const value2Keys = keys(<any>value2);

    if (value1Keys.length != value2Keys.length) return true;
    if (shallowCheckFirstLevel) {
        return any(value1Keys, value1Key => {
            if ((<any>value1)[value1Key] == (<any>value2)[value1Key]) return false;
            if (typeof (<any>value1)[value1Key] != 'object' && typeof (<any>value2)[value1Key] != 'object') return true;
            return JSON.stringify((<any>value1)[value1Key]) != JSON.stringify((<any>value2)[value1Key]);
        });
    }

    const result = JSON.stringify(value1) != JSON.stringify(value2);

    return result;
}

export function changedKeys<T>(value1: T, value2: T): (keyof T)[] {
    const keys = union(lodashKeys(value1), lodashKeys(value2));
    return <(keyof T)[]>filter(keys, function (key: string) {
        return isDifferent((<any>value1)[key], (<any>value2)[key]);
    });
}

export function boxingFn<T>(fn?: (...args: any[]) => T, ...fnArgs: any[]) {
    return () => {
        return fn != undefined ? fn(fnArgs) : undefined;
    };
}

export function assign<T extends object>(target: T, ...sources: Partial<T>[]): T {
    sources.forEach(source => {
        keys(source).forEach(sourceKey => {
            if ((<any>source)[sourceKey] !== undefined) (<any>target)[sourceKey] = (<any>source)[sourceKey];
        });
    });

    return target;
}

export function extend<T extends object>(target: T, ...sources: Partial<T>[]): T {
    sources.forEach(source => {
        keys(source).forEach(sourceKey => {
            if ((<any>target)[sourceKey] == undefined && (<any>source)[sourceKey] !== undefined)
                (<any>target)[sourceKey] = (<any>source)[sourceKey];
        });
    });

    return target;
}

export function assignDeep<T extends object>(
    target: T,
    source: T,
    checkDiff: false | true | 'deepCheck' = false,
    maxDeepLevel?: number
): boolean | undefined {
    return assignOrSetDeep(target, source, false, false, checkDiff, maxDeepLevel);
}

export function setDeep<T extends object>(
    target: T,
    source: T,
    checkDiff: false | true | 'deepCheck' = false,
    maxDeepLevel?: number
): boolean | undefined {
    return assignOrSetDeep(target, source, false, true, checkDiff, maxDeepLevel);
}

export function getCurrentMissingItems<T>(prevValue: Dictionary<T>, currentValue: Dictionary<T>): T[] {
    return keys(prevValue)
        .filter(key => {
            return prevValue[key] != undefined && currentValue[key] == undefined;
        })
        .map(key => prevValue[key]!);
}

export function removeProps(obj: object, filterProp: (propValue: any) => boolean) {
    const result = Object.assign({}, obj);
    keys(obj).forEach(key => {
        if (filterProp((<any>obj)[key])) delete (<any>result)[key];
    });
    return result;
}

export function getPropertyDescriptor(obj: object, prop: string): PropertyDescriptor | undefined {
    if (obj == null || typeof obj != 'object') return undefined;

    if (Object.getPrototypeOf(obj) == Object.prototype) {
        return Object.getOwnPropertyDescriptor(obj, prop);
    }

    return Object.getOwnPropertyDescriptor(obj, prop) ?? getPropertyDescriptor(Object.getPrototypeOf(obj), prop);
}

export function cleanFalsyValueProps<T>(obj: T) {
    if (obj != null && typeof obj == 'object') {
        const objKeys = Object.keys(obj);
        for (const key of objKeys) {
            if ((<any>obj)[key] != null) {
                // eslint-disable-next-line no-param-reassign
                delete (<any>obj)[key];
            }
        }
    }

    return obj;
}

export function removeNullProps<T>(obj: T): T {
    if (obj != null && typeof obj == 'object') {
        const objKeys = Object.keys(obj);
        for (const key of objKeys) {
            if ((<any>obj)[key] == null) {
                // eslint-disable-next-line no-param-reassign
                delete (<any>obj)[key];
            }
        }
    }

    return obj;
}

// Do assign deep props in object
// SetDeep mean that make target object number of prop values same as number of source value props <=>
// makeTargetValuesSameSourceValues = true
function assignOrSetDeep<T extends object>(
    target: T,
    source: T,
    cloneSource: boolean = false,
    makeTargetValuesSameSourceValues: boolean = false,
    checkDiff: false | true | 'deepCheck' = false,
    maxDeepLevel?: number,
    currentDeepLevel?: number
): boolean | undefined {
    let hasDataChanged: boolean | undefined = undefined;
    currentDeepLevel ??= 1;

    if (target instanceof Array && source instanceof Array) {
        return assignOrSetDeepArray(
            target,
            source,
            cloneSource,
            makeTargetValuesSameSourceValues,
            checkDiff,
            maxDeepLevel,
            currentDeepLevel
        );
    } else {
        if (checkDiff != false) hasDataChanged = false;
        if (makeTargetValuesSameSourceValues) removeTargetKeysNotInSource(target, source);

        // create plainObjTarget to checkDiff, not use the target directly because when target is updated
        // other prop may be updated to via setter of the object, then the check diff will not be correct
        // clone toPlainObj to keep original target value
        const cloneOrPlainObjTarget =
            checkDiff === true ? clone(target) : checkDiff == 'deepCheck' ? toPlainObj(target, true, source) : null;

        keys(source).forEach(key => {
            const targetKeyPropertyDescriptor = getPropertyDescriptor(target, key);
            if (
                (targetKeyPropertyDescriptor?.get != null && targetKeyPropertyDescriptor?.set == null) ||
                targetKeyPropertyDescriptor?.writable == false
            )
                return;

            if (
                (checkDiff === true && cloneOrPlainObjTarget[key] == (<any>source)[key]) ||
                (checkDiff === 'deepCheck' &&
                    !isDifferent(cloneOrPlainObjTarget[key], toPlainObj((<any>source)[key], true)))
            )
                return;

            setNewValueToTargetKeyProp(key, <number>currentDeepLevel);
            hasDataChanged = true;
        });
    }

    return hasDataChanged;

    function setNewValueToTargetKeyProp(key: keyof T & string, currentDeepLevel: number) {
        let newValueToSetToTarget = cloneSource ? cloneDeep((<any>source)[key]) : (<any>source)[key];

        // if value is object and not special object like Date, Time, etc ... so we could set deep for the value
        if (
            checkTwoValueShouldSetDirectlyAndNotSetDeep((<any>target)[key], (<any>source)[key]) == false &&
            (maxDeepLevel == undefined || currentDeepLevel + 1 <= maxDeepLevel)
        ) {
            // If setter exist, we need to clone deep the target prop value and set deep it to create
            // a new value which has been set deep to trigger setter of the child props or array item
            // which then use it as a new value to set to the target
            // If setter not exist, we could just shallow clone the target prop object so that when set deep,
            // we could just set deep the inner object values and combine if checkDiff, only inner prop of the target
            // key object has value changed will be set
            newValueToSetToTarget = clone((<any>target)[key]);

            if ((<any>target)[key] instanceof Array && (<any>source)[key] instanceof Array) {
                assignOrSetDeepArray(
                    newValueToSetToTarget,
                    (<any>source)[key],
                    cloneSource,
                    makeTargetValuesSameSourceValues,
                    checkDiff,
                    maxDeepLevel,
                    currentDeepLevel + 1
                );
            } else {
                assignOrSetDeep(
                    newValueToSetToTarget,
                    (<any>source)[key],
                    cloneSource,
                    makeTargetValuesSameSourceValues,
                    checkDiff,
                    maxDeepLevel,
                    currentDeepLevel + 1
                );
            }
        }

        // Always to set to trigger setter of the object is existing
        (<any>target)[key] = newValueToSetToTarget;
    }

    function checkTwoValueShouldSetDirectlyAndNotSetDeep(targetValue: unknown, sourceValue: unknown) {
        return (
            targetValue == undefined ||
            sourceValue == undefined ||
            typeof targetValue != 'object' ||
            typeof sourceValue != 'object' ||
            sourceValue instanceof Date ||
            sourceValue instanceof Time
        );
    }

    function assignOrSetDeepArray(
        targetArray: any[],
        sourceArray: any[],
        cloneSource: boolean = false,
        makeTargetValuesSameSourceValues: boolean = false,
        checkDiff: false | true | 'deepCheck' = false,
        maxDeepLevel?: number,
        currentDeepLevel: number = 1
    ): boolean {
        let hasDataChanged = false;

        if (targetArray.length > sourceArray.length) {
            targetArray.splice(sourceArray.length);
            hasDataChanged = true;
        }

        for (let i = 0; i < sourceArray.length; i++) {
            if (targetArray.length <= i) {
                targetArray.push(sourceArray[i]);
                hasDataChanged = true;
                continue;
            }
            if (checkDiff === true && targetArray[i] == sourceArray[i]) continue;
            if (checkDiff === 'deepCheck' && !isDifferent(targetArray[i], sourceArray[i])) continue;

            if (hasDataChanged == false) hasDataChanged = isDifferent(targetArray[i], sourceArray[i]);

            if (
                checkTwoValueShouldSetDirectlyAndNotSetDeep(targetArray[i], sourceArray[i]) ||
                (maxDeepLevel != null && currentDeepLevel + 1 > maxDeepLevel)
            ) {
                targetArray[i] = cloneSource ? cloneDeep(sourceArray[i]) : sourceArray[i];
            } else {
                targetArray[i] = clone(targetArray[i], clonedTargetArrayItem => {
                    assignOrSetDeep(
                        clonedTargetArrayItem,
                        sourceArray[i],
                        cloneSource,
                        makeTargetValuesSameSourceValues,
                        checkDiff,
                        maxDeepLevel,
                        currentDeepLevel + 1
                    );
                });
            }
        }

        return hasDataChanged;
    }
}

function removeTargetKeysNotInSource<T extends object>(target: T, source: T): any[] | void {
    if (target == undefined || source == undefined) return;

    if (target instanceof Array && source instanceof Array) {
        return target.slice(0, source.length);
    } else {
        const targetKeys = keys(target);
        const sourceKeys = keys(source);

        targetKeys.forEach(key => {
            if (sourceKeys.indexOf(key) < 0) delete (<any>target)[key];
        });
    }
}

export class ValueWrapper<TValue> {
    constructor(public value: TValue) {}

    public map<TMapValue>(func: (value: TValue) => TMapValue | ValueWrapper<TMapValue>): ValueWrapper<TMapValue> {
        const funcValue = func(this.value);
        if (funcValue instanceof ValueWrapper) {
            return new ValueWrapper(funcValue.value);
        }
        return new ValueWrapper(funcValue);
    }
}

export function pipe<T>(input: T): T;
export function pipe<T, TResult1>(input: T, fns: [(arg: T) => TResult1]): TResult1;
export function pipe<T, TResult1, TResult2>(
    input: T,
    fns: [(arg: T) => TResult1, (arg: TResult1) => TResult2]
): TResult2;
export function pipe<T, TResult1, TResult2, TResult3>(
    input: T,
    fns: [(arg: T) => TResult1, (arg: TResult1) => TResult2, (arg: TResult2) => TResult3]
): TResult3;
export function pipe<T, TResult1, TResult2, TResult3, TResult4>(
    input: T,
    fns: [(arg: T) => TResult1, (arg: TResult1) => TResult2, (arg: TResult2) => TResult3, (arg: TResult3) => TResult4]
): TResult4;
export function pipe<T, TResult1, TResult2, TResult3, TResult4, TResult5>(
    input: T,
    fns: [
        (arg: T) => TResult1,
        (arg: TResult1) => TResult2,
        (arg: TResult2) => TResult3,
        (arg: TResult3) => TResult4,
        (arg: TResult4) => TResult5
    ]
): TResult5;
export function pipe<T, TResult1, TResult2, TResult3, TResult4, TResult5, TResult6>(
    input: T,
    fns: [
        (arg: T) => TResult1,
        (arg: TResult1) => TResult2,
        (arg: TResult2) => TResult3,
        (arg: TResult3) => TResult4,
        (arg: TResult4) => TResult5,
        (arg: TResult5) => TResult6
    ]
): TResult6;
export function pipe<T, TResult1, TResult2, TResult3, TResult4, TResult5, TResult6, TResult7>(
    input: T,
    fns: [
        (arg: T) => TResult1,
        (arg: TResult1) => TResult2,
        (arg: TResult2) => TResult3,
        (arg: TResult3) => TResult4,
        (arg: TResult4) => TResult5,
        (arg: TResult5) => TResult6,
        (arg: TResult6) => TResult7
    ]
): TResult7;
export function pipe<T, TResult1, TResult2, TResult3, TResult4, TResult5, TResult6, TResult7, TResult8>(
    input: T,
    fns: [
        (arg: T) => TResult1,
        (arg: TResult1) => TResult2,
        (arg: TResult2) => TResult3,
        (arg: TResult3) => TResult4,
        (arg: TResult4) => TResult5,
        (arg: TResult5) => TResult6,
        (arg: TResult6) => TResult7,
        (arg: TResult7) => TResult8
    ]
): TResult8;
export function pipe<T, TResult1, TResult2, TResult3, TResult4, TResult5, TResult6, TResult7, TResult8, TResult9>(
    input: T,
    fns: [
        (arg: T) => TResult1,
        (arg: TResult1) => TResult2,
        (arg: TResult2) => TResult3,
        (arg: TResult3) => TResult4,
        (arg: TResult4) => TResult5,
        (arg: TResult5) => TResult6,
        (arg: TResult6) => TResult7,
        (arg: TResult7) => TResult8,
        (arg: TResult8) => TResult9
    ]
): TResult9;
export function pipe<
    T,
    TResult1,
    TResult2,
    TResult3,
    TResult4,
    TResult5,
    TResult6,
    TResult7,
    TResult8,
    TResult9,
    TResult10
>(
    input: T,
    fns: [
        (arg: T) => TResult1,
        (arg: TResult1) => TResult2,
        (arg: TResult2) => TResult3,
        (arg: TResult3) => TResult4,
        (arg: TResult4) => TResult5,
        (arg: TResult5) => TResult6,
        (arg: TResult6) => TResult7,
        (arg: TResult7) => TResult8,
        (arg: TResult8) => TResult9,
        (arg: TResult9) => TResult10
    ]
): TResult10;
// General implementation
export function pipe(input: any, fns?: Array<(arg: any) => any>): any {
    if (fns == undefined) return input;
    return fns.reduce((acc, fn) => fn(acc), input);
}

export function pipeAction<T>(action: (input: T) => void): (input: T) => T {
    return (input: T): T => {
        action(input); // Perform the side effect
        return input; // Return the original object
    };
}

export function pipeActionIf<T>(condition: boolean, action: (input: T) => void): (input: T) => T;
export function pipeActionIf<T>(conditionCheckFn: (input: T) => boolean, action: (input: T) => void): (input: T) => T;
export function pipeActionIf<T>(
    conditionOrFn: boolean | ((input: T) => boolean),
    action: (input: T) => void
): (input: T) => T {
    return (input: T): T => {
        const condition = typeof conditionOrFn === 'boolean' ? conditionOrFn : conditionOrFn(input);
        if (condition) action(input); // Perform the side effect
        return input; // Return the original object
    };
}

export function combine<TResult1>(fn1: () => TResult1): [TResult1];
export function combine<TResult1, TResult2>(fn1: () => TResult1, fn2: () => TResult2): [TResult1, TResult2];
export function combine<TResult1, TResult2, TResult3>(
    fn1: () => TResult1,
    fn2: () => TResult2,
    fn3: () => TResult3
): [TResult1, TResult2, TResult3];
export function combine<TResult1, TResult2, TResult3, TResult4>(
    fn1: () => TResult1,
    fn2: () => TResult2,
    fn3: () => TResult3,
    fn4: () => TResult4
): [TResult1, TResult2, TResult3, TResult4];
export function combine<TResult1, TResult2, TResult3, TResult4, TResult5>(
    fn1: () => TResult1,
    fn2: () => TResult2,
    fn3: () => TResult3,
    fn4: () => TResult4,
    fn5: () => TResult5
): [TResult1, TResult2, TResult3, TResult4, TResult5];
export function combine<TResult1, TResult2, TResult3, TResult4, TResult5, TResult6>(
    fn1: () => TResult1,
    fn2: () => TResult2,
    fn3: () => TResult3,
    fn4: () => TResult4,
    fn5: () => TResult5,
    fn6: () => TResult6
): [TResult1, TResult2, TResult3, TResult4, TResult5, TResult6];
export function combine<TResult1, TResult2, TResult3, TResult4, TResult5, TResult6, TResult7>(
    fn1: () => TResult1,
    fn2: () => TResult2,
    fn3: () => TResult3,
    fn4: () => TResult4,
    fn5: () => TResult5,
    fn6: () => TResult6,
    fn7: () => TResult7
): [TResult1, TResult2, TResult3, TResult4, TResult5, TResult6, TResult7];
export function combine<TResult1, TResult2, TResult3, TResult4, TResult5, TResult6, TResult7, TResult8>(
    fn1: () => TResult1,
    fn2: () => TResult2,
    fn3: () => TResult3,
    fn4: () => TResult4,
    fn5: () => TResult5,
    fn6: () => TResult6,
    fn7: () => TResult7,
    fn8: () => TResult8
): [TResult1, TResult2, TResult3, TResult4, TResult5, TResult6, TResult7, TResult8];
export function combine<TResult1, TResult2, TResult3, TResult4, TResult5, TResult6, TResult7, TResult8, TResult9>(
    fn1: () => TResult1,
    fn2: () => TResult2,
    fn3: () => TResult3,
    fn4: () => TResult4,
    fn5: () => TResult5,
    fn6: () => TResult6,
    fn7: () => TResult7,
    fn8: () => TResult8,
    fn9: () => TResult9
): [TResult1, TResult2, TResult3, TResult4, TResult5, TResult6, TResult7, TResult8, TResult9];
export function combine<
    TResult1,
    TResult2,
    TResult3,
    TResult4,
    TResult5,
    TResult6,
    TResult7,
    TResult8,
    TResult9,
    TResult10
>(
    fn1: () => TResult1,
    fn2: () => TResult2,
    fn3: () => TResult3,
    fn4: () => TResult4,
    fn5: () => TResult5,
    fn6: () => TResult6,
    fn7: () => TResult7,
    fn8: () => TResult8,
    fn9: () => TResult9,
    fn10: () => TResult10
): [TResult1, TResult2, TResult3, TResult4, TResult5, TResult6, TResult7, TResult8, TResult9, TResult10];
// General implementation
export function combine(...fns: Array<() => any>): any[] {
    return fns.map(fn => fn());
}

export function objectItems<TKey extends string | number | symbol, TValue>(
    source: Record<TKey, TValue>
): DictionaryItem<TKey, TValue>[] {
    return Object.keys(source).map(key => <DictionaryItem<TKey, TValue>>{ key: key, value: source[<TKey>key]! });
}
