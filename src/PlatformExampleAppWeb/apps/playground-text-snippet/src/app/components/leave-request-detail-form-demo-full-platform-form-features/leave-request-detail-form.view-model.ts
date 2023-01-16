// import {
//   cloneDeep,
//   date_daysInRange,
//   date_setToEndOfDay,
//   date_setToStartOfDay,
//   immutableUpdate,
//   isDifferent,
//   list_last,
//   PlatformFormMode,
//   PlatformVm
// } from '@libs/platform-core';

// export class LeaveRequestDetailFormDemoFullFormFeatureVm extends PlatformVm {
//   public static notWeekendDatesFilter = (date: Date): boolean => {
//     return date ? date.getDay() !== WEEKDAY.Sunday : false;
//   };

//   constructor(data?: Partial<LeaveRequestDetailFormDemoFullFormFeatureVm>) {
//     super(data);
//     if (data != null) {
//       if (data.formData !== undefined) this._formData = data.formData;
//       if (data.reason !== undefined) this.reason = data.reason;
//       if (data.fromDate !== undefined) this.fromDate = new Date(data.fromDate);
//       if (data.toDate !== undefined) this.toDate = new Date(data.toDate);
//       if (data.type !== undefined) this.type = data.type;
//       if (data.companyId !== undefined) this.companyId = data.companyId;
//       if (data.totalDays !== undefined) this.totalDays = data.totalDays;
//       if (data.mode !== undefined) this.mode = data.mode;
//       if (data.approver !== undefined) {
//         this.approver = data.approver;
//         this.approverId = data.approver.userId;
//       }
//       if (data.leaveRequest !== undefined) {
//         this.leaveRequest = data.leaveRequest;
//         this._formData = cloneDeep(data.leaveRequest);
//       }
//       if (data.selectedCompany !== undefined) this.selectedCompany = new Organization(data.selectedCompany);
//     }

//     this.updateFromToDateRelatedInfo();
//   }

//   public testPrimitivePropsForArrayControls: number[] = [1];
//   public testFormArrayGroupLeaveRequests: CancelLeaveRequestVm[] = [{ reason: '', leaveRequestId: 'leaveRequestId' }];
//   public testAddMoreItemForArrayFormDemo = (): Partial<LeaveRequestDetailFormDemoFullFormFeatureVm> => {
//     this.testPrimitivePropsForArrayControls = this.testPrimitivePropsForArrayControls.concat([1]);
//     this.testFormArrayGroupLeaveRequests = this.testFormArrayGroupLeaveRequests.concat({ reason: '', leaveRequestId: 'leaveRequestId' });

//     return {
//       testPrimitivePropsForArrayControls: this.testPrimitivePropsForArrayControls,
//       testFormArrayGroupLeaveRequests: this.testFormArrayGroupLeaveRequests
//     };
//   };

//   public mode: PlatformFormMode = 'create';
//   public approver: Employee | null;
//   // To View or Update LeaveRequest
//   public leaveRequest: LeaveRequest | null;
//   public selectedCompany: Organization;

//   private _formData: Partial<ILeaveRequest> | LeaveRequest = {
//     fromDate: date_setToStartOfDay(new Date()),
//     toDate: date_setToEndOfDay(new Date()),
//     type: LeaveRequestType.AnnualLeave
//   };
//   public get formData(): Partial<ILeaveRequest> {
//     return this._formData;
//   }
//   public set formData(v: Partial<ILeaveRequest>) {
//     if (!isDifferent(this._formData, v)) return;
//     this._formData = v;
//     this.updateFromToDateRelatedInfo();
//   }

//   private leaveRequestDates: LeaveRequestDate[] = [];

//   public get reason(): string | undefined {
//     return this.formData.reason;
//   }
//   public set reason(v: string) {
//     this._formData = immutableUpdate(this.formData, { reason: v });
//   }

//   public get fromDate(): Date | undefined {
//     return this.formData.fromDate;
//   }
//   public set fromDate(v: Date) {
//     if (!isDifferent(this.fromDate, v)) return;

//     this._formData = immutableUpdate(this.formData, { fromDate: v });
//     if (this.toDate != null && this.toDate < this.fromDate) {
//       this.toDate = this.fromDate;
//       return;
//     }
//     this.updateFromToDateRelatedInfo();
//   }

