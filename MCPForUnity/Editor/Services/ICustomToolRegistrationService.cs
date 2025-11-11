using System.Threading.Tasks;

namespace MCPForUnity.Editor.Services
{
    /// <summary>
    /// Service for registering custom tools with MCP server via HTTP
    /// </summary>
    public interface ICustomToolRegistrationService
    {
        /// <summary>
        /// Registers all discovered tools with the MCP server
        /// </summary>
        Task<bool> RegisterAllToolsAsync();
        
        /// <summary>
        /// Registers all discovered tools with the MCP server (synchronous)
        /// </summary>
        bool RegisterAllTools();
    }
}
