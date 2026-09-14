using Kayane.Models;
using Microsoft.AspNetCore.Http;

namespace Kayane.Extensions;

public static class HttpContextExtensions
{
    public static Vendor GetCurrentVendor(this HttpContext httpContext)
    {
        if (httpContext.Items["CurrentVendor"] is Vendor vendor)
        {
            return vendor;
        }

        throw new InvalidOperationException("CurrentVendor is only accessible within actions decorated with [ApprovedVendor].");
    }
}