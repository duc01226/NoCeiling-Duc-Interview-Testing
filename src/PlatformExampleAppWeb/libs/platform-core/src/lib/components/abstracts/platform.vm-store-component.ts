/* eslint-disable @typescript-eslint/no-explicit-any */
import { Directive, OnInit } from '@angular/core';
import { combineLatest, map, Observable } from 'rxjs';
import { PartialDeep } from 'type-fest';

import { keys } from '../../utils';
import { PlatformVm, PlatformVmStore, requestStateDefaultKey } from '../../view-models';
import { PlatformComponent } from './platform.component';

@Directive()
export abstract class PlatformVmStoreComponent<
        TViewModel extends PlatformVm,
        TViewModelStore extends PlatformVmStore<TViewModel>
    >
    extends PlatformComponent
    implements OnInit
{
    public constructor(public store: TViewModelStore) {
        super();
    }

    private _additionalStores?: PlatformVmStore<PlatformVm>[];
    public get additionalStores(): PlatformVmStore<PlatformVm>[] {
        if (this._additionalStores == null) {
            const mainStoreKey: keyof PlatformVmStoreComponent<PlatformVm, PlatformVmStore<PlatformVm>> = 'store';

            // ignore ['additionalStores', 'isStateError$', 'isStateLoading$'] to prevent maximum call stack error
            // use keys => access to properies => trigger it self a gain. isStateError, isStateLoading are using additionalStores
            // is also affected
            this._additionalStores = keys(this, true, ['additionalStores', 'isStateError$', 'isStateLoading$'])
                .filter(key => this[key] instanceof PlatformVmStore && key != mainStoreKey)
                .map(key => <PlatformVmStore<PlatformVm>>this[key]);
        }

        return this._additionalStores;
    }

    public override ngOnInit(): void {
        super.ngOnInit();
    }

    private _state$?: Observable<TViewModel>;
    /**
     * State is the root state of the store. Use this to check anything without need to wait the "vm" loaded.
     * Vm are actually still a state but it's a state with valid loaded value to show
     */
    public get state$(): Observable<TViewModel> {
        if (this._state$ == undefined) this._state$ = this.store.state$.pipe(this.untilDestroyed());

        return this._state$;
    }

    private _vm$?: Observable<TViewModel>;
    /**
     * Vm State is the state of the store, but it's a state with valid loaded value to show on UI.
     * Subscribe to this observable could also trigger load data. So should only subscribe it once on UI
     */
    public get vm$(): Observable<TViewModel> {
        if (this._vm$ == undefined) this._vm$ = this.store.vm$.pipe(this.untilDestroyed());

        return this._vm$;
    }

    private _isStatePending$?: Observable<boolean>;
    public get isStatePending$(): Observable<boolean> {
        if (this._isStatePending$ == undefined)
            this._isStatePending$ = this.store.isStatePending$.pipe(this.untilDestroyed());

        return this._isStatePending$;
    }

    private _isStateLoading$?: Observable<boolean>;
    public get isStateLoading$(): Observable<boolean> {
        if (this._isStateLoading$ == undefined)
            this._isStateLoading$ = combineLatest(
                this.additionalStores
                    .concat([<PlatformVmStore<PlatformVm>>(<any>this.store)])
                    .map(store => store.isStateLoading$.pipe(this.untilDestroyed()))
            ).pipe(map(isLoadings => isLoadings.find(isLoading => isLoading) != undefined));

        return this._isStateLoading$;
    }

    private _isStateSuccess$?: Observable<boolean>;
    public get isStateSuccess$(): Observable<boolean> {
        if (this._isStateSuccess$ == undefined)
            this._isStateSuccess$ = this.store.isStateSuccess$.pipe(this.untilDestroyed());

        return this._isStateSuccess$;
    }

    private _isStateError$?: Observable<boolean | undefined>;
    public get isStateError$(): Observable<boolean | undefined> {
        if (this._isStateError$ == null) {
            this._isStateError$ = combineLatest(
                this.additionalStores
                    .concat([<PlatformVmStore<PlatformVm>>(<any>this.store)])
                    .map(store => store.isStateError$.pipe(this.untilDestroyed()))
            ).pipe(map(isErrors => isErrors.find(isError => isError)));
        }

        return this._isStateError$;
    }

    public get isStatePending(): boolean {
        return this.store.currentState.isStatePending;
    }

    public get isStateLoading(): boolean {
        return this.store.currentState.isStateLoading;
    }

    public get isStateSuccess(): boolean {
        return this.store.currentState.isStateSuccess;
    }

    public get isStateError(): boolean {
        return this.store.currentState.isStateError;
    }

    public get currentState(): TViewModel {
        return this.store.currentState;
    }

    public override getErrorMsg$(requestKey: string = requestStateDefaultKey): Observable<string | null | undefined> {
        if (this.cachedErrorMsg$[requestKey] == null) {
            this.cachedErrorMsg$[requestKey] = <Observable<string | null>>(
                combineLatest(
                    this.additionalStores
                        .concat([<PlatformVmStore<PlatformVm>>(<any>this.store)])
                        .map(store => store.getErrorMsg$(requestKey).pipe(this.untilDestroyed()))
                ).pipe(map(errors => errors.find(p => p != null)))
            );
        }

        return this.cachedErrorMsg$[requestKey];
    }

    public updateVm(
        partialStateOrUpdaterFn:
            | PartialDeep<TViewModel>
            | Partial<TViewModel>
            | ((state: TViewModel) => void | PartialDeep<TViewModel> | Partial<TViewModel>)
    ): void {
        this.store.updateState(partialStateOrUpdaterFn);
    }

    public reload(): void {
        this.store.reload();
        this.additionalStores.forEach(p => p.reload());
    }
}
