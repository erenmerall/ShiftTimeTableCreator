using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShiftTimeTable.API.Data;

namespace ShiftTimeTable.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StoresController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public StoresController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetStores()
    {
        var stores = await _context.Stores
            .Select(x => new
            {
                x.Id,
                x.Name
            })
            .ToListAsync();

        return Ok(stores);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetStore(int id)
    {
        var store = await _context.Stores
            .Where(x => x.Id == id)
            .Select(x => new
            {
                x.Id,
                x.Name
            })
            .FirstOrDefaultAsync();

        if (store == null)
            return NotFound();

        return Ok(store);
    }

    [HttpPost]
    public async Task<IActionResult> CreateStore([FromBody] CreateStoreRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest("Mağaza adı boş olamaz.");
        }

        var store = new Models.Store
        {
            Name = request.Name.Trim()
        };

        _context.Stores.Add(store);

        await _context.SaveChangesAsync();

        return CreatedAtAction(
            nameof(GetStore),
            new { id = store.Id },
            new
            {
                store.Id,
                store.Name
            }
        );
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteStore(int id)
    {
        var store = await _context.Stores.FindAsync(id);

        if (store == null)
            return NotFound();

        _context.Stores.Remove(store);

        await _context.SaveChangesAsync();

        return NoContent();
    }
}

public class CreateStoreRequest
{
    public string Name { get; set; } = string.Empty;
}