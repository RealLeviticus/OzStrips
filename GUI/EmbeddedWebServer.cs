using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MaxRumsey.OzStripsPlugin.GUI.Shared;

namespace MaxRumsey.OzStripsPlugin.GUI;

/// <summary>
/// A lightweight HTTP + WebSocket server embedded in the plugin to serve the OzStrips web viewer.
/// </summary>
public sealed class EmbeddedWebServer : IDisposable
{
    private const int MaxHttpHeaderBytes = 16 * 1024;
    private const string WebSocketGuid = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
    private static readonly byte[] HttpHeaderDelimiter = [13, 10, 13, 10];

    private readonly CancellationTokenSource _cts = new();
    private readonly ConcurrentDictionary<Guid, ClientConnection> _clients = new();
    private readonly int _port;

    private TcpListener? _listener;
    private volatile bool _disposed;

    /// <summary>
    /// Raised when a WebSocket client sends a text message.
    /// </summary>
    public event Action<string>? OnClientMessage;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmbeddedWebServer"/> class.
    /// </summary>
    /// <param name="port">The port to listen on.</param>
    public EmbeddedWebServer(int port = 5199)
    {
        _port = port;
    }

    /// <summary>
    /// Starts the web server on a background thread.
    /// </summary>
    public void Start()
    {
        _listener = new TcpListener(IPAddress.Any, _port);
        _listener.Start();
        _ = Task.Run(AcceptLoop);
    }

