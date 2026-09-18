using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShiftTimeTable.API.Data;
using ShiftTimeTable.API.Models;

namespace ShiftTimeTable.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BreaksController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public BreaksController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetBreaks()
    {
        var breaks = await _context.Breaks
            .OrderBy(x => x.Date)
            .ThenBy(x => x.StartTime)
            .Select(x => new
            {
                x.Id,
                x.ShiftId,
                EmployeeName = x.Shift != null &&
                               x.Shift.Employee != null
                    ? x.Shift.Employee.Name
                    : null,
                x.Date,
                x.StartTime,
                x.EndTime
            })
            .ToListAsync();

        return Ok(breaks);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetBreak(int id)
    {
        var breakItem = await _context.Breaks
            .Where(x => x.Id == id)
            .Select(x => new
            {
                x.Id,
                x.ShiftId,
                EmployeeName = x.Shift != null &&
                               x.Shift.Employee != null
                    ? x.Shift.Employee.Name
                    : null,
                x.Date,
                x.StartTime,
                x.EndTime
            })
            .FirstOrDefaultAsync();

        if (breakItem == null)
            return NotFound();

        return Ok(breakItem);
    }

    [HttpPost]
    public async Task<IActionResult> CreateBreak(
        [FromBody] CreateBreakRequest request)
    {
        var shift = await _context.Shifts
            .FirstOrDefaultAsync(x => x.Id == request.ShiftId);

        if (shift == null)
        {
            return BadRequest("Belirtilen vardiya bulunamadı.");
        }

        if (!TimeSpan.TryParse(request.StartTime, out var startTime))
        {
            return BadRequest(
                "Başlangıç saati geçersiz. Örnek: 12:00"
            );
        }

        if (!TimeSpan.TryParse(request.EndTime, out var endTime))
        {
            return BadRequest(
                "Bitiş saati geçersiz. Örnek: 12:30"
            );
        }

        if (endTime <= startTime)
        {
            return BadRequest(
                "Mola bitiş saati başlangıç saatinden sonra olmalıdır."
            );
        }

        if (startTime < shift.StartTime ||
            endTime > shift.EndTime)
        {
            return BadRequest(
                "Mola, personelin vardiya saatleri içerisinde olmalıdır."
            );
        }

        var breakItem = new Break
        {
            ShiftId = shift.Id,
            Date = shift.Date,
            StartTime = startTime,
            EndTime = endTime
        };

        _context.Breaks.Add(breakItem);

        await _context.SaveChangesAsync();

        return CreatedAtAction(
            nameof(GetBreak),
            new { id = breakItem.Id },
            new
            {
                breakItem.Id,
                breakItem.ShiftId,
                breakItem.Date,
                breakItem.StartTime,
                breakItem.EndTime
            }
        );
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteBreak(int id)
    {
        var breakItem = await _context.Breaks.FindAsync(id);

        if (breakItem == null)
            return NotFound();

        _context.Breaks.Remove(breakItem);

        await _context.SaveChangesAsync();

        return NoContent();
    }
}

public class CreateBreakRequest
{
    public int ShiftId { get; set; }

    public string StartTime { get; set; } = string.Empty;

    public string EndTime { get; set; } = string.Empty;
}


