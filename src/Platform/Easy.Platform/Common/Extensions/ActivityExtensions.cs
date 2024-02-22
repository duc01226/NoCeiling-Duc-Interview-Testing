#nullable enable
using System.Diagnostics;

namespace Easy.Platform.Common.Extensions;

public static class ActivityExtensions
{
    /// <summary>
    /// Retrieves the Span ID of the activity.
    /// </summary>
    /// <param name="activity">The activity instance.</param>
    /// <returns>The Span ID of the activity in string format.</returns>
    /// <remarks>
    /// This method retrieves the Span ID of the activity based on the activity's ID format.
    /// If the ID format is Hierarchical, it returns the activity's ID.
    /// If the ID format is W3C, it returns the activity's Span ID in hexadecimal string format.
    /// </remarks>
    public static string GetSpanId(this Activity activity)
    {
        return activity.IdFormat switch
        {
            ActivityIdFormat.Hierarchical => activity.Id,
            ActivityIdFormat.W3C => activity.SpanId.ToHexString(),
            _ => null
        } ?? string.Empty;
    }

    /// <summary>
    /// Retrieves the Trace ID of the activity.
    /// </summary>
    /// <param name="activity">The activity instance.</param>
    /// <returns>The Trace ID of the activity in string format.</returns>
    /// <remarks>
    /// This method retrieves the Trace ID of the activity based on the activity's ID format.
    /// If the ID format is Hierarchical, it returns the activity's Root ID.
    /// If the ID format is W3C, it returns the activity's Trace ID in hexadecimal string format.
    /// </remarks>
    public static string GetTraceId(this Activity activity)
    {
        return activity.IdFormat switch
        {
            ActivityIdFormat.Hierarchical => activity.RootId,
            ActivityIdFormat.W3C => activity.TraceId.ToHexString(),
            _ => null
        } ?? string.Empty;
    }

    /// <summary>
    /// Retrieves the Parent ID of the activity.
    /// </summary>
    /// <param name="activity">The activity instance.</param>
    /// <returns>The Parent ID of the activity in string format.</returns>
    /// <remarks>
    /// This method retrieves the Parent ID of the activity based on the activity's ID format.
    /// If the ID format is Hierarchical, it returns the activity's Parent ID.
    /// If the ID format is W3C, it returns the activity's Parent Span ID in hexadecimal string format.
    /// </remarks>
    public static string GetParentId(this Activity activity)
    {
        return activity.IdFormat switch
        {
            ActivityIdFormat.Hierarchical => activity.ParentId,
            ActivityIdFormat.W3C => activity.ParentSpanId.ToHexString(),
            _ => null
        } ?? string.Empty;
    }
}
