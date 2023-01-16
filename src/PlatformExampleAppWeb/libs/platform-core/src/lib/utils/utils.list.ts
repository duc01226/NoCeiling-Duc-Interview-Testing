/* eslint-disable @typescript-eslint/no-explicit-any */
import {
  difference as lodashDifference,
  filter as lodashFilter,
  find as lodashFind,
  keyBy as lodashKeyBy,
  last as lodashLast,
  max as loadshMax,
  maxBy as lodashMaxBy,
  orderBy as lodashOrderBy,
  range as lodashRange,
  remove as lodashRemove,
  uniq as lodashUniq,
  uniqBy as lodashUniqBy
} from 'lodash-es';

import { any as privateAny } from './_common-functions';
import * as ObjectUtil from './utils.object';

export function list_find<T>(collection: T[] | undefined, predicate: (item: T) => boolean, fromIndex?: number): T | undefined {
  return lodashFind(collection, predicate, fromIndex);
}

export function list_get<T>(collection: T[] | undefined, predicate: (item: T) => boolean, fromIndex?: number): T {
  const result = lodashFind(collection, predicate, fromIndex);
  if (result == undefined) throw new Error(`Can't find any item`);
  return result;
}

export function list_findSelect<T, TSelect>(
  collection: T[] | undefined,
  predicate: (item: T) => boolean,
  selectCallback: (item: T) => TSelect,
  fromIndex?: number
): TSelect | undefined {
  const item = lodashFind(collection, predicate, fromIndex);
  return item != undefined ? selectCallback(item) : undefined;
}

export function list_filter<T>(collection: T[] | undefined, predicate: (item: T) => boolean): T[] {
  return lodashFilter(collection, predicate);
}

export function list_selectMany<T, S>(collection: T[] | undefined, selectCallback: (item: T) => S[]): S[] {
  if (collection == undefined || collection.length == 0) return [];
  const listOfChildList = collection.map(selectCallback);
  return listOfChildList.reduce((prevValue, currentValue) => prevValue.concat(currentValue));
}

export function list_max<T>(collection: ArrayLike<T> | undefined): T | undefined {
  return loadshMax(collection);
}

export function list_maxBy<T>(collection: ArrayLike<T> | undefined, iteratee: (item: T) => number): T | undefined {
  return lodashMaxBy(collection, iteratee);
}

export function list_toDictionary<T>(
  collection: ArrayLike<T> | undefined,
  dictionaryKeySelector: (item: T) => string | number
): Dictionary<T> {
  return lodashKeyBy(collection, dictionaryKeySelector) as Dictionary<T>;
}

export function list_toDictionarySelect<T, TSelect>(
  collection: T[] | undefined,
  dictionaryKeySelector: (item: T) => string | number,
  dictionaryValueSelector: (item: T) => TSelect
): Dictionary<TSelect> {
  if (collection == undefined) return {};

  const result: Dictionary<TSelect> = {};
  collection.forEach(item => {
    result[dictionaryKeySelector(item)] = dictionaryValueSelector(item);
  });
  return result;
}

export function list_includesAll<T>(superset: T[], subset: T[]): boolean {
  return lodashDifference(subset, superset).length === 0;
}

export function list_includesAny<T>(superset: T[], subset: T[]): boolean {
  for (let i = 0; i < subset.length; i++) {
    const subsetItem = subset[i];
    if (superset.indexOf(subsetItem) >= 0) return true;
  }
  return false;
}

export function list_all<T>(collection: ArrayLike<T> | undefined, predicate: (item: T) => boolean): boolean {
  return list_any(collection, (item: T) => !predicate(item)) ? false : true;
}

export function list_any<T>(collection: ArrayLike<T> | undefined, predicate: (item: T) => boolean): boolean {
  return privateAny(collection, predicate);
}

export function list_remove<T>(collection: T[], predicate: (item: T) => boolean): T[] {
  return lodashRemove(collection, predicate);
}

export function list_removeFirst<T>(collection: T[], predicate: (item: T) => boolean): T | undefined {
  let removedItem: T | undefined;
  for (let i = 0; i < collection.length; i++) {
    if (predicate(collection[i])) {
      removedItem = collection.splice(i, 1)[0];
    }
  }
  return removedItem;
}

export function list_removeMissedItems<T>(collection: T[], newCollection: T[], equalCallback: (item: T, newItem: T) => boolean) {
  return list_remove(collection, item => {
    return list_find(newCollection, newItem => equalCallback(item, newItem)) == undefined;
  });
}

