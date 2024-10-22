using Easy.Platform.Application.RequestContext;
using Easy.Platform.Common;
using Easy.Platform.Common.Cqrs;
using Easy.Platform.Common.Extensions;
using Microsoft.Extensions.Logging;

namespace Easy.Platform.Application.Cqrs;

/// <summary>
/// Provides a base class for application-level handlers of CQRS requests.
/// </summary>
/// <typeparam name="TRequest">The type of CQRS request handled by this class.</typeparam>
public abstract class PlatformCqrsRequestApplicationHandler<TRequest> : PlatformCqrsRequestHandler<TRequest>
    where TRequest : IPlatformCqrsRequest
{
    /// <summary>
    /// The logger factory used for creating loggers.
    /// </summary>
    protected readonly ILoggerFactory LoggerFactory;

    /// <summary>
    /// The request context accessor providing information about the current application request context.
    /// </summary>
    protected readonly IPlatformApplicationRequestContextAccessor RequestContextAccessor;

    /// <summary>
    /// The root service provider for resolving dependencies.
    /// </summary>
    protected readonly IPlatformRootServiceProvider RootServiceProvider;


    private readonly Lazy<ILogger> loggerLazy;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlatformCqrsRequestApplicationHandler{TRequest}" /> class.
    /// </summary>
    /// <param name="requestContextAccessor">The request context accessor providing information about the current application request context.</param>
    /// <param name="loggerFactory">The logger factory used for creating loggers.</param>
    /// <param name="rootServiceProvider">The root service provider for resolving dependencies.</param>
    public PlatformCqrsRequestApplicationHandler(
        IPlatformApplicationRequestContextAccessor requestContextAccessor,
        ILoggerFactory loggerFactory,
        IPlatformRootServiceProvider rootServiceProvider)
    {
        RequestContextAccessor = requestContextAccessor ?? throw new ArgumentNullException(nameof(requestContextAccessor));
        LoggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        RootServiceProvider = rootServiceProvider ?? throw new ArgumentNullException(nameof(rootServiceProvider));
        loggerLazy = new Lazy<ILogger>(
            () => loggerFactory.CreateLogger(typeof(PlatformCqrsRequestApplicationHandler<>).GetNameOrGenericTypeName() + $"-{GetType().Name}"));
    }

    /// <summary>
    /// Gets the current application request context from the user context.
    /// </summary>
    public IPlatformApplicationRequestContext RequestContext => RequestContextAccessor.Current;

    /// <summary>
    /// Gets the logger instance for this request handler.
    /// </summary>
    public ILogger Logger => loggerLazy.Value;

    /// <summary>
    /// Builds the audit information for the CQRS request.
    /// </summary>
    /// <param name="request">The CQRS request for which to build the audit information.</param>
    /// <returns>The constructed <see cref="IPlatformCqrsRequestAuditInfo" />.</returns>
    public IPlatformCqrsRequestAuditInfo BuildRequestAuditInfo(TRequest request)
    {
        return new PlatformCqrsRequestAuditInfo(
            auditTrackId: Ulid.NewUlid().ToString(),
            auditRequestByUserId: RequestContextAccessor.Current.UserId());
    }
}
