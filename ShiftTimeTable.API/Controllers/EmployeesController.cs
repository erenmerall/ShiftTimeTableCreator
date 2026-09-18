using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShiftTimeTable.API.Data;
using ShiftTimeTable.API.Models;

namespace ShiftTimeTable.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EmployeesController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public EmployeesController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetEmployees()
    {
        var employees = await _context.Employees
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.StoreId,
                StoreName = x.Store != null ? x.Store.Name : null
            })
            .ToListAsync();

        return Ok(employees);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetEmployee(int id)
    {
        var employee = await _context.Employees
            .Where(x => x.Id == id)
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.StoreId,
                StoreName = x.Store != null ? x.Store.Name : null
            })
            .FirstOrDefaultAsync();

        if (employee == null)
            return NotFound();

        return Ok(employee);
    }

    [HttpPost]
    public async Task<IActionResult> CreateEmployee(
        [FromBody] CreateEmployeeRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest("Personel adı boş olamaz.");
        }

        var storeExists = await _context.Stores
            .AnyAsync(x => x.Id == request.StoreId);

        if (!storeExists)
        {
            return BadRequest("Belirtilen mağaza bulunamadı.");
        }

        var employee = new Employee
        {
            Name = request.Name.Trim(),
            StoreId = request.StoreId
        };

        _context.Employees.Add(employee);

        await _context.SaveChangesAsync();

        return CreatedAtAction(
            nameof(GetEmployee),
            new { id = employee.Id },
            new
            {
                employee.Id,
                employee.Name,
                employee.StoreId
            }
        );
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteEmployee(int id)
    {
        var employee = await _context.Employees.FindAsync(id);

        if (employee == null)
            return NotFound();

        _context.Employees.Remove(employee);

        await _context.SaveChangesAsync();

        return NoContent();
    }
}

public class CreateEmployeeRequest
{
    public string Name { get; set; } = string.Empty;

    public int StoreId { get; set; }
}