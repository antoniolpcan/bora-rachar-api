using BoraRachar.DTOs.Group;
using BoraRachar.Models;
using BoraRachar.Security;
using BoraRachar.Services;
using Microsoft.AspNetCore.Mvc;

namespace BoraRachar.Controllers
{
    [ApiController]
    [Route("api/groups")]
    public sealed class GroupsController(IGroupService service) : ControllerBase
    {
        [HttpGet("{id}")]
        [ServiceFilter(typeof(GroupAccessFilter))]
        [ProducesResponseType(typeof(GroupResponseDto), 200)]
        [ProducesResponseType(typeof(ProblemDetails), 401)]
        [ProducesResponseType(typeof(ProblemDetails), 403)]
        public async Task<ActionResult<GroupResponseDto>> GetById(string id, CancellationToken cancellationToken)
        {
            var group = await service.GetByIdAsync(id, cancellationToken);
            return group is null ? NotFound() : Ok(ToResponse(group));
        }

        [HttpPost]
        [ProducesResponseType(typeof(CreatedGroupResponseDto), 201)]
        [ProducesResponseType(typeof(ValidationProblemDetails), 400)]
        public async Task<ActionResult<CreatedGroupResponseDto>> Create(CreateGroupDto dto, CancellationToken cancellationToken)
        {
            var result = await service.CreateAsync(dto.Name, dto.Members, cancellationToken);
            if (!result.IsSuccess)
            {
                ModelState.AddModelError(result.ErrorField!, result.ErrorMessage!);
                return ValidationProblem(ModelState);
            }
            var group = ToResponse(result.Group!);
            Response.Headers.CacheControl = "no-store";
            var response = new CreatedGroupResponseDto(group.Id, group.Name, group.Members, result.AccessToken!);
            return CreatedAtAction(nameof(GetById), new { id = group.Id }, response);
        }

        private static GroupResponseDto ToResponse(Group group) => new(group.Id!, group.Name,
            group.Members.Select(m => new MemberResponseDto(m.Id, m.Name)).ToArray());
    }
}
