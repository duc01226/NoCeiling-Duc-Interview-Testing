export function number_round(value: number, factionDigits: number = 0): number {
  const floatPointMovingValue = Math.pow(10, factionDigits) * 1.0;
  return Math.round(value * floatPointMovingValue) / floatPointMovingValue;
}

export function number_toFixed(value: number, factionDigits: number = 0): string {
  return number_round(value, factionDigits).toFixed(factionDigits);
}

export function number_formatLength(num: number, length: number) {
  let r = '' + num;

  while (r.length < length) {
    r = '0' + r;
  }

  return r;
}

export function number_isInteger(value: unknown): value is number {
  return typeof value === 'number' && isFinite(value) && Math.floor(value) === value;
}
