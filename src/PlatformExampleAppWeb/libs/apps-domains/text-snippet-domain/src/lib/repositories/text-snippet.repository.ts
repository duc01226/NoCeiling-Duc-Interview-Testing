import { Injectable } from '@angular/core';
import {
  PlatformCoreModuleConfig,
  PlatformEventManager,
  PlatformPagedResultDto,
  PlatformRepository,
} from '@platform-example-web/platform-core';
import { Observable } from 'rxjs';
import { catchError, tap } from 'rxjs/operators';

import { SaveTextSnippetCommand, SaveTextSnippetCommandResult, SearchTextSnippetQueryDto, TextSnippetApi } from '../apis';
import { TextSnippetRepositoryContext } from '../apps-text-snippet.repository-context';
import { TextSnippetDataModel } from '../data-models';

@Injectable()
export class TextSnippetRepository extends PlatformRepository<TextSnippetRepositoryContext> {
  public constructor(
    moduleConfig: PlatformCoreModuleConfig,
    context: TextSnippetRepositoryContext,
    eventManager: PlatformEventManager,
    private textSnippetApi: TextSnippetApi
  ) {
    super(moduleConfig, context, eventManager);
  }
  public search(query: SearchTextSnippetQueryDto): Observable<PlatformPagedResultDto<TextSnippetDataModel>> {
    return this.processUpsertData({
      repoDataSubject: this.context.textSnippetSubject,
      apiRequestFn: () => this.textSnippetApi.search(query),
      requestName: 'TextSnippet.Search',
      requestPayload: query,
      strategy: 'implicitReload',
      finalResultBuilder: (repoData, apiResult) => {
        apiResult.items = apiResult.items.map(item => repoData[<string>item.id]).filter(_ => _ != null);
        return apiResult;
      },
      modelDataExtractor: apiResult => apiResult.items,
      modelIdFn: x => x.id,
      initModelItemFn: x => new TextSnippetDataModel(x)
    });
  }

  public save(command: SaveTextSnippetCommand): Observable<SaveTextSnippetCommandResult> {
    return this.textSnippetApi.save(command).pipe(
      tap(() => this.processRefreshData({ requestName: 'TextSnippet.Search' })),
      catchError(error => this.catchApiError(error, 'TextSnippet.Save', command))
    );
  }
}
