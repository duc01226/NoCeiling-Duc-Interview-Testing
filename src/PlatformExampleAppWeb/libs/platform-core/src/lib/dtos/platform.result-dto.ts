import { IPlatformDataModel } from '@platform-example-web/platform-core';

export interface IPlatformResultDto {}

export class PlatformResultDto implements IPlatformResultDto {}

export interface IPlatformPagedResultDto<TItem> {
  items: TItem[];
  totalCount: number;
  pageSize: number;
}

export class PlatformPagedResultDto<TItem extends IPlatformDataModel>
  extends PlatformResultDto
  implements IPlatformPagedResultDto<TItem> {
  public constructor(data?: Partial<IPlatformPagedResultDto<TItem>>) {
    super();
    this.items = data?.items ?? [];
    this.totalCount = data?.totalCount ?? 0;
    this.pageSize = data?.pageSize ?? 0;
  }

  public items: TItem[];
  public totalCount: number;
  public pageSize: number;
}
