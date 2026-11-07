using AuthApi.Models;

namespace AuthApi.Services;

public interface ITokenService
{
    // TODO: A4: CreateMfaToken
    string CreateAccessToken(ApplicationUser user, string role, bool mfaPassed);
    
}
