using Microsoft.AspNetCore.Mvc;
using ShiftTimeTable.API.Services;

namespace ShiftTimeTable.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SchedulesController : ControllerBase
{
    private readonly BreakSchedulingService _breakSchedulingService;

    public SchedulesController(
        BreakSchedulingService breakSchedulingService)
    {
        _breakSchedulingService = breakSchedulingService;
    }

    [HttpPost("Generate")]
    public async Task<IActionResult> GenerateSchedule(
        [FromBody] GenerateScheduleRequest request)
    {
        try
        {
            var breaks =
                await _breakSchedulingService.GenerateBreaksAsync(
                    request.StoreId,
                    request.Date
                );

            var result = breaks
                .OrderBy(x => x.StartTime)
                .Select(x => new
                {
                    x.Id,
                    x.ShiftId,
                    EmployeeName = x.Shift?.Employee?.Name,
                    x.Date,
                    x.StartTime,
                    x.EndTime
                })
                .ToList();

            return Ok(new
            {
                message = "Mola programı başarıyla oluşturuldu.",
                storeId = request.StoreId,
                date = request.Date.Date,
                breakCount = result.Count,
                breaks = result
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new
            {
                message = ex.Message
            });
        }
    }
}

public class GenerateScheduleRequest
{
    public int StoreId { get; set; }

    public DateTime Date { get; set; }
}