from typing import Dict, Any, List
import logging
from fastmcp import FastMCP, Context
from pydantic import BaseModel
from custom_tools_manager import CustomToolsManager

logger = logging.getLogger("mcp-for-unity-server")

# Pydantic models for request/response


class ToolParameter(BaseModel):
    name: str
    description: str
    type: str
    required: bool = True
    default_value: str = None


class ToolDefinition(BaseModel):
    name: str
    description: str
    structured_output: bool = True
    parameters: List[ToolParameter] = []


class RegisterToolsRequest(BaseModel):
    project_id: str
    tools: List[ToolDefinition] = []


def register_tool_endpoints(mcp: FastMCP, custom_tools_manager: CustomToolsManager):
    """Register tool management endpoints with FastMCP"""

    @mcp.tool()
    async def register_custom_tools(project_id: str, tools: List[Dict[str, Any]]) -> Dict[str, Any]:
        """
        Register custom tools from Unity metadata

        Args:
            project_id: Unique identifier for the Unity project
            tools: List of tool definitions with metadata

        Returns:
            Result with registered tool names
        """
        try:
            logger.info(
                f"Received tool registration request for project {project_id}")

            result = custom_tools_manager.register_tools(project_id, tools)

            return result

        except Exception as e:
            logger.error(f"Error in register_custom_tools: {e}", exc_info=True)
            return {
                "success": False,
                "error": str(e)
            }

    @mcp.tool()
    async def get_registered_custom_tools() -> Dict[str, Any]:
        """Get list of all registered custom tools"""
        try:
            result = custom_tools_manager.get_registered_tools()
            return result

        except Exception as e:
            logger.error(
                f"Error in get_registered_custom_tools: {e}", exc_info=True)
            return {
                "success": False,
                "error": str(e),
                "tools": [],
                "count": 0
            }

    @mcp.tool()
    async def unregister_project_custom_tools(project_id: str) -> Dict[str, Any]:
        """Unregister all tools from a specific project"""
        try:
            result = custom_tools_manager.unregister_project_tools(project_id)
            return result

        except Exception as e:
            logger.error(
                f"Error in unregister_project_custom_tools: {e}", exc_info=True)
            return {
                "success": False,
                "error": str(e)
            }

    @mcp.tool()
    async def custom_tool_registry_health() -> Dict[str, Any]:
        """Health check for custom tool registry"""
        import time
        return {
            "status": "healthy",
            "timestamp": time.time(),
            "message": "Custom tool registry is operational"
        }

    logger.info("Registered custom tool management endpoints with FastMCP")
