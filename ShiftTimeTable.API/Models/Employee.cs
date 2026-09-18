namespace ShiftTimeTable.API.Models;

public class Employee
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public int StoreId { get; set; }

    public Store? Store { get; set; }

    public ICollection<Shift> Shifts { get; set; } = new List<Shift>();
}