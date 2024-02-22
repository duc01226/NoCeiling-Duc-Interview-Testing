export function enum_toItems(enumObject: object, ignoreOptions?: string[], keyTranslateMap?: Record<string, string>) {
    return Object.entries(enumObject)
        .filter(([key, val]) => isNaN(Number(val)) && !ignoreOptions?.includes(key))
        .map(([key, val]) => ({
            value: key,
            label: keyTranslateMap ? keyTranslateMap[key] : val
        }));
}
