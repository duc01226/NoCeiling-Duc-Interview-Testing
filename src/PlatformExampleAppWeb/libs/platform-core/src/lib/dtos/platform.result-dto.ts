/* eslint-disable @typescript-eslint/no-empty-interface */
import { IPlatformDataModel } from '../domain/data-model/platform.data-model';

export interface IPlatformResultDto {}

export class PlatformResultDto implements IPlatformResultDto {}

export interface IPlatformPagedResultDto<TItem> {
  items: TItem[];
  totalCount: number;
  pageSize?: number;
  skipCount?: number;
  totalPages?: number;
  pageIndex?: number;
}

export class PlatformPagedResultDto<TItem extends IPlatformDataModel>
  extends PlatformResultDto
  implements IPlatformPagedResultDto<TItem>
{
  public constructor(data?: Partial<IPlatformPagedResultDto<TItem>>, itemInstanceCreater?: (item: TItem) => TItem) {
    super();
    this.items = data?.items?.map(_ => (itemInstanceCreater != null ? itemInstanceCreater(_) : _)) ?? [];
    this.totalCount = data?.totalCount ?? 0;
    this.pageSize = data?.pageSize ?? 0;
    this.skipCount = data?.skipCount;
    this.totalPages = data?.totalPages;
    this.pageIndex = data?.pageIndex;
  }

  public items: TItem[];
  public totalCount: number;
  public pageSize: number;
  public skipCount?: number;
  public totalPages?: number;
  public pageIndex?: number;
}
