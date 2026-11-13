using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScanApi.Dtos;
using ScanApi.Services;
using Sims.Contracts;

namespace ScanApi.Controllers;

[ApiController]
[Route("audit")]
[Authorize(Policy = Policies.AdminOnly)]
public class AuditController(IAuditQueryService auditService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResponse<AuditEntryResponse>>> GetAll([FromQuery] AuditFilter filter)
    {
        PagedResponse<AuditEntryResponse> page = await auditService.ListAsync(filter);
        return Ok(page);
    }
}
