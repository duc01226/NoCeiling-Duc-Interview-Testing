import { SimpleChanges } from '@angular/core';

import * as _ from 'lodash-es';

export class LifeCycleHelper {
    public static isInputChanged(changes: SimpleChanges): boolean {
        return Object.keys(changes).some(
            key =>
                changes[key]?.currentValue != undefined &&
                !_.isEqual(changes[key]!.previousValue, changes[key]!.currentValue)
        );
    }
}
