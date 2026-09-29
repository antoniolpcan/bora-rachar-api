using BoraRachar.Configuration;
using BoraRachar.Repositories;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace BoraRachar.Tests;

// A fresh HTTP server per test isolates rate-limit counters. Only persistence
// is replaced: controllers, validation, CORS and rate-limit policies are real.
internal sealed class HttpPolicyTestHost : IAsyncDisposable
{
    public const string AllowedOrigin = "https://portfolio.example";
    private readonly WebApplication _app;
    public HttpClient Client { get; }
    public TestGroupRepository Repository { get; }

    private HttpPolicyTestHost(WebApplication app, TestGroupRepository repository)
    {
        _app = app;
        Repository = repository;
        Client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
    }

    public static async Task<HttpPolicyTestHost> StartAsync()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Testing"
        });
        builder.Logging.ClearProviders();
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            // Satisfies startup validation; the fake repository never opens MongoDB.
            ["MongoDbSettings:ConnectionString"] = "mongodb://127.0.0.1:27017",
            ["MongoDbSettings:DatabaseName"] = "unused_http_policy_tests",
            ["Cors:AllowedOrigins:0"] = AllowedOrigin
        });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddBoraRachar(builder.Configuration);
        builder.Services.AddBoraRacharRateLimiting();
        var repository = new TestGroupRepository();
        builder.Services.RemoveAll<IGroupRepository>();
        builder.Services.AddSingleton<IGroupRepository>(repository);

        var app = builder.Build();
        app.UseExceptionHandler();
        app.UseRouting();
        app.UseCors();
        app.UseRateLimiter();
        app.UseAuthorization();
        app.MapControllers();

        try
        {
            await app.StartAsync();
            return new HttpPolicyTestHost(app, repository);
        }
        catch
        {
            await app.DisposeAsync();
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await _app.DisposeAsync();
    }
}
