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
        this.initVm();
    }

    public initVm(forceReinit: boolean = false) {
        if (forceReinit) this.cancelStoredSubscription('initVm');

        const initialVm$ = this.onInitVm();

        if ((this.vm == undefined || forceReinit) && initialVm$ != undefined) {
            if (initialVm$ instanceof Observable) {
                this.storeSubscription(
                    'initVm',
                    initialVm$.subscribe(initialVm => {
                        this._vm = initialVm;
                        super.ngOnInit();
                    })
                );
            } else {
                this._vm = initialVm$;
                super.ngOnInit();
            }
        } else {
            super.ngOnInit();
        }
    }

    public reload() {
        this.initVm(true);
        this.clearErrorMsg();
    }

    protected abstract onInitVm: () => TViewModel | undefined | Observable<TViewModel>;

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
