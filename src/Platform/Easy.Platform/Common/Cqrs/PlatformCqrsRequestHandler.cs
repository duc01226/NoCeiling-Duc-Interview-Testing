using Easy.Platform.Common.Validations;

namespace Easy.Platform.Common.Cqrs;

public abstract class PlatformCqrsRequestHandler<TRequest> where TRequest : IPlatformCqrsRequest
{
    /// <summary>
    /// Override this function to implement additional asynchronous validation logic for the request.
    /// </summary>
    /// <param name="requestSelfValidation">The validation result of the request itself.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
    /// <returns>The validated <see cref="PlatformValidationResult{TRequest}" />.</returns>
    protected virtual Task<PlatformValidationResult<TRequest>> ValidateRequestAsync(
        PlatformValidationResult<TRequest> requestSelfValidation,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(requestSelfValidation);
    }

    /// <summary>
    /// Validates the CQRS request asynchronously.
    /// </summary>
    /// <param name="request">The CQRS request to validate.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
    /// <returns>The validated <see cref="PlatformValidationResult{TRequest}" />.</returns>
    protected Task<PlatformValidationResult<TRequest>> ValidateRequestAsync(
        TRequest request,
        CancellationToken cancellationToken)
    {
        return ValidateRequestAsync(request.Validate().Of<TRequest>(), cancellationToken);
    }
}
