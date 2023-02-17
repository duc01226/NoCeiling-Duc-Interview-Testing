import { OrderDirection } from '../common-values/order-direction.enum';
import { clone } from '../utils';

/* eslint-disable @typescript-eslint/no-empty-interface */
export interface IPlatformQueryDto {}

export class PlatformQueryDto implements IPlatformQueryDto {}

export interface IPlatformRepositoryPagedQuery extends IPlatformQueryDto {
  skipCount: number;
  maxResultCount: number;
  orderBy: string | null;
  orderDirection: OrderDirection | null;
}

export class PlatformPagedQueryDto extends PlatformQueryDto implements IPlatformRepositoryPagedQuery {
  public orderDirection: OrderDirection | null = null;
  public orderBy: string | null = null;

  public constructor(data?: Partial<IPlatformRepositoryPagedQuery>) {
    super();

    if (data == null) return;

    if (data.skipCount != null) this.skipCount = data.skipCount;
    if (data.maxResultCount != null) this.maxResultCount = data.maxResultCount;
    if (data.orderDirection != null) this.orderDirection = data.orderDirection;
    if (data.orderBy != null) this.orderBy = data.orderBy;
  }

  public skipCount: number = 0;
  public maxResultCount: number = 0;

  public withPageIndex(pageIndex: number): PlatformPagedQueryDto {
    return clone(this, _ => {
      _.skipCount = pageIndex * this.maxResultCount;
    });
  }

  public withSort(orderDirection: OrderDirection, orderBy: string): PlatformPagedQueryDto {
    return clone(this, _ => {
      _.orderBy = orderBy;
      _.orderDirection = orderDirection;
    });
  }

  public pageIndex(): number {
    if (this.maxResultCount == 0) return 0;
    return Math.floor(this.skipCount / this.maxResultCount);
  }

  public pageSize(): number {
    return this.maxResultCount;
  }
}
