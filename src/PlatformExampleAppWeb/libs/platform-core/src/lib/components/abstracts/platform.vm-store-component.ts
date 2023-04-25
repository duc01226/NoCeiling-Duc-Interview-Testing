import { Directive } from '@angular/core';
import { map, Observable } from 'rxjs';
import { PartialDeep } from 'type-fest';

import { PlatformVm, PlatformVmStore } from '../../view-models';
import { PlatformComponent } from './platform.component';

@Directive()
export abstract class PlatformVmStoreComponent<
    TViewModel extends PlatformVm,
    TViewModelStore extends PlatformVmStore<TViewModel>
> extends PlatformComponent {
    public constructor(public store: TViewModelStore) {
        super();
    }

    public get vm$(): Observable<TViewModel> {
        return this.store.vm$;
    }

    public get isStatePending$(): Observable<boolean> {
        return this.store.vm$.pipe(map(_ => _.status == 'Pending'));
    }

    public get isStateLoading$(): Observable<boolean> {
        return this.store.vm$.pipe(map(_ => _.status == 'Loading'));
    }

    public get isStateSuccess$(): Observable<boolean> {
        return this.store.vm$.pipe(map(_ => _.status == 'Success'));
    }

    public get isStateError$(): Observable<boolean> {
        return this.store.vm$.pipe(map(_ => _.status == 'Error'));
    }

    public currentVm(): TViewModel {
        return this.store.currentVm;
    }

    public updateVm(
        partialStateOrUpdaterFn:
            | PartialDeep<TViewModel>
            | Partial<TViewModel>
            | ((state: TViewModel) => void | PartialDeep<TViewModel> | Partial<TViewModel>)
    ): void {
        this.store.updateState(partialStateOrUpdaterFn);
    }
}
