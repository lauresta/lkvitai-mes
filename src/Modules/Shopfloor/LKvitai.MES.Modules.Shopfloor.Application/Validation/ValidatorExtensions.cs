using FluentValidation;
using LKvitai.MES.Modules.Shopfloor.Application.Exceptions;

namespace LKvitai.MES.Modules.Shopfloor.Application.Validation;

internal static class ValidatorExtensions
{
    /// <summary>
    /// Runs the validator and throws <see cref="ShopfloorValidationException"/>
    /// (→ HTTP 400) when the instance is invalid.
    /// </summary>
    public static async Task EnsureValidAsync<T>(
        this IValidator<T> validator,
        T instance,
        CancellationToken cancellationToken)
    {
        var result = await validator.ValidateAsync(instance, cancellationToken).ConfigureAwait(false);
        if (!result.IsValid)
        {
            var errors = result.Errors.Select(e => e.ErrorMessage).ToList();
            throw new ShopfloorValidationException(
                "Request validation failed: " + string.Join("; ", errors),
                errors);
        }
    }
}
