using Easy.Platform.Common.Extensions;

namespace Easy.Platform.Domain.UnitOfWork;

public static class PlatformUnitOfWorkExtension
{
    public static TUnitOfWork FirstOrDefaultUowOfType<TUnitOfWork>(this IEnumerable<IPlatformUnitOfWork> unitOfWorks)
        where TUnitOfWork : class, IPlatformUnitOfWork
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
