export function string_isNullOrEmpty(value: string | number | undefined | null) {
    return value == undefined || value.toString().trim() == '';
}
