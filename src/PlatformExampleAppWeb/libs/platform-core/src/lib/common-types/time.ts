import { number_formatLength } from '../utils';

export interface ITime {
    hour: number;
    minute: number;
    second: number;
}

export class Time implements ITime {
    public static parse(data: string | Time): Time {
        if (data instanceof Time) return data;

        return this.fromString(data) ?? new Time();
    }

    public static fromString(value: string | null | undefined | Time): Time | undefined {
        if (value == null) return undefined;

        const hour = value.toString().substring(0, 2);
        const minute = value.toString().substring(3, 5);
        const second = value.toString().substring(6, 8);

        const time: ITime = {
            hour: Number.parseInt(hour),
            minute: Number.parseInt(minute),
            second: Number.parseInt(second)
        };

        return Number.isNaN(time.hour) || Number.isNaN(time.minute) || Number.isNaN(time.second)
            ? undefined
            : new Time(time);
    }

    public static compareTime(from: Time | undefined, to: Time | undefined, isEqual: boolean = false): boolean {
        if (from == undefined || to == undefined) return false;

        const fromDate = this.setTimeIntoDate(new Date(), from) as Date;
        const toDate = this.setTimeIntoDate(new Date(), to) as Date;
        if (isEqual) return fromDate <= toDate;
        else return fromDate < toDate;
    }

    public hour: number = 0;
    public minute: number = 0;
    public second: number = 0;

    constructor(data?: Partial<ITime>) {
        if (data == undefined) return;

        if (data.hour != undefined) this.hour = data.hour;
        if (data.minute != undefined) this.minute = data.minute;
        if (data.second != undefined) this.second = data.second;
    }

    public changeHour(step: number = 1) {
        this.updateHour(this.hour + step);
    }

    public updateHour(hour: number) {
        this.hour = hour < 0 ? 0 : hour % 24;

        return this;
    }

    public changeMinute(step: number = 1) {
        this.updateMinute(this.minute + step);
    }

    public updateMinute(minute: number) {
        this.minute = minute % 60 < 0 ? 60 + (minute % 60) : minute % 60;
        this.changeHour(Math.floor(minute / 60));

        return this;
    }

    public changeSecond(step: number = 1) {
        this.updateSecond(this.second + step);
    }

    public updateSecond(second: number) {
        this.second = second < 0 ? 60 + (second % 60) : second % 60;
        this.changeMinute(Math.floor(second / 60));

        return this;
    }

    public toString(): string {
        return (
            `${number_formatLength(this.hour, 2)}` +
            `:${number_formatLength(this.minute, 2)}` +
            `:${number_formatLength(this.second, 2)}`
        );
    }

    public hourMinuteDisplay(): string {
        return `${number_formatLength(this.hour, 2)}` + `:${number_formatLength(this.minute, 2)}`;
    }

    public diff(otherTime: Time): number {
        const minutesInHour = 60;

        const currTime = this.hour * minutesInHour + this.minute;
        const otherTimeConvertToMinutes = otherTime.hour * minutesInHour + otherTime.minute;

        const timeDiffMinutes = Math.abs(currTime - otherTimeConvertToMinutes); //Ensure that the result is always positive
        const timeDiffHours = timeDiffMinutes / minutesInHour;
        return Number(timeDiffHours.toFixed(1)); //Round to 1 decimal, ex: 1.55555 to 1.6
    }

    public toJSON(): string {
        return this.toString();
    }

    public static setTimeIntoDate(date?: Date, time?: Time | null): Date | undefined {
        if (date == undefined || time == undefined) return;

        const newDate = new Date(date);
        newDate.setHours(time.hour, time.minute, time.second);

        return newDate;
    }

    public static isTimeInRange(
        from: Time | undefined,
        to: Time | undefined,
        time: Time | undefined,
        isIncludedFrom?: boolean,
        isIncludedTo?: boolean
    ): boolean {
        if (from == undefined || to == undefined || time == undefined) return false;

        if (isIncludedFrom && isIncludedTo) {
            return Time.compareTime(from, time, isIncludedFrom) && Time.compareTime(time, to, isIncludedTo);
        } else if (isIncludedFrom) {
            return Time.compareTime(from, time, isIncludedFrom) && Time.compareTime(time, to);
        } else if (isIncludedTo) {
            return Time.compareTime(from, time) && Time.compareTime(time, to, isIncludedTo);
        } else return Time.compareTime(from, time) && Time.compareTime(time, to);
    }
}
