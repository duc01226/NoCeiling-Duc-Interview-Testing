// import { AsyncValidatorFn, FormControl, ValidationErrors } from '@angular/forms';
// import { buildFormValidationErrors, PlatformApiServiceErrorResponse } from '@orient/platform-core';
// import { catchError, delay, map, Observable, of, Subject, switchMap, takeUntil } from 'rxjs';

// import { CheckOverlapLeaveRequestQuery, CheckOverlapLeaveRequestQueryResult } from '../api-services';

// export function checkIsLeaveRequestDateRangeOverlappedAsyncValidator(
//   errorKey: string,
//   checkOverlap$: (query: CheckOverlapLeaveRequestQuery) => Observable<CheckOverlapLeaveRequestQueryResult>,
//   queryFn: (control: FormControl<Date>) => CheckOverlapLeaveRequestQuery | undefined
// ): AsyncValidatorFn {
//   const cancelPreviousSub: Subject<null> = new Subject();

//   return validator((control: FormControl): Promise<ValidationErrors | null> | Observable<ValidationErrors | null> => {
//     cancelPreviousSub.next(null);
//     const query = queryFn(control);

//     if (query == null) {
//       return of(null);
//     }

//     return of(null).pipe(
//       takeUntil(cancelPreviousSub),
//       delay(300),
//       switchMap(() => {
//         return checkOverlap$(query).pipe(
//           takeUntil(cancelPreviousSub),
//           map((result: CheckOverlapLeaveRequestQueryResult) => {
//             return result.isOverlapped
//               ? buildFormValidationErrors(errorKey, 'The given date range is overlapping with the previous requests')
//               : null;
//           }),
//           catchError((error: PlatformApiServiceErrorResponse | Error) =>
//             of(buildFormValidationErrors(errorKey, PlatformApiServiceErrorResponse.getDefaultFormattedMessage(error)))
//           )
//         );
//       })
//     );
//   });
// }
