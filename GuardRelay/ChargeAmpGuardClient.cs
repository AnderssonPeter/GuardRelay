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
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
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
        if (client != null)
        {
            if (client.State == WebSocketState.Open)
            {
                try
                {
                    using var closeTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    closeTimeout.CancelAfter(options.Timeout);
                    await client.CloseAsync(WebSocketCloseStatus.NormalClosure, null, closeTimeout.Token);
                }
                catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
                {
                    logger.LogDebug(ex, "Failed to close old connection to ChargeAmpGuard");
                }
            }
            client.Dispose();
        }
        logger.LogInformation("Connecting to ChargeAmpGuard");
        State = ChargeAmpGuardClientState.Disconnected;
        client = new ClientWebSocket();
        client.Options.KeepAliveInterval = TimeSpan.Zero;
        client.Options.DangerousDeflateOptions = new WebSocketDeflateOptions();
        State = ChargeAmpGuardClientState.Connecting;
        using (var connectTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
        {
            connectTimeout.CancelAfter(options.Timeout);
            try
            {
                await client.ConnectAsync(options.Uri, connectTimeout.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException($"Timed out connecting to ChargeAmpGuard after {options.Timeout}");
            }
        }
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
        if (client.State != WebSocketState.Open || State != ChargeAmpGuardClientState.Authenticated)
        {
            logger.LogWarning("Connection to ChageAmpsGuard expected open and authenticated but was {socketState}/{state}, reconnecting!", client.State, State);
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
        // Without a timeout a request the Guard never answers would hang forever,
        // cancelling aborts the socket so the next fetch reconnects
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(options.Timeout);

        var receiveBuffer = new byte[1024];
        var offset = 0;
        try
        {
            logger.LogTrace("Sending '{requestOp},{message}'", requestOp, message);
            await client.SendAsync(Encoding.ASCII.GetBytes(requestOp + "," + message), WebSocketMessageType.Text, true, timeout.Token);

            logger.LogTrace("Receiving '{responseOp}'", responseOp);
            while (true)
            {
                var result = await client.ReceiveAsync(new ArraySegment<byte>(receiveBuffer, offset, receiveBuffer.Length - offset), timeout.Token);
                offset += result.Count;
                if (result.EndOfMessage)
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"No response from ChargeAmpGuard within {options.Timeout}");
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

