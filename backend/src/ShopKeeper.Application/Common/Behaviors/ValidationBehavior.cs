namespace ShopKeeper.Application.Common.Behaviors;

using FluentValidation;
using MediatR;

/// <summary>
/// `where TRequest : notnull` deliberately matches MediatR's own IPipelineBehavior constraint,
/// not `where TRequest : IRequest{TResponse}` - the latter looks more precise but silently broke
/// this and the other two pipeline behaviors for every plain (void) IRequest command in this
/// codebase (ChangePasswordCommand, DisableTwoFactorCommand, ...): .NET's built-in DI container
/// fails to close the open generic against MediatR.Unit through IRequest's indirect inheritance
/// of IRequest{Unit}, so `services.GetServices{IPipelineBehavior{TRequest,Unit}}` silently
/// returned zero behaviors - no validation, no audit logging, no plan-tier check ever ran for any
/// void command, with no error or warning. Confirmed via a real DI-container test: an obviously
/// invalid ChangePasswordCommand (2-character new password) sailed straight through to the
/// handler before this fix. Reproduces with `IRequest<T>` (a real T) unaffected - only the
/// Unit/void case breaks, which is exactly what a redundant, over-specific constraint like this
/// tends to hide until someone tests a void command's failure path specifically.
/// </summary>
public class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!validators.Any())
        {
            return await next();
        }

        var context = new ValidationContext<TRequest>(request);

        var failures = (await Task.WhenAll(validators.Select(v => v.ValidateAsync(context, cancellationToken))))
            .SelectMany(result => result.Errors)
            .Where(failure => failure != null)
            .ToList();

        if (failures.Count != 0)
        {
            throw new FluentValidation.ValidationException(failures);
        }

        return await next();
    }
}
