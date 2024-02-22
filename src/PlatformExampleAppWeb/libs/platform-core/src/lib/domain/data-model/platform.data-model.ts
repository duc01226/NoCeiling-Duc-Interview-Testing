export interface IPlatformDataModel {
    id?: string | null;
}

export abstract class PlatformDataModel implements IPlatformDataModel {
    public constructor(data?: Partial<IPlatformDataModel>) {
        this.id = data?.id;
    }
    public id?: string | null;
}
