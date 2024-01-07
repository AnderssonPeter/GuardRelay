using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GuardRelay;
internal class Engine : BackgroundService
{
    private readonly ChargeAmpGuardClient client;
    private readonly MQTTWriter mqttWriter;
    private readonly IServiceScope serviceScope;
    private GuardRelayContext guardRelayContext;
    private readonly ILogger<Engine> logger;
    private readonly ApplicationOptions options;

    public Engine(ChargeAmpGuardClient client, MQTTWriter mqttWriter, IServiceProvider serviceProvider, ILogger<Engine> logger, IOptions<ApplicationOptions> options)
    {
        this.client = client;
        this.mqttWriter = mqttWriter;
        this.serviceScope = serviceProvider.CreateScope();
        this.guardRelayContext = serviceScope.ServiceProvider.GetRequiredService<GuardRelayContext>();
        this.logger = logger;
        this.options = options.Value;

    }

    public override void Dispose()
    {
        serviceScope.Dispose();
        base.Dispose();
    }

    /// <summary>
    /// Return kWh
    /// </summary>
    /// <param name="lastPower">W</param>
    /// <param name="duration"></param>
    /// <returns></returns>
    private double CalculateEnergy(double lastPower, double currentPower, TimeSpan duration)
    {
        var partOfHour = duration / TimeSpan.FromHours(1);
        return (lastPower + currentPower) / 2 / 1000 * partOfHour;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Starting services");
        await guardRelayContext.Database.MigrateAsync(stoppingToken);


        await client.ConnectAsync(stoppingToken);
        await mqttWriter.ConnectAsync(stoppingToken);
        await mqttWriter.ConfigureDeviceAsync(stoppingToken);
        
        var lastDatabaseSnapshot = await guardRelayContext.PowerSnapshots
            .OrderBy(ps => ps.Timestamp)
            .LastOrDefaultAsync(stoppingToken);

        (double EnergyLine1, double EnergyLine2, double EnergyLine3) totalEnergy = 
            lastDatabaseSnapshot != null ? 
            (lastDatabaseSnapshot.EnergyLine1, lastDatabaseSnapshot.EnergyLine2, lastDatabaseSnapshot.EnergyLine3) : 
            (0d, 0d, 0d);

        (DateTimeOffset Timestamp, double PowerLine1, double PowerLine2, double PowerLine3)? lastSnapshot = 
            lastDatabaseSnapshot != null ? 
            (lastDatabaseSnapshot.Timestamp, lastDatabaseSnapshot.PowerLine1, lastDatabaseSnapshot.PowerLine2, lastDatabaseSnapshot.PowerLine3) : 
            null;

        while (!stoppingToken.IsCancellationRequested)
        {
            var watcher = Stopwatch.StartNew();
            var timestamp = DateTimeOffset.Now;
            var value = await client.FetchDataAsync(stoppingToken);
            var duration = lastSnapshot.HasValue ? (timestamp - lastSnapshot.Value.Timestamp) : TimeSpan.Zero;

            if (lastSnapshot != null && duration < options.FetchInterval * 10)
            {
                //calculate power based on last snapshot
                totalEnergy.EnergyLine1 += CalculateEnergy(lastSnapshot.Value.PowerLine1, value.Power[0], duration);
                totalEnergy.EnergyLine2 += CalculateEnergy(lastSnapshot.Value.PowerLine2, value.Power[1], duration);
                totalEnergy.EnergyLine3 += CalculateEnergy(lastSnapshot.Value.PowerLine3, value.Power[2], duration);

                //insert new value into database
                guardRelayContext.PowerSnapshots.Add(new PowerSnapshot(timestamp, lastSnapshot.Value.PowerLine1, lastSnapshot.Value.PowerLine2, lastSnapshot.Value.PowerLine3, duration, totalEnergy.EnergyLine1, totalEnergy.EnergyLine2, totalEnergy.EnergyLine3));
                await guardRelayContext.SaveChangesAsync(stoppingToken);

                //send values to MQTT
                await mqttWriter.SendValuesAsync(value, totalEnergy.EnergyLine1, totalEnergy.EnergyLine2, totalEnergy.EnergyLine3, stoppingToken);
            }

            if (lastSnapshot == null || duration >= options.FetchInterval * 10 || 
                lastSnapshot.Value.PowerLine1 != value.Power[0] || lastSnapshot.Value.PowerLine2 != value.Power[1] || lastSnapshot.Value.PowerLine3 != value.Power[2])
            {
                lastSnapshot = (timestamp, value.Power[0], value.Power[1], value.Power[2]);
            }
            
            watcher.Stop();
            if (watcher.Elapsed < options.FetchInterval)
            {
                await Task.Delay(options.FetchInterval - watcher.Elapsed);
            }
        }
    }
}
