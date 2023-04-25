import { Injectable } from '@angular/core';
import { BehaviorSubject } from 'rxjs';

const MinSpinnerDisplayTime = 500;

@Injectable({
    providedIn: 'root'
})
export class GlobalSpinnerService {
    public showSpinner$: BehaviorSubject<boolean> = new BehaviorSubject<boolean>(false);
    private showingTimer = 0;
    private showingTimerInterval: NodeJS.Timeout | NodeJS.Timer | undefined = undefined;

    public displaySpinner(): void {
        setTimeout(() => {
            this.showSpinner$.next(true);

            if (this.showingTimerInterval == undefined) {
                this.showingTimerInterval = setInterval(() => (this.showingTimer += 100), 100);
            }
        });
    }

    public hideSpinner(): void {
        setTimeout(() => {
            const timeToAdd: number = MinSpinnerDisplayTime - this.showingTimer;
            setTimeout(
                () => {
                    if (this.showingTimerInterval) clearInterval(this.showingTimerInterval);
                    this.showingTimerInterval = undefined;
                    this.showingTimer = 0;
                    this.showSpinner$.next(false);
                },
                timeToAdd >= 100 ? timeToAdd : 0
            );
        });
    }
}
