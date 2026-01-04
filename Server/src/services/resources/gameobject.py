"""
MCP Resources for reading GameObject data from Unity scenes.

These resources provide read-only access to:
- Single GameObject data (unity://scene/gameobject/{id})
- All components on a GameObject (unity://scene/gameobject/{id}/components)
- Single component on a GameObject (unity://scene/gameobject/{id}/component/{name})
"""
from typing import Any
from pydantic import BaseModel
from fastmcp import Context

from models import MCPResponse
from services.registry import mcp_for_unity_resource
from services.tools import get_unity_instance_from_context
from transport.unity_transport import send_with_unity_instance
from transport.legacy.unity_connection import async_send_command_with_retry


class TransformData(BaseModel):
    """Transform component data."""
    position: dict[str, float] = {"x": 0.0, "y": 0.0, "z": 0.0}
    localPosition: dict[str, float] = {"x": 0.0, "y": 0.0, "z": 0.0}
    rotation: dict[str, float] = {"x": 0.0, "y": 0.0, "z": 0.0}
    localRotation: dict[str, float] = {"x": 0.0, "y": 0.0, "z": 0.0}
    scale: dict[str, float] = {"x": 1.0, "y": 1.0, "z": 1.0}
    lossyScale: dict[str, float] = {"x": 1.0, "y": 1.0, "z": 1.0}


class GameObjectData(BaseModel):
    """Data for a single GameObject (without full component serialization)."""
    instanceID: int
    name: str
    tag: str = "Untagged"
    layer: int = 0
    layerName: str = "Default"
    active: bool = True
    activeInHierarchy: bool = True
    isStatic: bool = False
    transform: TransformData = TransformData()
    parent: int | None = None
    children: list[int] = []
    componentTypes: list[str] = []
    path: str = ""


class GameObjectResponse(MCPResponse):
    """Response containing GameObject data."""
    data: GameObjectData | None = None


@mcp_for_unity_resource(
    uri="unity://scene/gameobject/{instance_id}",
    name="gameobject",
    description="Get detailed information about a single GameObject by instance ID. Returns name, tag, layer, active state, transform data, parent/children IDs, and component type list (no full component properties)."
)
async def get_gameobject(ctx: Context, instance_id: str) -> MCPResponse:
    """Get GameObject data by instance ID."""
    unity_instance = get_unity_instance_from_context(ctx)
    
    try:
        id_int = int(instance_id)
    except ValueError:
        return MCPResponse(success=False, error=f"Invalid instance ID: {instance_id}")
    
    response = await send_with_unity_instance(
        async_send_command_with_retry,
        unity_instance,
        "get_gameobject",
        {"instanceID": id_int}
    )
    
    if isinstance(response, dict):
        if not response.get("success", True):
            return MCPResponse(**response)
        return MCPResponse(**response)
    return response


class ComponentsData(BaseModel):
    """Data for components on a GameObject."""
    gameObjectID: int
    gameObjectName: str
    components: list[Any] = []
    cursor: int = 0
    pageSize: int = 25
    nextCursor: int | None = None
    totalCount: int = 0
    hasMore: bool = False
    includeProperties: bool = True


class ComponentsResponse(MCPResponse):
    """Response containing components data."""
    data: ComponentsData | None = None


@mcp_for_unity_resource(
    uri="unity://scene/gameobject/{instance_id}/components",
    name="gameobject_components",
    description="Get all components on a GameObject with full property serialization. Supports pagination with pageSize and cursor parameters."
)
async def get_gameobject_components(
    ctx: Context, 
    instance_id: str,
    page_size: int = 25,
    cursor: int = 0,
    include_properties: bool = True
) -> MCPResponse:
    """Get all components on a GameObject."""
    unity_instance = get_unity_instance_from_context(ctx)
    
    try:
        id_int = int(instance_id)
    except ValueError:
        return MCPResponse(success=False, error=f"Invalid instance ID: {instance_id}")
    
    response = await send_with_unity_instance(
        async_send_command_with_retry,
        unity_instance,
        "get_gameobject_components",
        {
            "instanceID": id_int,
            "pageSize": page_size,
            "cursor": cursor,
            "includeProperties": include_properties
        }
    )
    
    if isinstance(response, dict):
        if not response.get("success", True):
            return MCPResponse(**response)
        return MCPResponse(**response)
    return response


class SingleComponentData(BaseModel):
    """Data for a single component."""
    gameObjectID: int
    gameObjectName: str
    component: Any = None


class SingleComponentResponse(MCPResponse):
    """Response containing single component data."""
    data: SingleComponentData | None = None


@mcp_for_unity_resource(
    uri="unity://scene/gameobject/{instance_id}/component/{component_name}",
    name="gameobject_component",
    description="Get a specific component on a GameObject by type name. Returns the fully serialized component with all properties."
)
async def get_gameobject_component(
    ctx: Context, 
    instance_id: str,
    component_name: str
) -> MCPResponse:
    """Get a specific component on a GameObject."""
    unity_instance = get_unity_instance_from_context(ctx)
    
    try:
        id_int = int(instance_id)
    except ValueError:
        return MCPResponse(success=False, error=f"Invalid instance ID: {instance_id}")
    
    response = await send_with_unity_instance(
        async_send_command_with_retry,
        unity_instance,
        "get_gameobject_component",
        {
            "instanceID": id_int,
            "componentName": component_name
        }
    )
    
    if isinstance(response, dict):
        if not response.get("success", True):
            return MCPResponse(**response)
        return MCPResponse(**response)
    return response

