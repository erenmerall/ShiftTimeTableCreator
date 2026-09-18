namespace ShiftTimeTable.API.Models;

public class Break
{
    public int Id { get; set; }

    public int ShiftId { get; set; }

    public Shift? Shift { get; set; }

    public DateTime Date { get; set; }

    public TimeSpan StartTime { get; set; }

    public TimeSpan EndTime { get; set; }
}