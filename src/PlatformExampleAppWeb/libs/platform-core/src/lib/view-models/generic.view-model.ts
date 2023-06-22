import { PlatformApiServiceErrorResponse } from '../api-services';
import { immutableUpdate, keys } from '../utils';

const requestStateDefaultKey = 'Default';
export type StateStatus = 'Pending' | 'Loading' | 'Success' | 'Error';

export interface IPlatformVm {
    status?: StateStatus;
    error?: string | null;
}

export class PlatformVm implements IPlatformVm {
    public static requestStateDefaultKey = requestStateDefaultKey;

    public status: StateStatus = 'Pending';
    public error: string | undefined | null = undefined;

    public errorMsgMap: Dictionary<string | undefined | null> = {};
    public loadingMap: Dictionary<boolean | undefined | null> = {};
    public allErrorMsgs?: string;

    constructor(data?: Partial<IPlatformVm>) {
        if (data == null) return;

        if (data.status !== undefined) this.status = data.status;
        if (data.error !== undefined) this.error = data.error;
    }

    public get isStatePending(): boolean {
        return this.status == 'Pending';
    }

    public get isStateLoading(): boolean {
        return this.status == 'Loading';
    }

    public get isStateSuccess(): boolean {
        return this.status == 'Success';
    }

    public get isStateError(): boolean {
        return this.status == 'Error';
    }

    public getAllErrorMsgs(): string | undefined {
        const joinedErrorsStr = keys(this.errorMsgMap)
            .map(key => this.errorMsgMap[key] ?? '')
            .concat([this.error ?? ''])
            .filter(msg => msg != '' && msg != null)
            .join('; ');

        return joinedErrorsStr == '' ? undefined : joinedErrorsStr;
    }

    public setErrorMsg(
        error: string | null | PlatformApiServiceErrorResponse | Error,
        requestKey: string = requestStateDefaultKey
    ) {
        const errorMsg =
            typeof error == 'string' || error == null
                ? <string | undefined>error
                : PlatformApiServiceErrorResponse.getDefaultFormattedMessage(error);

        this.errorMsgMap = immutableUpdate(this.errorMsgMap, _ => {
            _[requestKey] = errorMsg;
        });

        this.allErrorMsgs = this.getAllErrorMsgs();
        this.error = errorMsg;
    }

    public setLoading(value: boolean | undefined, requestKey: string = requestStateDefaultKey) {
        this.loadingMap = immutableUpdate(this.loadingMap, _ => {
            _[requestKey] = value;
        });
    }

    public getErrorMsg(requestKey: string = requestStateDefaultKey): string | null | undefined {
        if (this.errorMsgMap[requestKey] == null && requestKey == requestStateDefaultKey) return this.error;

        return this.errorMsgMap[requestKey];
    }

    public isLoading(requestKey: string = requestStateDefaultKey): boolean | null | undefined {
        return this.loadingMap[requestKey];
    }

    public isAnyLoadingRequest(): boolean | undefined {
        return keys(this.loadingMap).find(requestKey => this.loadingMap[requestKey]) != undefined;
    }
}