export function list_add<T>(
  collection: T[] | undefined,
  addItem: T,
  condition?: (addItem: T, collection: ArrayLike<T> | undefined) => boolean
): T[] | undefined {
  if (collection == undefined) return collection;
  if (condition == undefined || condition(addItem, collection)) {
    collection.push(addItem);
    return collection;
  }
  return collection;
}

export function list_replaceOne<T>(collection: T[], replaceItem: T, condition: (item: T) => boolean): T[] {
  const clonedCollection = ObjectUtil.clone(collection);
  for (let i = 0; i < clonedCollection.length; i++) {
    if (condition(clonedCollection[i])) {
      clonedCollection[i] = replaceItem;
      return clonedCollection;
    }
  }
  return collection;
}

export function list_replaceMany<T>(collection: T[], replaceItems: T[], condition: (item: T, replaceItem: T) => boolean): T[] {
  const replacedItems: T[] = [];
  replaceItems = ObjectUtil.clone(replaceItems);
  for (let i = 0; i < collection.length; i++) {
    for (let j = 0; j < replaceItems.length; j++) {
      if (condition(collection[i], replaceItems[j])) {
        collection[i] = replaceItems[j];
        replacedItems.push(replaceItems[j]);
        replaceItems.splice(j, 1);
        break;
      }
    }
  }
  return replacedItems;
}

export function list_addOrReplace<T>(collection: T[] | undefined, item: T, replaceCondition: (item: T) => boolean): T[] | undefined {
  if (collection == undefined) return collection;
  for (let i = 0; i < collection.length; i++) {
    if (replaceCondition(collection[i])) {
      collection[i] = item;
      return collection;
    }
  }

  collection.push(item);
  return collection;
}

export function list_addIfNotExist<T>(collection: T[], addItems: T[], equalBy?: (item: T) => any): T[] {
  addItems.forEach(addItem => {
    if (list_find(collection, p => (equalBy != undefined ? equalBy(p) == equalBy(addItem) : p == addItem)) == undefined) {
      collection.push(addItem);
    }
  });
  return collection;
}

export function list_last<T>(collection: ArrayLike<T> | undefined): T | undefined {
  if (collection == undefined) return undefined;
  return lodashLast(collection);
}

export function list_distinct<T>(collection: ArrayLike<T>): T[] {
  return lodashUniq(collection);
}

export function list_distinctBy<T>(collection: ArrayLike<T>, iteratee: (value: T) => object | undefined): T[] {
  return lodashUniqBy(collection, iteratee);
}

export function list_orderBy<T>(
  collection: T[],
  iteratees: (value: T, index: number, collection: ArrayLike<T>) => any,
  desc: boolean = false
): T[] {
  return lodashOrderBy(collection, iteratees, desc ? 'desc' : 'asc');
}

export function list_concatAll<T>(...collection: T[][]): T[] {
  let result: T[] = [];
  collection.forEach(item => {
    result = result.concat(item);
  });
  return result;
}

export function list_flat<T>(value: T[][]): T[] {
  let result: T[] = [];
  value.forEach(x => {
    result = result.concat(x);
  });
  return result;
}

export function list_rightMerge<T>(currentCollection: T[], newCollection: T[], compareSelector: (item: T) => string | number): T[] {
  if (currentCollection.length == 0) return newCollection;
  const currentCollectionDic = list_toDictionary(currentCollection, compareSelector);
  const result: T[] = [];
  for (let i = 0; i < newCollection.length; i++) {
    result.push(newCollection[i]);

    const innerJoinItem = currentCollectionDic[compareSelector(result[i]).toString()];
    if (innerJoinItem != undefined) {
      if (typeof innerJoinItem == 'object') {
        result[i] = ObjectUtil.clone(result[i], newResultItemValue => {
          return ObjectUtil.extend(<any>newResultItemValue, <any>currentCollectionDic[compareSelector(result[i]).toString()]);
        });
      }
    }
  }
  return result;
}

export function list_total<T>(collection: T[], valueSelector: (item: T) => number | undefined): number {
  let result = 0;
  collection.forEach(p => {
    const currentItemValue = valueSelector(p);
    if (currentItemValue != undefined) result += currentItemValue;
  });
  return result;
}

export function list_range(start: number, endInclude: number): number[] {
  return lodashRange(start, endInclude + 1);
}
