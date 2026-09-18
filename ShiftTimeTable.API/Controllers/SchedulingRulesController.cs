using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShiftTimeTable.API.Data;
using ShiftTimeTable.API.Models;

namespace ShiftTimeTable.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SchedulingRulesController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public SchedulingRulesController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet("{storeId}")]
    public async Task<IActionResult> GetRule(int storeId)
    {
        var rule = await _context.SchedulingRules
            .Where(x => x.StoreId == storeId)
            .Select(x => new
            {
                x.Id,
                x.StoreId,
                x.MaxContinuousWorkMinutes,
                x.BreakDurationMinutes,
                x.MaxConcurrentBreaks
            })
            .FirstOrDefaultAsync();

        if (rule == null)
            return NotFound();

        return Ok(rule);
    }

    [HttpPost]
    public async Task<IActionResult> CreateRule(
        [FromBody] CreateSchedulingRuleRequest request)
    {
        var storeExists = await _context.Stores
            .AnyAsync(x => x.Id == request.StoreId);

        if (!storeExists)
        {
            return BadRequest("Mağaza bulunamadı.");
        }

        var existingRule = await _context.SchedulingRules
            .AnyAsync(x => x.StoreId == request.StoreId);

        if (existingRule)
        {
            return BadRequest(
                "Bu mağaza için zaten bir vardiya kuralı tanımlanmış."
            );
        }

        if (request.MaxContinuousWorkMinutes <= 0)
        {
            return BadRequest(
                "Maksimum kesintisiz çalışma süresi 0'dan büyük olmalıdır."
            );
        }

        if (request.BreakDurationMinutes <= 0)
        {
            return BadRequest(
                "Mola süresi 0'dan büyük olmalıdır."
            );
        }

        if (request.MaxConcurrentBreaks <= 0)
        {
            return BadRequest(
                "Aynı anda verilebilecek maksimum mola sayısı 0'dan büyük olmalıdır."
            );
        }

        var rule = new SchedulingRule
        {
            StoreId = request.StoreId,
            MaxContinuousWorkMinutes = request.MaxContinuousWorkMinutes,
            BreakDurationMinutes = request.BreakDurationMinutes,
            MaxConcurrentBreaks = request.MaxConcurrentBreaks
        };

        _context.SchedulingRules.Add(rule);

        await _context.SaveChangesAsync();

        return CreatedAtAction(
            nameof(GetRule),
            new { storeId = rule.StoreId },
            new
            {
                rule.Id,
                rule.StoreId,
                rule.MaxContinuousWorkMinutes,
                rule.BreakDurationMinutes,
                rule.MaxConcurrentBreaks
            }
        );
    }
}

public class CreateSchedulingRuleRequest
{
    public int StoreId { get; set; }

    public int MaxContinuousWorkMinutes { get; set; }

    public int BreakDurationMinutes { get; set; }

    public int MaxConcurrentBreaks { get; set; }
}