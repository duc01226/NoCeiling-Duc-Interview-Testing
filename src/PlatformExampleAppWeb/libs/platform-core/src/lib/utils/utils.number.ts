export function number_round(value: number, factionDigits: number = 0): number {
  const floatPointMovingValue = Math.pow(10, factionDigits) * 1.0;
  return Math.round(value * floatPointMovingValue) / floatPointMovingValue;
}

export function number_toFixed(value: number, factionDigits: number = 0): string {
  return number_round(value, factionDigits).toFixed(factionDigits);
}
