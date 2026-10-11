using Microsoft.EntityFrameworkCore;
using ShiftTimeTable.API.Data;
using ShiftTimeTable.API.Models;

namespace ShiftTimeTable.API.Services;

public class BreakSchedulingService
{
    private readonly ApplicationDbContext _context;

    public BreakSchedulingService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<Break>> GenerateBreaksAsync(
        int storeId,
        DateTime date)
    {
        date = date.Date;

        var rule = await _context.SchedulingRules
            .FirstOrDefaultAsync(x => x.StoreId == storeId);

        if (rule == null)
        {
            throw new InvalidOperationException(
                "Bu mağaza için vardiya kuralı bulunamadı."
            );
        }

        var shifts = await _context.Shifts
            .Include(x => x.Employee)
            .Where(x =>
                x.Employee != null &&
                x.Employee.StoreId == storeId &&
                x.Date.Date == date)
            .OrderBy(x => x.StartTime)
            .ToListAsync();

        if (!shifts.Any())
        {
            throw new InvalidOperationException(
                "Bu tarih için vardiya bulunamadı."
            );
        }

        // Bu tarihte daha önce oluşturulmuş otomatik molaları temizle.
        var existingBreaks = await _context.Breaks
            .Where(x => x.Date.Date == date &&
                        x.Shift != null &&
                        x.Shift.Employee != null &&
                        x.Shift.Employee.StoreId == storeId)
            .ToListAsync();

        if (existingBreaks.Any())
        {
            _context.Breaks.RemoveRange(existingBreaks);
            await _context.SaveChangesAsync();
        }

        var generatedBreaks = new List<Break>();

        // Önce kısa vardiyaları, sonra uzun vardiyaları planlayacağız.
        // Her vardiyanın ilk mola ihtiyacı üzerinden sıralama yapılır.
        var shiftStates = shifts
            .Select(shift => new ShiftScheduleState
            {
                Shift = shift,
                NextBreakDue = shift.StartTime.Add(
                    TimeSpan.FromMinutes(
                        rule.MaxContinuousWorkMinutes
                    )
                )
            })
            .ToList();

        while (true)
        {
            var state = shiftStates
                .Where(x =>
                    x.NextBreakDue < x.Shift.EndTime)
                .OrderBy(x => x.NextBreakDue)
                .FirstOrDefault();

            if (state == null)
                break;

            var breakDuration = TimeSpan.FromMinutes(
                rule.BreakDurationMinutes
            );

            var breakStart = FindAvailableBreakTime(
                state,
                generatedBreaks,
                rule.MaxConcurrentBreaks,
                breakDuration
            );

            if (breakStart == null)
            {
                throw new InvalidOperationException(
                    $"{state.Shift.Employee?.Name} için " +
                    $"uygun mola zamanı bulunamadı. " +
                    $"Aynı anda verilebilecek mola sayısını artırmayı " +
                    $"veya vardiya düzenini kontrol etmeyi deneyin."
                );
            }

            var breakEnd = breakStart.Value.Add(breakDuration);

            var newBreak = new Break
            {
                ShiftId = state.Shift.Id,
                Date = state.Shift.Date.Date,
                StartTime = breakStart.Value,
                EndTime = breakEnd
            };

            generatedBreaks.Add(newBreak);

            // Bir sonraki mola için hesaplamaya devam et.
            state.NextBreakDue = breakEnd.Add(
                TimeSpan.FromMinutes(
                    rule.MaxContinuousWorkMinutes
                )
            );
        }

        if (generatedBreaks.Any())
        {
            _context.Breaks.AddRange(generatedBreaks);
            await _context.SaveChangesAsync();
        }

        return generatedBreaks;
    }

    private TimeSpan? FindAvailableBreakTime(
        ShiftScheduleState state,
        List<Break> generatedBreaks,
        int maxConcurrentBreaks,
        TimeSpan breakDuration)
    {
        var shift = state.Shift;

        /*
         * İlk tercih:
         * Personelin maksimum çalışma süresinin dolduğu nokta.
         *
         * Örneğin:
         * 09:00 + 5 saat = 14:00
         *
         * Eğer 14:00 müsaitse mola 14:00'te başlar.
         */

        var idealStart = state.NextBreakDue;

        if (CanPlaceBreak(
                shift,
                idealStart,
                breakDuration,
                generatedBreaks,
                maxConcurrentBreaks))
        {
            return idealStart;
        }

        /*
         * İdeal saat doluysa geriye doğru dakika dakika bakıyoruz.
         *
         * Böylece kişiyi 5 saatten fazla çalıştırmadan
         * daha erken bir mola vermeye çalışıyoruz.
         */

        var earliestAllowedStart =
            state.LastBreakEnd ?? shift.StartTime;

        var candidate = idealStart.Subtract(
            TimeSpan.FromMinutes(1)
        );

        while (candidate >= earliestAllowedStart)
        {
            if (CanPlaceBreak(
                    shift,
                    candidate,
                    breakDuration,
                    generatedBreaks,
                    maxConcurrentBreaks))
            {
                return candidate;
            }

            candidate = candidate.Subtract(
                TimeSpan.FromMinutes(1)
            );
        }

        /*
         * Geriye doğru uygun yer bulunamadıysa,
         * ideal saatten ileriye bakıyoruz.
         *
         * Bu durumda maksimum çalışma süresi aşılabilir.
         * O yüzden burada izin vermiyoruz.
         */

        return null;
    }

    private bool CanPlaceBreak(
        Shift shift,
        TimeSpan breakStart,
        TimeSpan breakDuration,
        List<Break> generatedBreaks,
        int maxConcurrentBreaks)
    {
        var breakEnd = breakStart.Add(breakDuration);

        // Mola vardiyanın dışına çıkamaz.
        if (breakStart < shift.StartTime)
            return false;

        if (breakEnd > shift.EndTime)
            return false;

        // Aynı personelin daha önceki molasıyla çakışamaz.
        var employeeBreaks = generatedBreaks
            .Where(x => x.ShiftId == shift.Id);

        foreach (var existingBreak in employeeBreaks)
        {
            if (IsOverlapping(
                    breakStart,
                    breakEnd,
                    existingBreak.StartTime,
                    existingBreak.EndTime))
            {
                return false;
            }
        }

        // Mağazadaki aynı anda mola veren kişi sayısını kontrol et.
        var concurrentBreaks = generatedBreaks.Count(x =>
            IsOverlapping(
                breakStart,
                breakEnd,
                x.StartTime,
                x.EndTime
            )
        );

        if (concurrentBreaks >= maxConcurrentBreaks)
            return false;

        return true;
    }

    private bool IsOverlapping(
        TimeSpan start1,
        TimeSpan end1,
        TimeSpan start2,
        TimeSpan end2)
    {
        return start1 < end2 && end1 > start2;
    }

    private class ShiftScheduleState
    {
        public Shift Shift { get; set; } = null!;

        public TimeSpan NextBreakDue { get; set; }

        public TimeSpan? LastBreakEnd { get; set; }
    }
}