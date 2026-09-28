using BoraRachar.Models;
using BoraRachar.Repositories;
using BoraRachar.Services;
using BoraRachar.Services.Results;

namespace BoraRachar.Tests
{
    public class ExpenseServiceTests
    {
        private const string GroupId = "507f1f77bcf86cd799439011";
        private const string AntonioId = "member-antonio";
        private const string MariaId = "member-maria";
        private const string OutsiderId = "member-outside";

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task CreateAsync_WithValidData_SavesExpense(
            bool payerParticipates)
        {
            var repository = new RecordingGroupRepository();
            var service = new ExpenseService(repository);

            var participants = payerParticipates
                ? new[] { AntonioId, MariaId }
                : new[] { MariaId };

            var result = await service.CreateAsync(
                GroupId,
                "  Jantar  ",
                90m,
                AntonioId,
                participants);

            Assert.Equal(CreateExpenseStatus.Created, result.Status);

            var expense = Assert.IsType<Expense>(result.Expense);

            Assert.Equal("Jantar", expense.Title);
            Assert.Equal(90m, expense.Amount);
            Assert.Equal(AntonioId, expense.PaidByMemberId);
            Assert.Equal<string>(
                participants,
                expense.SplitAmongMemberIds);

            Assert.False(string.IsNullOrWhiteSpace(expense.Id));
            Assert.Equal(DateTimeKind.Utc, expense.CreatedAt.Kind);

            Assert.Equal(1, repository.AddCalls);
            Assert.Equal(GroupId, repository.LastGroupId);
            Assert.Same(expense, repository.SavedExpense);

            Assert.Null(result.ErrorField);
            Assert.Null(result.ErrorMessage);
        }

        public static TheoryData<string, decimal, string, string[], string> InvalidExpenses => new()
        {
        // Título inválido.
        { "", 90m, AntonioId, new[] { MariaId }, "Title" },
        { " A ", 90m, AntonioId, new[] { MariaId }, "Title" },
        { new string('A', 101), 90m, AntonioId, new[] { MariaId }, "Title" },

        // Valor inválido.
        { "Jantar", 0m, AntonioId, new[] { MariaId }, "Amount" },
        { "Jantar", -10m, AntonioId, new[] { MariaId }, "Amount" },
        { "Jantar", 10.999m, AntonioId, new[] { MariaId }, "Amount" },

        // Pagador ausente.
        { "Jantar", 90m, " ", new[] { MariaId }, "PaidByMemberId" },

        // Divisão vazia, com ID vazio ou com repetição.
        { "Jantar", 90m, AntonioId, Array.Empty<string>(), "SplitAmongMemberIds" },
        { "Jantar", 90m, AntonioId, new[] { " " }, "SplitAmongMemberIds" },
        { "Jantar", 90m, AntonioId, new[] { MariaId, MariaId }, "SplitAmongMemberIds" },

        // Pessoas que não pertencem ao grupo.
        { "Jantar", 90m, OutsiderId, new[] { MariaId }, "PaidByMemberId" },
        { "Jantar", 90m, AntonioId, new[] { OutsiderId }, "SplitAmongMemberIds" },

        { "Jantar", ExpenseRules.MaxAmount + 0.01m, AntonioId,new[] { MariaId }, "Amount" },
        { "Jantar", decimal.MaxValue, AntonioId, new[] { MariaId }, "Amount"},

        };

        [Theory]
        [MemberData(nameof(InvalidExpenses))]
        public async Task CreateAsync_WithInvalidData_DoesNotSave(
            string title,
            decimal amount,
            string paidByMemberId,
            string[] participants,
            string expectedField)
        {
            var repository = new RecordingGroupRepository();
            var service = new ExpenseService(repository);

            var result = await service.CreateAsync(
                GroupId,
                title,
                amount,
                paidByMemberId,
                participants);

            Assert.Equal(
                CreateExpenseStatus.ValidationFailed,
                result.Status);

            Assert.Equal(expectedField, result.ErrorField);
            Assert.False(string.IsNullOrWhiteSpace(result.ErrorMessage));
            Assert.Null(result.Expense);

            Assert.Equal(0, repository.AddCalls);
            Assert.Null(repository.SavedExpense);
        }

