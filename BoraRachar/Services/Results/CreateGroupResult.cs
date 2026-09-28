using BoraRachar.Models;

namespace BoraRachar.Services.Results
{
    public sealed class CreateGroupResult
    {
        public Group? Group { get; }
        public string? AccessToken { get; }
        public string? ErrorField { get; }
        public string? ErrorMessage { get; }
        public bool IsSuccess => Group is not null;

        private CreateGroupResult(Group? group, string? errorField, string? errorMessage, string? accessToken = null)
        {
            Group = group;
            AccessToken = accessToken;
            ErrorField = errorField;
            ErrorMessage = errorMessage;
        }

        public static CreateGroupResult Success(Group group, string accessToken)
        {
            ArgumentNullException.ThrowIfNull(group);
            ArgumentException.ThrowIfNullOrWhiteSpace(accessToken);

            return new CreateGroupResult(group, null, null, accessToken);
        }

        public static CreateGroupResult Failure(string field, string message)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(field);
            ArgumentException.ThrowIfNullOrWhiteSpace(message);

            return new CreateGroupResult(null, field, message);
        }
    }
}
