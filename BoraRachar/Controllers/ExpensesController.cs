using System.ComponentModel.DataAnnotations;
using BoraRachar.DTOs.Expense;
using BoraRachar.Models;
using BoraRachar.Repositories;
using BoraRachar.Security;
using BoraRachar.Services;
using BoraRachar.Services.Results;
using Microsoft.AspNetCore.Mvc;

namespace BoraRachar.Controllers
{
    [ApiController]
    [Route("api/groups/{groupId}/expenses")]
    [ServiceFilter(typeof(GroupAccessFilter))]
    [ProducesResponseType(typeof(ProblemDetails), 401)]
    [ProducesResponseType(typeof(ProblemDetails), 403)]
    public sealed class ExpensesController(IExpenseService service) : ControllerBase
    {
        [HttpPost]
        [ProducesResponseType(typeof(ExpenseResponseDto), 201)]
        [ProducesResponseType(typeof(ValidationProblemDetails), 400)]
        [ProducesResponseType(typeof(ProblemDetails), 404)]
        [ProducesResponseType(typeof(ProblemDetails), 409)]
        public async Task<ActionResult<ExpenseResponseDto>> Create(string groupId, CreateExpenseDto dto, CancellationToken cancellationToken)
        {
            var result = await service.CreateAsync(groupId, dto.Title, dto.Amount, dto.PaidByMemberId, dto.SplitAmongMemberIds, cancellationToken);
            switch (result.Status)
            {
                case CreateExpenseStatus.ValidationFailed:
                    ModelState.AddModelError(result.ErrorField!, result.ErrorMessage!);
                    return ValidationProblem(ModelState);
                case CreateExpenseStatus.GroupNotFound: return NotFound();
                case CreateExpenseStatus.LimitReached: return Problem(statusCode: 409, title: "O grupo atingiu o limite de 1000 despesas.");
                case CreateExpenseStatus.Created:
                    var response = ToResponse(result.Expense!);
                    return CreatedAtAction(nameof(GetById), new { groupId, expenseId = response.Id }, response);
                default: throw new InvalidOperationException("Resultado de criação desconhecido.");
            }
        }

        [HttpGet]
        [ProducesResponseType(typeof(ExpenseResponseDto[]), 200)]
        [ProducesResponseType(typeof(ValidationProblemDetails), 400)]
        public async Task<ActionResult<ExpenseResponseDto[]>> GetAll(string groupId,
            [FromQuery, Range(1, GroupRules.MaxExpenses)] int page = 1,
            [FromQuery, Range(1, GroupRules.MaxPageSize)] int pageSize = GroupRules.DefaultPageSize,
            CancellationToken cancellationToken = default)
        {
            var expenses = await service.GetAllAsync(groupId, page, pageSize, cancellationToken);
            return expenses is null ? NotFound() : Ok(expenses.Select(ToResponse).ToArray());
        }

        [HttpGet("{expenseId}")]
        [ProducesResponseType(typeof(ExpenseResponseDto), 200)]
        [ProducesResponseType(typeof(ProblemDetails), 404)]
        public async Task<ActionResult<ExpenseResponseDto>> GetById(string groupId, string expenseId, CancellationToken cancellationToken)
        {
            var expense = await service.GetByIdAsync(groupId, expenseId, cancellationToken);
            return expense is null ? NotFound() : Ok(ToResponse(expense));
        }

        [HttpGet("/api/groups/{groupId}/balances")]
        [ProducesResponseType(typeof(MemberBalanceResponseDto[]), 200)]
        public async Task<ActionResult<MemberBalanceResponseDto[]>> GetBalances(string groupId, CancellationToken cancellationToken)
        {
            var balances = await service.GetBalancesAsync(groupId, cancellationToken);
            return balances is null ? NotFound() : Ok(balances.Select(b => new MemberBalanceResponseDto(
                b.MemberId, b.MemberName, b.TotalPaid, b.TotalShare, b.Balance)).ToArray());
        }

        [HttpPut("{expenseId}")]
        [ProducesResponseType(typeof(ExpenseResponseDto), 200)]
        [ProducesResponseType(typeof(ValidationProblemDetails), 400)]
        [ProducesResponseType(typeof(ProblemDetails), 404)]
        [ProducesResponseType(typeof(ProblemDetails), 409)]
        public async Task<ActionResult<ExpenseResponseDto>> Update(string groupId, string expenseId, UpdateExpenseDto dto, CancellationToken cancellationToken)
        {
            var result = await service.UpdateAsync(groupId, expenseId, dto.Title, dto.Amount,
                dto.PaidByMemberId, dto.SplitAmongMemberIds, dto.Version!.Value, cancellationToken);
            switch (result.Status)
            {
                case UpdateExpenseStatus.NotFound: return NotFound();
                case UpdateExpenseStatus.Conflict: return VersionConflict();
                case UpdateExpenseStatus.ValidationFailed:
                    ModelState.AddModelError(result.ErrorField!, result.ErrorMessage!);
                    return ValidationProblem(ModelState);
                case UpdateExpenseStatus.Updated: return Ok(ToResponse(result.Expense!));
                default: throw new InvalidOperationException("Resultado de edição desconhecido.");
            }
        }

        [HttpDelete("{expenseId}")]
        [ProducesResponseType(204)]
        [ProducesResponseType(typeof(ValidationProblemDetails), 400)]
        [ProducesResponseType(typeof(ProblemDetails), 404)]
        [ProducesResponseType(typeof(ProblemDetails), 409)]
        public async Task<IActionResult> Delete(string groupId, string expenseId,
            [FromQuery, Required, Range(0, int.MaxValue)] int? version, CancellationToken cancellationToken)
        {
            var result = await service.DeleteAsync(groupId, expenseId, version!.Value, cancellationToken);
            return result switch
            {
                ExpenseWriteStatus.Applied => NoContent(),
                ExpenseWriteStatus.Conflict => VersionConflict(),
                _ => NotFound()
            };
        }

        private ObjectResult VersionConflict() => Problem(statusCode: 409,
            title: "A despesa foi alterada. Atualize os dados antes de tentar novamente.");

        private static ExpenseResponseDto ToResponse(Expense e) => new(e.Id, e.Title, e.Amount,
            e.PaidByMemberId, e.SplitAmongMemberIds.ToArray(), e.CreatedAt, e.Version);
    }
}
