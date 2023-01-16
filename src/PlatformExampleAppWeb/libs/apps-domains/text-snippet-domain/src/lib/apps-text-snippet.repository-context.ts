import { PlatformRepositoryContext } from '@libs/platform-core';
import { BehaviorSubject } from 'rxjs';

import { TextSnippetDataModel } from './data-models';

export class TextSnippetRepositoryContext extends PlatformRepositoryContext {
  public textSnippetSubject: BehaviorSubject<Dictionary<TextSnippetDataModel>> = new BehaviorSubject({});
}
