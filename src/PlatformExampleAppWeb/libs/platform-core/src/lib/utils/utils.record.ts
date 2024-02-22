export function record_map<T, TResult>(
    sourceRecord: Record<string, T>,
    mapFn: (x: T) => TResult
): Record<string, TResult> {
    const result: Record<string, TResult> = {};

    Object.keys(sourceRecord).forEach(key => {
        result[key] = mapFn(sourceRecord[key]!);
    });

    return result;
}
