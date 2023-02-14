import { HttpStatusCode } from '@angular/common/http';

export interface IPlatformApiServiceErrorResponse {
  error: IPlatformApiServiceErrorInfo;
  statusCode?: HttpStatusCode;
  requestId: string;
}

export class PlatformApiServiceErrorResponse {
  public constructor(data?: IPlatformApiServiceErrorResponse) {
    if (data != null) {
      if (data.error != null) this.error = new PlatformApiServiceErrorInfo(data.error);
      if (data.statusCode != null) this.statusCode = data.statusCode;
    }
    this.requestId = data?.requestId ?? '';
  }

  public error: PlatformApiServiceErrorInfo = new PlatformApiServiceErrorInfo();
  public statusCode?: HttpStatusCode;
  public requestId: string;

  public static getDefaultFormattedMessage(errorResponse: PlatformApiServiceErrorResponse | Error): string {
    return errorResponse instanceof Error ? (<Error>errorResponse).message : errorResponse.getDefaultFormattedMessage();
  }

  public getDefaultFormattedMessage(): string {
    if (this.error.message == undefined) return '';
    const requestIdInfoMessagePart =
      this.requestId != '' && !this.error.isApplicationError() ? ` | RequestId: ${this.requestId}` : '';
    return `${this.error.message}${requestIdInfoMessagePart}`;
  }
}

export interface IPlatformApiServiceErrorInfo {
  code: PlatformApiServiceErrorInfoCode | string;
  message?: string;
  formattedMessagePlaceholderValues?: Map<string, string>;
  target?: string;
  details?: IPlatformApiServiceErrorInfo[];
}

export class PlatformApiServiceErrorInfo implements IPlatformApiServiceErrorInfo {
  public constructor(data?: Partial<IPlatformApiServiceErrorInfo>) {
    if (data != null) {
      if (data.code != null) this.code = data.code;
      if (data.message != null) this.message = data.message;
      if (data.formattedMessagePlaceholderValues != null)
        this.formattedMessagePlaceholderValues = data.formattedMessagePlaceholderValues;
      if (data.target != null) this.target = data.target;
      if (data.details != null) this.details = data.details.map(p => new PlatformApiServiceErrorInfo(p));
    }
  }

  public code: PlatformApiServiceErrorInfoCode | string = '';

  public message?: string;

  public formattedMessagePlaceholderValues?: Map<string, string>;

  public target?: string;

  public details?: IPlatformApiServiceErrorInfo[];

  public isApplicationError(): boolean {
    return (
      this.code == PlatformApiServiceErrorInfoCode.PlatformValidationException ||
      this.code == PlatformApiServiceErrorInfoCode.PlatformApplicationException ||
      this.code == PlatformApiServiceErrorInfoCode.PlatformApplicationValidationException ||
      this.code == PlatformApiServiceErrorInfoCode.PlatformDomainException ||
      this.code == PlatformApiServiceErrorInfoCode.PlatformDomainValidationException ||
      this.code == PlatformApiServiceErrorInfoCode.PlatformPermissionException
    );
  }
}

export enum PlatformApiServiceErrorInfoCode {
  PlatformPermissionException = 'PlatformPermissionException',
  PlatformNotFoundException = 'PlatformNotFoundException',

  PlatformApplicationException = 'PlatformApplicationException',
  PlatformApplicationValidationException = 'PlatformApplicationValidationException',

  PlatformDomainException = 'PlatformDomainException',
  PlatformDomainValidationException = 'PlatformDomainValidationException',

  PlatformValidationException = 'PlatformValidationException',

  InternalServerException = 'InternalServerException',
  ConnectionRefused = 'ConnectionRefused',
  TimeoutError = 'TimeoutError',
  Unknown = 'Unknown'
}
