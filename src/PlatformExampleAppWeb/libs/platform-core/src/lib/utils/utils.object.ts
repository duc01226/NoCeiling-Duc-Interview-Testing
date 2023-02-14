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
  if (typeof source != 'object') return [];

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

  let clonedValue = <T>lodashClone(value);

  if (updateClonedValueAction != undefined) {
    const updatedClonedValue = updateClonedValueAction(clonedValue);
    if (updatedClonedValue != undefined) {
      clonedValue = updatedClonedValue as T;
    }
  }
  return clonedValue;
}

export function immutableUpdate<TObject extends object>(
  targetObj: TObject,
  partialStateOrUpdaterFn:
    | PartialDeep<TObject>
    | Partial<TObject>
    | ((state: TObject) => void | PartialDeep<TObject> | Partial<TObject>),
  deepItemInArray: boolean = false
): TObject {
  const clonedObj = clone(targetObj);
  let stateChanged = false;

  if (typeof partialStateOrUpdaterFn == 'object') {
    stateChanged = assignDeep(clonedObj, <object>partialStateOrUpdaterFn, 'deepCheck', deepItemInArray);
  }

  if (typeof partialStateOrUpdaterFn == 'function') {
    const clonnedDeepState = cloneDeep(targetObj);

    const updatedStateResult = partialStateOrUpdaterFn(clonnedDeepState);

    if (updatedStateResult != undefined) {
      // Case the partialStateOrUpdaterFn return partial updated props object
      stateChanged = assignDeep(clonedObj, <object>updatedStateResult, 'deepCheck', deepItemInArray);
    } else {
      // Case the partialStateOrUpdaterFn edit the object state directly.
      // Then the clonnedDeepState is actual an updated result, use it to update the clonedState
      stateChanged = assignDeep(clonedObj, <object>clonnedDeepState, 'deepCheck', deepItemInArray);
    }
  }

  return stateChanged ? clonedObj : targetObj;
}

export function cloneWithNewValues<T extends object>(value: T, newValues: T | Partial<T>): T {
  if (value == undefined) return value;
  const clonedValue = <T>lodashClone(value);
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
  checkDiff: false | true | 'deepCheck' = false,
  deepItemInArray: boolean = false
): boolean {
  return mapObject(target, source, false, false, checkDiff, deepItemInArray);
}

export function setDeep<T extends object>(
  target: T,
  source: T,
  checkDiff: false | true | 'deepCheck' = false,
  deepItemInArray: boolean = false
): boolean {
  return mapObject(target, source, false, true, checkDiff, deepItemInArray);
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
  return (
    Object.getOwnPropertyDescriptor(obj, prop) ?? Object.getOwnPropertyDescriptor(Object.getPrototypeOf(obj), prop)
  );
}

export function removeNullProps<T>(obj: T): T {
  if (obj == null || typeof obj !== 'object') {
    return obj;
  }
  const objKeys = Object.keys(obj);
  for (const key of objKeys) {
    if ((<any>obj)[key] == null) {
      // eslint-disable-next-line no-param-reassign
      delete (<any>obj)[key];
    }
  }
  return obj;
}

function mapObject<T extends object>(
  target: T,
  source: T,
  cloneSource: boolean = false,
  makeTargetValuesSameSourceValues: boolean = false,
  checkDiff: false | true | 'deepCheck' = false,
  deepItemInArray: boolean = false
): boolean {
  let hasDataChanged = false;

  if (target instanceof Array && source instanceof Array) {
    return mapArray(target, source, cloneSource, makeTargetValuesSameSourceValues, checkDiff);
  } else {
    if (makeTargetValuesSameSourceValues) removeTargetKeysNotInSource(target, source);
    const sourceKeys = Object.keys(source);
    sourceKeys.forEach(key => {
      if (
        getPropertyDescriptor(target, key)?.writable == false ||
        (getPropertyDescriptor(target, key)?.get != null && getPropertyDescriptor(target, key)?.set == null)
      )
        return;
      if (checkDiff === true && (<any>target)[key] == (<any>source)[key]) return;
      if (checkDiff === 'deepCheck' && !isDifferent((<any>target)[key], (<any>source)[key])) return;

      if (
        mapObjectCheckTwoValueCanSetDirectly((<any>target)[key], (<any>source)[key]) ||
        getPropertyDescriptor(target, key)?.set != null
      ) {
        (<any>target)[key] = cloneSource ? cloneDeep((<any>source)[key]) : (<any>source)[key];
        hasDataChanged = true;
      } else {
        (<any>target)[key] = clone((<any>target)[key]);

        if ((<any>target)[key] instanceof Array && (<any>source)[key] instanceof Array) {
          if (deepItemInArray) {
            hasDataChanged = mapArray(
              (<any>target)[key],
              (<any>source)[key],
              cloneSource,
              makeTargetValuesSameSourceValues,
              checkDiff
            );
          } else {
            (<any>target)[key] = cloneSource ? cloneDeep((<any>source)[key]) : (<any>source)[key];
            hasDataChanged = true;
          }
        } else {
          hasDataChanged = mapObject(
            (<any>target)[key],
            (<any>source)[key],
            cloneSource,
            makeTargetValuesSameSourceValues,
            checkDiff
          );
        }
      }
    });
  }

  return hasDataChanged;

  function mapObjectCheckTwoValueCanSetDirectly(targetValue: unknown, sourceValue: unknown) {
    return (
      targetValue == undefined ||
      sourceValue == undefined ||
      typeof targetValue != 'object' ||
      typeof sourceValue != 'object' ||
      sourceValue instanceof Date
    );
  }

  function mapArray(
    targetArray: any[],
    sourceArray: any[],
    cloneSource: boolean = false,
    makeTargetValuesSameSourceValues: boolean = false,
    checkDiff: false | true | 'deepCheck' = false
  ): boolean {
    let hasDataChanged = false;

    if (targetArray.length > sourceArray.length && makeTargetValuesSameSourceValues) {
      targetArray.splice(sourceArray.length);
    }

    for (let i = 0; i < sourceArray.length; i++) {
      if (checkDiff === true && targetArray[i] == sourceArray[i]) continue;
      if (checkDiff === 'deepCheck' && !isDifferent(targetArray[i], sourceArray[i])) continue;
      if (mapObjectCheckTwoValueCanSetDirectly(targetArray[i], sourceArray[i])) {
        targetArray[i] = cloneSource ? cloneDeep(sourceArray[i]) : sourceArray[i];
        hasDataChanged = true;
      } else {
        targetArray[i] = clone(targetArray[i], newTargetArrayItem => {
          hasDataChanged = mapObject(
            newTargetArrayItem,
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
