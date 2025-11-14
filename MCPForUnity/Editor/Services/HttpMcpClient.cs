using System;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using MCPForUnity.Editor.Helpers;
using UnityEditor;

namespace MCPForUnity.Editor.Services
{
    /// <summary>
    /// Simple HTTP client for connecting to FastMCP server
    /// Uses REST API for tool execution
    /// </summary>
    public class HttpMcpClient : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;
        private bool _isConnected;
        private string _sessionId;
        private int _requestId = 0;

        public bool IsConnected => _isConnected;
        public string BaseUrl => _baseUrl;
        public string SessionId => _sessionId;

        public HttpMcpClient(string baseUrl)
        {
            if (string.IsNullOrEmpty(baseUrl))
            {
                throw new ArgumentException("Base URL cannot be null or empty", nameof(baseUrl));
            }

            _baseUrl = baseUrl.TrimEnd('/');
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(30)
            };
            _isConnected = false;
            _sessionId = null;
        }

        /// <summary>
        /// Initialize MCP session with the server
        /// Sends initialize request and stores session ID if provided
        /// </summary>
        public async Task<bool> InitializeAsync()
        {
            try
            {
                var initRequest = new
                {
                    jsonrpc = "2.0",
                    id = 1,
                    method = "initialize",
                    @params = new
                    {
                        protocolVersion = "2024-11-05",
                        capabilities = new { },
                        clientInfo = new
                        {
                            name = "unity-mcp-client",
                            version = "1.0.0"
                        }
                    }
                };

                string jsonContent = JsonConvert.SerializeObject(initRequest);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                // Add required headers
                var request = new HttpRequestMessage(HttpMethod.Post, _baseUrl)
                {
                    Content = content
                };
                request.Headers.Add("Accept", "application/json, text/event-stream");
                request.Headers.Add("MCP-Protocol-Version", "2024-11-05");

                // Add session ID if we have one
                if (!string.IsNullOrEmpty(_sessionId))
                {
                    request.Headers.Add("Mcp-Session-Id", _sessionId);
                }

                var response = await _httpClient.SendAsync(request);
                string responseText = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    // Check for session ID in response headers
                    if (response.Headers.TryGetValues("Mcp-Session-Id", out var sessionIds))
                    {
                        _sessionId = sessionIds.FirstOrDefault();
                        McpLog.Info($"MCP session initialized with ID: {_sessionId}");
                    }
                    else
                    {
                        McpLog.Info("MCP session initialized (no session ID provided by server)");
                    }

                    _isConnected = true;
                    return true;
                }
                else
                {
                    McpLog.Error($"MCP initialize failed: {response.StatusCode} - {responseText}");
                    _isConnected = false;
                    return false;
                }
            }
            catch (HttpRequestException ex)
            {
                McpLog.Error($"MCP initialize failed: {ex.Message}");
                _isConnected = false;
                return false;
            }
            catch (Exception ex)
            {
                McpLog.Error($"MCP initialize failed: {ex.Message}");
                _isConnected = false;
                return false;
            }
        }

        /// <summary>
        /// Send ping request to test MCP server health
        /// </summary>
        public async Task<bool> PingAsync()
        {
            try
            {
                var pingRequest = new
                {
                    jsonrpc = "2.0",
                    id = 2,
                    method = "ping"
                };

                string jsonContent = JsonConvert.SerializeObject(pingRequest);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                var request = new HttpRequestMessage(HttpMethod.Post, _baseUrl)
                {
                    Content = content
                };
                request.Headers.Add("Accept", "application/json, text/event-stream");
                request.Headers.Add("MCP-Protocol-Version", "2024-11-05");

                if (!string.IsNullOrEmpty(_sessionId))
                {
                    request.Headers.Add("Mcp-Session-Id", _sessionId);
                }

                var response = await _httpClient.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    McpLog.Info("MCP ping successful");
                    return true;
                }
                else
                {
                    string responseText = await response.Content.ReadAsStringAsync();
                    McpLog.Warn($"MCP ping failed: {response.StatusCode} - {responseText}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                McpLog.Error($"MCP ping failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// End the MCP session
        /// </summary>
        public async Task<bool> DisconnectAsync()
        {
            try
            {
                if (string.IsNullOrEmpty(_sessionId))
                {
                    // No session to end
                    _isConnected = false;
                    return true;
                }

                var request = new HttpRequestMessage(HttpMethod.Delete, _baseUrl);
                request.Headers.Add("Mcp-Session-Id", _sessionId);
                request.Headers.Add("MCP-Protocol-Version", "2024-11-05");

                var response = await _httpClient.SendAsync(request);

                if (response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.MethodNotAllowed)
                {
                    // 405 means server doesn't support explicit session termination, which is fine
                    McpLog.Info("MCP session ended");
                    _sessionId = null;
                    _isConnected = false;
                    return true;
                }
                else
                {
                    McpLog.Warn($"MCP session end returned {response.StatusCode}");
                    _sessionId = null;
                    _isConnected = false;
                    return true; // Still consider it ended
                }
            }
            catch (Exception ex)
            {
                McpLog.Error($"MCP disconnect failed: {ex.Message}");
                _sessionId = null;
                _isConnected = false;
                return false;
            }
        }

        /// <summary>
        /// Execute a tool on the MCP server via HTTP using JSON-RPC protocol
        /// </summary>
        public async Task<JObject> ExecuteToolAsync(string toolName, JObject parameters)
        {
            try
            {
                // Build MCP JSON-RPC request
                var mcpRequest = new
                {
                    jsonrpc = "2.0",
                    id = System.Threading.Interlocked.Increment(ref _requestId),
                    method = "tools/call",
                    @params = new
                    {
                        name = toolName,
                        arguments = parameters
                    }
                };

                string jsonContent = JsonConvert.SerializeObject(mcpRequest);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                // Create request with proper MCP headers
                var request = new HttpRequestMessage(HttpMethod.Post, _baseUrl)
                {
                    Content = content
                };
                request.Headers.Add("Accept", "application/json, text/event-stream");
                request.Headers.Add("MCP-Protocol-Version", "2024-11-05");

                if (!string.IsNullOrEmpty(_sessionId))
                {
                    request.Headers.Add("Mcp-Session-Id", _sessionId);
                }

                var response = await _httpClient.SendAsync(request);
                string responseText = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    // Parse JSON-RPC response
                    var jsonResponse = JObject.Parse(responseText);

                    // Check for JSON-RPC error
                    if (jsonResponse["error"] != null)
                    {
                        McpLog.Error($"MCP tool call returned error: {jsonResponse["error"]}");
                        return new JObject
                        {
                            ["success"] = false,
                            ["error"] = jsonResponse["error"].ToString()
                        };
                    }

                    // Return the result
                    return jsonResponse["result"] as JObject ?? jsonResponse;
                }
                else
                {
                    McpLog.Error($"HTTP tool execution failed: {response.StatusCode} - {responseText}");
                    return new JObject
                    {
                        ["success"] = false,
                        ["error"] = $"HTTP {response.StatusCode}: {responseText}"
                    };
                }
            }
            catch (Exception ex)
            {
                McpLog.Error($"HTTP tool execution exception: {ex.Message}");
                return new JObject
                {
                    ["success"] = false,
                    ["error"] = ex.Message
                };
            }
        }

        /// <summary>
        /// Send a command to Unity via the MCP server
        /// This is the main method for Unity plugin communication
        /// </summary>
        public async Task<string> SendCommandAsync(string commandJson)
        {
            try
            {
                // Parse the command to extract tool name and parameters
                var command = JObject.Parse(commandJson);
                string toolName = command["tool"]?.ToString();
                var parameters = command["parameters"] as JObject ?? new JObject();

                if (string.IsNullOrEmpty(toolName))
                {
                    return JsonConvert.SerializeObject(new { success = false, error = "Tool name is required" });
                }

                // Execute the tool
                var result = await ExecuteToolAsync(toolName, parameters);
                return result.ToString();
            }
            catch (Exception ex)
            {
                McpLog.Error($"HTTP send command exception: {ex.Message}");
                return JsonConvert.SerializeObject(new { success = false, error = ex.Message });
            }
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
            _isConnected = false;
        }
    }
}
