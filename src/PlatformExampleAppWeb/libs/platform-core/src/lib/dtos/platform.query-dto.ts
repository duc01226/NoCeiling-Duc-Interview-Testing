import { OrderDirection } from '../common-values/order-direction.enum';
import { clone } from '../utils';

/* eslint-disable @typescript-eslint/no-empty-interface */
export interface IPlatformQueryDto {}

export class PlatformQueryDto implements IPlatformQueryDto {}

export interface IPlatformRepositoryPagedQuery extends IPlatformQueryDto {
    skipCount?: number | null;
    maxResultCount?: number | null;
    orderBy?: string;
    orderDirection?: OrderDirection;
}

export class PlatformPagedQueryDto extends PlatformQueryDto implements IPlatformRepositoryPagedQuery {
    public orderDirection?: OrderDirection;
    public orderBy?: string;

    public constructor(data?: Partial<IPlatformRepositoryPagedQuery>) {
        super();

        if (data == null) return;

        if (data.skipCount !== undefined) this.skipCount = data.skipCount;
        if (data.maxResultCount !== undefined) this.maxResultCount = data.maxResultCount;
        if (data.orderDirection !== undefined) this.orderDirection = data.orderDirection;
        if (data.orderBy !== undefined) this.orderBy = data.orderBy;
    }

    public skipCount?: number | null = 0;
    public maxResultCount?: number | null = 20;

    public withPageIndex(pageIndex: number) {
        const newSkipCount = pageIndex * (this.maxResultCount ?? 0);

        if (this.skipCount == newSkipCount) return this;
        return clone(this, _ => {
            _.skipCount = newSkipCount;
        });
    }

    public withSort(orderDirection: OrderDirection | undefined, orderBy?: string) {
        if (this.orderBy == orderBy && this.orderDirection == orderDirection) return this;
        return clone(this, _ => {
            _.orderBy = orderBy;
            _.orderDirection = orderDirection;
        });
    }

    public pageIndex(): number {
        if (this.maxResultCount == 0 || this.maxResultCount == null || this.skipCount == null) return 0;

        return Math.floor(this.skipCount / this.maxResultCount);
    }

    public pageSize(): number {
        return this.maxResultCount ?? Number.MAX_SAFE_INTEGER;
    }
}
