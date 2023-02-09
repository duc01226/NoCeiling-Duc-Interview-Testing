import { clone } from '../utils';

/* eslint-disable @typescript-eslint/no-empty-interface */
export interface IPlatformQueryDto {}

export class PlatformQueryDto implements IPlatformQueryDto {}

export interface IPlatformRepositoryPagedQuery extends IPlatformQueryDto {
  skipCount: number;
  maxResultCount: number;
}

export class PlatformPagedQueryDto extends PlatformQueryDto implements IPlatformRepositoryPagedQuery {
  public constructor(data?: Partial<IPlatformRepositoryPagedQuery>) {
    super();

    if (data == null) return;

    if (data.skipCount != null) this.skipCount = data.skipCount;
    if (data.maxResultCount != null) this.maxResultCount = data.maxResultCount;
  }

  public skipCount: number = 0;
  public maxResultCount: number = 0;

  public withPageIndex(pageIndex: number): PlatformPagedQueryDto {
    return clone(this, _ => {
      _.skipCount = pageIndex * this.maxResultCount;
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
