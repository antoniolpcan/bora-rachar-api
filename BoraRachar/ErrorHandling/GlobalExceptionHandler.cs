using Microsoft.AspNetCore.Diagnostics;

namespace BoraRachar.ErrorHandling;

public sealed class GlobalExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        await Results.Problem(
            statusCode: StatusCodes.Status500InternalServerError,
            title: "Erro interno do servidor.",
            detail: "Não foi possível concluir a operação. Tente novamente mais tarde.",
            extensions: new Dictionary<string, object?>
            {
                ["traceId"] = httpContext.TraceIdentifier
            }).ExecuteAsync(httpContext);

        return true;
    }
}
