using BoraRachar.Models;
using BoraRachar.Services;

namespace BoraRachar.Tests
{
    public class SettlementServiceTests
    {
        [Fact]
        public async Task GetAsync_WhenOneMemberPays_SettlesOtherMembers()
        {
            var group = new Group
            {
                Id = "507f1f77bcf86cd799439011",
                Name = "Viagem",
                Members = new List<Member>
            {
                new() { Id = "antonio", Name = "Antônio" },
                new() { Id = "maria", Name = "Maria" },
                new() { Id = "joao", Name = "João" }
            },
                Expenses = new List<Expense>
            {
                new()
                {
                    Title = "Jantar",
                    Amount = 90m,
                    PaidByMemberId = "antonio",
                    SplitAmongMemberIds = new List<string>
                    {
                        "antonio", "maria", "joao"
                    }
                }
            }
            };

            var repository = new StubGroupRepository(group);
            var expenseService = new ExpenseService(repository);
            var service = new SettlementService(expenseService);

            var payments = await service.GetAsync(group.Id!);

            Assert.NotNull(payments);
            Assert.Equal(2, payments.Count);

            Assert.Contains(
                new PaymentInstruction("maria", "antonio", 30m),
                payments);

            Assert.Contains(
                new PaymentInstruction("joao", "antonio", 30m),
                payments);

            Assert.Equal(60m, payments.Sum(payment => payment.Amount));
        }

        private sealed class StubGroupRepository : TestGroupRepository
        {
            public StubGroupRepository(Group group) { ReturnedGroup = group; }
        }
    }
}
