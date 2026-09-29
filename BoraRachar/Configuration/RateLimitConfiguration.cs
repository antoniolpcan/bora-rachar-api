using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace BoraRachar.Configuration
{
    public static class RateLimitConfiguration
    {
        public const string CreateGroupPolicy = "create-group";

        public static IServiceCollection AddBoraRacharRateLimiting(
            this IServiceCollection services)
        {
            services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode =
                    StatusCodes.Status429TooManyRequests;

                options.GlobalLimiter =
                    PartitionedRateLimiter.Create<HttpContext, string>(context =>
                        RateLimitPartition.GetFixedWindowLimiter(
                            partitionKey: GetClientIp(context),
                            factory: _ => new FixedWindowRateLimiterOptions
                            {
                                PermitLimit = 120,
                                Window = TimeSpan.FromMinutes(1),
                                QueueLimit = 0,
                                AutoReplenishment = true
                            }));

                options.AddPolicy(CreateGroupPolicy, context =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: GetClientIp(context),
                        factory: _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = 5,
                            Window = TimeSpan.FromMinutes(10),
                            QueueLimit = 0,
                            AutoReplenishment = true
                        }));

                options.OnRejected = async (context, cancellationToken) =>
                {
                    var response = context.HttpContext.Response;

                    response.Headers.CacheControl = "no-store";

                    if (context.Lease.TryGetMetadata(
                        MetadataName.RetryAfter, out var retryAfter))
                    {
                        var seconds = Math.Max(
                            1,
                            (int)Math.Ceiling(retryAfter.TotalSeconds));

                        response.Headers["Retry-After"] =
                            seconds.ToString(CultureInfo.InvariantCulture);
                    }

                    await Results.Problem(
                        statusCode: StatusCodes.Status429TooManyRequests,
                        title: "Muitas requisições.",
                        detail: "Você atingiu o limite de requisições. " +
                                "Aguarde antes de tentar novamente."
                    ).ExecuteAsync(context.HttpContext);
                };
            });

            return services;
        }

        private static string GetClientIp(HttpContext context)
        {
            var address = context.Connection.RemoteIpAddress;

            if (address is null)
                return "unknown";

            if (address.IsIPv4MappedToIPv6)
                address = address.MapToIPv4();

            return address.ToString();
        }
    }
}
