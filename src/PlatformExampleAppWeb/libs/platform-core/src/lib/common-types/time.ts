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

  public static fromString(value: string | null): Time | null {
    if (value == null) return null;

    const hour = value.substring(0, 2);
    const minute = value.substring(3, 5);
    const second = value.substring(6, 8);

    const time: ITime = {
      hour: Number.parseInt(hour),
      minute: Number.parseInt(minute),
      second: Number.parseInt(second)
    };

    return Number.isNaN(time.hour) || Number.isNaN(time.minute) || Number.isNaN(time.second)
      ? null
      : new Time(time.hour, time.minute, time.second);
  }

  public hour: number = 0;
  public minute: number = 0;
  public second: number = 0;

  constructor(hour: number = 0, minute: number = 0, second: number = 0) {
    this.hour = hour;
    this.minute = minute;
    this.second = second;
  }

  public changeHour(step: number = 1) {
    this.updateHour(this.hour + step);
  }

  public updateHour(hour: number) {
    const validHour = hour < 0 ? 0 : hour % 24;
    this.hour = validHour;

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
  }

  public toString(): string {
    return `${number_formatLength(this.hour, 0)}:${number_formatLength(this.minute, 0)}:${number_formatLength(
      this.second,
      0
    )}`;
  }

  public toJSON(): string {
    return this.toString();
  }
}
