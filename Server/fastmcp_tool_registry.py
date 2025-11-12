from typing import Dict, Any, List
import logging
from fastapi import APIRouter, HTTPException
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
    """Register MCP tools and HTTP endpoints for custom tool management."""

    router = _configure_http_router(mcp)
    if router is not None:
        _register_http_routes(router, custom_tools_manager)
    else:
        logger.warning(
            "HTTP router unavailable; custom tool HTTP endpoints disabled")

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

    logger.info("Registered custom tool management endpoints (MCP + HTTP)")


def _configure_http_router(mcp: FastMCP) -> APIRouter | None:
    """Ensure there is an HTTP router attached to the FastMCP instance."""
    try:
        router = getattr(mcp, "http_router", None)
        if router is not None:
            return router

        app = None
        for attr in ("http_app", "app", "fastapi_app"):
            app = getattr(mcp, attr, None)
            if app is not None and hasattr(app, "include_router"):
                break

        if app is None or not hasattr(app, "include_router"):
            return None

        router = APIRouter()
        app.include_router(router)
        setattr(mcp, "http_router", router)
        return router
    except Exception as exc:
        logger.warning("Failed to configure HTTP router: %s", exc)
        return None


def _register_http_routes(router: APIRouter, custom_tools_manager: CustomToolsManager) -> None:
    """Register FastAPI routes for tool registration."""

    @router.post("/register-tools")
    async def register_tools_endpoint(payload: RegisterToolsRequest):
        logger.info("Received HTTP tool registration for project %s",
                    payload.project_id)
        result = custom_tools_manager.register_tools(
            payload.project_id,
            [tool.model_dump(exclude_none=True) for tool in payload.tools]
        )

        if not result.get("success"):
            raise HTTPException(status_code=400, detail=result)
        return result

    @router.get("/tools")
    async def list_tools_endpoint():
        return custom_tools_manager.get_registered_tools()

    @router.delete("/tools/{project_id}")
    async def unregister_project_tools_endpoint(project_id: str):
        result = custom_tools_manager.unregister_project_tools(project_id)
        if not result.get("success"):
            raise HTTPException(status_code=400, detail=result)
        return result

    @router.get("/health")
    async def health_endpoint():
        import time
        return {
            "status": "healthy",
            "timestamp": time.time(),
            "message": "Custom tool registry is operational"
        }
