using System;
using System.Threading.Tasks;
using MCPForUnity.Editor.Helpers;

namespace MCPForUnity.Editor.Services.Transport.Transports
{
    /// <summary>
    /// Wraps HttpMcpClient in the unified transport interface.
    /// </summary>
    public class HttpTransportClient : IMcpTransportClient
    {
        private HttpMcpClient _client;
        private TransportState _state = TransportState.Disconnected("http");

        public bool IsConnected => _client?.IsConnected ?? false;
        public string TransportName => "http";
        public TransportState State => _state;

        public HttpMcpClient Client => _client;

        public async Task<bool> StartAsync()
        {
            await StopAsync();

            string rpcUrl = HttpEndpointUtility.GetMcpRpcUrl();
            McpLog.Info($"[HTTP] Connecting to MCP at {rpcUrl}");

            _client = new HttpMcpClient(rpcUrl);
            bool initialized = await _client.InitializeAsync();
            if (initialized)
            {
                _state = TransportState.Connected("http", sessionId: _client.SessionId, details: rpcUrl);
                return true;
            }

            _state = TransportState.Disconnected("http", "Initialization failed");
            await StopAsync();
            return false;
        }

        public async Task StopAsync()
        {
            if (_client == null)
            {
                _state = TransportState.Disconnected("http");
                return;
            }

            try
            {
                await _client.DisconnectAsync();
            }
            catch (Exception ex)
            {
                McpLog.Warn($"[HTTP] Error during disconnect: {ex.Message}");
            }
            finally
            {
                _client.Dispose();
                _client = null;
                _state = TransportState.Disconnected("http");
            }
        }

        public async Task<bool> VerifyAsync()
        {
            if (_client == null)
            {
                _state = TransportState.Disconnected("http", "Client not started");
                return false;
            }

            bool ok = await _client.PingAsync();
            _state = ok
                ? TransportState.Connected("http", sessionId: _client.SessionId, details: HttpEndpointUtility.GetMcpRpcUrl())
                : _state.WithError("Ping failed");
            return ok;
        }

        public async Task<string> SendCommandAsync(string commandJson)
        {
            if (_client == null)
            {
                _state = TransportState.Disconnected("http", "Client not started");
                throw new InvalidOperationException("HTTP transport is not started");
            }

            try
            {
                return await _client.SendCommandAsync(commandJson);
            }
            catch (Exception ex)
            {
                _state = _state.WithError(ex.Message);
                throw;
            }
        }
    }
}
