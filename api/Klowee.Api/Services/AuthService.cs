using Klowee.Api.Common;
using Klowee.Api.Contracts.Auth;
using Klowee.Api.Data;
using Klowee.Api.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Klowee.Api.Services;

public class AuthService : IAuthService
{
    private readonly KloweeDbContext _db;
    private readonly IJwtTokenService _tokens;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly ICurrentUser _currentUser;

    public AuthService(
        KloweeDbContext db,
        IJwtTokenService tokens,
        IPasswordHasher<User> passwordHasher,
        ICurrentUser currentUser)
    {
        _db = db;
        _tokens = tokens;
        _passwordHasher = passwordHasher;
        _currentUser = currentUser;
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var email = NormalizeEmail(request.Email);

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

        // Both branches raise the same generic error: the response must never
        // reveal whether it was the email or the password that was wrong.
        if (user is null)
        {
            throw new InvalidCredentialsException();
        }

        var result = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (result == PasswordVerificationResult.Failed)
        {
            throw new InvalidCredentialsException();
        }

        var (token, expiresAt) = _tokens.CreateAccessToken(user);

        return new LoginResponse(token, expiresAt, ToDto(user));
    }

    public async Task<UserDto> GetCurrentUserAsync(CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId ?? throw new InvalidCredentialsException();

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        // A structurally valid token for a user who has since been removed is
        // not a usable identity.
        return user is null ? throw new InvalidCredentialsException() : ToDto(user);
    }

    /// <summary>Emails are stored and compared lower-cased so login is case-insensitive.</summary>
    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    private static UserDto ToDto(User user) => new(user.Id, user.Email, user.DisplayName);
}
