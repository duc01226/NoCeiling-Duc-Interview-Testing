using Easy.Platform.Common.Extensions;
using Easy.Platform.Common.Extensions.WhenCases;

namespace Easy.Platform.Domain.UnitOfWork;

public static class UnitOfWorkExtension
{
    public static TInnerUow FindFirstInnerUowOfType<TInnerUow>(this IEnumerable<IUnitOfWork> innerUnitOfWorks)
        where TInnerUow : class, IUnitOfWork
    {
        return innerUnitOfWorks
            .Select(
                innerUnitOfWork => innerUnitOfWork
                    .When(_ => innerUnitOfWork.GetType().IsAssignableTo(typeof(TInnerUow)), _ => innerUnitOfWork.As<TInnerUow>())
                    .Else(
                        _ => innerUnitOfWork.InnerUnitOfWorks
                            .Select(innerUnitOfWorkLevel2 => innerUnitOfWorkLevel2.FindFirstInnerUowOfType<TInnerUow>())
                            .FirstOrDefault(innerUnitOfWorkLevel2 => innerUnitOfWorkLevel2 != null))
                    .Execute())
            .FirstOrDefault(p => p != null);
    }
}
