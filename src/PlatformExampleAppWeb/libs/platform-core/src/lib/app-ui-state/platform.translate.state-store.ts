/* eslint-disable @typescript-eslint/no-explicit-any */
import { Injectable } from '@angular/core';
import { ComponentStore } from '@ngrx/component-store';

import { skip } from 'rxjs';
import { PlatformLanguageItem, PlatformTranslateService } from '../translations';

export interface TranslateState {
    currentLang?: string | null;
    defaultLanguage: string;
    availableLangs: PlatformLanguageItem[];
    restrictSupportLangs?: string[] | null;
}

@Injectable({ providedIn: 'root' })
export class PlatformTranslateStateStore extends ComponentStore<TranslateState> {
    public currentLang$ = this.select(state => state.currentLang);
    public currentOrDefaultLang$ = this.select(state => state.currentLang ?? state.defaultLanguage);
    public defaultLanguage$ = this.select(state => state.defaultLanguage);
    public availableLangs$ = this.select(state => state.availableLangs);
    public restrictSupportLangs$ = this.select(state => state.restrictSupportLangs);

    constructor(private translateService: PlatformTranslateService) {
        super({
            defaultLanguage: translateService.defaultLanguage,
            currentLang: translateService.getCurrentLang(),
            availableLangs: translateService.getAvailableLangs(),
            restrictSupportLangs: translateService.getRestrictSupportLangs()
        });

        this.restrictSupportLangs$.pipe(skip(1)).subscribe(restrictSupportLangs => {
            this.translateService.setRestrictSupportLangs(restrictSupportLangs);

            this.autoFixValidLanguage(this.get().availableLangs, restrictSupportLangs);
        });
        this.defaultLanguage$.pipe(skip(1)).subscribe(defaultLanguage => {
            this.translateService.setDefaultLang(defaultLanguage);

            setTimeout(() => this.autoFixValidLanguage(this.get().availableLangs, this.get().restrictSupportLangs));
        });
        this.currentLang$.pipe(skip(1)).subscribe(currentLang => {
            if (currentLang != null) this.translateService.setCurrentLang(currentLang);

            setTimeout(() => this.autoFixValidLanguage(this.get().availableLangs, this.get().restrictSupportLangs));
        });
        this.availableLangs$.pipe(skip(1)).subscribe(availableLangs => {
            this.translateService.setAvailableLangs(availableLangs);

            this.autoFixValidLanguage(availableLangs, this.get().restrictSupportLangs);
        });
    }

    private autoFixValidLanguage(
        availableLangs: PlatformLanguageItem[],
        restrictSupportLangs: string[] | null | undefined
    ) {
        if (restrictSupportLangs != null && restrictSupportLangs.length > 0) {
            if (!restrictSupportLangs.includes(this.get().defaultLanguage))
                this.patchState({ defaultLanguage: restrictSupportLangs[0]! });
            if (this.get().currentLang != null && !restrictSupportLangs.includes(this.get().currentLang!))
                this.patchState({ currentLang: restrictSupportLangs[0]! });
        }

        if (availableLangs.length > 0) {
            if (!availableLangs.some(availableLang => availableLang.value == this.get().defaultLanguage))
                this.patchState({ defaultLanguage: availableLangs[0]!.value });
            if (
                this.get().currentLang != null &&
                !availableLangs.some(availableLang => availableLang.value == this.get().currentLang)
            )
                this.patchState({ currentLang: availableLangs[0]!.value });
        }
    }
}
