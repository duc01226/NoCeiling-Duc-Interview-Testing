import { clone, cloneDeep, keyBy } from 'lodash';

/* eslint-disable @typescript-eslint/no-unsafe-assignment */
/* eslint-disable @typescript-eslint/no-unsafe-return */
/* eslint-disable @typescript-eslint/no-explicit-any */
export class ObjectExtension {
  public static cloneDeep<T>(value: T): T {
    if (value == null) return value;
    return cloneDeep<T>(value);
  }

  public static clone<T>(value: T, updateClonedValueFn?: (clonedValue: T) => undefined | T | void): T {
    if (value == null) {
      return value;
    }
    let clonedValue: T = clone(value);
    if (updateClonedValueFn != null) {
      const updatedClonedValue: undefined | T | void = updateClonedValueFn(clonedValue);
      if (updatedClonedValue != null) {
        clonedValue = updatedClonedValue;
      }
    }
    return clonedValue;
  }

  public static toJsonObj<T>(source: T, ignorePrivate: boolean = true): T {
    if (source == undefined) return source;
    if (typeof source != 'object') return source;
    if (source instanceof Array) {
      return <any>source.map(p => ObjectExtension.toJsonObj(p, ignorePrivate));
    }
    if (source instanceof Date) return source;
    const objResult: any = {};
    ObjectExtension.keys(source, ignorePrivate).forEach(key => {
      objResult[key] = ObjectExtension.toJsonObj((<any>source)[key], ignorePrivate);
    });
    return objResult;
  }

  public static keys(source: any, ignorePrivate: boolean = true): string[] {
    if (typeof source != 'object') return [];
    const result: string[] = [];
    for (const key in source) {
      if (typeof source[key] != 'function' && (ignorePrivate == false || !key.startsWith('_'))) {
        if (key.startsWith('_')) {
          const publicKey = key.substring(1);
          if (!ignorePrivate) result.push(key);
          if (source[key] === source[publicKey]) {
            result.push(publicKey);
          }
        } else {
          result.push(key);
        }
      }
    }
    return result;
  }

  public static isDifferent<T>(value1: T, value2: T): boolean {
    if (value1 == null && value2 == null) {
      return false;
    }
    if (value1 == null && value2 != null) {
      return true;
    }
    if (value1 != null && value2 == null) {
      return true;
    }
    if (typeof value1 !== 'object' && typeof value2 !== 'object') {
      return value1 !== value2;
    }
    if (value1 instanceof Array && value2 instanceof Array) {
      if (value1.length !== value2.length) {
        return true;
      }
    }
    return JSON.stringify(value1) !== JSON.stringify(value2);
  }

  public static isEqual<T>(value1: T, value2: T): boolean {
    return !ObjectExtension.isDifferent(value1, value2);
  }

  public static removeNullProps<T>(obj: T): T {
    if (obj == null || typeof obj !== 'object') {
      return obj;
    }
    const objKeys = Object.keys(obj);
    for (let i = 0; i < objKeys.length; i += 1) {
      const key = objKeys[i];
      if ((<any>obj)[key] == null) {
        // eslint-disable-next-line no-param-reassign
        delete (<any>obj)[key];
      }
    }
    return obj;
  }

  public static toDictionary<T>(
    collection: ArrayLike<T> | undefined,
    dictionaryKeySelector: (item: T) => string | number
  ): Dictionary<T> {
    return keyBy(collection, dictionaryKeySelector);
  }

  /**
   * Assign deep all properties from source to target object
   */
  public static assignDeep<T>(target: T, source: T, cloneSource: boolean = false): T {
    return ObjectExtension.mapObject(target, source, cloneSource, false, false);
  }

  public static assign<T extends object>(target: T, ...sources: Partial<T>[]): T {
    sources.forEach(source => {
      ObjectExtension.keys(source).forEach(sourceKey => {
        if ((<any>source)[sourceKey] != null) {
          // Catch this to prevent can not set get only prop
          try {
            // eslint-disable-next-line no-param-reassign
            (<any>target)[sourceKey] = (<any>source)[sourceKey];
          } catch (error) {
            // Not throw error
          }
        }
      });
    });

    return target;
  }

