export type ValidationError = { code: string; errorMsg: string };
export class Validation<TValue = void> {
    public value?: TValue;
    public errors: ValidationError[] = [];
    public get isValid() {
        return this.errors.length == 0;
    }
    public get error(): ValidationError | undefined {
        return this.errors[0];
    }

    constructor(data?: Partial<Validation<TValue>>) {
        if (data == undefined) return;

        if (data.errors != null) this.errors = data.errors;
        if (data.value != null) this.value = data.value;
    }

    public static valid<TValue>(value?: TValue): Validation<TValue> {
        return new Validation<TValue>({ value: value });
    }

    public static invalid<TValue>(value: TValue, errorOrMsg: ValidationError | string): Validation<TValue> {
        return new Validation<TValue>({ value: value, errors: [Validation.buildValidationError(errorOrMsg)] });
    }

    public static buildValidationError(errorOrMsg: ValidationError | string): ValidationError {
        return typeof errorOrMsg == 'string' ? { code: '', errorMsg: errorOrMsg } : errorOrMsg;
    }

    public static validate<TValue>(
        value: TValue,
        validCondition: boolean,
        error: ValidationError | string
    ): Validation<TValue> {
        return validCondition ? this.valid(value) : this.invalid(value, error);
    }

    public static validateNot<TValue>(
        value: TValue,
        invalidCondition: boolean,
        error: ValidationError | string
    ): Validation<TValue> {
        return !invalidCondition ? this.valid(value) : this.invalid(value, error);
    }

    public and(
        validCondition: (value: TValue | undefined) => boolean,
        error: ValidationError | string
    ): Validation<TValue> {
        if (!this.isValid) return this;

        return <Validation<TValue>>(
            (validCondition(this.value) ? Validation.valid(this.value) : Validation.invalid(this.value, error))
        );
    }

    public andNot(
        invalidCondition: (value: TValue | undefined) => boolean,
        error: ValidationError | string
    ): Validation<TValue> {
        if (!this.isValid) return this;

        return <Validation<TValue>>(
            (!invalidCondition(this.value) ? Validation.valid(this.value) : Validation.invalid(this.value, error))
        );
    }

    public andNextValidate<TNextValue>(
        nextValidate: (value: TValue | undefined) => Validation<TNextValue>
    ): Validation<TNextValue> {
        if (!this.isValid) return this.ofValue(<TNextValue>undefined);

        return nextValidate(this.value);
    }

    public ofValue<TValue>(value: TValue): Validation<TValue> {
        return new Validation<TValue>({ value: value, errors: this.errors });
    }

    public totalErrorMsg(): string {
        return this.errors.map(p => (typeof p == 'string' ? p : p.errorMsg)).join('; ');
    }

    public match<TValidReturn = void, TInvalidReturn = void>(options: {
        valid: (value: TValue | undefined) => TValidReturn;
        invalid: (errorValidation: Validation<TValue>) => TInvalidReturn;
    }): TValidReturn | TInvalidReturn {
        return this.isValid ? options.valid(this.value) : options.invalid(this);
    }
}
