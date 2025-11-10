from typing import Dict, Any, List
import logging
from fastmcp import FastMCP, Context
from registry import mcp_for_unity_tool
from unity_connection import send_command_with_retry

logger = logging.getLogger("mcp-for-unity-server")


class CustomToolsManager:
    """Manages dynamic registration of custom tools from Unity projects"""

    def __init__(self, mcp_instance: FastMCP):
        self.mcp = mcp_instance
        self.registered_tools: Dict[str, Any] = {}

    def register_tools(self, project_id: str, tools: List[Dict[str, Any]]) -> Dict[str, Any]:
        """
        Register custom tools from Unity metadata

        Args:
            project_id: Unique identifier for the Unity project
            tools: List of tool definitions with metadata

        Returns:
            Result with registered tool names
        """
        try:
            registered_names = []

            for tool_def in tools:
                tool_name = tool_def.get('name')
                description = tool_def.get('description')
                parameters = tool_def.get('parameters', [])
                structured_output = tool_def.get('structured_output', True)

                if not tool_name:
                    logger.warning(f"Skipping tool with no name: {tool_def}")
                    continue

                # Remove old version if exists
                if f"{project_id}:{tool_name}" in self.registered_tools:
                    self._unregister_tool(tool_name)

                # Generate and register tool dynamically
                success = self._register_tool_from_metadata(
                    project_id, tool_name, description, parameters, structured_output
                )

                if success:
                    registered_names.append(tool_name)

            # Notify clients
            if registered_names:
                self._notify_tools_changed()

            return {
                "success": True,
                "registered": registered_names,
                "message": f"Registered {len(registered_names)} custom tools"
            }

        except Exception as e:
            logger.error(
                f"Failed to register custom tools: {e}", exc_info=True)
            return {
                "success": False,
                "error": str(e)
            }

    def _register_tool_from_metadata(self, project_id: str, tool_name: str,
                                     description: str, parameters: List[Dict],
                                     structured_output: bool) -> bool:
        """Dynamically create and register a tool from metadata"""
        try:
            # Build parameter annotations for FastMCP
            from typing import Annotated, Any

            # Create the tool function dynamically
            param_annotations = {}
            param_defaults = {}

            for param in parameters:
                param_name = param['name']
                param_desc = param['description']
                param_type = self._get_python_type(param['type'])
                required = param.get('required', True)
                default_val = param.get('default_value')

                # Build annotation
                if required:
                    param_annotations[param_name] = Annotated[param_type, param_desc]
                else:
                    param_annotations[param_name] = Annotated[param_type |
                                                              None, param_desc]
                    param_defaults[param_name] = None if default_val is None else default_val

            # Create the async function
            async def custom_tool_handler(ctx: Context, **kwargs) -> dict[str, Any]:
                """Dynamically generated tool handler"""
                await ctx.info(f"Executing custom tool: {tool_name}")

                # Forward to Unity via existing socket connection
                params = {k: v for k, v in kwargs.items() if v is not None}
                response = send_command_with_retry(tool_name, params)

                return response if isinstance(response, dict) else {
                    "success": False,
                    "message": str(response)
                }

            # Set function metadata
            custom_tool_handler.__name__ = tool_name
            custom_tool_handler.__annotations__ = {
                'ctx': Context,
                **param_annotations,
                'return': dict[str, Any]
            }

            # Apply defaults
            if param_defaults:
                custom_tool_handler.__defaults__ = tuple(
                    param_defaults.values())

            # Register with FastMCP using decorator
            decorated_func = mcp_for_unity_tool(
                description=description)(custom_tool_handler)

            # Store metadata
            self.registered_tools[f"{project_id}:{tool_name}"] = {
                "name": tool_name,
                "project_id": project_id,
                "description": description,
                "parameters": parameters
            }

            logger.info(
                f"Registered custom tool: {tool_name} from project {project_id}")
            return True

        except Exception as e:
            logger.error(
                f"Failed to register tool {tool_name}: {e}", exc_info=True)
            return False

    def _get_python_type(self, json_type: str):
        """Map JSON schema types to Python types"""
        type_map = {
            'string': str,
            'integer': int,
            'number': float,
            'boolean': bool,
            'array': list,
            'object': dict
        }
        return type_map.get(json_type, str)

    def _unregister_tool(self, tool_name: str):
        """Remove a tool from FastMCP"""
        try:
            self.mcp.remove_tool(tool_name)
            logger.info(f"Unregistered tool: {tool_name}")
        except Exception as e:
            logger.warning(f"Failed to unregister tool {tool_name}: {e}")

    def _notify_tools_changed(self):
        """Notify MCP clients that the tool list has changed"""
        logger.info("Tool list changed - clients should re-query tools/list")

    def unregister_project_tools(self, project_id: str) -> Dict[str, Any]:
        """Unregister all tools from a specific project"""
        try:
            tools_to_remove = []
            for key in self.registered_tools:
                if key.startswith(f"{project_id}:"):
                    tool_name = key.split(":", 1)[1]
                    tools_to_remove.append(tool_name)

            for tool_name in tools_to_remove:
                self._unregister_tool(tool_name)
                del self.registered_tools[f"{project_id}:{tool_name}"]

            return {
                "success": True,
                "unregistered": tools_to_remove,
                "message": f"Unregistered {len(tools_to_remove)} tools from project {project_id}"
            }

        except Exception as e:
            logger.error(
                f"Failed to unregister project tools: {e}", exc_info=True)
            return {
                "success": False,
                "error": str(e)
            }

    def get_registered_tools(self) -> Dict[str, Any]:
        """Get list of all registered tools"""
        return {
            "tools": list(self.registered_tools.values()),
            "count": len(self.registered_tools)
        }
