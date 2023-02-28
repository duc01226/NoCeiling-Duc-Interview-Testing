/* eslint-disable @typescript-eslint/no-unnecessary-type-constraint */
/* eslint-disable @typescript-eslint/no-explicit-any */
import {
  clone as lodashClone,
  cloneDeep as lodashCloneDeep,
  keys as lodashKeys,
  values as lodashValues
} from 'lodash-es';
import { PartialDeep } from 'type-fest';

import { Time } from '../common-types';
import { any } from './_common-functions';
import { list_distinct } from './utils.list';

export function keys<T extends object>(source: T, ignorePrivate: boolean = true): (keyof T & string)[] {
  if (typeof source != 'object' || source == null) return [];

  const objectOwnProps: (keyof T & string)[] = [];
  for (const key in source) {
    if (typeof (<any>source)[key] != 'function' && (ignorePrivate == false || !key.startsWith('_'))) {
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
          key => typeof source[key] != 'function'
        )
      );

      if (Object.getPrototypeOf(sourceCurrentAncestorPrototype) != Object.prototype) {
        result = result.concat(getObjectPrototypeProps(source, Object.getPrototypeOf(sourceCurrentAncestorPrototype)));
      }
    }

    return <(keyof T & string)[]>result;
  }
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
export function toPlainObj<T>(source: T, ignorePrivate: boolean = true): any {
  if (source == undefined) return undefined;
  if (typeof source != 'object') return source;
  if (source instanceof Array) {
    return source.map(p => toPlainObj(p, ignorePrivate));
  }
  if (source instanceof Date || source instanceof Time) return source;
  const objResult: Dictionary<any> = {};
  keys(source, ignorePrivate).forEach(key => {
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

export function immutableUpdate<TObject extends object>(
  targetObj: TObject,
  partialStateOrUpdaterFn:
    | PartialDeep<TObject>
    | Partial<TObject>
    | ((state: TObject) => void | PartialDeep<TObject> | Partial<TObject>)
): TObject {
  const clonedObj = clone(targetObj);
  let stateChanged = false;

  if (typeof partialStateOrUpdaterFn == 'object') {
    stateChanged = assignDeep(clonedObj, <object>partialStateOrUpdaterFn, 'deepCheck');
  }

  if (typeof partialStateOrUpdaterFn == 'function') {
    const clonnedDeepState = cloneDeep(targetObj);

    const updatedStateResult = partialStateOrUpdaterFn(clonnedDeepState);

    if (updatedStateResult != undefined) {
      // Case the partialStateOrUpdaterFn return partial updated props object
      stateChanged = assignDeep(clonedObj, <object>updatedStateResult, 'deepCheck');
    } else {
      // Case the partialStateOrUpdaterFn edit the object state directly.
      // Then the clonnedDeepState is actual an updated result, use it to update the clonedState
      stateChanged = assignDeep(clonedObj, <object>clonnedDeepState, 'deepCheck');
    }
  }

  return stateChanged ? clonedObj : targetObj;
}

export function cloneWithNewValues<T extends object>(value: T, newValues: T | Partial<T>): T {
  if (value == undefined) return value;
  const clonedValue = lodashClone(value);
  Object.keys(newValues).forEach(newValueKey => {
    (<any>clonedValue)[newValueKey] = (<any>newValues)[newValueKey];
  });
  return clonedValue;
}

export function cloneDeep<T extends any>(
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
  return lodashKeys(object).map((key: any) => <T>(!isNaN(<any>key) ? parseInt(key) : key));
}

export function values<T>(object?: Dictionary<T> | ArrayLike<T> | undefined): T[] {
  return lodashValues(object);
}

export function isDifferent<T extends any>(value1: T, value2: T, shallowCheckFirstLevel: boolean = false) {
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
  const value1Keys = Object.keys(<any>value1);
  const value2Keys = Object.keys(<any>value2);
  if (value1Keys.length != value2Keys.length) return true;
  if (shallowCheckFirstLevel) {
    return any(value1Keys, value1Key => {
      if ((<any>value1)[value1Key] == (<any>value2)[value1Key]) return false;
      if (typeof (<any>value1)[value1Key] != 'object' && typeof (<any>value2)[value1Key] != 'object') return true;
      return JSON.stringify((<any>value1)[value1Key]) != JSON.stringify((<any>value2)[value1Key]);
    });
  }
  return JSON.stringify(value1) != JSON.stringify(value2);
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
  checkDiff: false | true | 'deepCheck' = false
): boolean {
  return assignOrSetDeep(target, source, false, false, checkDiff);
}

export function setDeep<T extends object>(
  target: T,
  source: T,
  checkDiff: false | true | 'deepCheck' = false
): boolean {
  return assignOrSetDeep(target, source, false, true, checkDiff);
}

export function getCurrentMissingItems<T>(prevValue: Dictionary<T>, currentValue: Dictionary<T>): T[] {
  return keys(prevValue)
    .filter(key => {
      return prevValue[key] != undefined && currentValue[key] == undefined;
    })
    .map(key => prevValue[key]);
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
// SetDeep mean that make target object number of prop values same as number of source value props <=> makeTargetValuesSameSourceValues = true
function assignOrSetDeep<T extends object>(
  target: T,
  source: T,
  cloneSource: boolean = false,
  makeTargetValuesSameSourceValues: boolean = false,
  checkDiff: false | true | 'deepCheck' = false
): boolean {
  let hasDataChanged = false;

  if (target instanceof Array && source instanceof Array) {
    return assignOrSetDeepArray(target, source, cloneSource, makeTargetValuesSameSourceValues, checkDiff);
  } else {
    if (makeTargetValuesSameSourceValues) removeTargetKeysNotInSource(target, source);

    // create plainObjTarget to checkDiff, not use the target directly because when target is updated
    // other prop may be updated to via setter of the object, then the check diff will not be correct
    // clone toPlainObj to keep original target value
    const cloneOrPlainObjTarget =
      checkDiff === true ? clone(target) : checkDiff == 'deepCheck' ? toPlainObj(target) : null;

    keys(source).forEach(key => {
      const targetKeyPropertyDescriptor = getPropertyDescriptor(target, key);
      if (
        (targetKeyPropertyDescriptor?.get != null && targetKeyPropertyDescriptor?.set == null) ||
        targetKeyPropertyDescriptor?.writable == false
      )
        return;

      if (
        (checkDiff === true && cloneOrPlainObjTarget[key] == (<any>source)[key]) ||
        (checkDiff === 'deepCheck' && !isDifferent(cloneOrPlainObjTarget[key], (<any>source)[key]))
      )
        return;

      setNewValueToTargetKeyProp(key);
      if (hasDataChanged == false) hasDataChanged = isDifferent(cloneOrPlainObjTarget[key], (<any>source)[key]);
    });
  }

  return hasDataChanged;

  function setNewValueToTargetKeyProp(key: keyof T & string) {
    let newValueToSetToTarget = cloneSource ? cloneDeep((<any>source)[key]) : (<any>source)[key];

    // if value is object and not special object like Date, Time, etc ... so we could set deep for the value
    if (checkTwoValueShouldSetDirectlyAndNotSetDeep((<any>target)[key], (<any>source)[key]) == false) {
      // If setter exist, we need to clone deep the target prop value and set deep it to create
      // a new value which has been set deep to trigger setter of the child props or array item
      // which then use it as a new value to set to the target
      // If setter not exist, we could just shallow clone the target prop object so that when set deep,
      // we could just set deep the inner object values and combine if checkDiff, only inner prop of the target key object
      // has value changed will be set
      newValueToSetToTarget =
        getPropertyDescriptor(target, key)?.set != null ? cloneDeep((<any>target)[key]) : clone((<any>target)[key]);

      if ((<any>target)[key] instanceof Array && (<any>source)[key] instanceof Array) {
        assignOrSetDeepArray(
          newValueToSetToTarget,
          (<any>source)[key],
          cloneSource,
          makeTargetValuesSameSourceValues,
          checkDiff
        );
      } else {
        assignOrSetDeep(
          newValueToSetToTarget,
          (<any>source)[key],
          cloneSource,
          makeTargetValuesSameSourceValues,
          checkDiff
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
    checkDiff: false | true | 'deepCheck' = false
  ): boolean {
    let hasDataChanged = false;

    if (targetArray.length > sourceArray.length) {
      targetArray.splice(sourceArray.length);
      hasDataChanged = true;
    }

    for (let i = 0; i < sourceArray.length; i++) {
      if (checkDiff === true && targetArray[i] == sourceArray[i]) continue;
      if (checkDiff === 'deepCheck' && !isDifferent(targetArray[i], sourceArray[i])) continue;

      if (hasDataChanged == false) hasDataChanged = isDifferent(targetArray[i], sourceArray[i]);

      if (checkTwoValueShouldSetDirectlyAndNotSetDeep(targetArray[i], sourceArray[i])) {
        targetArray[i] = cloneSource ? cloneDeep(sourceArray[i]) : sourceArray[i];
      } else {
        targetArray[i] = clone(targetArray[i], clonedTargetArrayItem => {
          assignOrSetDeep(
            clonedTargetArrayItem,
            sourceArray[i],
            cloneSource,
            makeTargetValuesSameSourceValues,
            checkDiff
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
