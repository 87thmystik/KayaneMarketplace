using System.Security.Claims;
using Kayane.Models;


namespace Kayane.Services
{
    public interface IAuthService
    {
        Task SignInAsync(User user, Vendor? vendor = null, bool isPersistent = false);
        Task SignOutAsync();
    }
}
