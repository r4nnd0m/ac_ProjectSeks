using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScanApi.Dtos;
using Sims.Contracts;
using ScanApi.Services;

namespace ScanApi.Controllers;

[ApiController]
[Route("tickets")]
[Authorize]
public class TicketsController(ITicketService ticketService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResponse<TicketResponse>>> GetAll([FromQuery] TicketFilter filter)
    {
        PagedResponse<TicketResponse> page = await ticketService.ListAsync(filter, User);
        return Ok(page);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TicketResponse>> GetById(Guid id)
    {
        TicketResponse? ticket = await ticketService.GetAsync(id, User);
        return ticket is null ? NotFound() : Ok(ticket);
    }

    [HttpPost]
    public async Task<ActionResult<TicketResponse>> Create([FromBody] CreateTicketRequest request)
    {
        TicketResponse? created = await ticketService.CreateAsync(request, User);

        if (created is null)
        {
            return NotFound();
        }

        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<TicketResponse>> Update(Guid id, [FromBody] UpdateTicketRequest request)
    {
        TicketResult result = await ticketService.UpdateAsync(id, request, User);

        if (result.Outcome == TicketOutcome.NotFound)
        {
            return NotFound();
        }

        if (result.Outcome == TicketOutcome.Conflict)
        {
            return ConflictProblem();
        }

        return Ok(result.Ticket);
    }

    [HttpPost("{id:guid}/take")]
    public async Task<IActionResult> Take(Guid id)
    {
        TicketOutcome outcome = await ticketService.TakeAsync(id, User);
        return ToResult(outcome);
    }

    [HttpPost("{id:guid}/close")]
    public async Task<IActionResult> Close(Guid id)
    {
        TicketOutcome outcome = await ticketService.CloseAsync(id, User);
        return ToResult(outcome);
    }
    [HttpPost("{id:guid}/accept-risk")]
    [Authorize(Policy = Policies.AdminOnly)]
    [Authorize(Policy = Policies.MfaRequired)]
    public async Task<IActionResult> AcceptRisk(Guid id, [FromBody] AcceptRiskRequest request)
    {
        TicketOutcome outcome = await ticketService.AcceptRiskAsync(id, request, User);
        return ToResult(outcome);
    }

    private IActionResult ToResult(TicketOutcome outcome)
    {
        return outcome switch
        {
            TicketOutcome.Ok => NoContent(),
            TicketOutcome.NotFound => NotFound(),
            _ => ConflictProblem()
        };
    }

    private ObjectResult ConflictProblem()
    {
        return Problem(statusCode: StatusCodes.Status409Conflict, title: "Das Ticket ist bereits abgeschlossen.");
    }
}