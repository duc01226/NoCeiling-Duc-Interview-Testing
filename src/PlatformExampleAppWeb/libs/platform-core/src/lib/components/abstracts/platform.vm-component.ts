import { Directive, EventEmitter, Input, OnInit, Output } from '@angular/core';
import { Observable } from 'rxjs';
import { PartialDeep } from 'type-fest';

import { immutableUpdate, isDifferent } from '../../utils';
import { IPlatformVm } from '../../view-models';
import { PlatformComponent } from './platform.component';

@Directive()
export abstract class PlatformVmComponent<TViewModel extends IPlatformVm> extends PlatformComponent implements OnInit {
    public constructor() {
        super();
    }

    protected _vm!: TViewModel;
    public get vm(): TViewModel {
        return this._vm;
    }
    @Input()
    public set vm(v: TViewModel) {
        if (isDifferent(this._vm, v)) {
            this.internalSetVm(v, false);
        }
    }

    @Output('vmChange')
    public vmChangeEvent = new EventEmitter<TViewModel>();

    public override ngOnInit(): void {
        const initialVmOrVm$ = this.onInitVm();
        if (this.vm == undefined && initialVmOrVm$ != undefined) {
            if (initialVmOrVm$ instanceof Observable) {
                this.storeAnonymousSubscription(
                    initialVmOrVm$.subscribe(initialOrPartialVm => {
                        if (this._vm == undefined) {
                            this._vm = <TViewModel>initialOrPartialVm;
                            super.ngOnInit();
                        } else {
                            this.updateVm(initialOrPartialVm);
                        }
                    })
                );
            } else {
                this._vm = initialVmOrVm$;
                super.ngOnInit();
            }
        } else {
            super.ngOnInit();
        }
    }

    protected abstract onInitVm: () => TViewModel | undefined | Observable<TViewModel | PartialDeep<TViewModel>>;

    protected updateVm(
        partialStateOrUpdaterFn:
            | PartialDeep<TViewModel>
            | Partial<TViewModel>
            | ((state: TViewModel) => void | PartialDeep<TViewModel>)
    ): TViewModel {
        const newUpdatedVm: TViewModel = immutableUpdate(this.vm, partialStateOrUpdaterFn);

        if (newUpdatedVm != this.vm) {
            this.internalSetVm(newUpdatedVm);
        }

        return this.vm;
    }

    protected internalSetVm = (v: TViewModel, shallowCheckDiff: boolean = true): void => {
        if (shallowCheckDiff == false || this._vm != v) {
            this._vm = v;

            if (this.initiated$.value) {
                this.detectChanges();
                this.vmChangeEvent.emit(v);
            }
        }
    };
}
