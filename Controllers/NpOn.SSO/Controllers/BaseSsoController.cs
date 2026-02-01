using Common.Applications.ApplicationsExtensions.NpOn.TokenValidatorExtUse.Controllers;
using Common.Applications.ApplicationsExtensions.NpOn.TokenValidatorExtUse.Services;
using Common.Extensions.NpOn.CommonInternalCache;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;

namespace Controllers.NpOn.SSO.Controllers;

[ApiController]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
[EnableCors(Constant.CorsPolicy)]
[Produces("application/json")]
[Route("api/[controller]/[action]")]
public class BaseSsoController(
    ILogger<BaseSsoController> logger,
    ContextService contextService) : CommonController(
    logger, contextService)
{
}