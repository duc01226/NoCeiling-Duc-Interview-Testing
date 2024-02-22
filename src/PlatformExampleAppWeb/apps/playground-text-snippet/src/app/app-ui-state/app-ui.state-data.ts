import { PlatformApiServiceErrorResponse, PlatformVm } from '@libs/platform-core';

export class AppUiStateData extends PlatformVm {
    public constructor(data?: Partial<AppUiStateData>) {
        super(data);
        if (data == undefined) return;

        this.selectedSnippetTextId = data?.selectedSnippetTextId;
        this.appError = data?.appError;
    }
    public selectedSnippetTextId?: string;
    public appError?: PlatformApiServiceErrorResponse | Error;

    public get appErrorMsg(): string | undefined {
        return this.appError != undefined
            ? PlatformApiServiceErrorResponse.getDefaultFormattedMessage(this.appError)
            : undefined;
    }

    // Demo get/set using platform watch decorator
    // Shorthand execute a target function doing something directly if on change only do this logic
    // @Watch('pagedResultWatch')
    // public pagedResult?: PlatformPagedResultDto<LeaveType>;

    // // Full syntax execute a NORMAL FUNCTION
    // @Watch<LeaveTypesState, PlatformPagedQueryDto>((value, change, targetObj) => {
    //   targetObj.updatePageInfo();
    // })
    // public pagedQuery: PlatformPagedQueryDto = new PlatformPagedQueryDto();

    // public pagedResultWatch(
    //   value: PlatformPagedResultDto<LeaveType> | undefined,
    //   change: SimpleChange<PlatformPagedResultDto<LeaveType> | undefined>
    // ) {
    //   this.updatePageInfo();
    // }

    // Demo using validation object
    /**
   * return Validation.validateNot(remainingLeave, remainingLeave.totalRemainingLeaveDays <= 0, {
                    code: LeaveRequestDetailFormValidationKeys.notEnoughRemainingLeave,
                    errorMsg:
                      'The number of remaining leaves is not sufficient for this leave type. Please try another one!'
                  })
                    .andNextValidate(remainingLeave =>
                      remainingLeave.validateEnoughAvailableRemainingLeaveDays(
                        this.vm().totalDays,
                        this.vm().fromDate,
                        LeaveRequestDetailFormValidationKeys.reachedMaximumTotalDays
                      )
                    )
                    .match({
                      valid: value => <ValidationErrors | null>null,
                      invalid: errorValidation =>
                        buildFormValidationErrors(errorValidation.error.code, errorValidation.error.errorMsg)
                    });
  */
}
