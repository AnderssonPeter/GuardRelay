using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GuardRelay;

class ChargeAmpGuardClientOptions
{
    public Uri Uri { get; set; } = null!;
    public string Pin { get; set; } = null!;
}

enum ChargeAmpGuardClientState
{
    Disconnected,
    Connecting,
    Connected,
    Authenticating,
    Authenticated
}

public record ChargeAmpGuardResponse(double[] Currents, double[] Voltages, double[] Power, double[] PhaseAngles);

class ChargeAmpGuardClient : IDisposable
{
    const char fetchDataOp = '?';
    const string preAuthenticateRequest = "6";
    const string preAuthenticateResponse = "6";
    const char authenticateOp = '5';
    const string authenticateResponse = "1";

    private readonly ChargeAmpGuardClientOptions options;
    private readonly ILogger<ChargeAmpGuardClient> logger;
    private ClientWebSocket client = new ClientWebSocket();
    public ChargeAmpGuardClientState State { get; private set; } = ChargeAmpGuardClientState.Disconnected;

    public ChargeAmpGuardClient(IOptions<ChargeAmpGuardClientOptions> options, ILogger<ChargeAmpGuardClient> logger)
    {
        this.options = options.Value;
        this.logger = logger;
    }

    public async Task ConnectAsync(CancellationToken cancellationToken)
    {
        if (client != null && client.State == WebSocketState.Open)
        {
            await client.CloseAsync(WebSocketCloseStatus.NormalClosure, null, cancellationToken);
            client.Dispose();
        }
        logger.LogInformation("Connecting to ChargeAmpGuard");
        State = ChargeAmpGuardClientState.Disconnected;
        client = new ClientWebSocket();
        client.Options.KeepAliveInterval = TimeSpan.Zero;
        client.Options.DangerousDeflateOptions = new WebSocketDeflateOptions();
        State = ChargeAmpGuardClientState.Connecting;
        await client.ConnectAsync(options.Uri, cancellationToken);
        State = ChargeAmpGuardClientState.Connected;
        await PreAuthenticateAsync(cancellationToken);
        await AuthenticateAsync(cancellationToken);
    }

    private async Task PreAuthenticateAsync(CancellationToken cancellationToken)
    {
        logger.LogDebug("PreAuthenticateAsync");
        var response = await SendAndReceiveAsync(fetchDataOp, fetchDataOp, preAuthenticateRequest, cancellationToken);
        if (response != preAuthenticateResponse)
        {
            throw new InvalidOperationException("Invalid pre authentication response received");
        }
    }

    private async Task AuthenticateAsync(CancellationToken cancellationToken)
    {
        logger.LogDebug("AuthenticateAsync");
        State = ChargeAmpGuardClientState.Authenticating;
        var response = await SendAndReceiveAsync(authenticateOp, authenticateOp, options.Pin, cancellationToken);
        if (response != authenticateResponse)
        {
            throw new InvalidOperationException("Invalid pre authentication response received");
        }
        State = ChargeAmpGuardClientState.Authenticated;
    }

    public async Task<ChargeAmpGuardResponse> FetchDataAsync(CancellationToken cancellationToken)
    {
        logger.LogDebug("FetchDataAsync");
        if (client.State != WebSocketState.Open)
        {
            logger.LogWarning("Connection to ChageAmpsGuard expected open but was {state}, reconnecting!", client.State);
            await ConnectAsync(cancellationToken);
        }
        if (State != ChargeAmpGuardClientState.Authenticated)
        {
            throw new InvalidOperationException("Must be authenticated to fetch data");
        }
        var response = await SendAndReceiveAsync(fetchDataOp, '1', "1", cancellationToken);
        var parts = response.Split(',').Select(c => double.Parse(c, CultureInfo.InvariantCulture)).Chunk(3).ToArray();
        return new ChargeAmpGuardResponse(parts[0], parts[1], parts[2], parts[3]);
    }

    private async Task<string> SendAndReceiveAsync(char requestOp, char responseOp, string message, CancellationToken cancellationToken)
    {
        logger.LogTrace("Sending '{requestOp},{message}'", requestOp, message);
        await client.SendAsync(Encoding.ASCII.GetBytes(requestOp + "," + message), WebSocketMessageType.Text, true, cancellationToken);
        
        var receiveBuffer = new byte[1024];
        var offset = 0;

        logger.LogTrace("Receiving '{responseOp}'", responseOp);
        while (true)
        {
            var result = await client.ReceiveAsync(new ArraySegment<byte>(receiveBuffer, offset, receiveBuffer.Length - offset), cancellationToken);
            offset += result.Count;
            if (result.EndOfMessage)
            {
                break;
            }
        }
        var content = Encoding.ASCII.GetString(receiveBuffer, 0, offset);
        logger.LogTrace("Received '{content}'", content);
        if (!content.StartsWith(responseOp + ","))
        {
            throw new InvalidOperationException($"Invalid response op code was returned, expected {responseOp} but got {content[0]}");
        }
        return content[2..];
    }

    public void Dispose()
    {
        if (client != null)
        {
            client.Dispose();
            client = null!;
        }
    }
}

