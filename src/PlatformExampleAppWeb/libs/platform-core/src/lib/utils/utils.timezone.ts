export function timezone_getCurrentTimezone(): string {
    return Intl.DateTimeFormat().resolvedOptions().timeZone;
}
