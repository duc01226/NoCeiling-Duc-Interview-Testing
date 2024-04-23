using Easy.Platform.Common.Extensions;

namespace Easy.Platform.Domain.UnitOfWork;

public static class UnitOfWorkExtension
{
    public static TUnitOfWork FirstOrDefaultUowOfType<TUnitOfWork>(this IEnumerable<IUnitOfWork> unitOfWorks)
        where TUnitOfWork : class, IUnitOfWork
    {
        return unitOfWorks
            .Select(
                uow => uow.As<TUnitOfWork>() ??
                       uow.InnerUnitOfWorks
                           .Select(innerUow => innerUow.As<TUnitOfWork>() ?? innerUow.InnerUnitOfWorks.FirstOrDefaultUowOfType<TUnitOfWork>())
                           .FirstOrDefault(recursiveInnerUow => recursiveInnerUow != null))
            .FirstOrDefault(p => p != null);
    }
}
