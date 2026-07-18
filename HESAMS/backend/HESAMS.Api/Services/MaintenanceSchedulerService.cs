using HESAMS.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace HESAMS.Api.Services;

/// <summary>
/// Background job (runs hourly) that:
///  1. Sends T-30 / T-14 / T-7 / T-1 / due-today / overdue reminders
///  2. Marks overdue maintenance orders
///  3. Applies the hard maintenance interlock (MaintenanceLocked = 1)
///     when a machine passes its due date or its operating-hours target.
///     Only maintenance.unlock permission can clear it (EquipmentController).
/// </summary>
public class MaintenanceSchedulerService : BackgroundService
{
    private static readonly int[] ReminderOffsets = { 30, 14, 7, 1, 0 };
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MaintenanceSchedulerService> _log;

    public MaintenanceSchedulerService(IServiceScopeFactory scopeFactory, ILogger<MaintenanceSchedulerService> log)
    { _scopeFactory = scopeFactory; _log = log; }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try { await RunOnceAsync(ct); }
            catch (Exception ex) { _log.LogError(ex, "Maintenance scheduler cycle failed"); }
            await Task.Delay(TimeSpan.FromHours(1), ct);
        }
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HesamsDbContext>();
        var notify = scope.ServiceProvider.GetRequiredService<INotificationService>();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var fleet = await db.Equipment.Where(e => e.NextMaintenanceDate != null || e.NextMaintenanceHours != null)
                                      .ToListAsync(ct);
        foreach (var eq in fleet)
        {
            int? daysLeft = eq.NextMaintenanceDate is null
                ? null : eq.NextMaintenanceDate.Value.DayNumber - today.DayNumber;
            bool hoursReached = eq.NextMaintenanceHours is not null && eq.OperatingHours >= eq.NextMaintenanceHours;

            // Reminders at fixed offsets
            if (daysLeft is not null && ReminderOffsets.Contains(daysLeft.Value))
                await notify.QueueAsync(null, "InApp",
                    daysLeft == 0 ? $"Maintenance due today: {eq.EquipmentName}"
                                  : $"Maintenance in {daysLeft}d: {eq.EquipmentName}",
                    $"Scheduled {eq.NextMaintenanceDate}. Current hours: {eq.OperatingHours}.",
                    "Equipment", eq.EquipmentId, ct);

            // Overdue or hour-threshold reached -> hard interlock
            if (((daysLeft is < 0) || hoursReached) && !eq.MaintenanceLocked)
            {
                eq.MaintenanceLocked = true;
                await notify.QueueAsync(null, "InApp",
                    $"EQUIPMENT LOCKED: {eq.EquipmentName}",
                    hoursReached ? "Operating-hours maintenance threshold reached."
                                 : "Maintenance overdue. Machine locked until completion.",
                    "Equipment", eq.EquipmentId, ct);
            }
        }

        // Mark overdue orders
        await db.MaintenanceOrders
            .Where(m => m.Status == "Scheduled" && m.ScheduledDate < today)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.Status, "Overdue"), ct);

        await db.SaveChangesAsync(ct);
    }
}
