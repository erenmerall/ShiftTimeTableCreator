namespace ShiftTimeTable.API.Models;

public class SchedulingRule
{
    public int Id { get; set; }

    public int StoreId { get; set; }

    public Store? Store { get; set; }

    public int MaxContinuousWorkMinutes { get; set; }

    public int BreakDurationMinutes { get; set; }

    public int MaxConcurrentBreaks { get; set; }
}   