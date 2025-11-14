namespace MCPForUnity.Editor.Services
{
    /// <summary>
    /// Interface for server management operations
    /// </summary>
    public interface IServerManagementService
    {
        /// <summary>
        /// Start the local HTTP server in a new terminal window
        /// </summary>
        /// <returns>True if server was started successfully, false otherwise</returns>
        bool StartLocalHttpServer();

        /// <summary>
        /// Check if the configured HTTP URL is a local address
        /// </summary>
        /// <returns>True if URL is local (localhost, 127.0.0.1, etc.)</returns>
        bool IsLocalUrl();

        /// <summary>
        /// Check if the local HTTP server can be started
        /// </summary>
        /// <returns>True if HTTP transport is enabled and URL is local</returns>
        bool CanStartLocalServer();
    }
}
