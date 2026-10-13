using Microsoft.Extensions.Options;

namespace XW.Shared.WebApi.Authentication.Basic;

public class BasicPostConfigureOptions : IPostConfigureOptions<BasicSchemeOptions>
{
    public void PostConfigure(string? name, BasicSchemeOptions options)
    {
        // Method intentionally left empty.
    }
}
