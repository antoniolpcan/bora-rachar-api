using System.Net;
using System.Net.Http.Json;
using BoraRachar.Configuration;
using BoraRachar.Data;
using BoraRachar.DTOs.Expense;
using BoraRachar.DTOs.Group;
using BoraRachar.Models;
using BoraRachar.Repositories;
using BoraRachar.Security;
using BoraRachar.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BoraRachar.Tests
{
    public sealed class MongoFactAttribute : FactAttribute
    {
        public MongoFactAttribute()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("BORARACHAR_TEST_MONGO")))
                Skip = "Defina BORARACHAR_TEST_MONGO para um MongoDB local de testes.";
        }
    }

    public sealed class MongoApiFixture : IAsyncLifetime
    {
        private WebApplication? _app;
        private MongoClient? _mongo;
        private readonly string _databaseName = "borarachar_test_" + Guid.NewGuid().ToString("N");
        public HttpClient Client { get; private set; } = null!;
        public IMongoCollection<Group> Groups { get; private set; } = null!;
        public IMongoCollection<BsonDocument> Documents { get; private set; } = null!;
        public GroupRepository Repository { get; private set; } = null!;

        public async Task InitializeAsync()
        {
            var uri = Environment.GetEnvironmentVariable("BORARACHAR_TEST_MONGO");
            if (string.IsNullOrWhiteSpace(uri)) return;
            var url = new MongoUrl(uri);
            if (!uri.StartsWith("mongodb://", StringComparison.OrdinalIgnoreCase) || url.Servers.Any(s => s.Host is not ("localhost" or "127.0.0.1" or "::1")))
                throw new InvalidOperationException("Os testes aceitam somente MongoDB local, nunca o banco remoto do projeto.");

            _mongo = new MongoClient(uri);
            var db = _mongo.GetDatabase(_databaseName);
            await db.RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1));
            Groups = db.GetCollection<Group>("Groups");
            Documents = db.GetCollection<BsonDocument>("Groups");
            Repository = new GroupRepository(_mongo, Options.Create(new MongoDbSettings { ConnectionString = uri, DatabaseName = _databaseName }));

            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
            builder.Logging.ClearProviders();
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MongoDbSettings:ConnectionString"] = uri,
                ["MongoDbSettings:DatabaseName"] = _databaseName
            });
            builder.Services.AddBoraRachar(builder.Configuration);
            _app = builder.Build();
            _app.UseExceptionHandler();
            _app.UseRouting();
            _app.UseCors();
            _app.UseAuthorization();
            _app.MapControllers();
            await _app.StartAsync();
            Client = new HttpClient { BaseAddress = new Uri(_app.Urls.Single()) };
        }

        public async Task DisposeAsync()
        {
            Client?.Dispose();
            if (_app is not null) await _app.DisposeAsync();
            // Nome gerado internamente, exclusivamente para esta execução.
            if (_mongo is not null) await _mongo.DropDatabaseAsync(_databaseName);
        }
    }

    public class ApiIntegrationTests(MongoApiFixture fixture) : IClassFixture<MongoApiFixture>
    {
        private async Task<CreatedGroupResponseDto> CreateGroup()
        {
            using var response = await fixture.Client.PostAsJsonAsync("/api/groups", new { name = "  Viagem  ", members = new[] { "Ana", "Bruno", "Clara" } });
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
            var group = await response.Content.ReadFromJsonAsync<CreatedGroupResponseDto>();
            Assert.NotNull(group);
            Assert.Equal("Viagem", group.Name);
            Assert.Equal(64, group.AccessToken.Length);
            return group;
        }

        private async Task<HttpResponseMessage> Send(HttpMethod method, string path, string? token, object? body = null)
        {
            using var request = new HttpRequestMessage(method, path);
            if (token is not null) request.Headers.Add(GroupTokens.HeaderName, token);
            if (body is not null) request.Content = JsonContent.Create(body);
            return await fixture.Client.SendAsync(request);
        }

        private static object Payload(CreatedGroupResponseDto group, decimal amount = 90, int? version = null) => new
        {
            title = "Jantar",
            amount,
            paidByMemberId = group.Members[0].Id,
            splitAmongMemberIds = group.Members.Select(m => m.Id).ToArray(),
            version
        };

        private async Task<ExpenseResponseDto> CreateExpense(CreatedGroupResponseDto group)
        {
            using var response = await Send(HttpMethod.Post, $"/api/groups/{group.Id}/expenses", group.AccessToken, Payload(group));
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            return (await response.Content.ReadFromJsonAsync<ExpenseResponseDto>())!;
        }

        [MongoFact]
        public async Task Lifecycle_PersistsEditsBalancesAndVersionedDeletion()
        {
            var group = await CreateGroup();
            var expense = await CreateExpense(group);
            var path = $"/api/groups/{group.Id}/expenses/{expense.Id}";
            Assert.Equal(1, expense.Version);
            using var update = await Send(HttpMethod.Put, path, group.AccessToken, Payload(group, 120, expense.Version));
            Assert.Equal(HttpStatusCode.OK, update.StatusCode);
            var edited = (await update.Content.ReadFromJsonAsync<ExpenseResponseDto>())!;
            Assert.Equal(2, edited.Version);
            Assert.Equal(expense.CreatedAt, edited.CreatedAt);
            using var get = await Send(HttpMethod.Get, path, group.AccessToken);
            Assert.Equal(120m, (await get.Content.ReadFromJsonAsync<ExpenseResponseDto>())!.Amount);
            using var balances = await Send(HttpMethod.Get, $"/api/groups/{group.Id}/balances", group.AccessToken);
            var values = (await balances.Content.ReadFromJsonAsync<MemberBalanceResponseDto[]>())!;
            Assert.Equal(0m, values.Sum(b => b.Balance));
            Assert.Equal(80m, values.Single(b => b.MemberId == group.Members[0].Id).Balance);
            using var settlement = await Send(HttpMethod.Get, $"/api/groups/{group.Id}/settlements", group.AccessToken);
            Assert.Equal(80m, (await settlement.Content.ReadFromJsonAsync<PaymentInstruction[]>())!.Sum(p => p.Amount));
            using var staleDelete = await Send(HttpMethod.Delete, path + "?version=1", group.AccessToken);
            Assert.Equal(HttpStatusCode.Conflict, staleDelete.StatusCode);
            using var delete = await Send(HttpMethod.Delete, path + "?version=2", group.AccessToken);
            Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
            using var repeat = await Send(HttpMethod.Delete, path + "?version=2", group.AccessToken);
            Assert.Equal(HttpStatusCode.NotFound, repeat.StatusCode);
        }

        [MongoFact]
        public async Task EveryProtectedRoute_RejectsMissingAndWrongToken()
        {
            var group = await CreateGroup();
            var expense = await CreateExpense(group);
            var root = $"/api/groups/{group.Id}";
            var cases = new (HttpMethod Method, string Path, object? Body)[]
            {
                (HttpMethod.Get, root, null), (HttpMethod.Get, root + "/expenses", null),
                (HttpMethod.Get, root + "/expenses/" + expense.Id, null),
                (HttpMethod.Get, root + "/balances", null), (HttpMethod.Get, root + "/settlements", null),
                (HttpMethod.Post, root + "/expenses", Payload(group)),
                (HttpMethod.Put, root + "/expenses/" + expense.Id, Payload(group, 120, 1)),
                (HttpMethod.Delete, root + "/expenses/" + expense.Id + "?version=1", null)
            };
            foreach (var item in cases)
            {
                using var missing = await Send(item.Method, item.Path, null, item.Body);
                Assert.Equal(HttpStatusCode.Unauthorized, missing.StatusCode);
                using var wrong = await Send(item.Method, item.Path, new string('0', 64), item.Body);
                Assert.Equal(HttpStatusCode.Forbidden, wrong.StatusCode);
            }
            var stored = await fixture.Groups.Find(g => g.Id == group.Id).SingleAsync();
            Assert.NotEqual(group.AccessToken, stored.AccessTokenHash);
            Assert.True(GroupTokens.Matches(group.AccessToken, stored.AccessTokenHash));
            using var publicGroup = await Send(HttpMethod.Get, root, group.AccessToken);
            var json = await publicGroup.Content.ReadAsStringAsync();
            Assert.DoesNotContain("accessToken", json, StringComparison.OrdinalIgnoreCase);
        }

        [MongoFact]
        public async Task ConcurrentUpdates_OnlyOneWriterWins()
        {
            var group = await CreateGroup(); var expense = await CreateExpense(group);
            var path = $"/api/groups/{group.Id}/expenses/{expense.Id}";
            var results = await Task.WhenAll(
                Send(HttpMethod.Put, path, group.AccessToken, Payload(group, 120, 1)),
                Send(HttpMethod.Put, path, group.AccessToken, Payload(group, 150, 1)));
            try
            {
                Assert.Single(results.Where(r => r.StatusCode == HttpStatusCode.OK));
                Assert.Single(results.Where(r => r.StatusCode == HttpStatusCode.Conflict));
            }
            finally { foreach (var result in results) result.Dispose(); }
        }

        [MongoFact]
        public async Task VersionsAndValidation_AreRequiredWithoutChangingStoredExpense()
        {
            var group = await CreateGroup(); var expense = await CreateExpense(group);
            var path = $"/api/groups/{group.Id}/expenses/{expense.Id}";
            using var noVersion = await Send(HttpMethod.Put, path, group.AccessToken, Payload(group));
            Assert.Equal(HttpStatusCode.BadRequest, noVersion.StatusCode);
            using var noDeleteVersion = await Send(HttpMethod.Delete, path, group.AccessToken);
            Assert.Equal(HttpStatusCode.BadRequest, noDeleteVersion.StatusCode);
            using var invalid = await Send(HttpMethod.Put, path, group.AccessToken, Payload(group, -1, 1));
            Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
            var stored = (await fixture.Repository.GetForExpenseAsync(group.Id, expense.Id))!.Expenses.Single();
            Assert.Equal(90m, stored.Amount); Assert.Equal(1, stored.Version);
            using var unchanged = await Send(HttpMethod.Put, path, group.AccessToken, Payload(group, 90, 1));
            Assert.Equal(HttpStatusCode.OK, unchanged.StatusCode);
        }

        [MongoFact]
        public async Task OtherGroups_CannotReadEditOrDeleteExpense()
        {
            var group = await CreateGroup(); var other = await CreateGroup(); var expense = await CreateExpense(group);
            var path = $"/api/groups/{other.Id}/expenses/{expense.Id}";
            using var read = await Send(HttpMethod.Get, path, other.AccessToken);
            using var edit = await Send(HttpMethod.Put, path, other.AccessToken, Payload(other, 120, 1));
            using var delete = await Send(HttpMethod.Delete, path + "?version=1", other.AccessToken);
            Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, edit.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, delete.StatusCode);
            Assert.Single((await fixture.Repository.GetByIdAsync(group.Id))!.Expenses);
        }

        [MongoFact]
        public async Task PaginationAndProjections_ReturnOnlyRequestedData()
        {
            var group = await CreateGroup();
            for (var i = 0; i < 3; i++) await CreateExpense(group);
            using var page = await Send(HttpMethod.Get, $"/api/groups/{group.Id}/expenses?page=2&pageSize=2", group.AccessToken);
            var expenses = (await page.Content.ReadFromJsonAsync<ExpenseResponseDto[]>())!;
            Assert.Single(expenses);
            Assert.Empty((await fixture.Repository.GetSummaryAsync(group.Id))!.Expenses);
            Assert.Null((await fixture.Repository.GetSummaryAsync(group.Id))!.AccessTokenHash);
            Assert.Single((await fixture.Repository.GetForExpenseAsync(group.Id, expenses[0].Id))!.Expenses);
            using var invalid = await Send(HttpMethod.Get, $"/api/groups/{group.Id}/expenses?pageSize=101", group.AccessToken);
            Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        }

        [MongoFact]
        public async Task ExpenseLimit_IsAtomicUnderConcurrentInsertions()
        {
            var group = await CreateGroup();
            var seed = Enumerable.Range(0, GroupRules.MaxExpenses - 1).Select(_ => new Expense
            {
                Title = "Teste",
                Amount = 1,
                PaidByMemberId = group.Members[0].Id,
                SplitAmongMemberIds = new() { group.Members[0].Id },
                Version = 1
            }).ToList();
            await fixture.Groups.UpdateOneAsync(g => g.Id == group.Id, Builders<Group>.Update.Set(g => g.Expenses, seed));
            var results = await Task.WhenAll(
                Send(HttpMethod.Post, $"/api/groups/{group.Id}/expenses", group.AccessToken, Payload(group)),
                Send(HttpMethod.Post, $"/api/groups/{group.Id}/expenses", group.AccessToken, Payload(group)));
            try
            {
                Assert.Single(results.Where(r => r.StatusCode == HttpStatusCode.Created));
                Assert.Single(results.Where(r => r.StatusCode == HttpStatusCode.Conflict));
                Assert.Equal(GroupRules.MaxExpenses, (await fixture.Repository.GetByIdAsync(group.Id))!.Expenses.Count);
            }
            finally { foreach (var r in results) r.Dispose(); }
        }

        [MongoFact]
        public async Task LegacyDocuments_RequireTokenAndSupportVersionZero()
        {
            var group = await CreateGroup(); var expense = await CreateExpense(group);
            await fixture.Documents.UpdateOneAsync(new BsonDocument("_id", ObjectId.Parse(group.Id)),
                new BsonDocument("$unset", new BsonDocument { { "AccessTokenHash", 1 }, { "Expenses.0.Version", 1 } }));
            using var denied = await Send(HttpMethod.Get, $"/api/groups/{group.Id}", group.AccessToken);
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
            await fixture.Groups.UpdateOneAsync(g => g.Id == group.Id,
                Builders<Group>.Update.Set(g => g.AccessTokenHash, GroupTokens.Hash(group.AccessToken)));
            using var update = await Send(HttpMethod.Put, $"/api/groups/{group.Id}/expenses/{expense.Id}", group.AccessToken, Payload(group, 120, 0));
            Assert.Equal(HttpStatusCode.OK, update.StatusCode);
            Assert.Equal(1, (await update.Content.ReadFromJsonAsync<ExpenseResponseDto>())!.Version);
        }

        [MongoFact]
        public async Task CancelledRepositoryOperation_DoesNotPersist()
        {
            using var cts = new CancellationTokenSource(); cts.Cancel();
            var group = new Group { Id = ObjectId.GenerateNewId().ToString(), Name = "Cancelado" };
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Repository.CreateAsync(group, cts.Token));
            Assert.Null(await fixture.Repository.GetByIdAsync(group.Id));
        }
    }
}
