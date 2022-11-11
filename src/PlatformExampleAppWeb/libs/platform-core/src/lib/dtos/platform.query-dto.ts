export interface IPlatformQueryDto {}

export class PlatformQueryDto implements IPlatformQueryDto {}

export interface IPlatformRepositoryPagedQuery extends IPlatformQueryDto {
  skipCount: number;
  maxResultCount: number;
}

export class PlatformPagedQueryDto extends PlatformQueryDto implements IPlatformRepositoryPagedQuery {
  public constructor(data?: Partial<IPlatformRepositoryPagedQuery>) {
    super();
    this.skipCount = data?.skipCount ?? 0;
    this.maxResultCount = data?.maxResultCount ?? 10;
  }

  public skipCount: number;
  public maxResultCount: number;
}
