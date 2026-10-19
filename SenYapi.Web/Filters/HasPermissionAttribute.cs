using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using SenYapi.Business.Services;

namespace SenYapi.Web.Filters;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public class HasPermissionAttribute : Attribute, IAsyncActionFilter
{
    public string Permission { get; }

    public HasPermissionAttribute(string permission)
    {
        Permission = permission;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var user = context.HttpContext.User;
        if (user == null || user.Identity?.IsAuthenticated != true)
        {
            context.Result = new ChallengeResult();
            return;
        }

        var permissionService = context.HttpContext.RequestServices.GetRequiredService<IPermissionService>();
        var hasPermission = await permissionService.HasPermissionAsync(user, Permission);

        if (!hasPermission)
        {
            if (context.HttpContext.Request.Headers["X-Requested-With"] == "XMLHttpRequest" ||
                context.HttpContext.Request.Headers.Accept.ToString().Contains("application/json"))
            {
                context.Result = new JsonResult(new { success = false, message = "Bu işlemi gerçekleştirmek için yetkiniz bulunmamaktadır." })
                {
                    StatusCode = 403
                };
                return;
            }

            context.Result = new ForbidResult();
            return;
        }

        await next();
    }
}
