import { Directive, Input } from '@angular/core';
import { NgControl } from '@angular/forms';
import { PlatformDirective } from './abstracts/platform.directive';

@Directive({
    selector: '[platformDisabledControl]',
    standalone: true
})
export class DisabledControlDirective extends PlatformDirective {
    @Input('platformDisabledControl') public set isDisabled(v: boolean) {
        if (v) this.ngControl.control?.disable();
        else this.ngControl.control?.enable();
    }

    constructor(public readonly ngControl: NgControl) {
        super();
    }
}
