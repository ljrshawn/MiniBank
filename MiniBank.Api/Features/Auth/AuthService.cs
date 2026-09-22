using MiniBank.Api.Features.Customers;

namespace MiniBank.Api.Features.Auth;

public sealed class AuthService(
    CustomerRegistrationService registrationService,
    JwtTokenService jwtTokenService
)
{
    public async Task<AuthResult> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken
    )
    {
        var result = await registrationService.RegisterAsync(
            new CreateCustomerRequest
            {
                Email = request.Email,
                Password = request.Password,
                FirstName = request.FirstName,
                LastName = request.LastName,
                PhoneNumber = request.PhoneNumber,
            },
            cancellationToken
        );

        return result.Status switch
        {
            CustomerWriteStatus.Success => new(
                AuthResultStatus.Success,
                jwtTokenService.CreateToken(result.Customer!.User)
            ),
            CustomerWriteStatus.DuplicateEmail => new(AuthResultStatus.DuplicateEmail),
            CustomerWriteStatus.InvalidRequest => new(
                AuthResultStatus.InvalidRequest,
                Errors: result.Errors
            ),
            _ => throw new InvalidOperationException($"Unknown registration status: {result.Status}."),
        };
    }
}
