using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BoraRachar.DTOs.Group;
using BoraRachar.Security;

namespace BoraRachar.Tests;

public class RateLimitIntegrationTests
{
    [Fact]
    public async Task CreateGroup_SixthAttemptReturns429_WithoutPersisting_AndReadsStillWork()
    {
        await using var host = await HttpPolicyTestHost.StartAsync();
        host.Client.DefaultRequestHeaders.Add("Origin", HttpPolicyTestHost.AllowedOrigin);
        CreatedGroupResponseDto? group = null;

        for (var attempt = 0; attempt < 5; attempt++)
        {
            using var response = await host.Client.PostAsJsonAsync("/api/groups", new
            {
                name = "Viagem",
                members = new[] { "Ana", "Bruno" }
            });
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            group = await response.Content.ReadFromJsonAsync<CreatedGroupResponseDto>();
        }

        using var rejected = await host.Client.PostAsJsonAsync("/api/groups", new
        {
            name = "Outro grupo",
            members = new[] { "Ana", "Bruno" }
        });
        await AssertRateLimitedAsync(rejected, 600);
        Assert.Equal(5, host.Repository.CreateCalls);
        Assert.Equal(HttpPolicyTestHost.AllowedOrigin,
            Assert.Single(rejected.Headers.GetValues("Access-Control-Allow-Origin")));
        Assert.Contains("Retry-After", string.Join(",", rejected.Headers.GetValues("Access-Control-Expose-Headers")),
            StringComparison.OrdinalIgnoreCase);

        Assert.NotNull(group);
        using var readRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/groups/{group!.Id}");
        readRequest.Headers.Add(GroupTokens.HeaderName, group.AccessToken);
        using var read = await host.Client.SendAsync(readRequest);
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
    }

    [Fact]
    public async Task InvalidCreateAttempts_AlsoConsumeTheQuota()
    {
        await using var host = await HttpPolicyTestHost.StartAsync();
        for (var attempt = 0; attempt < 5; attempt++)
        {
            using var response = await host.Client.PostAsJsonAsync("/api/groups", new { });
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        using var rejected = await host.Client.PostAsJsonAsync("/api/groups", new { });
        await AssertRateLimitedAsync(rejected, 600);
        Assert.Equal(0, host.Repository.CreateCalls);
    }

    [Fact]
    public async Task GlobalQuota_IsSharedAcrossRoutes_AndDoesNotTrustSpoofedForwardedFor()
    {
        await using var host = await HttpPolicyTestHost.StartAsync();
        const string groupPath = "/api/groups/507f1f77bcf86cd799439011";
        for (var attempt = 0; attempt < 120; attempt++)
        {
            var path = attempt % 2 == 0 ? groupPath : groupPath + "/balances";
            using var response = await host.Client.GetAsync(path);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, groupPath + "/expenses");
        request.Headers.Add("X-Forwarded-For", "203.0.113.50");
        using var rejected = await host.Client.SendAsync(request);
        await AssertRateLimitedAsync(rejected, 60);
        Assert.Equal(0, host.Repository.GetCalls);
    }

    private static async Task AssertRateLimitedAsync(HttpResponseMessage response, int maxRetrySeconds)
    {
        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.True(response.Headers.CacheControl?.NoStore == true);
        var retryAfter = response.Headers.RetryAfter?.Delta;
        Assert.True(retryAfter.HasValue, "429 must include a Retry-After delay.");
        Assert.InRange(retryAfter!.Value.TotalSeconds, 1d, (double)maxRetrySeconds);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(429, body.RootElement.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(body.RootElement.GetProperty("title").GetString()));
    }
}
