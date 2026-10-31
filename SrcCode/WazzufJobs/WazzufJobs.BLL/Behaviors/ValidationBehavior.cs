using FluentValidation.Results;

namespace WazzufJobs.BLL.Behaviors;

/// <summary>
/// Runs every FluentValidation validator registered for the request before its handler (Phase 02, S1).
/// Requests without validators pass through unchanged. Failures from all validators are collected and raised as one
/// <see cref="ValidationException"/>, which <see cref="GlobalExceptionsHandler"/> turns into an RFC 9457 400
/// response. Validators run sequentially, so a validator may safely use scoped services such as the DbContext.
/// Authorization is unaffected: it runs in the ASP.NET Core pipeline before a request reaches MediatR.
/// </summary>
public sealed class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var failures = new List<ValidationFailure>();

        foreach (var validator in validators)
        {
            // a fresh context per validator: a ValidationContext accumulates failures, so sharing one would report
            // earlier validators' failures again in later results
            var result = await validator.ValidateAsync(new ValidationContext<TRequest>(request), cancellationToken);
            failures.AddRange(result.Errors);
        }

        if (failures.Count > 0)
            throw new ValidationException(failures);

        return await next();
    }
}
