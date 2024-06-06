/* eslint-disable @typescript-eslint/no-explicit-any */
import { Watch, WatchCallBackFunction } from '../decorators';
import { isDifferent } from '../utils';
import { PlatformComponent } from './abstracts';

/**
 * Operator used to watch a component input when it is set after component init.
 *
 * @template TComponent The type of the component, defaults to PlatformComponent.
 * @template TProp The type of the property being watched, defaults to object.
 *
 * @param {WatchCallBackFunction<TProp, TComponent> | keyof TComponent} callbackFnOrName
 *        A callback function to be executed when the watched property changes, or the name of a method on the component.
 * @param {Object} [options] Optional parameters.
 * @param {boolean} [options.beforeInitiated=false] If true, the watch will be active before the component is fully initialized.
 * @param {boolean | 'deep-check'} [options.checkDiff=false] If true, the watch will only trigger if the value changes.
 *        If 'deep-check', the watch will perform a deep comparison to check for changes.
 *
 * @returns {MethodDecorator} A method decorator to watch the specified property.
 *
 * @example
 * // Shorthand to execute a target function directly on change
 * @WatchInput('pagedResultWatch')
 * public pagedResult?: PlatformPagedResultDto<LeaveType>;
 *
 * // Full syntax to execute a normal function
 * @WatchInput<LeaveTypesState, PlatformPagedQueryDto>((value, change, targetObj) => {
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
export function WatchInput<TComponent extends PlatformComponent = PlatformComponent, TProp = object>(
    callbackFnOrName: WatchCallBackFunction<TProp, TComponent> | keyof TComponent,
    options?: { beforeInitiated?: boolean; checkDiff?: boolean | 'deep-check' }
) {
    return Watch(callbackFnOrName, (obj, change) => {
        if (options?.beforeInitiated != true && obj.initiated$.value != true) return false;
        if (options?.checkDiff == true && change.previousValue == change.currentValue) return false;
        if (options?.checkDiff == 'deep-check' && !isDifferent(change.previousValue, change.currentValue)) return false;

        return true;
    });
}
