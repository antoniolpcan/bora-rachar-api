using BoraRachar.Models;

namespace BoraRachar.Services.Results
{
    public enum CreateExpenseStatus
    {
        Created,
        ValidationFailed,
        GroupNotFound,
        LimitReached
    }

    public sealed class CreateExpenseResult
    {
        public CreateExpenseStatus Status { get; }
        public Expense? Expense { get; }
        public string? ErrorField { get; }
        public string? ErrorMessage { get; }

        private CreateExpenseResult(CreateExpenseStatus status, Expense? expense, string? errorField, string? errorMessage)
        {
            Status = status;
            Expense = expense;
            ErrorField = errorField;
            ErrorMessage = errorMessage;
        }

        public static CreateExpenseResult Success(Expense expense)
        {
            ArgumentNullException.ThrowIfNull(expense);

            return new CreateExpenseResult(CreateExpenseStatus.Created, expense, null, null);
        }

        public static CreateExpenseResult ValidationFailure(string field, string message)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(field);
            ArgumentException.ThrowIfNullOrWhiteSpace(message);

            return new CreateExpenseResult(CreateExpenseStatus.ValidationFailed, null, field, message);
        }

        public static CreateExpenseResult LimitReached()
            => new(CreateExpenseStatus.LimitReached, null, null, null);

        public static CreateExpenseResult GroupNotFound()
        {
            return new CreateExpenseResult(CreateExpenseStatus.GroupNotFound, null, null, null);
        }
    }
}
