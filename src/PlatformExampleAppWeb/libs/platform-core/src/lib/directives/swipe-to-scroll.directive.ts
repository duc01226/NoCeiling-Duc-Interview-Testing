import { AfterViewInit, Directive } from '@angular/core';
import { PlatformDirective } from './abstracts/platform.directive';

@Directive({ selector: '[platformSwipeToScroll]', standalone: true })
export class SwipeToScrollDirective extends PlatformDirective implements AfterViewInit {
    public isMousePress = false;
    public scrollLeft = 0;
    public startX = 0;

    constructor() {
        super();
    }

    public override ngAfterViewInit(): void {
        super.ngAfterViewInit();

        this.elementRef.nativeElement.addEventListener('mousedown', (e: MouseEvent) => {
            if (e.button === 0) {
                this.isMousePress = true;
                this.startX = e.pageX - this.elementRef.nativeElement.offsetLeft;
                this.scrollLeft = this.elementRef.nativeElement.scrollLeft;
            }
        });

        this.elementRef.nativeElement.addEventListener('mouseup', () => {
            this.isMousePress = false;
        });

        this.elementRef.nativeElement.addEventListener('touchend', () => {
            this.isMousePress = false;
        });

        this.elementRef.nativeElement.addEventListener('mousemove', (e: MouseEvent) => {
            e.preventDefault();
            if (!this.isMousePress) return;
            const x = e.pageX - this.elementRef.nativeElement.offsetLeft;
            const walk = (x - this.startX) * 1;
            this.elementRef.nativeElement.scrollLeft = this.scrollLeft - walk;
        });
    }
}
