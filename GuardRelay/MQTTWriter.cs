using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Diagnostics;

namespace GuardRelay;

class MQTTOptions
{
    public MQTTConnectionOptions Connection { get; set; } = null!;
    public string DiscoveryPrefix { get; set; } = "homeassistant";
    public string BaseTopic { get; set; } = "GuardRelay";
    public string ObjectId { get; set; } = "ChargeAmpsGuard";
}
class MQTTConnectionOptions
{
    public string Server { get; set; } = null!;
    public int? Port { get; set; }
    public bool UseTls { get; set; }
    public string? ClientCertificate { get; set; }
    public string? ClientId { get; set; }
    public MQTTCredentialsOptions? Credentials { get; set; }
}

class MQTTCredentialsOptions
{
    public string Username { get; set; } = null!;
    public string Password { get; set; } = null!;
}

class MQTTLogger : IMqttNetLogger
{
    private readonly ILogger logger;

    public bool IsEnabled => true;

    public MQTTLogger(ILogger logger)
    {
        this.logger = logger;
    }

    public void Publish(MqttNetLogLevel logLevel, string source, string message, object[] parameters, Exception exception)
    {
        var level = logLevel switch
        {
            MqttNetLogLevel.Verbose => LogLevel.Debug,
            MqttNetLogLevel.Info => LogLevel.Information,
            MqttNetLogLevel.Warning => LogLevel.Warning,
            MqttNetLogLevel.Error => LogLevel.Error
        };
        if (exception != null)
        {
            logger.Log(level, exception, message, parameters);
        }
        else
        {
            logger.Log(level, message, parameters);
        }
    }
}

class MQTTWriter : IDisposable
{
    private readonly MqttFactory mqttFactory = new();
    private IMqttClient mqttClient;
    private readonly MqttClientOptions mqttClientOptions;
    private readonly IOptions<MQTTOptions> options;
    private readonly ILogger<ChargeAmpGuardClient> logger;

    public MQTTWriter(IOptions<MQTTOptions> options, ILogger<ChargeAmpGuardClient> logger)
    {
        this.options = options;
        this.logger = logger;
        mqttClientOptions = BuildMqttClientOptions(options.Value.Connection);
        mqttClient = mqttFactory.CreateMqttClient(new MQTTLogger(logger));
    }

    private static MqttClientOptions BuildMqttClientOptions(MQTTConnectionOptions connectionOptions)
    {
        var builder = new MqttClientOptionsBuilder();
        builder.WithTcpServer(connectionOptions.Server, connectionOptions.Port);
        builder.WithTlsOptions(builder =>
        {
            builder.UseTls(connectionOptions.UseTls);
            if (!string.IsNullOrEmpty(connectionOptions.ClientCertificate))
            {
                var bytes = File.ReadAllBytes(connectionOptions.ClientCertificate);
                var certificate = new X509Certificate2(bytes);
                builder.WithClientCertificates([certificate]);
            }
        });
        if (!string.IsNullOrEmpty(connectionOptions.ClientId))
        {
            builder.WithClientId(connectionOptions.ClientId);
        }
        if (connectionOptions.Credentials != null)
        {
            builder.WithCredentials(connectionOptions.Credentials.Username, connectionOptions.Credentials.Password);
        }
        return builder.Build();
    }

