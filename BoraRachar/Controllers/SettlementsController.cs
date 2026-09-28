using BoraRachar.Security;
using BoraRachar.Models;
using BoraRachar.Services;
using Microsoft.AspNetCore.Mvc;

namespace BoraRachar.Controllers
{
    [ApiController]
    [ServiceFilter(typeof(GroupAccessFilter))]
    [ProducesResponseType(typeof(ProblemDetails), 401)]
    [ProducesResponseType(typeof(ProblemDetails), 403)]
    [Route("api/groups/{groupId}/settlements")]
    public sealed class SettlementsController : ControllerBase
    {
        private readonly ISettlementService _settlementService;

        public SettlementsController(ISettlementService settlementService)
        {
            _settlementService = settlementService;
        }

        [HttpGet]
        [ProducesResponseType(typeof(PaymentInstruction[]), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<PaymentInstruction[]>> Get([FromRoute] string groupId, CancellationToken cancellationToken)
        {
            var payments = await _settlementService.GetAsync(groupId, cancellationToken);

            if (payments is null)
            {
                return NotFound();
            }

            return Ok(payments.ToArray());
        }
    }
}
