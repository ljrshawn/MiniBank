namespace MiniBank.Api.Features.Auth;

public sealed record AuthResponse(string AccessToken, int ExpiresIn, string TokenType = "Bearer");
