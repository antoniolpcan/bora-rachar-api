using BoraRachar.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace BoraRachar.Security
{
    public sealed class GroupAccessFilter(IGroupRepository repository) : IAsyncActionFilter
    {
        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var http = context.HttpContext;
            if (!http.Request.Headers.TryGetValue(GroupTokens.HeaderName, out var tokens)
                || tokens.Count != 1 || tokens[0]?.Length != 64)
            {
                context.Result = new ObjectResult(new ProblemDetails { Status = 401, Title = "Informe o token de acesso ao grupo." }) { StatusCode = 401 };
                return;
            }
            var id = (http.Request.RouteValues["groupId"] ?? http.Request.RouteValues["id"])?.ToString();
            var hash = await repository.GetAccessHashAsync(id ?? "", http.RequestAborted);
            if (!GroupTokens.Matches(tokens[0]!, hash))
            {
                context.Result = new ObjectResult(new ProblemDetails { Status = 403, Title = "Acesso ao grupo não autorizado." }) { StatusCode = 403 };
                return;
            }
            await next();
        }
    }
}
