using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShiftTimeTable.API.Data;
using ShiftTimeTable.API.Models;

namespace ShiftTimeTable.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ShiftsController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public ShiftsController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetShifts()
    {
        var shifts = await _context.Shifts
            .OrderBy(x => x.Date)
            .ThenBy(x => x.StartTime)
            .Select(x => new
            {
                x.Id,
                x.EmployeeId,
                EmployeeName = x.Employee != null
                    ? x.Employee.Name
                    : null,
                x.Date,
                x.StartTime,
                x.EndTime
            })
            .ToListAsync();

        return Ok(shifts);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetShift(int id)
    {
        var shift = await _context.Shifts
            .Where(x => x.Id == id)
            .Select(x => new
            {
                x.Id,
                x.EmployeeId,
                EmployeeName = x.Employee != null
                    ? x.Employee.Name
                    : null,
                x.Date,
                x.StartTime,
                x.EndTime
            })
            .FirstOrDefaultAsync();

        if (shift == null)
            return NotFound();

        return Ok(shift);
    }

    [HttpPost]
    public async Task<IActionResult> CreateShift(
        [FromBody] CreateShiftRequest request)
    {
        var employeeExists = await _context.Employees
            .AnyAsync(x => x.Id == request.EmployeeId);

        if (!employeeExists)
        {
            return BadRequest("Belirtilen personel bulunamadı.");
        }

        if (!TimeSpan.TryParse(request.StartTime, out var startTime))
        {
            return BadRequest(
                "Başlangıç saati geçersiz. Örnek: 09:00"
            );
        }

        if (!TimeSpan.TryParse(request.EndTime, out var endTime))
        {
            return BadRequest(
                "Bitiş saati geçersiz. Örnek: 18:00"
            );
        }

        if (endTime <= startTime)
        {
            return BadRequest(
                "Vardiya bitiş saati başlangıç saatinden sonra olmalıdır."
            );
        }

        var shift = new Shift
        {
            EmployeeId = request.EmployeeId,
            Date = request.Date.Date,
            StartTime = startTime,
            EndTime = endTime
        };

        _context.Shifts.Add(shift);

        await _context.SaveChangesAsync();

        return CreatedAtAction(
            nameof(GetShift),
            new { id = shift.Id },
            new
            {
                shift.Id,
                shift.EmployeeId,
                shift.Date,
                shift.StartTime,
                shift.EndTime
            }
        );
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteShift(int id)
    {
        var shift = await _context.Shifts.FindAsync(id);

        if (shift == null)
            return NotFound();

        _context.Shifts.Remove(shift);

        await _context.SaveChangesAsync();

        return NoContent();
    }
}

public class CreateShiftRequest
{
    public int EmployeeId { get; set; }

    public DateTime Date { get; set; }

    public string StartTime { get; set; } = string.Empty;

    public string EndTime { get; set; } = string.Empty;
}