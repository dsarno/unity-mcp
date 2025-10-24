"""
Tests for JSON string parameter parsing in manage_asset tool.
"""
import pytest
import json
from unittest.mock import Mock, AsyncMock
from tools.manage_asset import manage_asset


class TestManageAssetJsonParsing:
    """Test JSON string parameter parsing functionality."""
    
    @pytest.mark.asyncio
    async def test_properties_json_string_parsing(self):
        """Test that JSON string properties are correctly parsed to dict."""
        # Mock context
        ctx = Mock()
        ctx.info = AsyncMock()
        ctx.warning = AsyncMock()
        
        # Mock Unity connection
        mock_connection = Mock()
        mock_connection.send_command_with_retry = AsyncMock(return_value={
            "success": True,
            "message": "Asset created successfully",
            "data": {"path": "Assets/Test.mat"}
        })
        
        # Test with JSON string properties
        result = await manage_asset(
            ctx=ctx,
            action="create",
            path="Assets/Test.mat",
            asset_type="Material",
            properties='{"shader": "Universal Render Pipeline/Lit", "color": [0, 0, 1, 1]}'
        )
        
        # Verify JSON parsing was logged
        ctx.info.assert_called_with("manage_asset: coerced properties from JSON string to dict")
        
        # Verify the result
        assert result["success"] is True
        assert "Asset created successfully" in result["message"]
    
    @pytest.mark.asyncio
    async def test_properties_invalid_json_string(self):
        """Test handling of invalid JSON string properties."""
        ctx = Mock()
        ctx.info = AsyncMock()
        ctx.warning = AsyncMock()
        
        # Mock Unity connection
        mock_connection = Mock()
        mock_connection.send_command_with_retry = AsyncMock(return_value={
            "success": True,
            "message": "Asset created successfully"
        })
        
        # Test with invalid JSON string
        result = await manage_asset(
            ctx=ctx,
            action="create",
            path="Assets/Test.mat",
            asset_type="Material",
            properties='{"invalid": json, "missing": quotes}'
        )
        
        # Verify warning was logged
        ctx.warning.assert_called()
        assert "failed to parse properties JSON string" in str(ctx.warning.call_args)
        assert result.get("success") is True
    
    @pytest.mark.asyncio
    async def test_properties_dict_unchanged(self):
        """Test that dict properties are passed through unchanged."""
        ctx = Mock()
        ctx.info = AsyncMock()
        
        # Mock Unity connection
        mock_connection = Mock()
        mock_connection.send_command_with_retry = AsyncMock(return_value={
            "success": True,
            "message": "Asset created successfully"
        })
        
        # Test with dict properties
        properties_dict = {"shader": "Universal Render Pipeline/Lit", "color": [0, 0, 1, 1]}
        
        result = await manage_asset(
            ctx=ctx,
            action="create",
            path="Assets/Test.mat",
            asset_type="Material",
            properties=properties_dict
        )
        
        # Verify no JSON parsing was attempted
        ctx.info.assert_not_called()
        assert result["success"] is True
    
    @pytest.mark.asyncio
    async def test_properties_none_handling(self):
        """Test that None properties are handled correctly."""
        ctx = Mock()
        ctx.info = AsyncMock()
        
        # Mock Unity connection
        mock_connection = Mock()
        mock_connection.send_command_with_retry = AsyncMock(return_value={
            "success": True,
            "message": "Asset created successfully"
        })
        
        # Test with None properties
        result = await manage_asset(
            ctx=ctx,
            action="create",
            path="Assets/Test.mat",
            asset_type="Material",
            properties=None
        )
        
        # Verify no JSON parsing was attempted
        ctx.info.assert_not_called()
        assert result["success"] is True


class TestManageGameObjectJsonParsing:
    """Test JSON string parameter parsing for manage_gameobject tool."""
    
    @pytest.mark.asyncio
    async def test_component_properties_json_string_parsing(self):
        """Test that JSON string component_properties are correctly parsed."""
        from tools.manage_gameobject import manage_gameobject
        
        ctx = Mock()
        ctx.info = AsyncMock()
        ctx.warning = AsyncMock()
        
        # Mock Unity connection
        mock_connection = Mock()
        mock_connection.send_command_with_retry = AsyncMock(return_value={
            "success": True,
            "message": "GameObject created successfully"
        })
        
        # Test with JSON string component_properties
        result = await manage_gameobject(
            ctx=ctx,
            action="create",
            name="TestObject",
            component_properties='{"MeshRenderer": {"material": "Assets/Materials/BlueMaterial.mat"}}'
        )
        
        # Verify JSON parsing was logged
        ctx.info.assert_called_with("manage_gameobject: coerced component_properties from JSON string to dict")
        
        # Verify the result
        assert result["success"] is True
