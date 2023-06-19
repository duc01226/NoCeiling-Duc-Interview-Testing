/* eslint-disable @typescript-eslint/no-explicit-any */
import { AbstractControl, FormArray, FormGroup } from '@angular/forms';

export class FormHelpers {
    public static isFormValid(form: FormGroup): boolean {
        if (!form.controls) {
            return form.valid;
        }

        Object.values(form.controls).forEach((control: AbstractControl) => {
            if (control.invalid) {
                control.markAsDirty();
                control.markAsTouched();
                control.updateValueAndValidity({ onlySelf: true });
            }

            if (control instanceof FormArray)
                control.controls.some((form: AbstractControl) => !FormHelpers.isFormValid(form as FormGroup));
        });

        return form.valid;
    }

    public static convertModelToFormData(
        model: object,
        form: FormData | null = null,
        namespace: string = ''
    ): FormData {
        const formData: FormData = form || new FormData();

        Object.keys(model).forEach(propertyName => {
            if (Object.prototype.hasOwnProperty.call(model, propertyName) && (<any>model)[propertyName] != undefined) {
                const formKey = namespace ? namespace + '.' + propertyName : propertyName.toString();
                if ((<any>model)[propertyName] instanceof Date) {
                    formData.append(formKey, (<any>model)[propertyName].toISOString());
                } else if ((<any>model)[propertyName] instanceof Array) {
                    (<any[]>(<any>model)[propertyName]).forEach((element, index) => {
                        if (element instanceof File) {
                            formData.append(formKey, element, element.name);
                        } else if (typeof element === 'string' || typeof element === 'boolean') {
                            formData.append(formKey, element.toString());
                        } else if (element == undefined) {
                            formData.append(formKey, <any>null);
                        } else {
                            const tempFormKey = formKey + '[' + index + ']';
                            this.convertModelToFormData(element, formData, tempFormKey);
                        }
                    });
                } else if ((<any>model)[propertyName] instanceof File) {
                    formData.append(formKey, (<any>model)[propertyName], (<any>model)[propertyName].name);
                } else if (
                    typeof (<any>model)[propertyName] === 'object' &&
                    !((<any>model)[propertyName] instanceof File)
                ) {
                    this.convertModelToFormData((<any>model)[propertyName], formData, formKey);
                } else {
                    formData.append(formKey, (<any>model)[propertyName].toString());
                }
            }
        });

        return formData;
    }
}
