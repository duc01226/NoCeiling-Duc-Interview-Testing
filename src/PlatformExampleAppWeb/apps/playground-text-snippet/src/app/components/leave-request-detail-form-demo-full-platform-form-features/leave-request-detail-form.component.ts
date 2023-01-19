// import { CommonModule } from '@angular/common';
// import { ChangeDetectionStrategy, Component, Input, OnInit, ViewEncapsulation } from '@angular/core';
// import { FormControl, Validators } from '@angular/forms';
// import { MatDatepickerModule } from '@angular/material/datepicker';
// import { MatLegacyButtonModule as MatButtonModule } from '@angular/material/legacy-button';
// import { MatLegacyDialogRef } from '@angular/material/legacy-dialog';
// import { MatLegacyFormFieldModule as MatFormFieldModule } from '@angular/material/legacy-form-field';
// import { MatLegacyInputModule as MatInputModule } from '@angular/material/legacy-input';
// import { MatLegacyRadioModule as MatRadioModule } from '@angular/material/legacy-radio';
// import { BravoCommonModule, BravoDialogService, BravoSelectDefaultItem } from '@orient/bravo-common';
// import {
//   checkIsLeaveRequestDateRangeOverlappedAsyncValidator,
//   LeaveRequest,
//   LeaveRequestApiService,
//   LeaveRequestDateType,
//   LeaveRequestStatusComponent,
//   LeaveType,
//   LeaveTypeApiService,
//   OrgApiService,
//   RemainingLeave,
//   RemainingLeaveApiService
// } from '@orient/bravo-domain/growth';
// import {
//   buildFormValidationErrors,
//   FormHelpers,
//   ifAsyncValidator,
//   noWhitespaceValidator,
//   PlatformApiServiceErrorResponse,
//   PlatformFormComponent,
//   PlatformFormConfig,
//   startEndValidator
// } from '@orient/platform-core';
// import { catchError, combineLatest, delay, map, Observable, of, switchMap, tap } from 'rxjs';

// import { AuthService } from '../../../core/auth/auth.service';
// import { LeaveTypeDisplayPipe } from '../../pipes/leave-type-display.pipe';
// import { CancelLeaveRequestComponent } from '../cancel-leave-request/cancel-leave-request.component';
// import { ConfirmationComponent } from '../confirmation/confirmation.component';
// import { EmployeeCardComponent } from '../employee-info/employee-card.component';
// import { LeaveRequestApproverComponent } from '../leave-request-approver/leave-request-approver.component';
// import { LeaveRequestDetailFormVm } from './leave-request-detail-form.view-model';

// export enum LeaveRequestDetailFormValidationKeys {
//   requestDateRangeOverlapped = 'requestDateRangeOverlapped',
//   notEnoughRemainingLeave = 'notEnoughRemainingLeave',
//   reachedMaximumTotalDays = 'reachedMaximumTotalDays'
// }

// @Component({
//   standalone: true,
//   imports: [
//     CommonModule,
//     BravoCommonModule,
//     MatButtonModule,
//     MatDatepickerModule,
//     MatRadioModule,
//     LeaveRequestApproverComponent,
//     LeaveRequestStatusComponent,
//     ConfirmationComponent,
//     MatFormFieldModule,
//     MatInputModule,
//     LeaveTypeDisplayPipe,
//     EmployeeCardComponent
//   ],
//   selector: 'app-leave-request-detail-form [organization]',
//   templateUrl: './leave-request-detail-form.component.html',
//   styleUrls: ['./leave-request-detail-form.component.scss'],
//   encapsulation: ViewEncapsulation.None,
//   changeDetection: ChangeDetectionStrategy.OnPush
// })
// export class LeaveRequestDetailFormComponent extends PlatformFormComponent<LeaveRequestDetailFormVm> implements OnInit {
//   @Input() public selectedCompanyId!: string;
//   @Input() public leaveRequest?: LeaveRequest;
//   @Input() public leaveRequestId?: string;

