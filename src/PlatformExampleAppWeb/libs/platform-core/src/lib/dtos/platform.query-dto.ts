import { OrderDirection } from '../common-values/order-direction.enum';
import { clone } from '../utils';

/* eslint-disable @typescript-eslint/no-empty-interface */
export interface IPlatformQueryDto {}

export class PlatformQueryDto implements IPlatformQueryDto {}

export interface IPlatformRepositoryPagedQuery extends IPlatformQueryDto {
    skipCount: number;
    maxResultCount: number;
    orderBy?: string;
    orderDirection?: OrderDirection;
}

export class PlatformPagedQueryDto extends PlatformQueryDto implements IPlatformRepositoryPagedQuery {
    public orderDirection?: OrderDirection;
    public orderBy?: string;

    public constructor(data?: Partial<IPlatformRepositoryPagedQuery>) {
        super();

        if (data == null) return;

        if (data.skipCount != null) this.skipCount = data.skipCount;
        if (data.maxResultCount != null) this.maxResultCount = data.maxResultCount;
        if (data.orderDirection != null) this.orderDirection = data.orderDirection;
        if (data.orderBy != null) this.orderBy = data.orderBy;
    }

    public skipCount: number = 0;
    public maxResultCount: number = -1;

    public withPageIndex(pageIndex: number): PlatformPagedQueryDto {
        const newSkipCount = pageIndex * this.maxResultCount;

        if (this.skipCount == newSkipCount) return this;
        return clone(this, _ => {
            _.skipCount = newSkipCount;
        });
    }

    public withSort(orderDirection: OrderDirection, orderBy: string): PlatformPagedQueryDto {
        if (this.orderBy == orderBy && this.orderDirection == orderDirection) return this;
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
