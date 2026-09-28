namespace BoraRachar.DTOs.Group
{
    public sealed record GroupResponseDto(string Id, string Name, IReadOnlyList<MemberResponseDto> Members);
}
