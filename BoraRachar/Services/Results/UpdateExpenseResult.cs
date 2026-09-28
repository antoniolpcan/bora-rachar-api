using BoraRachar.Models;

namespace BoraRachar.Services.Results
{
    public enum UpdateExpenseStatus
    {
        Updated,
        ValidationFailed,
        NotFound,
        Conflict
    }

    public sealed class UpdateExpenseResult
    {
        public UpdateExpenseStatus Status { get; }
        public Expense? Expense { get; }
        public string? ErrorField { get; }
        public string? ErrorMessage { get; }

        private UpdateExpenseResult(
            UpdateExpenseStatus status,
            Expense? expense,
            string? errorField,
            string? errorMessage)
        {
            Status = status;
            Expense = expense;
            ErrorField = errorField;
            ErrorMessage = errorMessage;
        }

        public static UpdateExpenseResult Success(Expense expense)
        {
            ArgumentNullException.ThrowIfNull(expense);

            return new UpdateExpenseResult(
                UpdateExpenseStatus.Updated, expense, null, null);
        }

        public static UpdateExpenseResult ValidationFailure(string field, string message)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(field);
            ArgumentException.ThrowIfNullOrWhiteSpace(message);

            return new UpdateExpenseResult(
                UpdateExpenseStatus.ValidationFailed, null, field, message);
        }

        public static UpdateExpenseResult Conflict()
            => new(UpdateExpenseStatus.Conflict, null, null, null);

        public static UpdateExpenseResult NotFound()
        {
            return new UpdateExpenseResult(
                UpdateExpenseStatus.NotFound, null, null, null);
        }
    }
}
