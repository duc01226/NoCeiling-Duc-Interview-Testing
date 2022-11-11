/* eslint-disable @typescript-eslint/no-explicit-any */
import { keys } from 'lodash';

import { ObjectExtension } from './object.extension';

/* eslint-disable @typescript-eslint/no-unsafe-assignment */
export class DictionaryExtension {
  public static upsert<T>(
    currentData: Dictionary<T>,
    newData: Dictionary<Partial<T>> | Partial<T>[],
    getItemKey: (item: T | Partial<T>) => string | number,
    initItem: (data: T | Partial<T>) => T,
    removeNotExistedItems?: boolean,
    removeNotExistedItemsFilter?: (item: Partial<T>) => boolean,
    replaceEachItem?: boolean,
    onHasNewStateDifferent?: (newState: Dictionary<T>) => any,
    optionalProps: (keyof T)[] = []
  ): Dictionary<T> {
    return modifyDic(currentData, newState => {
      const newDataDic = newData instanceof Array ? ObjectExtension.toDictionary(newData, x => getItemKey(x)) : newData;
      if (removeNotExistedItems) {
        removeNotExistedItemsInNewData(newState, newDataDic);
      }

      keys(newDataDic).forEach(id => {
        if (
          newState[id] == null ||
          newDataDic[id] == null ||
          typeof newDataDic[id] !== 'object' ||
          typeof newState[id] !== 'object'
        ) {
          // eslint-disable-next-line no-param-reassign
          newState[id] = initItem(newDataDic[id]);
        } else {
          const prevNewStateItem = newState[id];
          const newStateItemData = replaceEachItem
            ? newDataDic[id]
            : ObjectExtension.assign<Partial<T>>(ObjectExtension.clone(newState[id]), newDataDic[id]);
          if (optionalProps.length > 0) {
            optionalProps.forEach(optionalProp => {
              if (prevNewStateItem[optionalProp] != null && newStateItemData[optionalProp] == null) {
                newStateItemData[optionalProp] = prevNewStateItem[optionalProp];
              }
            });
          }
          // eslint-disable-next-line no-param-reassign
          newState[id] = initItem(newStateItemData);
        }
      });
    });

    function removeNotExistedItemsInNewData(state: Dictionary<Partial<T>>, newDataDic: Dictionary<Partial<T>>): void {
      const removeItemIds = keys(state).filter(
        id => newDataDic[id] == null && (removeNotExistedItemsFilter == null || removeNotExistedItemsFilter(state[id]))
      );
      removeItemIds.forEach(id => {
        // eslint-disable-next-line no-param-reassign
        delete state[id];
      });
    }

    function modifyDic(
      state: Dictionary<T>,
      modifyDicAction: (state: Dictionary<T>) => void | Dictionary<T>
    ): Dictionary<T> {
      const newState = ObjectExtension.clone(state);
      const modifiedState = modifyDicAction(newState);
      if (modifiedState === state) {
        return state;
      }
      if (ObjectExtension.isDifferent(state, newState)) {
        if (onHasNewStateDifferent != null) {
          onHasNewStateDifferent(newState);
        }
        return newState;
      }
      return state;
    }
  }
}
