using Microsoft.AspNetCore.Http;
using XW.Shared.Application.Contracts.ResultModels;
using System.Net;
using AppProblemDetails = XW.Shared.Application.Contracts.ResultModels.ProblemDetails;

namespace Microsoft.AspNetCore.Mvc;

public abstract class XWControllerBase : ControllerBase
{
    /// <summary>
    /// XW.Shared.Application.Services.ProblemDetails.ProblemDetails => Problem
    /// </summary>
    /// <param name="problemDetails"></param>
    /// <returns></returns>
    [NonAction]
    protected virtual ObjectResult Problem(AppProblemDetails problemDetails)
    {
        if (problemDetails is null)
        {
            return Problem("操作失败", Request.Path.ToString(), StatusCodes.Status500InternalServerError);
        }

        problemDetails.Instance ??= Request.Path.ToString();
        return Problem(problemDetails.Detail
            , problemDetails.Instance
            , problemDetails.Status
            , problemDetails.Title
            , problemDetails.Type);
    }

    /// <summary>
    /// exception => Problem
    /// </summary>
    /// <param name="exception"></param>
    /// <param name="statusCode"></param>
    /// <returns></returns>
    [NonAction]
    protected virtual ObjectResult Problem(Exception exception, HttpStatusCode statusCode = HttpStatusCode.InternalServerError)
    {
        var status = (int)statusCode;
        var type = string.Concat("https://httpstatuses.com/", status);
        return Problem(exception.GetDetail(), Request.Path.ToString(), status, exception.Message, type);
    }

    /// <summary>
    /// ServiceResult{TValue} => ActionResult{TValue}
    /// </summary>
    /// <typeparam name="TValue"></typeparam>
    /// <param name="appSrvResult"><see cref="ServiceResult{TValue}"/></param>
    /// <returns><see cref="ActionResult{TValue}"/> if normal return status 200</returns>
    [NonAction]
    protected virtual ActionResult<TValue> Result<TValue>(ServiceResult<TValue> appSrvResult)
    {
        if (appSrvResult.IsSuccess)
        {
            return appSrvResult.Content;
        }

        if (appSrvResult.ProblemDetails is not null)
        {
            return Problem(appSrvResult.ProblemDetails);
        }

        return Problem(new AppProblemDetails(HttpStatusCode.BadRequest, "操作失败"));
    }

    /// <summary>
    /// ServiceResult => ActionResult
    /// </summary>
    /// <param name="appSrvResult"><see cref="ServiceResult"/></param>
    /// <returns><see cref="ActionResult"/> if normal return statuscode 204</returns>
    [NonAction]
    protected virtual ActionResult Result(ServiceResult appSrvResult)
    {
        if (appSrvResult.IsSuccess)
        {
            return NoContent();
        }

        if (appSrvResult.ProblemDetails is not null)
        {
            return Problem(appSrvResult.ProblemDetails);
        }

        return Problem(new AppProblemDetails(HttpStatusCode.BadRequest, "操作失败"));
    }

    /// <summary>
    /// ServiceResult {TValue} => ActionResult{TValue}
    /// </summary>
    /// <typeparam name="TValue"></typeparam>
    /// <param name="appSrvResult"><see cref="ServiceResult{TValue}"/></param>
    /// <returns><see cref="ActionResult{TValue}"/> if normal return statuscode 201</returns>
    [NonAction]
    protected virtual ActionResult<TValue> CreatedResult<TValue>(ServiceResult<TValue> appSrvResult)
    {
        if (appSrvResult.IsSuccess)
        {
            return Created(Request.Path, appSrvResult.Content);
        }

        if (appSrvResult.ProblemDetails is not null)
        {
            return Problem(appSrvResult.ProblemDetails);
        }

        return Problem(new AppProblemDetails(HttpStatusCode.BadRequest, "操作失败"));
    }
}
