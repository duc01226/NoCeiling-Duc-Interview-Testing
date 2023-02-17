import { PlatformApiServiceErrorResponse, PlatformVm } from '@libs/platform-core';

export class AppUiStateData extends PlatformVm {
  public constructor(data?: Partial<AppUiStateData>) {
    super(data);

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
  // Another way to use
  // @Watch<PlatformPagedQueryDto>(function (value, change) {
  //   this.updatePageInfo();
  // })
  // @Watch('pagedQueryWatch')
  // public pagedQuery: PlatformPagedQueryDto = new PlatformPagedQueryDto();

  // public pagedQueryWatch: WatchCallBackFunction<PlatformPagedQueryDto> = (value, change) => {
  //   this.updatePageInfo();
  // };

  // public updatePageInfo(): void {
  //   this.pagedInfo = {
  //     pageIndex: this.pagedQuery.pageIndex() ?? 0,
  //     pageSize: this.pagedQuery.pageSize() ?? 0,
  //     totalItems: this.pagedResult?.totalCount
  //   };
  // }
  // Shorthand execute a target function doing something directly if on change only do this logic
  // @Watch('updatePageInfo')
  // public pagedResult?: PlatformPagedResultDto<LeaveType>;

  // Demo using validation object
  /** 
   * return Validation.validateNot(remainingLeave, remainingLeave.totalRemainingLeaveDays <= 0, {
                    code: LeaveRequestDetailFormValidationKeys.notEnoughRemainingLeave,
                    errorMsg:
                      'The number of remaining leaves is not sufficient for this leave type. Please try another one!'
                  })
                    .andNextValidate(remainingLeave =>
                      remainingLeave.validateEnoughAvailableRemainingLeaveDays(
                        this.vm.totalDays,
                        this.vm.fromDate,
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