  /**
   * Assign deep all properties from source to target object
   */
  private static mapObject<T extends any>(
    target: T,
    source: T,
    cloneSource: boolean = false,
    makeTargetValuesSameSourceValues: boolean = false,
    checkDiff: false | true | 'deepCheck' = false
  ) {
    if (target instanceof Array && source instanceof Array) {
      ObjectExtension.mapArray(target, source, cloneSource, makeTargetValuesSameSourceValues, checkDiff);
    } else {
      if (makeTargetValuesSameSourceValues) ObjectExtension.removeTargetKeysNotInSource(target, source);
      const sourceKeys = Object.keys(<any>source);
      sourceKeys.forEach(key => {
        let targetValue = (<any>target)[key];
        let sourceValue = (<any>source)[key];

        if (checkDiff === true && targetValue == sourceValue) return;
        if (checkDiff === 'deepCheck' && !ObjectExtension.isDifferent(targetValue, sourceValue)) return;

        if (ObjectExtension.mapObjectCheckTwoValueCanSetDirectly(targetValue, sourceValue)) {
          // eslint-disable-next-line no-param-reassign
          (<any>target)[key] = cloneSource ? ObjectExtension.cloneDeep(sourceValue) : sourceValue;
        } else {
          // eslint-disable-next-line no-param-reassign
          (<any>target)[key] = ObjectExtension.clone(targetValue);

          if (targetValue instanceof Array && sourceValue instanceof Array) {
            ObjectExtension.mapArray(
              (<any>target)[key],
              sourceValue,
              cloneSource,
              makeTargetValuesSameSourceValues,
              checkDiff
            );
          } else {
            ObjectExtension.mapObject(
              (<any>target)[key],
              sourceValue,
              cloneSource,
              makeTargetValuesSameSourceValues,
              checkDiff
            );
          }
        }
      });
    }
    return target;
  }

  private static mapArray<T>(
    targetArray: T[],
    sourceArray: T[],
    cloneSource: boolean = false,
    makeTargetValuesSameSourceValues: boolean = false,
    checkDiff: false | true | 'deepCheck' = false
  ) {
    if (targetArray.length > sourceArray.length && makeTargetValuesSameSourceValues) {
      targetArray.splice(sourceArray.length);
    }

    for (let i = 0; i < sourceArray.length; i += 1) {
      if (checkDiff === true && targetArray[i] == sourceArray[i]) continue;
      if (checkDiff === 'deepCheck' && !ObjectExtension.isDifferent(targetArray[i], sourceArray[i])) continue;
      if (ObjectExtension.mapObjectCheckTwoValueCanSetDirectly(targetArray[i], sourceArray[i])) {
        // eslint-disable-next-line no-param-reassign
        targetArray[i] = cloneSource ? ObjectExtension.cloneDeep(sourceArray[i]) : sourceArray[i];
      } else {
        // eslint-disable-next-line no-param-reassign
        targetArray[i] = ObjectExtension.clone(targetArray[i], newTargetArrayItem => {
          ObjectExtension.mapObject(
            newTargetArrayItem,
            sourceArray[i],
            cloneSource,
            makeTargetValuesSameSourceValues,
            checkDiff
          );
        });
      }
    }
  }

  private static removeTargetKeysNotInSource<T>(target: T, source: T): T {
    if (target == undefined || source == undefined) return target;
    if (target instanceof Array && source instanceof Array) {
      return <T>(<unknown>target.slice(0, source.length));
    }
    const targetKeys = ObjectExtension.keys(target);
    const sourceKeys = new Set(ObjectExtension.keys(source));

    targetKeys.forEach(targetKey => {
      // eslint-disable-next-line no-param-reassign
      if (!sourceKeys.has(targetKey)) delete (<any>target)[targetKey];
    });

    return target;
  }

  private static mapObjectCheckTwoValueCanSetDirectly(targetValue: unknown, sourceValue: unknown) {
    if (
      targetValue == null ||
      sourceValue == null ||
      typeof targetValue != 'object' ||
      typeof sourceValue != 'object' ||
      targetValue?.constructor != sourceValue?.constructor
    ) {
      return true;
    }

    return false;
  }
}