        [Fact]
        public async Task CreateAsync_WhenGroupDoesNotExist_DoesNotSave()
        {
            var repository = new RecordingGroupRepository
            {
                ReturnedGroup = null
            };

            var service = new ExpenseService(repository);

            var result = await service.CreateAsync(
                GroupId,
                "Jantar",
                90m,
                AntonioId,
                new[] { MariaId });

            Assert.Equal(
                CreateExpenseStatus.GroupNotFound,
                result.Status);

            Assert.Null(result.Expense);
            Assert.Equal(0, repository.AddCalls);
            Assert.Null(repository.SavedExpense);
        }

        [Fact]
        public async Task CreateAsync_WhenUpdateFindsNoGroup_ReturnsNotFound()
        {
            var repository = new RecordingGroupRepository
            {
                AddResult = false
            };

            var service = new ExpenseService(repository);

            var result = await service.CreateAsync(
                GroupId,
                "Jantar",
                90m,
                AntonioId,
                new[] { MariaId });

            Assert.Equal(
                CreateExpenseStatus.GroupNotFound,
                result.Status);

            Assert.Null(result.Expense);
            Assert.Equal(1, repository.AddCalls);
            Assert.Null(repository.SavedExpense);
        }

        private static Group CreateGroup()
        {
            return new Group
            {
                Id = GroupId,
                Name = "Viagem",
                Members = new List<Member>
            {
                new() { Id = AntonioId, Name = "Antonio" },
                new() { Id = MariaId, Name = "Maria" }
            }
            };
        }

        [Fact]
        public async Task GetBalancesAsync_DistributesRemainder_AndPreservesTotal()
        {
            var group = new Group
            {
                Id = GroupId,
                Name = "Viagem",
                Members = new List<Member>
                {
                    new() { Id = AntonioId, Name = "Antonio" },
                    new() { Id = MariaId, Name = "Maria" },
                    new() { Id = "member-joao", Name = "João" }
                },
                Expenses = new List<Expense>
                {
                    new()
                    {
                        Title = "Lanche",
                        Amount = 10m,
                        PaidByMemberId = AntonioId,
                        SplitAmongMemberIds = new List<string>
                        {
                            AntonioId,
                            MariaId,
                            "member-joao"
                        }
                    }
                }
            };

            var repository = new RecordingGroupRepository
            {
                ReturnedGroup = group
            };

            var service = new ExpenseService(repository);

            var result = await service.GetBalancesAsync(GroupId);

            Assert.NotNull(result);
            Assert.Equal(3, result.Count);

            var antonio = result.Single(x => x.MemberId == AntonioId);
            var maria = result.Single(x => x.MemberId == MariaId);
            var joao = result.Single(x => x.MemberId == "member-joao");

            Assert.Equal(10m, antonio.TotalPaid);
            Assert.Equal(3.34m, antonio.TotalShare);
            Assert.Equal(6.66m, antonio.Balance);

            Assert.Equal(-3.33m, maria.Balance);
            Assert.Equal(-3.33m, joao.Balance);

            Assert.Equal(10m, result.Sum(x => x.TotalShare));
            Assert.Equal(0m, result.Sum(x => x.Balance));
        }

        [Theory]
        [InlineData(120, true)]
        [InlineData(-10, false)]
        public async Task UpdateAsync_ValidatesAmount_AndPreservesIdentity(int amount, bool shouldUpdate)
        {
            var group = CreateGroup();
            var createdAt = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

            group.Expenses.Add(new Expense
            {
                Id = "expense-jantar",
                Title = "Jantar",
                Amount = 90m,
                PaidByMemberId = AntonioId,
                SplitAmongMemberIds = new List<string>
                {
                    AntonioId,
                    MariaId
                },
                CreatedAt = createdAt
            });

            var repository = new RecordingGroupRepository
            {
                ReturnedGroup = group
            };

            var service = new ExpenseService(repository);

            var result = await service.UpdateAsync(GroupId, "expense-jantar", "Jantar corrigido", amount, AntonioId, new[] { AntonioId, MariaId });

            Assert.Equal(1, repository.GetCalls);

            var saved = Assert.Single(group.Expenses);

            Assert.Equal("expense-jantar", saved.Id);
            Assert.Equal(createdAt, saved.CreatedAt);

            if (shouldUpdate)
            {
                Assert.Equal(UpdateExpenseStatus.Updated, result.Status);
                Assert.Equal(120m, saved.Amount);
                Assert.Equal("Jantar corrigido", saved.Title);

                var balances = await service.GetBalancesAsync(GroupId);

                Assert.NotNull(balances);
                Assert.Equal(60m, balances.Single(x => x.MemberId == AntonioId).Balance);
                Assert.Equal(-60m, balances.Single(x => x.MemberId == MariaId).Balance);
            }
            else
            {
                Assert.Equal(UpdateExpenseStatus.ValidationFailed, result.Status);

                Assert.Equal("Amount", result.ErrorField);
                Assert.Equal(90m, saved.Amount);
                Assert.Equal("Jantar", saved.Title);
            }
        }

