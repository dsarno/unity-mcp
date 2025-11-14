
using System;
using System.Linq;
using System.Threading.Tasks;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Services.Transport.Transports;

namespace MCPForUnity.Editor.Services.Transport
{
    /// <summary>
    /// Coordinates the active transport client and exposes lifecycle helpers.
    /// </summary>
    public class TransportManager
    {
        private IMcpTransportClient _active;
        private TransportMode? _activeMode;
        private Func<IMcpTransportClient> _httpFactory;
        private Func<IMcpTransportClient> _hubFactory;
        private Func<IMcpTransportClient> _stdioFactory;
        private IMcpTransportClient _companion;

        public TransportManager()
        {
            Configure(
                () => new HttpTransportClient(),
                () => new WebSocketTransportClient(),
                () => new StdioTransportClient());
        }

        public IMcpTransportClient ActiveTransport => _active;
        public TransportMode? ActiveMode => _activeMode;

        public void Configure(
            Func<IMcpTransportClient> httpFactory,
            Func<IMcpTransportClient> hubFactory,
            Func<IMcpTransportClient> stdioFactory)
        {
            _httpFactory = httpFactory ?? throw new ArgumentNullException(nameof(httpFactory));
            _hubFactory = hubFactory ?? throw new ArgumentNullException(nameof(hubFactory));
            _stdioFactory = stdioFactory ?? throw new ArgumentNullException(nameof(stdioFactory));
        }

        public async Task<bool> StartAsync(TransportMode mode)
        {
            await StopAsync();

            IMcpTransportClient primary = mode switch
            {
                TransportMode.HttpPush => _hubFactory(),
                TransportMode.Stdio => _stdioFactory(),
                _ => _httpFactory()
            };

            if (primary == null)
            {
                throw new InvalidOperationException($"Factory returned null for transport mode {mode}");
            }

            IMcpTransportClient companion = null;
            if (mode == TransportMode.Http)
            {
                companion = _hubFactory?.Invoke();
                if (companion == null)
                {
                    McpLog.Warn("WebSocket transport factory returned null; continuing without push channel");
                }
            }

            bool started = await primary.StartAsync();
            if (!started)
            {
                await primary.StopAsync();
                _active = null;
                _activeMode = null;
                return false;
            }

            if (companion != null)
            {
                bool companionStarted = false;
                try
                {
                    companionStarted = await companion.StartAsync();
                }
                catch (Exception ex)
                {
                    McpLog.Warn($"Companion WebSocket transport failed to start: {ex.Message}");
                }

                if (!companionStarted)
                {
                    await primary.StopAsync();
                    _active = null;
                    _activeMode = null;
                    return false;
                }
            }

            _active = primary;
            _companion = companion;
            _activeMode = mode;
            return true;
        }

        public async Task StopAsync()
        {
            if (_active != null)
            {
                try
                {
                    await _active.StopAsync();
                }
                catch (Exception ex)
                {
                    McpLog.Warn($"Error while stopping transport {_active.TransportName}: {ex.Message}");
                }
                finally
                {
                    _active = null;
                    _activeMode = null;
                }
            }

            if (_companion != null)
            {
                try
                {
                    await _companion.StopAsync();
                }
                catch (Exception ex)
                {
                    McpLog.Warn($"Error while stopping companion transport {_companion.TransportName}: {ex.Message}");
                }
                finally
                {
                    _companion = null;
                }
            }
        }

        public async Task<bool> VerifyAsync()
        {
            if (_active == null)
            {
                return false;
            }
            bool primaryOk = await _active.VerifyAsync();
            if (!primaryOk)
            {
                return false;
            }

            if (_companion != null)
            {
                bool companionOk = await _companion.VerifyAsync();
                return primaryOk && companionOk;
            }

            return primaryOk;
        }

        public TransportState GetState()
        {
            if (_active == null)
            {
                return TransportState.Disconnected(_activeMode?.ToString()?.ToLowerInvariant() ?? "unknown", "Transport not started");
            }

            var primaryState = _active.State ?? TransportState.Disconnected(_active.TransportName, "No state reported");

            if (_companion == null)
            {
                return primaryState;
            }

            var companionState = _companion.State ?? TransportState.Disconnected(_companion.TransportName, "No state reported");

            if (!primaryState.IsConnected || !companionState.IsConnected)
            {
                string error = companionState.Error ?? primaryState.Error ?? "Transport disconnected";
                string name = $"{primaryState.TransportName}+{companionState.TransportName}";
                int? port = primaryState.Port;
                return TransportState.Disconnected(name, error, port);
            }

            string sessionId = companionState.SessionId ?? primaryState.SessionId;
            string details = string.Join(" | ", new[] { primaryState.Details, companionState.Details }.Where(s => !string.IsNullOrWhiteSpace(s)));
            return TransportState.Connected(
                $"{primaryState.TransportName}+{companionState.TransportName}",
                port: primaryState.Port,
                sessionId: sessionId,
                details: string.IsNullOrWhiteSpace(details) ? null : details);
        }

        public async Task<string> SendCommandAsync(string commandJson)
        {
            if (_active == null)
            {
                throw new InvalidOperationException("Transport not started");
            }

            return await _active.SendCommandAsync(commandJson);
        }
    }

    public enum TransportMode
    {
        Http,
        HttpPush,
        Stdio
    }
}
