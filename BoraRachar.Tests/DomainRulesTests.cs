using BoraRachar.Models;
using BoraRachar.Security;
using BoraRachar.Services;
using BoraRachar.Services.Calculations;
using BoraRachar.Services.Validation;

namespace BoraRachar.Tests
{
    public class DomainRulesTests
    {
        [Fact]
        public void Tokens_AreRandomAndRequireExactMatch()
        {
            var token = GroupTokens.Generate();
            var hash = GroupTokens.Hash(token);
            Assert.Equal(64, token.Length);
            Assert.NotEqual(token, GroupTokens.Generate());
            Assert.NotEqual(token, hash);
            Assert.True(GroupTokens.Matches(token, hash));
            Assert.False(GroupTokens.Matches(GroupTokens.Generate(), hash));
            Assert.False(GroupTokens.Matches(token, null));
            Assert.False(GroupTokens.Matches(token, new string('X', 64)));
        }

        [Theory]
        [InlineData(50, 100, true)]
        [InlineData(51, 100, false)]
        [InlineData(2, 101, false)]
        public async Task GroupLimits_AreEnforced(int count, int length, bool accepted)
        {
            var repository = new TestGroupRepository();
            var service = new GroupService(repository);
            var result = await service.CreateAsync("Viagem", Enumerable.Repeat(new string('A', length), count).ToArray());
            Assert.Equal(accepted, result.IsSuccess);
            Assert.Equal(accepted ? 1 : 0, repository.CreateCalls);
        }

        [Fact]
        public void SplitLimit_RejectsTooManyParticipants()
        {
            var error = ExpenseValidator.ValidateInput("Jantar", 10, "payer",
                Enumerable.Range(0, 51).Select(i => i.ToString()).ToArray());
            Assert.Equal("SplitAmongMemberIds", error?.Field);
        }

        [Fact]
        public async Task CancelledCreate_DoesNotAccessRepository()
        {
            var repository = new TestGroupRepository();
            var service = new ExpenseService(repository);
            using var cts = new CancellationTokenSource(); cts.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                service.CreateAsync("group", "Jantar", 10, "a", new[] { "a" }, cts.Token));
            Assert.Equal(0, repository.GetCalls);
            Assert.Equal(0, repository.AddCalls);
        }

        [Fact]
        public void OneCentAndMultiplePayers_ConserveMoneyAndSettleAllBalances()
        {
            var group = new Group
            {
                Members = new() { new() { Id = "a" }, new() { Id = "b" }, new() { Id = "c" } },
                Expenses = new()
                {
                    new() { Amount = 0.01m, PaidByMemberId = "b", SplitAmongMemberIds = new() { "a", "b", "c" } },
                    new() { Amount = 20m, PaidByMemberId = "a", SplitAmongMemberIds = new() { "b", "c" } },
                    new() { Amount = 10m, PaidByMemberId = "b", SplitAmongMemberIds = new() { "a", "c" } }
                }
            };
            var balances = BalanceCalculator.Calculate(group);
            Assert.Equal(30.01m, balances.Sum(b => b.TotalShare));
            Assert.Equal(0m, balances.Sum(b => b.Balance));
            var remaining = balances.ToDictionary(b => b.MemberId, b => b.Balance);
            foreach (var payment in SettlementCalculator.Calculate(balances))
            {
                Assert.True(payment.Amount > 0);
                Assert.NotEqual(payment.FromMemberId, payment.ToMemberId);
                remaining[payment.FromMemberId] += payment.Amount;
                remaining[payment.ToMemberId] -= payment.Amount;
            }
            Assert.All(remaining.Values, value => Assert.Equal(0m, value));
        }
    }
}
