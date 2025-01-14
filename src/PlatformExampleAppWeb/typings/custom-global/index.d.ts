declare interface Dictionary<T> {
    [index: string]: T;
}

declare interface DictionaryItem<TKey, TValue> {
    key: TKey;
    value: TValue;
}
