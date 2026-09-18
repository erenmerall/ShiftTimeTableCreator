using Microsoft.EntityFrameworkCore;
using ShiftTimeTable.API.Models;

namespace ShiftTimeTable.API.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Store> Stores => Set<Store>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<Shift> Shifts => Set<Shift>();
    public DbSet<SchedulingRule> SchedulingRules => Set<SchedulingRule>();
    public DbSet<Break> Breaks => Set<Break>();
}