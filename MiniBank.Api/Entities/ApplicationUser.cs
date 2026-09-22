using Microsoft.AspNetCore.Identity;

namespace MiniBank.Api.Entities;

public sealed class ApplicationUser : IdentityUser
{
    public Customer? Customer { get; set; }

    public DateTime CreatedAt { get; set; }
}
