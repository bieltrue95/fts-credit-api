using FluentValidation;
using FtsCredit.Api.Features.CreditRequest.Create;
using FtsCredit.Api.Features.CreditRequest.GetStatus;
using FtsCredit.Api.Features.Receivables.Anticipate;
using FtsCredit.Api.Features.ScoreValidation.ValidateScore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FtsCredit.Api.Controllers;

[ApiController]
[Route("api/credit")]
[Authorize]
public class CreditController : ControllerBase
{
    [HttpPost("request")]
    public async Task<IActionResult> CreateRequest(
        [FromBody] CreateCreditRequestCommand command,
        [FromServices] IValidator<CreateCreditRequestCommand> validator,
        [FromServices] CreateCreditRequestHandler handler,
        CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(command, ct);
        if (!validation.IsValid)
            return BadRequest(validation.Errors.Select(e => e.ErrorMessage));

        var result = await handler.HandleAsync(command, ct);
        return CreatedAtAction(nameof(GetStatus), new { id = result.RequestId }, result);
    }

    [HttpGet("{id:guid}/status")]
    public async Task<IActionResult> GetStatus(
        Guid id,
        [FromServices] GetCreditStatusHandler handler,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(new GetCreditStatusQuery(id), ct);
        if (result is null) return NotFound();
        return Ok(result);
    }

    [HttpPost("validate-score")]
    public async Task<IActionResult> ValidateScore(
        [FromBody] ValidateScoreCommand command,
        [FromServices] IValidator<ValidateScoreCommand> validator,
        [FromServices] ValidateScoreHandler handler,
        CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(command, ct);
        if (!validation.IsValid)
            return BadRequest(validation.Errors.Select(e => e.ErrorMessage));

        var result = await handler.HandleAsync(command, ct);
        return Ok(result);
    }

    [HttpPost("receivables")]
    public async Task<IActionResult> AnticipateReceivables(
        [FromBody] AnticipateReceivablesCommand command,
        [FromServices] IValidator<AnticipateReceivablesCommand> validator,
        [FromServices] AnticipateReceivablesHandler handler,
        CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(command, ct);
        if (!validation.IsValid)
            return BadRequest(validation.Errors.Select(e => e.ErrorMessage));

        var result = await handler.HandleAsync(command, ct);
        return Ok(result);
    }
}
