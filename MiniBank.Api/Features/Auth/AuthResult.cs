namespace MiniBank.Api.Features.Auth;

public enum AuthResultStatus
{
    Success,
    DuplicateEmail,
    InvalidRequest,
}

public sealed record AuthResult(
    AuthResultStatus Status,
    AuthResponse? Response = null,
    IDictionary<string, string[]>? Errors = null
);
