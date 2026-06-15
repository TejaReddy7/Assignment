using FluentValidation;
using IplStore.Shared;
using MediatR;

namespace IplStore.Application.Common.Behaviors;

/// <summary>
/// Runs all registered FluentValidation validators for a request before it reaches the handler.
/// On failure, short-circuits with a Result carrying a ValidationError — no exception thrown,
/// so the API layer maps it to a 400 via the standard Result→HTTP translation.
/// </summary>
public sealed class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
    where TResponse : Result
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators) => _validators = validators;

    public async Task<TResponse> Handle(
        TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (!_validators.Any()) return await next();

        var context = new ValidationContext<TRequest>(request);
        var failures = (await Task.WhenAll(_validators.Select(v => v.ValidateAsync(context, cancellationToken))))
            .SelectMany(r => r.Errors)
            .Where(f => f is not null)
            .ToList();

        if (failures.Count == 0) return await next();

        var firstFailure = failures[0];
        var error = Error.Validation(
            firstFailure.ErrorCode ?? "validation.failed",
            firstFailure.ErrorMessage);

        return CreateValidationResult(error);
    }

    private static TResponse CreateValidationResult(Error error)
    {
        if (typeof(TResponse) == typeof(Result))
            return (TResponse)Result.Failure(error);

        // TResponse is Result<TValue> — build the failure via the open generic factory.
        var valueType = typeof(TResponse).GetGenericArguments()[0];
        var failureMethod = typeof(Result)
            .GetMethods()
            .First(m => m is { Name: nameof(Result.Failure), IsGenericMethod: true })
            .MakeGenericMethod(valueType);

        return (TResponse)failureMethod.Invoke(null, new object[] { error })!;
    }
}
