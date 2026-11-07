using Microsoft.AspNetCore.Identity;

namespace AuthApi.Models;

public class ApplicationUser : IdentityUser<Guid>
{
    public Guid TeamId { get; set; }
    public bool IsActive { get; set; } = true;
}
