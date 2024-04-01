import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';

import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';

import {
    IPlatformPagedResultDto,
    PlatformApiService,
    PlatformCommandDto,
    PlatformCoreModuleConfig,
    PlatformEventManager,
    PlatformHttpOptionsConfigService,
    PlatformPagedQueryDto,
    PlatformPagedResultDto,
    PlatformResultDto
} from '@libs/platform-core';

import { AppsTextSnippetDomainModuleConfig } from '../apps-text-snippet-domain.config';
import { TextSnippetDataModel } from '../data-models';

@Injectable()
export class TextSnippetApi extends PlatformApiService {
    public constructor(
        moduleConfig: PlatformCoreModuleConfig,
        http: HttpClient,
        httpOptionsConfigService: PlatformHttpOptionsConfigService,
        eventManager: PlatformEventManager,
        private domainModuleConfig: AppsTextSnippetDomainModuleConfig
    ) {
        super(http, moduleConfig, httpOptionsConfigService, eventManager);
    }
    protected get apiUrl(): string {
        return `${this.domainModuleConfig.textSnippetApiHost}/api/TextSnippet`;
    }

    public search(query: SearchTextSnippetQuery): Observable<PlatformPagedResultDto<TextSnippetDataModel>> {
        return this.get<IPlatformPagedResultDto<TextSnippetDataModel>>('/search', query).pipe(
            map(_ => {
                _.items = _.items.map(item => new TextSnippetDataModel(item));
                return new PlatformPagedResultDto(_);
            })
        );
    }

    public save(command: SaveTextSnippetCommand): Observable<SaveTextSnippetCommandResult> {
        return this.post<SaveTextSnippetCommandResult>('/save', command).pipe(
            map(_ => new SaveTextSnippetCommandResult(_))
        );
    }
}

// ----------------- SearchTextSnippetQuery -------------------
export class SearchTextSnippetQuery extends PlatformPagedQueryDto {
    public constructor(data?: Partial<SearchTextSnippetQuery>) {
        super(data);
        this.searchText = data?.searchText;
        this.searchId = data?.searchId;
    }
    public searchText?: string | null;
    public searchId?: string | null;
}

// ----------------- SaveTextSnippetCommand -------------------
export class SaveTextSnippetCommand extends PlatformCommandDto {
    public constructor(data?: Partial<SaveTextSnippetCommand>) {
        super();
        this.data = data?.data ?? new TextSnippetDataModel();
    }
    public data: TextSnippetDataModel;
}

export class SaveTextSnippetCommandResult extends PlatformResultDto {
    public constructor(data?: Partial<SaveTextSnippetCommandResult>) {
        super();
        this.savedData = new TextSnippetDataModel(data?.savedData);
    }
    public savedData: TextSnippetDataModel;
}