        [Fact]
        public async Task DeleteAsync_RemovesExpense_AndRecalculatesBalances()
        {
            var group = CreateGroup();

            group.Expenses.Add(new Expense
            {
                Id = "expense-jantar",
                Title = "Jantar",
                Amount = 90m,
                PaidByMemberId = AntonioId,
                SplitAmongMemberIds = new List<string>
                {
                    AntonioId,
                    MariaId
                }
            });

            var repository = new RecordingGroupRepository
            {
                ReturnedGroup = group
            };

            var service = new ExpenseService(repository);

            var before = await service.GetBalancesAsync(GroupId);

            Assert.NotNull(before);
            Assert.Equal(45m, before.Single(member => member.MemberId == AntonioId).Balance);

            var deleted = await service.DeleteAsync(GroupId, "expense-jantar");

            Assert.Equal(ExpenseWriteStatus.Applied, deleted);
            Assert.Empty(group.Expenses);

            var after = await service.GetBalancesAsync(GroupId);

            Assert.NotNull(after);
            Assert.All(after, member => Assert.Equal(0m, member.Balance));

            var deletedAgain = await service.DeleteAsync(GroupId, "expense-jantar");

            Assert.Equal(ExpenseWriteStatus.NotFound, deletedAgain);
        }


        [Theory]
        [InlineData(99, true)]
        [InlineData(100, true)]
        [InlineData(101, false)]
        public async Task CreateAsync_ValidatesTitleAfterTrimming(int length, bool accepted)
        {
            var dto = new BoraRachar.DTOs.Expense.CreateExpenseDto
            {
                Title = " " + new string('A', length) + " ",
                Amount = 10m,
                PaidByMemberId = AntonioId,
                SplitAmongMemberIds = new() { MariaId }
            };
            var errors = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
            Assert.True(System.ComponentModel.DataAnnotations.Validator.TryValidateObject(
                dto, new System.ComponentModel.DataAnnotations.ValidationContext(dto), errors, true));

            var repository = new RecordingGroupRepository();
            var service = new ExpenseService(repository);
            var result = await service.CreateAsync(GroupId, dto.Title, dto.Amount,
                dto.PaidByMemberId, dto.SplitAmongMemberIds);

            Assert.Equal(accepted ? CreateExpenseStatus.Created : CreateExpenseStatus.ValidationFailed, result.Status);
            Assert.Equal(accepted ? 1 : 0, repository.AddCalls);
            if (accepted)
                Assert.Equal(new string('A', length), result.Expense!.Title);
            else
                Assert.Equal("Title", result.ErrorField);
        }
        [Fact]
        public void UpdateResultFactories_RejectIncompleteResults()
        {
            Assert.Throws<ArgumentNullException>(() => UpdateExpenseResult.Success(null!));

            foreach (var invalid in new string?[] { null, "", " " })
            {
                Assert.ThrowsAny<ArgumentException>(() =>
                    UpdateExpenseResult.ValidationFailure(invalid!, "Valor inválido."));
                Assert.ThrowsAny<ArgumentException>(() =>
                    UpdateExpenseResult.ValidationFailure("Amount", invalid!));
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task UpdateAsync_WhenGroupOrExpenseIsMissing_ReturnsNotFound(bool groupExists)
        {
            var repository = new RecordingGroupRepository
            {
                ReturnedGroup = groupExists ? CreateGroup() : null
            };
            var service = new ExpenseService(repository);

            var result = await service.UpdateAsync(GroupId, "missing-expense", "Jantar",
                90m, AntonioId, new[] { AntonioId, MariaId });

            Assert.Equal(UpdateExpenseStatus.NotFound, result.Status);
            Assert.Null(result.Expense);
            Assert.Null(result.ErrorField);
            Assert.Null(result.ErrorMessage);
            Assert.Equal(1, repository.GetCalls);
        }
        private sealed class RecordingGroupRepository : TestGroupRepository
        {
            public RecordingGroupRepository() { ReturnedGroup = CreateGroup(); }
        }
    }
}
