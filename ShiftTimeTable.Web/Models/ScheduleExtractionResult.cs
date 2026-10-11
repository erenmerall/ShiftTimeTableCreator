
namespace ShiftTimeTable.Web.Models;

public sealed class ScheduleExtractionResult
{
    public List<EmployeeSchedule> Employees { get; set; } = [];
}

public sealed class EmployeeSchedule
{
    public string Name { get; set; } = string.Empty;

    public List<EmployeeShift> Shifts { get; set; } = [];
}

public sealed class EmployeeShift
{
    public string Day { get; set; } = string.Empty;

    public string? StartTime { get; set; }

    public string? EndTime { get; set; }
}
