export interface IPlatformFormValidationError {
  errorMsg: string;
  params?: Dictionary<string | number | Date>;
}

export function buildFormValidationError(value: IPlatformFormValidationError): IPlatformFormValidationError {
  return value;
}
