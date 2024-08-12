/* eslint-disable @typescript-eslint/no-explicit-any */
/* eslint-disable */

import { string_isIncluded } from './utils.string';

export function hierarchy_flatten<T>(nodes: T[], key: string, arr: T[] = []) {
    const result = arr;
    nodes = nodes?.filter(node => node != undefined);

    if (nodes == null || nodes.length == 0) return result;

    nodes.forEach(node => {
        result.push(node);
        if ((<any>node)[key] != null && (<any>node)[key].length > 0) {
            hierarchy_flatten((<any>node)[key], key, result);
        }
    });
    return result;
}

export function hierarchy_searchNodeParentByLabelProp<T>(
    items: T[] = [],
    searchValue: string,
    valueProp: string,
    labelProp: string,
    parentProp: string,
    childrenProp: string
): T[] {
    return items.reduce(
        (accumulator, item) => {
            if (string_isIncluded((<any>item)[labelProp], searchValue)) {
                accumulator.push(item);
            } else if ((<any>item)[childrenProp] != null && (<any>item)[childrenProp].length > 0) {
                const newItems = hierarchy_searchNodeParentByLabelProp(
                    (<any>item)[childrenProp],
                    searchValue,
                    valueProp,
                    labelProp,
                    parentProp,
                    childrenProp
                );

                if (newItems.length > 0) {
                    const noteRoot = <T>{
                        [valueProp]: (<any>item)[valueProp],
                        [labelProp]: (<any>item)[labelProp],
                        [childrenProp]: newItems,
                        [parentProp]: (<any>item)[parentProp]
                    };
                    accumulator.push(noteRoot);
                }
            }

            return accumulator;
        },
        <T[]>[]
    );
}

export function hierarchy_hasChildrenNode<T>(node: T, childrenProp: string): boolean {
    if (node == null) return false;
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return (<Dictionary<any>>node)[childrenProp]?.length > 0;
}

export function findItemByLabelAndValueProp<T>(
    tree: T[],
    labelProp: keyof T,
    value: string,
    childNodeProp: keyof T
): T | undefined {
    let result: T | undefined;

    tree.some((node: any) => {
        if ((<any>node)[labelProp] === value) {
            result = node as T;
            return true;
        }

        if ((<any>node)[childNodeProp]) {
            result = findItemByLabelAndValueProp<T>((<any>node)[childNodeProp], labelProp, value, childNodeProp);
            return result !== undefined;
        }

        return false;
    });

    return result;
}

export function findRootItemByChildLabelAndValueProp<T>(
    tree: T[],
    labelProp: keyof T,
    value: string,
    childNodeProp: keyof T
): T | undefined {
    let result: T | undefined;

    tree.some((node: any) => {
        if ((<any>node)[childNodeProp]) {
            const childResult = findItemByLabelAndValueProp(
                (<any>node)[childNodeProp],
                labelProp,
                value,
                childNodeProp
            );
            if (childResult) {
                result = node as T; // Set current node as the result if child is found
                return true; // Stop iteration
            }
        }

        return false; // Continue searching
    });

    return result;
}
