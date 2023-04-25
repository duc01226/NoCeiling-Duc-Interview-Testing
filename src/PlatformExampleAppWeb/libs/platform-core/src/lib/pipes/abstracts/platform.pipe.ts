import { PipeTransform } from '@angular/core';

export abstract class PlatformPipe<TValue, TArg, TReturn> implements PipeTransform {
    public abstract transform(value: TValue, ...args: TArg[]): TReturn;
}
