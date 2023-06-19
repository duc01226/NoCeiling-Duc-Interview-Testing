/* eslint-disable @typescript-eslint/no-explicit-any */
import { Watch, WatchCallBackFunction } from '../decorators';
import { PlatformComponent } from './abstracts';

/**
 * Operator used to watch a component input when it is set after component init
 *
 * Example:
 *
 * // Shorthand execute a target function doing something directly if on change only do this logic
 * @WatchInput('pagedResultWatch')
 * public pagedResult?: PlatformPagedResultDto<LeaveType>;
 *
 * // Full syntax execute a NORMAL FUNCTION
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
    beforeInitiated: boolean = false
) {
    return Watch(callbackFnOrName, beforeInitiated ? undefined : p => p.initiated$?.value);
}
