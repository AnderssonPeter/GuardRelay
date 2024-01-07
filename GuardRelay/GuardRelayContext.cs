using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace GuardRelay;

[PrimaryKey(nameof(Timestamp))]
public record PowerSnapshot(DateTimeOffset Timestamp, double PowerLine1, double PowerLine2, double PowerLine3, TimeSpan Duration, double EnergyLine1, double EnergyLine2, double EnergyLine3);

public class GuardRelayContext : DbContext
{
    public DbSet<PowerSnapshot> PowerSnapshots { get; set; }

    public GuardRelayContext(DbContextOptions<GuardRelayContext> options) : base(options)
    {
        
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTimeOffset>().HaveConversion<DateTimeOffsetToBinaryConverter>();
        configurationBuilder.Properties<TimeSpan>().HaveConversion<TimeSpanToTicksConverter>();
    }
}