//   public leaveTypeSelectOptions: BravoSelectDefaultItem<string>[] = [];
//   public availableLeaveTypes: LeaveType[] = [];
//   public weekday: string[] = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'];
//   public dateTypeDisplay: Record<LeaveRequestDateType, string> = LeaveRequest.dateTypeDisplay;
//   public remainingLeave?: RemainingLeave;

//   constructor(
//     private bravoDialogService: BravoDialogService,
//     private leaveRequestApi: LeaveRequestApiService,
//     private orgApiService: OrgApiService,
//     private leaveTypeApi: LeaveTypeApiService,
//     private authService: AuthService,
//     private dialogRef: MatLegacyDialogRef<LeaveRequestDetailFormComponent>,
//     private remainingLeaveApi: RemainingLeaveApiService
//   ) {
//     super();
//   }

//   protected onInitVm = (): Observable<LeaveRequestDetailFormVm> => {
//     return combineLatest([
//       this.leaveRequestApi.getLeaveRequestApprover({
//         companyId: this.selectedCompanyId
//       }),
//       this.leaveRequestId != null
//         ? this.leaveRequestApi.getLeaveRequestDetail(this.leaveRequestId)
//         : of(this.leaveRequest),
//       this.orgApiService.getOrg(this.selectedCompanyId),
//       this.leaveTypeApi.getLeaveType({
//         skipCount: 0,
//         maxResultCount: 1000 // Temporary support for get all leave type
//       })
//     ]).pipe(
//       this.observerLoadingState(),
//       map(([approver, leaveRequest, selectedCompany, leaveTypesResult]) => {
//         this.availableLeaveTypes = leaveTypesResult.items ?? [];
//         this.leaveTypeSelectOptions = leaveTypesResult.items
//           .map(item => ({
//             value: item.id,
//             label: item.name
//           }))
//           .sort((previous, next) => previous.label.localeCompare(next.label));

//         const initialVm: LeaveRequestDetailFormVm = new LeaveRequestDetailFormVm({
//           mode: this.mode,
//           approver: approver,
//           leaveRequest: leaveRequest,
//           companyId: this.selectedCompanyId,
//           selectedCompany: selectedCompany,
//           leaveTypeId: this.leaveTypeSelectOptions[0].value
//         });

//         return initialVm;
//       })
//     );
//   };

//   protected initialFormConfig = (): PlatformFormConfig<LeaveRequestDetailFormVm> => {
//     return {
//       controls: {
//         reason: new FormControl(this.vm.reason, [Validators.required, noWhitespaceValidator]),
//         fromDate: new FormControl(
//           this.vm.fromDate,
//           [
//             Validators.required,
//             startEndValidator(
//               'fromToDateRange',
//               control => control.value,
//               () => this.vm.toDate,
//               { allowEqual: true, checkDatePart: 'dateOnly' }
//             )
//           ],
//           [
//             ifAsyncValidator(
//               () => !this.isViewMode,
//               checkIsLeaveRequestDateRangeOverlappedAsyncValidator(
//                 LeaveRequestDetailFormValidationKeys.requestDateRangeOverlapped,
//                 query => this.leaveRequestApi.checkOverlapLeaveRequest(query).pipe(this.finalDetectChanges()),
//                 () => ({
//                   fromDate: this.vm.getFromDateBaseOnMorningWorkingHour(),
//                   toDate: this.vm.getToDateBaseOnAfternoonWorkingHour(),
//                   userId: this.authService.currentUserInfo.userId,
//                   checkForCurrentExistingRequestId: this.vm.formData.id,
//                   companyId: this.vm.selectedCompany.id
//                 })
//               )
//             )
//           ]
//         ),
//         toDate: new FormControl(this.vm.toDate, [Validators.required]),
//         type: new FormControl(this.vm.type, Validators.required),
//         leaveTypeId: new FormControl(this.vm.leaveTypeId, Validators.required, [
//           ifAsyncValidator(
//             control => !this.isViewMode && this.vm.canValidateRemainingDate(control.value, this.availableLeaveTypes),
//             control => {
//               return of(control.value).pipe(
//                 delay(100),
//                 switchMap(() => this.remainingLeaveApi.getRemainingLeave(control.value)),
//                 tap(result => {
//                   this.remainingLeave = result;
//                   this.detectChanges();
//                 }),
//                 map((remainingLeave: RemainingLeave) => {
//                   if (remainingLeave.currentRemainingInfo.remainingDays <= 0) {
//                     return buildFormValidationErrors(
//                       LeaveRequestDetailFormValidationKeys.notEnoughRemainingLeave,
//                       'The number of remaining leaves is not sufficient for this leave type. Please try another one!'
//                     );
//                   } else if (remainingLeave.currentRemainingInfo.remainingDays < this.vm.totalDays) {
//                     return buildFormValidationErrors(
//                       LeaveRequestDetailFormValidationKeys.reachedMaximumTotalDays,
//                       'The total number of leaves could not be greater than the remaining leaves.'
//                     );
//                   }

