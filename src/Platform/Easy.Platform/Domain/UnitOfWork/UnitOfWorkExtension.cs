using Easy.Platform.Common.Extensions;
using Easy.Platform.Common.Extensions.WhenCases;

namespace Easy.Platform.Domain.UnitOfWork;

public static class UnitOfWorkExtension
{
    public static TUnitOfWork FirstUowOfType<TUnitOfWork>(this IEnumerable<IUnitOfWork> unitOfWorks)
        where TUnitOfWork : class, IUnitOfWork
    {
        return unitOfWorks
            .Select(
                uow => uow
                    .When(_ => uow is TUnitOfWork, _ => uow.As<TUnitOfWork>())
                    .Else(
                        _ => uow.InnerUnitOfWorks
                            .Select(
                                innerUow => uow is TUnitOfWork
                                    ? innerUow.As<TUnitOfWork>()
                                    : innerUow.InnerUnitOfWorks.FirstUowOfType<TUnitOfWork>())
                            .FirstOrDefault(recursiveInnerUow => recursiveInnerUow != null))
                    .Execute())
            .FirstOrDefault(p => p != null);
    }
}