    public async Task ConnectAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Connecting to mqtt server");
        await mqttClient.ConnectAsync(mqttClientOptions, cancellationToken);
    }

    public async Task ConfigureDeviceAsync(CancellationToken cancellationToken)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var device = new DiscoveryDevice("Charge Amps Amp Guard", [options.Value.ObjectId]);
        var origin = new DiscoveryOrigin(assembly.GetName().Name, assembly.GetName().Version.ToString(), "https://github.com/AnderssonPeter/GuardRelay");
        await SendAsync($"{options.Value.DiscoveryPrefix}/sensor/{options.Value.ObjectId}/current_l1/config", new Discovery(device, "mdi:lightning-bolt-outline", "Current L1", $"{options.Value.ObjectId}_current_l1", $"{options.Value.ObjectId}_current_l1", "measurement", $"{options.Value.BaseTopic}", "A", "{{ value_json.currents.l1 }}", origin), true, cancellationToken);
        await SendAsync($"{options.Value.DiscoveryPrefix}/sensor/{options.Value.ObjectId}/current_l2/config", new Discovery(device, "mdi:lightning-bolt-outline", "Current L2", $"{options.Value.ObjectId}_current_l2", $"{options.Value.ObjectId}_current_l2", "measurement", $"{options.Value.BaseTopic}", "A", "{{ value_json.currents.l2 }}", origin), true, cancellationToken);
        await SendAsync($"{options.Value.DiscoveryPrefix}/sensor/{options.Value.ObjectId}/current_l3/config", new Discovery(device, "mdi:lightning-bolt-outline", "Current L3", $"{options.Value.ObjectId}_current_l3", $"{options.Value.ObjectId}_current_l3", "measurement", $"{options.Value.BaseTopic}", "A", "{{ value_json.currents.l3 }}", origin), true, cancellationToken);
        await SendAsync($"{options.Value.DiscoveryPrefix}/sensor/{options.Value.ObjectId}/current_total/config", new Discovery(device, "mdi:lightning-bolt-outline", "Current Total", $"{options.Value.ObjectId}_current_total", $"{options.Value.ObjectId}_current_total", "measurement", $"{options.Value.BaseTopic}", "A", "{{ value_json.currents.total }}", origin), true, cancellationToken);

        await SendAsync($"{options.Value.DiscoveryPrefix}/sensor/{options.Value.ObjectId}/voltage_l1/config", new Discovery(device, "mdi:sine-wave", "Voltage L1", $"{options.Value.ObjectId}_voltage_l1", $"{options.Value.ObjectId}_voltage_l1", "measurement", $"{options.Value.BaseTopic}", "V", "{{ value_json.voltages.l1 }}", origin), true, cancellationToken);
        await SendAsync($"{options.Value.DiscoveryPrefix}/sensor/{options.Value.ObjectId}/voltage_l2/config", new Discovery(device, "mdi:sine-wave", "Voltage L2", $"{options.Value.ObjectId}_voltage_l2", $"{options.Value.ObjectId}_voltage_l2", "measurement", $"{options.Value.BaseTopic}", "V", "{{ value_json.voltages.l2 }}", origin), true, cancellationToken);
        await SendAsync($"{options.Value.DiscoveryPrefix}/sensor/{options.Value.ObjectId}/voltage_l3/config", new Discovery(device, "mdi:sine-wave", "Voltage L3", $"{options.Value.ObjectId}_voltage_l3", $"{options.Value.ObjectId}_voltage_l3", "measurement", $"{options.Value.BaseTopic}", "V", "{{ value_json.voltages.l3 }}", origin), true, cancellationToken);
        await SendAsync($"{options.Value.DiscoveryPrefix}/sensor/{options.Value.ObjectId}/voltage_total/config", new Discovery(device, "mdi:sine-wave", "Voltage Total", $"{options.Value.ObjectId}_voltage_total", $"{options.Value.ObjectId}_voltage_total", "measurement", $"{options.Value.BaseTopic}", "V", "{{ value_json.voltages.total }}", origin), true, cancellationToken);

        await SendAsync($"{options.Value.DiscoveryPrefix}/sensor/{options.Value.ObjectId}/power_l1/config", new Discovery(device, "mdi:flash-outline", "Power L1", $"{options.Value.ObjectId}_power_l1", $"{options.Value.ObjectId}_power_l1", "measurement", $"{options.Value.BaseTopic}", "W", "{{ value_json.power.l1 }}", origin), true, cancellationToken);
        await SendAsync($"{options.Value.DiscoveryPrefix}/sensor/{options.Value.ObjectId}/power_l2/config", new Discovery(device, "mdi:flash-outline", "Power L2", $"{options.Value.ObjectId}_power_l2", $"{options.Value.ObjectId}_power_l2", "measurement", $"{options.Value.BaseTopic}", "W", "{{ value_json.power.l2 }}", origin), true, cancellationToken);
        await SendAsync($"{options.Value.DiscoveryPrefix}/sensor/{options.Value.ObjectId}/power_l3/config", new Discovery(device, "mdi:flash-outline", "Power L3", $"{options.Value.ObjectId}_power_l3", $"{options.Value.ObjectId}_power_l3", "measurement", $"{options.Value.BaseTopic}", "W", "{{ value_json.power.l3 }}", origin), true, cancellationToken);
        await SendAsync($"{options.Value.DiscoveryPrefix}/sensor/{options.Value.ObjectId}/power_total/config", new Discovery(device, "mdi:flash-outline", "Power Total", $"{options.Value.ObjectId}_power_total", $"{options.Value.ObjectId}_power_total", "measurement", $"{options.Value.BaseTopic}", "W", "{{ value_json.power.total }}", origin), true, cancellationToken);

        await SendAsync($"{options.Value.DiscoveryPrefix}/sensor/{options.Value.ObjectId}/energy_l1/config", new Discovery(device, "mdi:lightning-bolt", "Energy L1", $"{options.Value.ObjectId}_energy_l1", $"{options.Value.ObjectId}_energy_l1", "measurement", $"{options.Value.BaseTopic}", "kWh", "{{ value_json.energy.l1 }}", origin), true, cancellationToken);
        await SendAsync($"{options.Value.DiscoveryPrefix}/sensor/{options.Value.ObjectId}/energy_l2/config", new Discovery(device, "mdi:lightning-bolt", "Energy L2", $"{options.Value.ObjectId}_energy_l2", $"{options.Value.ObjectId}_energy_l2", "measurement", $"{options.Value.BaseTopic}", "kWh", "{{ value_json.energy.l2 }}", origin), true, cancellationToken);
        await SendAsync($"{options.Value.DiscoveryPrefix}/sensor/{options.Value.ObjectId}/energy_l3/config", new Discovery(device, "mdi:lightning-bolt", "Energy L3", $"{options.Value.ObjectId}_energy_l3", $"{options.Value.ObjectId}_energy_l3", "measurement", $"{options.Value.BaseTopic}", "kWh", "{{ value_json.energy.l3 }}", origin), true, cancellationToken);
        await SendAsync($"{options.Value.DiscoveryPrefix}/sensor/{options.Value.ObjectId}/energy_total/config", new Discovery(device, "mdi:lightning-bolt", "Energy Total", $"{options.Value.ObjectId}_energy_total", $"{options.Value.ObjectId}_energy_total", "measurement", $"{options.Value.BaseTopic}", "kWh", "{{ value_json.energy.total }}", origin), true, cancellationToken);

        await SendAsync($"{options.Value.DiscoveryPrefix}/sensor/{options.Value.ObjectId}/phase_angle_l1/config", new Discovery(device, "mdi:angle-acute", "Phase angle L1", $"{options.Value.ObjectId}_phase_angle_l1", $"{options.Value.ObjectId}_phase_angle_l1", "measurement", $"{options.Value.BaseTopic}", "deg", "{{ value_json.phase_angles.l1 }}", origin), true, cancellationToken);
        await SendAsync($"{options.Value.DiscoveryPrefix}/sensor/{options.Value.ObjectId}/phase_angle_l2/config", new Discovery(device, "mdi:angle-acute", "Phase angle L2", $"{options.Value.ObjectId}_phase_angle_l2", $"{options.Value.ObjectId}_phase_angle_l2", "measurement", $"{options.Value.BaseTopic}", "deg", "{{ value_json.phase_angles.l2 }}", origin), true, cancellationToken);
        await SendAsync($"{options.Value.DiscoveryPrefix}/sensor/{options.Value.ObjectId}/phase_angle_l3/config", new Discovery(device, "mdi:angle-acute", "Phase angle L3", $"{options.Value.ObjectId}_phase_angle_l3", $"{options.Value.ObjectId}_phase_angle_l3", "measurement", $"{options.Value.BaseTopic}", "deg", "{{ value_json.phase_angles.l3 }}", origin), true, cancellationToken);
    }

    public async Task SendValuesAsync(ChargeAmpGuardResponse changeAmpGuardResponse, double energy_l1, double energy_l2, double energy_l3, CancellationToken cancellationToken)
    {
        await SendAsync($"{options.Value.BaseTopic}", new Values(new WithTotal(changeAmpGuardResponse.Currents), new WithTotal(changeAmpGuardResponse.Voltages), new WithTotal(changeAmpGuardResponse.Power), new WithTotal([energy_l1, energy_l2, energy_l3]), new WithoutTotal(changeAmpGuardResponse.PhaseAngles)), false, cancellationToken);
    }

    private async Task SendAsync<T>(string topic, T value, bool retain, CancellationToken cancellationToken)
    {
        var content = JsonSerializer.Serialize(value, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });
        logger.LogTrace("SendAsync({topic}, {value}, {retain}", topic, content, retain);
        if (!mqttClient.IsConnected)
        {
            logger.LogWarning("Not connected");
            await ConnectAsync(cancellationToken);
        }
        await mqttClient.PublishAsync(new MqttApplicationMessage() { Topic = topic, Retain = retain, PayloadSegment = Encoding.UTF8.GetBytes(content) }, cancellationToken);
    }

    public void Dispose()
    {
        if (mqttClient != null)
        {
            mqttClient.Dispose();
            mqttClient = null!;
        }
    }

    private record Discovery(DiscoveryDevice Device,
        string Icon, string Name, string UniqueId, string ObjectId, string StateClass, string StateTopic, string UnitOfMeasurement, string ValueTemplate,
        DiscoveryOrigin Origin);
    private record DiscoveryAvailability(string Topic, string ValueTemplate);
    private record DiscoveryOrigin(string Name, string SWVersion, string Url);
    private record DiscoveryDevice(string Name, string[] Identifiers);

    private record WithTotal(double L1, double L2, double L3, double Total)
    {
        public WithTotal(double[] values) : this(Math.Round(values[0], 3), Math.Round(values[1], 3), Math.Round(values[2], 3), Math.Round(values[0] + values[1] + values[2], 3)) { }
    }

    private record WithoutTotal(double L1, double L2, double L3)
    {
        public WithoutTotal(double[] values) : this(Math.Round(values[0], 3), Math.Round(values[1], 3), Math.Round(values[2], 3)) { }
    }
    private record Values(WithTotal Currents, WithTotal Voltages, WithTotal Power, WithTotal Energy, WithoutTotal PhaseAngles);
}