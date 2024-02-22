import { Directive, OnInit } from '@angular/core';
import { Observable } from 'rxjs';

import { PlatformAppUiStateData, PlatformAppUiStateStore } from '../../app-ui-state';
import { PlatformVm } from '../../view-models';
import { PlatformVmComponent } from './platform.vm-component';

/**
 * @classdesc
 * Abstract class `PlatformSmartComponent` is an Angular directive designed to be extended for building smart components. It extends the abstract class `PlatformVmComponent` and implements the `OnInit` interface.
 *
 * @class
 * @abstract
 * @extends PlatformVmComponent
 * @implements OnInit
 * @template TAppUiStateData - Generic type extending `PlatformAppUiStateData`.
 * @template TAppUiStateStore - Generic type extending `PlatformAppUiStateStore<TAppUiStateData>`.
 * @template TViewModel - Generic type extending `PlatformVm`.
 *
 * @constructor
 * @param {TAppUiStateStore} appUiStateStore - The application UI state store of type `TAppUiStateStore` for initializing the component.
 *
 * @method ngOnInit - Overrides the `ngOnInit` lifecycle hook to perform initialization tasks.
 * @method selectAppUiState - Selects a specific part of the application UI state using a selector function.
 *
 * @protected
 * @property {TAppUiStateStore} appUiStateStore - The application UI state store used by the component.
 *
 * @protected
 * @method untilDestroyed - A utility method to manage the lifecycle of subscriptions until the component is destroyed.
 */
@Directive()
export abstract class PlatformSmartComponent<
        TAppUiStateData extends PlatformAppUiStateData,
        TAppUiStateStore extends PlatformAppUiStateStore<TAppUiStateData>,
        TViewModel extends PlatformVm
    >
    extends PlatformVmComponent<TViewModel>
    implements OnInit
{
    public constructor(protected appUiStateStore: TAppUiStateStore) {
        super();
    }

    /**
     * Selects a specific part of the application UI state using a selector function.
     * @method
     * @template T - The type of the selected UI state.
     * @param {(uiStateData: TAppUiStateData) => T} selector - The selector function.
     * @returns {Observable<T>} - An observable representing the selected UI state.
     */
    protected selectAppUiState<T>(selector: (uiStateData: TAppUiStateData) => T): Observable<T> {
        return this.appUiStateStore.select(selector).pipe(this.untilDestroyed());
    }
}
