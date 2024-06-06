import { ElementRef } from '@angular/core';
import { PlatformComponent } from '../../components';

export class PlatformDirective extends PlatformComponent {
    constructor(public elementRef: ElementRef) {
        super();
    }

    public get element(): HTMLElement {
        return this.elementRef.nativeElement;
    }
}