//                   return null;
//                 }),
//                 catchError((error: PlatformApiServiceErrorResponse | Error) =>
//                   of(
//                     buildFormValidationErrors(
//                       LeaveRequestDetailFormValidationKeys.notEnoughRemainingLeave,
//                       PlatformApiServiceErrorResponse.getDefaultFormattedMessage(error)
//                     )
//                   )
//                 )
//               );
//             }
//           )
//         ]),
//         companyId: new FormControl(this.vm.companyId, Validators.required),
//         totalDays: new FormControl(this.vm.totalDays, Validators.required),
//         approverId: new FormControl(this.vm.approverId, Validators.required)
//       },
//       groupValidations: [
//         ['fromDate', 'toDate'],
//         ['totalDays', 'leaveTypeId']
//       ]
//     };
//   };

//   public notWeekendDatesFilter = LeaveRequest.notWeekendDatesFilter;

//   public close() {
//     this.dialogRef.close();
//   }

//   public onLeaveRequestDateTypeChanged(): void {
//     this.updateVm(_ => _.handleOnLeaveRequestDateTypeChanged());
//   }

//   public onClickSubmitLeaveRequest(): void {
//     if (!FormHelpers.validateForm(this.form)) return;

//     this.bravoDialogService
//       .openConfirmDialog({ confirmMsg: 'Do you want to submit your leave request?', showCloseBtn: true })
//       .subscribe((ok: boolean) => {
//         if (!ok) return;

//         this.submitLeaveRequest();
//       });
//   }

//   public submitLeaveRequest = this.effect(() => {
//     this.form.disable();
//     console.log(this.vm.formData);
//     return this.leaveRequestApi
//       .createLeaveRequest({
//         data: {
//           ...this.vm.formData,
//           fromDate: this.vm.getFromDateBaseOnMorningWorkingHour(),
//           toDate: this.vm.getToDateBaseOnAfternoonWorkingHour()
//         }
//       })
//       .pipe(
//         this.observerLoadingState('submitLeaveRequest'),
//         this.tapResponse(
//           () => {
//             this.toast.success('Your leave request has been created successfully');
//             this.bravoDialogService.closeLastCurrentDialog({ saveLeaveRequest: true });
//           },
//           () => {
//             this.form.enable({ emitEvent: false, onlySelf: true });
//           }
//         )
//       );
//   });

//   public closeDialog(): void {
//     this.bravoDialogService
//       .openConfirmDialog({
//         confirmMsg: 'Your request will be discarded. Do you want to continue?',
//         showCloseBtn: true
//       })
//       .subscribe((ok: boolean) => {
//         if (!ok) return;

//         this.close();
//       });
//   }

//   public onCancelCreatedLeaveRequest() {
//     if (!this.vm.leaveRequest) return;

//     this.bravoDialogService
//       .openDialog(
//         CancelLeaveRequestComponent,
//         {
//           leaveRequestId: this.vm.leaveRequest.id,
//           currentLeaveRequestStatus: this.vm.leaveRequest.status
//         },
//         {
//           autoFocus: false,
//           closeOn$: this.destroyed$
//         }
//       )
//       .subscribe(reloadRequired => {
//         if (reloadRequired) this.dialogRef.close({ saveLeaveRequest: reloadRequired });
//       });
//   }
// }