    /// <summary>
    /// Broadcasts a full state snapshot to all connected WebSocket clients.
    /// </summary>
    /// <param name="state">The serializable state object.</param>
    public void BroadcastState(WebViewerState state)
    {
        if (_clients.IsEmpty)
        {
            return;
        }

        var json = JsonSerializer.Serialize(state);
        var payload = Encoding.UTF8.GetBytes(json);

        foreach (var kvp in _clients)
        {
            var id = kvp.Key;
            var connection = kvp.Value;
            if (connection.Closed)
            {
                RemoveClient(id, connection);
                continue;
            }

            _ = Task.Run(async () =>
            {
                try
                {
                    await SendFrameAsync(connection, 0x1, payload, CancellationToken.None).ConfigureAwait(false);
                }
                catch
                {
                    RemoveClient(id, connection);
                }
            });
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _cts.Cancel();

        var listener = _listener;
        _listener = null;

        try
        {
            listener?.Stop();
            listener?.Server.Close();
        }
        catch
        {
        }

        foreach (var kvp in _clients)
        {
            RemoveClient(kvp.Key, kvp.Value);
        }

        _clients.Clear();
        _cts.Dispose();
    }

    private async Task AcceptLoop()
    {
        while (!_disposed)
        {
            try
            {
                var listener = _listener;
                if (listener is null)
                {
                    break;
                }

                var client = await listener.AcceptTcpClientAsync().ConfigureAwait(false);
                _ = Task.Run(() => HandleTcpClient(client));
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (SocketException)
            {
                if (_disposed)
                {
                    break;
                }
            }
            catch (InvalidOperationException)
            {
                if (_disposed)
                {
                    break;
                }
            }
            catch
            {
                if (_disposed)
                {
                    break;
                }
            }
        }
    }

    private async Task HandleTcpClient(TcpClient client)
    {
        using (client)
        {
            NetworkStream stream;
            try
            {
                stream = client.GetStream();
            }
            catch
            {
                return;
            }

            var request = await ReadHttpRequestAsync(stream, _cts.Token).ConfigureAwait(false);
            if (request is null)
            {
                return;
            }

            if (IsWebSocketRequest(request))
            {
                await HandleWebSocketClient(client, stream, request).ConfigureAwait(false);
                return;
            }

            await ServeStaticContent(stream).ConfigureAwait(false);
        }
    }

    private async Task HandleWebSocketClient(TcpClient client, NetworkStream stream, HttpRequestData request)
    {
        var key = request.GetHeader("Sec-WebSocket-Key");
        if (string.IsNullOrWhiteSpace(key))
        {
            await WriteSimpleResponse(stream, 400, "Bad Request", "Missing Sec-WebSocket-Key").ConfigureAwait(false);
            return;
        }

        string response;
        try
        {
            response =
                "HTTP/1.1 101 Switching Protocols\r\n" +
                "Connection: Upgrade\r\n" +
                "Upgrade: websocket\r\n" +
                $"Sec-WebSocket-Accept: {ComputeWebSocketAcceptKey(key.Trim())}\r\n" +
                "\r\n";
            var bytes = Encoding.ASCII.GetBytes(response);
            await stream.WriteAsync(bytes, 0, bytes.Length, _cts.Token).ConfigureAwait(false);
        }
        catch
        {
            return;
        }

        var id = Guid.NewGuid();
        var connection = new ClientConnection(client, stream);
        _clients[id] = connection;

        try
        {
            await ReceiveWebSocketMessages(connection).ConfigureAwait(false);
        }
        finally
        {
            RemoveClient(id, connection);
        }
    }

    private async Task ReceiveWebSocketMessages(ClientConnection connection)
    {
        using var textMessage = new MemoryStream(1024);
        var assemblingText = false;

        try
        {
            while (!_disposed && !connection.Closed)
            {
                var frame = await ReadFrameAsync(connection, _cts.Token).ConfigureAwait(false);
                if (frame is null)
                {
                    break;
                }

                switch (frame.Opcode)
                {
                    case 0x8: // close
                        await SendFrameAsync(connection, 0x8, [], CancellationToken.None).ConfigureAwait(false);
                        return;

                    case 0x9: // ping
                        await SendFrameAsync(connection, 0xA, frame.Payload, CancellationToken.None).ConfigureAwait(false);
                        continue;

                    case 0x1: // text
                        textMessage.SetLength(0);
                        assemblingText = true;
                        if (frame.Payload.Length > 0)
                        {
                            textMessage.Write(frame.Payload, 0, frame.Payload.Length);
                        }

                        if (!frame.Fin)
                        {
                            continue;
                        }

                        if (textMessage.Length > 0)
                        {
                            try
                            {
                                OnClientMessage?.Invoke(Encoding.UTF8.GetString(textMessage.ToArray()));
                            }
                            catch
                            {
                            }
                        }

                        assemblingText = false;
                        continue;

                    case 0x0: // continuation
                        if (!assemblingText)
                        {
                            continue;
                        }

                        if (frame.Payload.Length > 0)
                        {
                            textMessage.Write(frame.Payload, 0, frame.Payload.Length);
                        }

                        if (frame.Fin)
                        {
                            if (textMessage.Length > 0)
                            {
                                try
                                {
                                    OnClientMessage?.Invoke(Encoding.UTF8.GetString(textMessage.ToArray()));
                                }
                                catch
                                {
                                }
                            }

                            assemblingText = false;
                        }

                        continue;
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
        }
    }

    private static async Task<HttpRequestData?> ReadHttpRequestAsync(NetworkStream stream, CancellationToken token)
    {
        var headerBytes = await ReadHttpHeaderBytesAsync(stream, token).ConfigureAwait(false);
        if (headerBytes is null || headerBytes.Length <= HttpHeaderDelimiter.Length)
        {
            return null;
        }

        var headerText = Encoding.ASCII.GetString(headerBytes, 0, headerBytes.Length - HttpHeaderDelimiter.Length);
        var lines = headerText.Split(["\r\n"], StringSplitOptions.None);
        if (lines.Length == 0)
        {
            return null;
        }

        var parts = lines[0].Split(' ');
        if (parts.Length < 2)
        {
            return null;
        }

        var method = parts[0];
        var path = parts[1];
        var queryIndex = path.IndexOf('?');
        if (queryIndex >= 0)
        {
            path = path.Substring(0, queryIndex);
        }

        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 1; i < lines.Length; i++)
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var sep = line.IndexOf(':');
            if (sep <= 0)
            {
                continue;
            }

            var key = line.Substring(0, sep).Trim();
            var value = line.Substring(sep + 1).Trim();
            headers[key] = value;
        }

        return new HttpRequestData(method, path, headers);
    }

    private static async Task<byte[]?> ReadHttpHeaderBytesAsync(NetworkStream stream, CancellationToken token)
    {
        var bytes = new List<byte>(1024);
        var one = new byte[1];
        var matched = 0;

        while (bytes.Count < MaxHttpHeaderBytes)
        {
            var read = await stream.ReadAsync(one, 0, 1, token).ConfigureAwait(false);
            if (read <= 0)
            {
                return null;
            }

            var current = one[0];
            bytes.Add(current);

            if (current == HttpHeaderDelimiter[matched])
            {
                matched++;
                if (matched == HttpHeaderDelimiter.Length)
                {
                    return bytes.ToArray();
                }
            }
            else
            {
                matched = current == HttpHeaderDelimiter[0] ? 1 : 0;
            }
        }

        return null;
    }

    private static bool IsWebSocketRequest(HttpRequestData request)
    {
        if (!string.Equals(request.Method, "GET", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(request.Path, "/ws", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var upgrade = request.GetHeader("Upgrade");
        var connection = request.GetHeader("Connection");

        return !string.IsNullOrWhiteSpace(upgrade) &&
               !string.IsNullOrWhiteSpace(connection) &&
               string.Equals(upgrade, "websocket", StringComparison.OrdinalIgnoreCase) &&
               connection.IndexOf("upgrade", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static string ComputeWebSocketAcceptKey(string key)
    {
        var bytes = Encoding.ASCII.GetBytes(key + WebSocketGuid);
        using var sha1 = SHA1.Create();
        return Convert.ToBase64String(sha1.ComputeHash(bytes));
    }

    private static async Task<WebSocketFrame?> ReadFrameAsync(ClientConnection connection, CancellationToken token)
    {
        var stream = connection.Stream;

        var header = await ReadExactAsync(stream, 2, token).ConfigureAwait(false);
        if (header is null)
        {
            return null;
        }

        var fin = (header[0] & 0x80) != 0;
        var opcode = header[0] & 0x0F;
        var masked = (header[1] & 0x80) != 0;
        ulong length = (byte)(header[1] & 0x7F);

        if (length == 126)
        {
            var ext = await ReadExactAsync(stream, 2, token).ConfigureAwait(false);
            if (ext is null)
            {
                return null;
            }

            length = (ulong)((ext[0] << 8) | ext[1]);
        }
        else if (length == 127)
        {
            var ext = await ReadExactAsync(stream, 8, token).ConfigureAwait(false);
            if (ext is null)
            {
                return null;
            }

            length =
                ((ulong)ext[0] << 56) |
                ((ulong)ext[1] << 48) |
                ((ulong)ext[2] << 40) |
                ((ulong)ext[3] << 32) |
                ((ulong)ext[4] << 24) |
                ((ulong)ext[5] << 16) |
                ((ulong)ext[6] << 8) |
                ext[7];
        }

        if (length > int.MaxValue)
        {
            return null;
        }

        byte[]? mask = null;
        if (masked)
        {
            mask = await ReadExactAsync(stream, 4, token).ConfigureAwait(false);
            if (mask is null)
            {
                return null;
            }
        }

        var payload = length == 0
            ? []
            : await ReadExactAsync(stream, (int)length, token).ConfigureAwait(false);
        if (payload is null)
        {
            return null;
        }

        if (masked && mask is not null)
        {
            for (var i = 0; i < payload.Length; i++)
            {
                payload[i] = (byte)(payload[i] ^ mask[i % 4]);
            }
        }

        return new WebSocketFrame(fin, opcode, payload);
    }

    private static async Task SendFrameAsync(ClientConnection connection, byte opcode, byte[] payload, CancellationToken token)
    {
        if (connection.Closed)
        {
            return;
        }

        await connection.SendLock.WaitAsync(token).ConfigureAwait(false);
        try
        {
            if (connection.Closed)
            {
                return;
            }

            var stream = connection.Stream;
            var header = new List<byte>(10)
            {
                (byte)(0x80 | (opcode & 0x0F)),
            };

            if (payload.Length <= 125)
            {
                header.Add((byte)payload.Length);
            }
            else if (payload.Length <= ushort.MaxValue)
            {
                header.Add(126);
                header.Add((byte)((payload.Length >> 8) & 0xFF));
                header.Add((byte)(payload.Length & 0xFF));
            }
            else
            {
                header.Add(127);
                var len = (ulong)payload.Length;
                header.Add((byte)((len >> 56) & 0xFF));
                header.Add((byte)((len >> 48) & 0xFF));
                header.Add((byte)((len >> 40) & 0xFF));
                header.Add((byte)((len >> 32) & 0xFF));
                header.Add((byte)((len >> 24) & 0xFF));
                header.Add((byte)((len >> 16) & 0xFF));
                header.Add((byte)((len >> 8) & 0xFF));
                header.Add((byte)(len & 0xFF));
            }

            var headerBytes = header.ToArray();
            await stream.WriteAsync(headerBytes, 0, headerBytes.Length, token).ConfigureAwait(false);

            if (payload.Length > 0)
            {
                await stream.WriteAsync(payload, 0, payload.Length, token).ConfigureAwait(false);
            }
        }
        finally
        {
            connection.SendLock.Release();
        }
    }

    private static async Task<byte[]?> ReadExactAsync(NetworkStream stream, int length, CancellationToken token)
    {
        var buffer = new byte[length];
        var offset = 0;
        while (offset < length)
        {
            var read = await stream.ReadAsync(buffer, offset, length - offset, token).ConfigureAwait(false);
            if (read <= 0)
            {
                return null;
            }

            offset += read;
        }

        return buffer;
    }

    private static async Task ServeStaticContent(NetworkStream stream)
    {
        try
        {
            var html = Encoding.UTF8.GetBytes(WebViewerHtml.Html);
            var header =
                "HTTP/1.1 200 OK\r\n" +
                "Content-Type: text/html; charset=utf-8\r\n" +
                $"Content-Length: {html.Length}\r\n" +
                "Connection: close\r\n" +
                "\r\n";

            var headerBytes = Encoding.ASCII.GetBytes(header);
            await stream.WriteAsync(headerBytes, 0, headerBytes.Length).ConfigureAwait(false);
            await stream.WriteAsync(html, 0, html.Length).ConfigureAwait(false);
        }
        catch
        {
        }
    }

    private static async Task WriteSimpleResponse(NetworkStream stream, int code, string status, string body)
    {
        try
        {
            var bodyBytes = Encoding.UTF8.GetBytes(body);
            var header =
                $"HTTP/1.1 {code} {status}\r\n" +
                "Content-Type: text/plain; charset=utf-8\r\n" +
                $"Content-Length: {bodyBytes.Length}\r\n" +
                "Connection: close\r\n" +
                "\r\n";

            var headerBytes = Encoding.ASCII.GetBytes(header);
            await stream.WriteAsync(headerBytes, 0, headerBytes.Length).ConfigureAwait(false);
            await stream.WriteAsync(bodyBytes, 0, bodyBytes.Length).ConfigureAwait(false);
        }
        catch
        {
        }
    }

    private void RemoveClient(Guid id, ClientConnection connection)
    {
        _clients.TryRemove(id, out _);
        connection.Closed = true;

        try
        {
            connection.SendLock.Dispose();
        }
        catch
        {
        }

        try
        {
            connection.Stream.Close();
            connection.Stream.Dispose();
        }
        catch
        {
        }

        try
        {
            connection.TcpClient.Close();
            connection.TcpClient.Dispose();
        }
        catch
        {
        }
    }

    private sealed class ClientConnection(TcpClient tcpClient, NetworkStream stream)
    {
        public TcpClient TcpClient { get; } = tcpClient;

        public NetworkStream Stream { get; } = stream;

        public SemaphoreSlim SendLock { get; } = new(1, 1);

        public bool Closed { get; set; }
    }

    private sealed class WebSocketFrame(bool fin, int opcode, byte[] payload)
    {
        public bool Fin { get; } = fin;

        public int Opcode { get; } = opcode;

        public byte[] Payload { get; } = payload;
    }

    private sealed class HttpRequestData(string method, string path, Dictionary<string, string> headers)
    {
        public string Method { get; } = method;

        public string Path { get; } = path;

        public Dictionary<string, string> Headers { get; } = headers;

        public string? GetHeader(string key)
        {
            return Headers.TryGetValue(key, out var value) ? value : null;
        }
    }
}

/// <summary>
/// Represents the full state snapshot sent to web clients.
/// </summary>
public class WebViewerState
{
    /// <summary>
    /// Gets or sets the aerodrome code.
    /// </summary>
    public string Aerodrome { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the ATIS code.
    /// </summary>
    public string Atis { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the METAR string.
    /// </summary>
    public string Metar { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the current UTC time.
    /// </summary>
    public string UtcTime { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether connected to the OzStrips server.
    /// </summary>
    public bool Connected { get; set; }

    /// <summary>
    /// Gets or sets the number of connected controllers on this aerodrome.
    /// </summary>
    public int ConnectionsCount { get; set; }

    /// <summary>
    /// Gets or sets the number of pending PDC requests.
    /// </summary>
    public int PendingPDCs { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether autofill is available.
    /// </summary>
    public bool AutoFillAvailable { get; set; }

    /// <summary>
    /// Gets or sets the strip render scale (matches desktop StripScale setting).
    /// </summary>
    public float StripScale { get; set; } = 1f;

    /// <summary>
    /// Gets or sets available runway pairs for crossing/release actions.
    /// </summary>
    public List<string> RunwayPairs { get; set; } = new();

    /// <summary>
    /// Gets or sets smart resize mode (0-3).
    /// </summary>
    public int SmartResizeMode { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether circuit bay mode is active.
    /// </summary>
    public bool CircuitActive { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether coordinator bay mode is active.
    /// </summary>
    public bool CoordinatorActive { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether CDM mode is active.
    /// </summary>
    public bool CdmEnabled { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether PDC sounds are enabled.
    /// </summary>
    public bool PdcSoundEnabled { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether ground map updates are inhibited.
    /// </summary>
    public bool GroundMapsInhibited { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether circuit bay toggling is currently available.
    /// </summary>
    public bool CircuitToggleAvailable { get; set; }

    /// <summary>
    /// Gets or sets the list of available layout/view mode names.
    /// </summary>
    public List<string> Layouts { get; set; } = new();

    /// <summary>
    /// Gets or sets the currently active layout/view mode name.
    /// </summary>
    public string CurrentLayout { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the bays with their ordered items.
    /// </summary>
    public List<WebViewerBay> Bays { get; set; } = new();
}

/// <summary>
/// A bay and its display items for the web viewer.
/// </summary>
public class WebViewerBay
{
    /// <summary>
    /// Gets or sets the bay type.
    /// </summary>
    public StripBay Bay { get; set; }

    /// <summary>
    /// Gets or sets the bay display name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the visual column index used by the desktop layout.
    /// </summary>
    public int Column { get; set; }

    /// <summary>
    /// Gets or sets the ordered items shown in the bay (strip/bar/queue bar).
    /// </summary>
    public List<WebViewerBayItem> Items { get; set; } = new();

    /// <summary>
    /// Gets or sets strips (legacy payload compatibility).
    /// </summary>
    public List<WebViewerStrip> Strips { get; set; } = new();

    /// <summary>
    /// Gets or sets queue bar position index (-1 if none, legacy compatibility).
    /// </summary>
    public int QueueBarIndex { get; set; } = -1;
}

/// <summary>
/// A renderable item within a bay.
/// </summary>
public class WebViewerBayItem
{
    /// <summary>
    /// Gets or sets item type: STRIP, BAR, or QUEUEBAR.
    /// </summary>
    public string ItemType { get; set; } = "STRIP";

    /// <summary>
    /// Gets or sets the text for bar items.
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets bar style (0-3) for bar items.
    /// </summary>
    public int Style { get; set; }

    /// <summary>
    /// Gets or sets strip payload for strip items.
    /// </summary>
    public WebViewerStrip? Strip { get; set; }
}

/// <summary>
/// A strip representation for the web viewer.
/// </summary>
public class WebViewerStrip
{
    /// <summary>Gets or sets the callsign.</summary>
    public string Callsign { get; set; } = string.Empty;

    /// <summary>Gets or sets the clearance.</summary>
    public string CLX { get; set; } = string.Empty;

    /// <summary>Gets or sets the gate/stand.</summary>
    public string Gate { get; set; } = string.Empty;

    /// <summary>Gets or sets the remark.</summary>
    public string Remark { get; set; } = string.Empty;

    /// <summary>Gets or sets the departure frequency.</summary>
    public string DepartureFreq { get; set; } = string.Empty;

    /// <summary>Gets or sets the runway.</summary>
    public string Runway { get; set; } = string.Empty;

    /// <summary>Gets or sets the cock level (0-3).</summary>
    public int CockLevel { get; set; }

    /// <summary>Gets or sets the bay the strip is in.</summary>
    public StripBay Bay { get; set; }

    /// <summary>Gets or sets the strip type (departure/arrival/local).</summary>
    public string StripType { get; set; } = string.Empty;

    /// <summary>Gets or sets the EOBT/time.</summary>
    public string Time { get; set; } = string.Empty;

    /// <summary>Gets or sets the aircraft type.</summary>
    public string AircraftType { get; set; } = string.Empty;

    /// <summary>Gets or sets the wake turbulence category.</summary>
    public string WTC { get; set; } = string.Empty;

    /// <summary>Gets or sets the SSR code.</summary>
    public string SSR { get; set; } = string.Empty;

    /// <summary>Gets or sets the destination aerodrome.</summary>
    public string ADES { get; set; } = string.Empty;

    /// <summary>Gets or sets the SID.</summary>
    public string SID { get; set; } = string.Empty;

    /// <summary>Gets or sets the first waypoint.</summary>
    public string FirstWpt { get; set; } = string.Empty;

    /// <summary>Gets or sets the requested flight level.</summary>
    public string RFL { get; set; } = string.Empty;

    /// <summary>Gets or sets the cleared flight level.</summary>
    public string CFL { get; set; } = string.Empty;

    /// <summary>Gets or sets the global ops data.</summary>
    public string GLOP { get; set; } = string.Empty;

    /// <summary>Gets or sets the flight rules.</summary>
    public string FlightRules { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether ready.</summary>
    public bool Ready { get; set; }

    /// <summary>Gets or sets the route indicator (T/R/empty).</summary>
    public string RouteIndicator { get; set; } = string.Empty;

    /// <summary>Gets or sets the allocated bay.</summary>
    public string AllocatedBay { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether crossing highlight is enabled.</summary>
    public bool Crossing { get; set; }

    /// <summary>Gets or sets the HDG from GLOP.</summary>
    public string HDG { get; set; } = string.Empty;

    /// <summary>Gets or sets the takeoff timer text.</summary>
    public string Tot { get; set; } = "00:00";

    /// <summary>Gets or sets the PDC indicator text.</summary>
    public string PDCIndicator { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether there is a pending PDC not yet sent.</summary>
    public bool PdcRequested { get; set; }

    /// <summary>Gets or sets a value indicating whether pending PDC is unacknowledged.</summary>
    public bool PdcNeedsAck { get; set; }

    /// <summary>Gets or sets a value indicating whether this SID has a transition.</summary>
    public bool SidTransition { get; set; }

    /// <summary>Gets or sets a value indicating route alert state.</summary>
    public bool RouteAlert { get; set; }

    /// <summary>Gets or sets a value indicating RFL alert state.</summary>
    public bool RflAlert { get; set; }

    /// <summary>Gets or sets a value indicating SSR alert state.</summary>
    public bool SsrAlert { get; set; }

    /// <summary>Gets or sets a value indicating ready alert state.</summary>
    public bool ReadyAlert { get; set; }

    /// <summary>Gets or sets a value indicating no-heading alert state.</summary>
    public bool NoHdgAlert { get; set; }

    /// <summary>Gets or sets a value indicating VFR SID alert state.</summary>
    public bool VfrSidAlert { get; set; }

    /// <summary>Gets or sets a value indicating the CDM time cell should use CDM background coloring.</summary>
    public bool CdmActive { get; set; }

    /// <summary>Gets or sets a value indicating aircraft is ready to push per CDM.</summary>
    public bool CdmReadyToPush { get; set; }

    /// <summary>Gets or sets a value indicating aircraft currently has a slot allocation.</summary>
    public bool CdmHasSlot { get; set; }

    /// <summary>Gets or sets a value indicating stand came from allocated bay autofill.</summary>
    public bool StandAutofilled { get; set; }

    /// <summary>Gets or sets a value indicating this is the desktop-picked strip.</summary>
    public bool Picked { get; set; }

    /// <summary>Gets or sets a value indicating this strip is the last transmitter.</summary>
    public bool LastTransmit { get; set; }

    /// <summary>Gets or sets a value indicating this strip belongs to a world-flight team.</summary>
    public bool WorldFlight { get; set; }
}
