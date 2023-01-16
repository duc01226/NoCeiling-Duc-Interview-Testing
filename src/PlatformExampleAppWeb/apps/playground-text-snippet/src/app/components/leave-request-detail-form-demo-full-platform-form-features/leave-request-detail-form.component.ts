// import { CommonModule } from '@angular/common';
// import { ChangeDetectionStrategy, Component, Input, OnInit, QueryList, ViewChildren, ViewEncapsulation } from '@angular/core';
// import { FormControl, Validators } from '@angular/forms';
// import { DateAdapter } from '@angular/material/core';
// import { MatDatepickerModule } from '@angular/material/datepicker';
// import { MatLegacyButtonModule as MatButtonModule } from '@angular/material/legacy-button';
// import { MatLegacyFormFieldModule as MatFormFieldModule } from '@angular/material/legacy-form-field';
// import { MatLegacyInputModule as MatInputModule } from '@angular/material/legacy-input';
// import { MatLegacyRadioModule as MatRadioModule } from '@angular/material/legacy-radio';
// import { FormHelpers, ifAsyncValidator, PlatformFormComponent, PlatformFormConfig, startEndValidator } from '@libs/platform-core';
// import { combineLatest, map, Observable, of } from 'rxjs';

// import { checkIsLeaveRequestDateRangeOverlappedAsyncValidator } from '../../form-validators';
// import { LeaveRequestDetailFormDemoFullFormFeatureVm } from './leave-request-detail-form.view-model';

// @Component({
//   standalone: true,
//   imports: [
//     CommonModule,
//     MatButtonModule,
//     MatDatepickerModule,
//     MatRadioModule,
//     LeaveRequestApproverComponent,
//     LeaveRequestStatusComponent,
//     ConfirmationComponent,
//     MatFormFieldModule,
//     MatInputModule,
//     LeaveTypeDisplayPipe,
//     CancelLeaveRequestComponent
//   ],
//   selector: 'app-leave-request-detail-form-demo-full-features [organization]',
//   templateUrl: './leave-request-detail-form.component.html',
//   styleUrls: ['./leave-request-detail-form.component.scss'],
//   encapsulation: ViewEncapsulation.None,
//   changeDetection: ChangeDetectionStrategy.OnPush,
//   providers: [{ provide: DateAdapter, useClass: CustomDateAdapter }]
// })
// export class LeaveRequestDetailDemoFullFeaturesFormComponent
//   extends PlatformFormComponent<LeaveRequestDetailFormDemoFullFormFeatureVm>
//   implements OnInit
// {
//   @Input() public selectedCompanyId!: string;
//   @Input() public leaveRequest?: LeaveRequest;
//   @Input() public leaveRequestId?: string;

//   @ViewChildren(CancelLeaveRequestComponent) public cancelLeaveRequestForms: QueryList<CancelLeaveRequestComponent>;

//   public leaveTypeSelectOptions: BravoSelectDefaultItem<string>[] = [
//     { value: LeaveRequestType.AnnualLeave, label: LeaveRequestType.AnnualLeave },
//     { value: LeaveRequestType.UnpaidLeave, label: LeaveRequestType.UnpaidLeave },
//     { value: LeaveRequestType.SickLeave, label: LeaveRequestType.SickLeave },
//     { value: LeaveRequestType.MarriageLeave, label: LeaveRequestType.MarriageLeave },
//     { value: LeaveRequestType.ChildrenMarriageLeave, label: LeaveRequestType.ChildrenMarriageLeave },
//     { value: LeaveRequestType.BereavementLeave, label: LeaveRequestType.BereavementLeave }
//   ];
//   public weekday: string[] = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'];
//   public approver: LeaveRequestApprover;

//   constructor(
//     private bravoDialogService: BravoDialogService,
//     private leaveRequestApi: LeaveRequestApiService,
//     private leaveTypeApi: LeaveTypeApiService,
//     private orgApiService: OrgApiService,
//     private authService: AuthService
//   ) {
//     super();
//   }

//   protected onInitVm = (): Observable<LeaveRequestDetailFormDemoFullFormFeatureVm> => {
//     return combineLatest([
//       this.leaveRequestApi.getLeaveRequestApprover({
//         companyId: this.selectedCompanyId
//       }),
//       this.leaveRequestId != null ? this.leaveRequestApi.getLeaveRequestDetail(this.leaveRequestId) : of(this.leaveRequest),
//       this.orgApiService.getOrg(this.selectedCompanyId),
//       this.leaveTypeApi.getLeaveType({
//         skipCount: 0,
//         maxResultCount: 1000 // Temporary support for get all leave type
//       })
//     ]).pipe(
//       this.observerLoadingState(),
//       map(([approver, leaveRequest, selectedCompany, leaveTypes]) => {
//         console.log(leaveTypes, 1);
//         const initialVm = new LeaveRequestDetailFormDemoFullFormFeatureVm({
//           mode: this.mode,
//           approver: approver,
//           leaveRequest: leaveRequest,
//           companyId: this.selectedCompanyId,
//           selectedCompany: selectedCompany
//         });

//         initialVm.formData.employeeId = this.authService.currentUserInfo.userId;

//         return initialVm;
//       })
//     );
//   };

//   protected initialFormConfig = (): PlatformFormConfig<LeaveRequestDetailFormDemoFullFormFeatureVm> | undefined => {
//     return {
//       controls: {
//         reason: new FormControl(this.vm.reason, Validators.required),
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
//                 'requestDateRangeOverlapped',
//                 query => this.leaveRequestApi.checkOverlapLeaveRequest(query).pipe(this.finalDetectChanges()),
//                 () => ({
//                   fromDate: this.vm.getFromDateBaseOnMorningWorkingHour(),
//                   toDate: this.vm.getToDateBaseOnAfternoonWorkingHour(),
//                   userId: this.vm.formData.employeeId,
//                   checkForCurrentExistingRequestId: this.vm.formData.id,
//                   companyId: this.selectedCompanyId
//                 })
//               )
//             )
//           ]
//         ),
//         toDate: new FormControl(this.vm.toDate, [Validators.required]),
//         type: new FormControl(this.vm.type, Validators.required),
//         companyId: new FormControl(this.vm.companyId, Validators.required),
//         totalDays: new FormControl(this.vm.totalDays, Validators.required),
//         testFormArrayGroupLeaveRequests: {
//           modelItems: () => this.vm.testFormArrayGroupLeaveRequests,
//           itemControl: item => ({
//             reason: new FormControl(item.reason, Validators.required),
//             leaveRequestId: new FormControl(item.leaveRequestId, Validators.required)
//           })
//         },
//         testPrimitivePropsForArrayControls: {
//           modelItems: () => this.vm.testPrimitivePropsForArrayControls,
//           itemControl: item => new FormControl(item, [Validators.required])
//         }
//       },
//       groupValidations: [['fromDate', 'toDate']],
//       childForms: () => [this.cancelLeaveRequestForms]
//     };
//   };

//   public notWeekendDatesFilter = LeaveRequestDetailFormDemoFullFormFeatureVm.notWeekendDatesFilter;

//   public close() {
//     this.bravoDialogService.closeLastCurrentDialog();
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
//             this.toast.success('Successfully create your leave request!');
//             this.bravoDialogService.closeLastCurrentDialog({ saveLeaveRequest: true });
//           },
//           () => {
//             this.form.enable({ emitEvent: false, onlySelf: true });
//           }
//         )
//       );
//   });

//   public cancelLeaveRequest(): void {
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

//   public testAddMoreItemForArrayFormDemo() {
//     this.updateVm(p => p.testAddMoreItemForArrayFormDemo());
//   }
// }
