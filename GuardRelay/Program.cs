using GuardRelay;
using Mcrio.Configuration.Provider.Docker.Secrets;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

Console.WriteLine("Starting application");
Console.WriteLine("Arguments:");
foreach(var arg in args)
{
    Console.WriteLine($"\t{arg}");
}

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddUserSecrets<Program>();
}
else
{
    builder.Configuration.AddDockerSecrets();
}
builder.Configuration.AddJsonFile("appsettings.json", false);
builder.Configuration.AddEnvironmentVariables();

builder.Services.AddOptions<ChargeAmpGuardClientOptions>().BindConfiguration("ChargeAmpGuard");
builder.Services.AddOptions<MQTTOptions>().BindConfiguration("MQTT");
builder.Services.AddOptions<ApplicationOptions>().BindConfiguration("Application");
builder.Services.AddSingleton<ChargeAmpGuardClient>();
builder.Services.AddSingleton<MQTTWriter>();
builder.Services.AddHostedService<Engine>();
builder.Services.AddDbContext<GuardRelayContext>((serviceProvider, options) => { 
    var applicationOptions = serviceProvider.GetRequiredService<IOptions<ApplicationOptions>>().Value;
    options.UseSqlite($"Data Source={applicationOptions.DatabaseLocation}");
});
using IHost host = builder.Build();
try
{
    await host.RunAsync();
}
catch(Exception ex)
{
    Console.WriteLine("Exception occurred");
    Console.WriteLine(ex.ToString());
    if (args.Contains("--pause-on-crash"))
    {
        Console.WriteLine("pause on crash detected, sleeping for 10 minutes");
        await Task.Delay(1000 * 60 * 10);
    }
}