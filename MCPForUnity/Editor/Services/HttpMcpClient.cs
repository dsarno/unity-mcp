using System;
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

        public bool IsConnected => _isConnected;
        public string BaseUrl => _baseUrl;

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
        }

        /// <summary>
        /// Test connection to the MCP server
        /// </summary>
        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                // Try to ping the server
                var response = await _httpClient.GetAsync($"{_baseUrl}/health");
                _isConnected = response.IsSuccessStatusCode;
                return _isConnected;
            }
            catch (Exception ex)
            {
                McpLog.Error($"HTTP connection test failed: {ex.Message}");
                _isConnected = false;
                return false;
            }
        }

        /// <summary>
        /// Execute a tool on the MCP server via HTTP
        /// </summary>
        public async Task<JObject> ExecuteToolAsync(string toolName, JObject parameters)
        {
            try
            {
                var requestBody = new
                {
                    name = toolName,
                    arguments = parameters
                };

                string jsonContent = JsonConvert.SerializeObject(requestBody);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"{_baseUrl}/tools/call", content);
                string responseText = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    return JObject.Parse(responseText);
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
