using AuthApi.Models;

namespace AuthApi.Services;

public interface ITokenService
{
    string CreateAccessToken(ApplicationUser user, string role, bool mfaPassed);

    string CreateMfaToken(ApplicationUser user);
}
