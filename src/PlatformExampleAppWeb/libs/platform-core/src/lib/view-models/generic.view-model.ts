import { PlatformApiServiceErrorResponse } from '../api-services';
import { immutableUpdate, keys, list_distinct } from '../utils';

const requestStateDefaultKey = 'Default';
export type StateStatus = 'Pending' | 'Loading' | 'Success' | 'Error' | 'Reloading';

export interface IPlatformVm {
    status?: StateStatus;
    error?: string | null;
}

/**
 * Class representing the Platform View Model.
 *
 * @class
 * @implements IPlatformVm
 *
 * @constructor
 * @param {Partial<IPlatformVm>} data - Data to initialize the view model state.
 *
 * @property {StateStatus} status - Current status of the view model state.
 * @property {string | undefined} error - Error message associated with the view model state.
 * @property {Dictionary<string | undefined>} errorMsgMap - Dictionary storing error messages for different request keys.
 * @property {Dictionary<boolean | undefined | null>} loadingMap - Dictionary storing loading states for different request keys.
 * @property {Dictionary<boolean | undefined | null>} reloadingMap - Dictionary storing reloading states for different request keys.
 * @property {string | undefined} allErrorMsgs - Concatenated error messages for all request keys.
 *
 * @method getAllErrorMsgs - Gets the concatenated error messages for all request keys.
 * @method setErrorMsg - Sets the error message for a specific request key in the view model state.
 * @method getErrorMsg - Gets the error message for a specific request key from the view model state.
 * @method setLoading - Sets the loading state for a specific request key in the view model state.
 * @method setReloading - Sets the reloading state for a specific request key in the view model state.
 * @method isLoading - Gets the loading state for a specific request key from the view model state.
 * @method isReloading - Gets the reloading state for a specific request key from the view model state.
 * @method isAnyLoadingRequest - Checks if there is any request with a loading state in the view model state.
 * @method isAnyReloadingRequest - Checks if there is any request with a reloading state in the view model state.
 */
export class PlatformVm implements IPlatformVm {
    public static requestStateDefaultKey = requestStateDefaultKey;

    public status: StateStatus = 'Pending';
    public error: string | undefined | null = undefined;

    public errorMsgMap: Dictionary<string | undefined> = {};
    public loadingMap: Dictionary<boolean | undefined> = {};
    public reloadingMap: Dictionary<boolean | undefined> = {};
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

    public get isStateReloading(): boolean {
        return this.status == 'Reloading';
    }

    public get isStateSuccess(): boolean {
        return this.status == 'Success' && this.error == undefined;
    }

    public get isStateError(): boolean {
        return this.status == 'Error' || this.error != undefined;
    }

    public getAllErrorMsgs(requestKeys?: string[], excludeKeys?: string[]): string | undefined {
        const joinedErrorsStr = list_distinct(
            keys(this.errorMsgMap)
                .map(key => {
                    if ((requestKeys != undefined && !requestKeys.includes(key)) || excludeKeys?.includes(key) == true)
                        return '';

                    return this.errorMsgMap[key] ?? '';
                })
                .concat([this.error ?? ''])
                .filter(msg => msg != '' && msg != null)
        ).join('; ');

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

        this.errorMsgMap = immutableUpdate(
            this.errorMsgMap,
            _ => {
                _[requestKey] = errorMsg;
            },
            { updaterNotDeepMutate: true }
        );

        this.allErrorMsgs = this.getAllErrorMsgs();
        this.error = errorMsg;
    }

    public getErrorMsg(requestKey: string = requestStateDefaultKey): string | undefined {
        if (this.errorMsgMap[requestKey] == null && requestKey == requestStateDefaultKey)
            return <string | undefined>this.error;

        return this.errorMsgMap[requestKey];
    }

    public setLoading(value: boolean | undefined, requestKey: string = requestStateDefaultKey) {
        this.loadingMap = immutableUpdate(
            this.loadingMap,
            _ => {
                _[requestKey] = value;
            },
            { updaterNotDeepMutate: true }
        );
    }

    public setReloading(value: boolean | undefined, requestKey: string = requestStateDefaultKey) {
        this.reloadingMap = immutableUpdate(
            this.reloadingMap,
            _ => {
                _[requestKey] = value;
            },
            { updaterNotDeepMutate: true }
        );
    }

    public isLoading(requestKey: string = requestStateDefaultKey): boolean {
        return this.loadingMap[requestKey] == true;
    }

    public isReloading(requestKey: string = requestStateDefaultKey): boolean {
        return this.reloadingMap[requestKey] == true;
    }

    public isAnyLoadingRequest(): boolean | undefined {
        return keys(this.loadingMap).find(requestKey => this.loadingMap[requestKey]) != undefined;
    }

    public isAnyReloadingRequest(): boolean | undefined {
        return keys(this.reloadingMap).find(requestKey => this.reloadingMap[requestKey]) != undefined;
    }

    public clearAllErrorMsgs() {
        this.allErrorMsgs = undefined;
        this.errorMsgMap = {};
        this.error = undefined;

        return this;
    }
}
