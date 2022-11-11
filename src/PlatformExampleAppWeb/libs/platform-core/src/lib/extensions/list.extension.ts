export class ListExtension {
  public static selectMany<T, S>(collection: T[] | undefined, selectCallback: (item: T) => S[]): S[] {
    if (collection == undefined || collection.length == 0) return [];
    const listOfChildList = collection.map(selectCallback);
    return listOfChildList.reduce((prevValue, currentValue) => prevValue.concat(currentValue));
  }
}
