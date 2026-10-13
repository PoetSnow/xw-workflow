using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore.Query.Internal;

namespace XW.Shared.WebApi.Authentication.Basic;

public class BasicTokenValidatedContext(HttpContext context, AuthenticationScheme scheme, BasicSchemeOptions options) : ResultContext<BasicSchemeOptions>(context, scheme, options)
{ }