//   public get toDate(): Date | undefined {
//     return this.formData.toDate;
//   }
//   public set toDate(v: Date) {
//     if (!isDifferent(this.toDate, v)) return;

//     this._formData = immutableUpdate(this.formData, { toDate: v });
//     if (this.fromDate != null && this.fromDate > this.toDate) {
//       this.fromDate = this.toDate;
//       return;
//     }
//     this.updateFromToDateRelatedInfo();
//   }

//   public get type(): string | undefined {
//     return this.formData.type;
//   }
//   public set type(v: string) {
//     this._formData = immutableUpdate(this.formData, { type: v });
//   }

//   public get companyId(): string | undefined {
//     return this.formData.companyId;
//   }
//   public set companyId(v: string) {
//     this._formData = immutableUpdate(this.formData, { companyId: v });
//   }

//   public get totalDays(): number | undefined {
//     return this.formData.totalDays;
//   }
//   public set totalDays(v: number) {
//     this._formData = immutableUpdate(this.formData, { totalDays: v });
//   }

//   public get approverId(): string | undefined {
//     return this.formData.approverId;
//   }
//   public set approverId(v: string) {
//     this._formData = immutableUpdate(this.formData, { approverId: v });
//   }

//   public updateFromToDateRelatedInfo() {
//     this.updateLeaveRequestDates();
//     this.updateFromToLeaveRequestDateType();
//     this.updateTotalDays();
//   }

//   public updateLeaveRequestDates(): void {
//     if (this.fromDate == null || this.toDate == null) return;

//     this.leaveRequestDates = date_daysInRange(this.fromDate, this.toDate)
//       .filter((date: Date) => LeaveRequestDetailFormDemoFullFormFeatureVm.notWeekendDatesFilter(date))
//       .map((date: Date) => ({
//         date: date.toISOString(),
//         dayOfWeek: date.getDay(),
//         type: LeaveRequestDateType.Full
//       }));
//   }

//   public updateFromToLeaveRequestDateType(): void {
//     if (!this.leaveRequestDates.length) return;

//     if (this.fromDate.getHours() >= 13) {
//       this.leaveRequestDates[0].type = LeaveRequestDateType.SecondHalf;
//     }
//     if (this.toDate.getHours() <= 12) {
//       list_last(this.leaveRequestDates).type = LeaveRequestDateType.FirstHalf;
//     }
//   }

//   public updateTotalDays(): void {
//     if (!this.leaveRequestDates.length) return;

//     let total: number = 0;
//     this.leaveRequestDates.forEach((day: LeaveRequestDate) => {
//       total = total + (day.type !== LeaveRequestDateType.Full ? 0.5 : 1);
//     });

//     this.totalDays = total;
//   }

//   public getFromDateBaseOnMorningWorkingHour(): Date {
//     const type: LeaveRequestDateType = this.leaveRequestDates[0].type;

//     if (type === LeaveRequestDateType.FirstHalf || type === LeaveRequestDateType.Full) {
//       return new Date(this.fromDate.setHours(8));
//     } else {
//       return new Date(this.fromDate.setHours(13));
//     }
//   }

//   public updateFromToDateTimeByRequestDateTypes(): void {
//     this.formData.fromDate?.setHours(LeaveRequest.getFromDateHourByLeaveRequestDateType(this.leaveRequestDates[0].type));
//     this.formData.toDate?.setHours(LeaveRequest.getToDateHourByLeaveRequestDateType(list_last(this.leaveRequestDates).type));
//   }

//   public getToDateBaseOnAfternoonWorkingHour(): Date {
//     const type: LeaveRequestDateType = this.leaveRequestDates[this.leaveRequestDates.length - 1].type;

//     if (type === LeaveRequestDateType.FirstHalf) {
//       return new Date(this.toDate.setHours(12));
//     } else {
//       return new Date(this.toDate.setHours(18));
//     }
//   }

//   public handleOnLeaveRequestDateTypeChanged(): void {
//     this.updateTotalDays();
//     this.updateFromToDateTimeByRequestDateTypes();
//   }
// }
