import { Directive, Input } from '@angular/core';
import { NgControl } from '@angular/forms';
import { PlatformComponent } from '../components';

@Directive({
    selector: '[platformDisabledControl]',
    standalone: true
})
export class DisabledControlDirective extends PlatformComponent {
    @Input('platformDisabledControl') public set isDisabled(v: boolean) {
        if (v) this.ngControl.control?.disable();
        else this.ngControl.control?.enable();
    }

    constructor(public readonly ngControl: NgControl) {
        super();
    }
}
