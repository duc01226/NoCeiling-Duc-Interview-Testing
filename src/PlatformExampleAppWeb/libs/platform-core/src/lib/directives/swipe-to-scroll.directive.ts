import { AfterViewInit, Directive, ElementRef } from '@angular/core';

@Directive({ selector: '[platformSwipeToScroll]', standalone: true })
export class SwipeToScrollDirective implements AfterViewInit {
  public isMousePress = false;
  public scrollLeft = 0;
  public startX = 0;

  constructor(public elementRef: ElementRef<HTMLElement>) {}

  public ngAfterViewInit(): void {
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
