import { Injector, ViewContainerRef, inject } from '@angular/core';
import { PlatformComponent } from '../../components';

export abstract class PlatformDirective extends PlatformComponent {
    constructor() {
        super();
    }

    protected viewContainerRef: ViewContainerRef = inject(ViewContainerRef);
    protected injector: Injector = inject(Injector);

    public get element(): HTMLElement {
        return this.elementRef.nativeElement;
    }
}
