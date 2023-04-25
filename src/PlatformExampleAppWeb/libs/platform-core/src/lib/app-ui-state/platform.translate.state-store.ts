/* eslint-disable @typescript-eslint/no-explicit-any */
import { Injectable } from '@angular/core';
import { ComponentStore } from '@ngrx/component-store';

import { PlatformLanguageItem, PlatformTranslateService } from '../translations';

export interface TranslateState {
    currentLang: string;
    availableLangs: PlatformLanguageItem[];
}

@Injectable({ providedIn: 'root' })
export class PlatformTranslateStateStore extends ComponentStore<TranslateState> {
    public currentLang$ = this.select(state => state.currentLang);
    public availableLangs$ = this.select(state => state.availableLangs);

    public setLanguage = (useLanguage: string) => {
        if (this.get().availableLangs.find(lang => lang.value === useLanguage)) {
            this.translateService.setCurrentLang(useLanguage);
            this.patchState({
                currentLang: useLanguage
            });
        }
    };

    constructor(private translateService: PlatformTranslateService) {
        super({
            currentLang: translateService.getCurrentLang(),
            availableLangs: translateService.getAvailableLangs()
        });

        this.currentLang$.subscribe(lang => {
            this.translateService.setup(lang);
        });
    }
}
