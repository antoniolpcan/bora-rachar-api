using System.Net;
using System.Text.Json;
using BoraRachar.ErrorHandling;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BoraRachar.Tests
{
    public class GlobalExceptionHandlerTests
    {
        [Theory]
        [InlineData("Development", "application/json")]
        [InlineData("Production", "text/html")]
        public async Task UnhandledException_ReturnsSafeProblemDetails(
            string environment,
            string accept)
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                EnvironmentName = environment
            });
            builder.Logging.ClearProviders();
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            builder.Services.AddProblemDetails();
            builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

            await using var app = builder.Build();
            app.UseExceptionHandler();
            app.MapGet("/failure", (HttpContext context) =>
            {
                context.TraceIdentifier = "test-trace-id";
                throw new InvalidOperationException("private-database-secret");
            });
            app.MapGet("/healthy", () => Results.Ok(new { ready = true }));

            await app.StartAsync();
            using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
            client.DefaultRequestHeaders.Accept.ParseAdd(accept);

            using var response = await client.GetAsync("/failure");
            var body = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
            Assert.DoesNotContain("private-database-secret", body);
            Assert.DoesNotContain("InvalidOperationException", body);

            using var json = JsonDocument.Parse(body);
            Assert.Equal(500, json.RootElement.GetProperty("status").GetInt32());
            Assert.Equal("test-trace-id", json.RootElement.GetProperty("traceId").GetString());
            Assert.False(json.RootElement.TryGetProperty("stackTrace", out _));

            using var healthy = await client.GetAsync("/healthy");
            Assert.Equal(HttpStatusCode.OK, healthy.StatusCode);
        }
    }
}
