using System.Net;
using System.Net.Http.Json;

namespace BoraRachar.Tests;

public class CorsIntegrationTests
{
    [Theory]
    [InlineData("GET", "/api/groups/507f1f77bcf86cd799439011/balances")]
    [InlineData("POST", "/api/groups")]
    [InlineData("PUT", "/api/groups/507f1f77bcf86cd799439011/expenses/example")]
    [InlineData("DELETE", "/api/groups/507f1f77bcf86cd799439011/expenses/example")]
    public async Task AllowedPreflight_AcceptsMethodAndGroupTokenHeader(string method, string path)
    {
        await using var host = await HttpPolicyTestHost.StartAsync();
        using var request = Preflight(HttpPolicyTestHost.AllowedOrigin, method, path);
        using var response = await host.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpPolicyTestHost.AllowedOrigin,
            Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
        AssertHeaderContains(response, "Access-Control-Allow-Methods", method);
        AssertHeaderContains(response, "Access-Control-Allow-Headers", "content-type");
        AssertHeaderContains(response, "Access-Control-Allow-Headers", "x-group-token");
        Assert.Equal(0, host.Repository.CreateCalls);
        Assert.Equal(0, host.Repository.GetCalls);
    }

    [Theory]
    [InlineData("https://untrusted.example")]
    [InlineData("https://portfolio.example.attacker.test")]
    [InlineData("http://portfolio.example")]
    public async Task UntrustedOrigin_DoesNotReceiveCorsPermission(string origin)
    {
        await using var host = await HttpPolicyTestHost.StartAsync();
        using var request = Preflight(origin, "POST", "/api/groups");
        using var response = await host.Client.SendAsync(request);

        // CORS denial is missing permission headers, not necessarily HTTP 403.
        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
        Assert.False(response.Headers.Contains("Access-Control-Allow-Credentials"));
        Assert.Equal(0, host.Repository.CreateCalls);
    }

    [Fact]
    public async Task AllowedOrigin_CanReadAuthenticationErrors()
    {
        await using var host = await HttpPolicyTestHost.StartAsync();
        using var request = new HttpRequestMessage(HttpMethod.Get,
            "/api/groups/507f1f77bcf86cd799439011");
        request.Headers.Add("Origin", HttpPolicyTestHost.AllowedOrigin);
        using var response = await host.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(HttpPolicyTestHost.AllowedOrigin,
            Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
    }

    [Fact]
    public async Task Preflight_DoesNotConsumeCreateGroupQuota()
    {
        await using var host = await HttpPolicyTestHost.StartAsync();
        for (var attempt = 0; attempt < 6; attempt++)
        {
            using var request = Preflight(HttpPolicyTestHost.AllowedOrigin, "POST", "/api/groups");
            using var response = await host.Client.SendAsync(request);
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }

        using var create = await host.Client.PostAsJsonAsync("/api/groups", new
        {
            name = "Viagem",
            members = new[] { "Ana", "Bruno" }
        });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        Assert.Equal(1, host.Repository.CreateCalls);
    }

    private static HttpRequestMessage Preflight(string origin, string method, string path)
    {
        var request = new HttpRequestMessage(HttpMethod.Options, path);
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", method);
        request.Headers.Add("Access-Control-Request-Headers", "content-type,x-group-token");
        return request;
    }

    private static void AssertHeaderContains(HttpResponseMessage response, string header, string expected)
    {
        var values = response.Headers.GetValues(header)
            .SelectMany(value => value.Split(','))
            .Select(value => value.Trim());
        Assert.Contains(values, value => string.Equals(value, expected, StringComparison.OrdinalIgnoreCase));
    }
}
