import { Pipe, PipeTransform } from '@angular/core';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';

import { PlatformPipe } from './abstracts/platform.pipe';

@Pipe({
    name: 'platformHighlight'
})
export class PlatformHighlightSearchTextPipe
    extends PlatformPipe<string | undefined, string | undefined, string | undefined | SafeHtml>
    implements PipeTransform
{
    public constructor(protected sanitizer: DomSanitizer) {
        super();
    }
    public transform(value: string | undefined, args: string | undefined): string | undefined | SafeHtml {
        if (args == undefined || value == undefined) {
            return value;
        }
        const reg = new RegExp(args, 'gi'); //'gi' for global and case insensitive.
        return this.sanitizer.bypassSecurityTrustHtml(value.replace(reg, '<mark>$&</mark>'));
    }
}
