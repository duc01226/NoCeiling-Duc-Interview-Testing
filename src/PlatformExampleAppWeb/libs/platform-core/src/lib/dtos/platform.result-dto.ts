/* eslint-disable @typescript-eslint/no-explicit-any */
/* eslint-disable @typescript-eslint/no-empty-interface */
import { IPlatformDataModel } from '../domain/data-model/platform.data-model';
import { immutableUpdate } from '../utils';

export class PlatformResultDto {}

export interface IPlatformPagedResultDto<TItem> {
    items: TItem[];
    totalCount: number;
    pageSize?: number;
    skipCount?: number;
    totalPages?: number;
    pageIndex?: number;
    selectedItems?: TItem[];
}

export interface PlatformPagedResultDtoOptions<
    TData extends PlatformPagedResultDto<TItem>,
    TItem extends IPlatformDataModel
> {
    data: Partial<TData> | undefined;
    itemInstanceCreator: (item: TItem | Partial<TItem>) => TItem;
}

export class PlatformPagedResultDto<TItem extends IPlatformDataModel>
    extends PlatformResultDto
    implements IPlatformPagedResultDto<TItem>
{
    public constructor(options?: PlatformPagedResultDtoOptions<PlatformPagedResultDto<TItem>, TItem>) {
        super();

        const data = options?.data;
        const itemInstanceCreator = options?.itemInstanceCreator;

        this.items = data?.items?.map(item => (itemInstanceCreator != null ? itemInstanceCreator(item) : item)) ?? [];
        if (data?.totalCount != undefined) this.totalCount = data?.totalCount;
        if (data?.pageSize != undefined) this.pageSize = data?.pageSize;

        this.skipCount = data?.skipCount;
        this.totalPages = data?.totalPages;
        this.pageIndex = data?.pageIndex;
        this.selectedItems = data?.selectedItems;
    }

    public items: TItem[] = [];
    public totalCount: number = 0;
    public pageSize: number = 0;
    public skipCount?: number;
    public totalPages?: number;
    public pageIndex?: number;
    public selectedItems?: TItem[] = [];

    public withItems(items: TItem[]): PlatformPagedResultDto<TItem> {
        return immutableUpdate(<PlatformPagedResultDto<TItem>>this, {
            items: items
        });
    }
}
