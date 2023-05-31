using Microsoft.EntityFrameworkCore;
using Serilog.Exceptions.Destructurers;

namespace Easy.Platform.EfCore.Logging.Exceptions;

/// <summary>
/// A destructurer for <see cref="DbUpdateException"/>.
/// </summary>
/// <seealso cref="ExceptionDestructurer" />
public class PlatformEfCoreDbUpdateExceptionDestructurer : ExceptionDestructurer
{
    /// <inheritdoc />
    public override Type[] TargetTypes => new[] { typeof(DbUpdateException), typeof(DbUpdateConcurrencyException) };

    ///// <inheritdoc />
    //public override void Destructure(
    //    Exception exception,
    //    IExceptionPropertiesBag propertiesBag,
    //    Func<Exception, IReadOnlyDictionary<string, object>> destructureException)
    //{
    //    base.Destructure(exception, propertiesBag, destructureException);

    //    // Do not log entries to fix memory issues. It will log all information which lead to memory issues
    //    //var dbUpdateException = (DbUpdateException)exception;
    //    //var entriesValue = dbUpdateException.Entries
    //    //    .Select(
    //    //        e => new
    //    //        {
    //    //            EntryProperties = e.Properties.Select(
    //    //                p => new
    //    //                {
    //    //                    PropertyName = p.Metadata.Name,
    //    //                    p.OriginalValue,
    //    //                    p.CurrentValue,
    //    //                    p.IsTemporary,
    //    //                    p.IsModified
    //    //                }),
    //    //            e.State
    //    //        })
    //    //    .ToList();
    //    //propertiesBag.AddProperty(nameof(DbUpdateException.Entries), entriesValue);
    //}
}
