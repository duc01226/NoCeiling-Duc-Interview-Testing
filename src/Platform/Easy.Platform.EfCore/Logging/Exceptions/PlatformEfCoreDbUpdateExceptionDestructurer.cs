using Easy.Platform.Common.JsonSerialization;
using Microsoft.EntityFrameworkCore;
using Serilog.Exceptions.Core;
using Serilog.Exceptions.Destructurers;

namespace Easy.Platform.EfCore.Logging.Exceptions;

/// <summary>
/// A destructurer for <see cref="DbUpdateException" />.
/// </summary>
/// <seealso cref="ExceptionDestructurer" />
public class PlatformEfCoreDbUpdateExceptionDestructurer : ExceptionDestructurer
{
    /// <inheritdoc />
    public override Type[] TargetTypes => new[] { typeof(DbUpdateException), typeof(DbUpdateConcurrencyException) };

    public override void Destructure(
        Exception exception,
        IExceptionPropertiesBag propertiesBag,
        Func<Exception, IReadOnlyDictionary<string, object>> destructureException)
    {
        ArgumentNullException.ThrowIfNull(exception);
        ArgumentNullException.ThrowIfNull(propertiesBag);
        ArgumentNullException.ThrowIfNull(destructureException);

        if (exception.Data.Count != 0) propertiesBag.AddProperty(nameof(Exception.Data), exception.Data.ToStringObjectDictionary());

        if (!string.IsNullOrEmpty(exception.HelpLink)) propertiesBag.AddProperty(nameof(Exception.HelpLink), exception.HelpLink);

        if (exception.HResult != 0) propertiesBag.AddProperty(nameof(Exception.HResult), exception.HResult);

        propertiesBag.AddProperty(nameof(Exception.Source), exception.Source);

#if !NETSTANDARD1_3
        if (exception.TargetSite is not null) propertiesBag.AddProperty(nameof(Exception.TargetSite), exception.TargetSite.ToString());
#endif

        if (exception.InnerException is not null) propertiesBag.AddProperty(nameof(Exception.InnerException), destructureException(exception.InnerException));

        // Custom Message With first entry info
        var firstEntry = exception.As<DbUpdateException>().Entries.FirstOrDefault();

        if (firstEntry != null)
        {
            var firstEntryInfo = PlatformJsonSerializer.Serialize(
                Util.DictionaryBuilder.New(
                    ("EntityType", firstEntry.Metadata.Name),
                    ("EntityId", firstEntry.Members.FirstOrDefault(p => p.Metadata.Name == "Id")?.CurrentValue?.ToString())));

            propertiesBag.AddProperty(nameof(DbUpdateException.Entries), firstEntryInfo);
            propertiesBag.AddProperty(nameof(Exception.StackTrace), $"[FirstEntryInfo: {firstEntryInfo}] [StackTrace: {exception.StackTrace}]");
            propertiesBag.AddProperty(nameof(Exception.Message), $"{exception.Message}. [FirstEntryInfo: {firstEntryInfo}]");
        }
        else
        {
            propertiesBag.AddProperty(nameof(Exception.StackTrace), exception.StackTrace);
            propertiesBag.AddProperty(nameof(Exception.Message), exception.Message);
        }
    }

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
