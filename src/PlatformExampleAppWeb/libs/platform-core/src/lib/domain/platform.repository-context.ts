export abstract class PlatformRepositoryContext {
  public loadedRequestDataDic: Dictionary<unknown> = {};
  public loadedRequestRefreshFnDic: Dictionary<() => void> = {};
  public loadedRequestSubscriberCountDic: Dictionary<number> = {};

  public clearLoadedRequestInfo(key: string) {
    delete this.loadedRequestDataDic[key];
    delete this.loadedRequestRefreshFnDic[key];
    delete this.loadedRequestSubscriberCountDic[key];
  }
}
