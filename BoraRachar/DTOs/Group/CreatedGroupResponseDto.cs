namespace BoraRachar.DTOs.Group
{
    public sealed record CreatedGroupResponseDto(
        string Id,
        string Name,
        IReadOnlyList<MemberResponseDto> Members,
        string AccessToken
    );
}
