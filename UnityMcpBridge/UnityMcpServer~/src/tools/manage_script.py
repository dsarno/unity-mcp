from mcp.server.fastmcp import FastMCP, Context
from typing import Dict, Any, List
from unity_connection import send_command_with_retry
import base64
import os
from urllib.parse import urlparse, unquote


def register_manage_script_tools(mcp: FastMCP):
    """Register all script management tools with the MCP server."""

    def _split_uri(uri: str) -> tuple[str, str]:
        """Split an incoming URI or path into (name, directory) suitable for Unity.

        Rules:
        - unity://path/Assets/... → keep as Assets-relative (after decode/normalize)
        - file://... → percent-decode, normalize, strip host and leading slashes,
          then, if any 'Assets' segment exists, return path relative to that 'Assets' root.
          Otherwise, fall back to original name/dir behavior.
        - plain paths → decode/normalize separators; if they contain an 'Assets' segment,
          return relative to 'Assets'.
        """
        raw_path: str
        if uri.startswith("unity://path/"):
            raw_path = uri[len("unity://path/") :]
        elif uri.startswith("file://"):
            parsed = urlparse(uri)
            # Use parsed.path (percent-encoded) and decode it
            raw_path = unquote(parsed.path or "")
            # Handle cases like file://localhost/...
            if not raw_path and uri.startswith("file://"):
                raw_path = uri[len("file://") :]
        else:
            raw_path = uri

        # Percent-decode any residual encodings and normalize separators
        raw_path = unquote(raw_path).replace("\\", "/")
        if raw_path.startswith("//"):
            # Strip possible leading '//' from malformed file URIs
            raw_path = raw_path.lstrip("/")

        # Normalize path (collapse ../, ./)
        norm = os.path.normpath(raw_path).replace("\\", "/")

        # If an 'Assets' segment exists, compute path relative to it
        parts = [p for p in norm.split("/") if p not in ("", ".")]
        try:
            idx = parts.index("Assets")
            assets_rel = "/".join(parts[idx:])
        except ValueError:
            assets_rel = None

        effective_path = assets_rel if assets_rel else norm

        name = os.path.splitext(os.path.basename(effective_path))[0]
        directory = os.path.dirname(effective_path)
        return name, directory

    @mcp.tool()
    def apply_text_edits(
        ctx: Context,
        uri: str,
        edits: List[Dict[str, Any]],
        precondition_sha256: str | None = None,
    ) -> Dict[str, Any]:
        """Apply small text edits to a C# script identified by URI."""
        name, directory = _split_uri(uri)
        params = {
            "action": "apply_text_edits",
            "name": name,
            "path": directory,
            "edits": edits,
            "precondition_sha256": precondition_sha256,
        }
        params = {k: v for k, v in params.items() if v is not None}
        resp = send_command_with_retry("manage_script", params)
        return resp if isinstance(resp, dict) else {"success": False, "message": str(resp)}

    @mcp.tool()
    def create_script(
        ctx: Context,
        path: str,
        contents: str = "",
        script_type: str | None = None,
        namespace: str | None = None,
    ) -> Dict[str, Any]:
        """Create a new C# script at the given path."""
        name = os.path.splitext(os.path.basename(path))[0]
        directory = os.path.dirname(path)
        params: Dict[str, Any] = {
            "action": "create",
            "name": name,
            "path": directory,
            "namespace": namespace,
            "scriptType": script_type,
        }
        if contents:
            params["encodedContents"] = base64.b64encode(contents.encode("utf-8")).decode("utf-8")
            params["contentsEncoded"] = True
        params = {k: v for k, v in params.items() if v is not None}
        resp = send_command_with_retry("manage_script", params)
        return resp if isinstance(resp, dict) else {"success": False, "message": str(resp)}

    @mcp.tool()
    def delete_script(ctx: Context, uri: str) -> Dict[str, Any]:
        """Delete a C# script by URI."""
        name, directory = _split_uri(uri)
        params = {"action": "delete", "name": name, "path": directory}
        resp = send_command_with_retry("manage_script", params)
        return resp if isinstance(resp, dict) else {"success": False, "message": str(resp)}

    @mcp.tool()
    def validate_script(
        ctx: Context, uri: str, level: str = "basic"
    ) -> Dict[str, Any]:
        """Validate a C# script and return diagnostics."""
        name, directory = _split_uri(uri)
        params = {
            "action": "validate",
            "name": name,
            "path": directory,
            "level": level,
        }
        resp = send_command_with_retry("manage_script", params)
        return resp if isinstance(resp, dict) else {"success": False, "message": str(resp)}

    @mcp.tool()
    def manage_script(
        ctx: Context,
        action: str,
        name: str,
        path: str,
        contents: str,
        script_type: str,
        namespace: str,
    ) -> Dict[str, Any]:
        """Compatibility router for legacy script operations.

        IMPORTANT:
        - Direct file reads should use resources/read.
        - Edits should use apply_text_edits.

        Args:
            action: Operation ('create', 'read', 'delete').
            name: Script name (no .cs extension).
            path: Asset path (default: "Assets/").
            contents: C# code for 'create'/'update'.
            script_type: Type hint (e.g., 'MonoBehaviour').
            namespace: Script namespace.

        Returns:
            Dictionary with results ('success', 'message', 'data').
        """
        try:
            # Deprecate full-file update path entirely
            if action == 'update':
                return {"success": False, "message": "Deprecated: use apply_text_edits or resources/read + small edits."}

            # Prepare parameters for Unity
            params = {
                "action": action,
                "name": name,
                "path": path,
                "namespace": namespace,
                "scriptType": script_type,
            }

            # Base64 encode the contents if they exist to avoid JSON escaping issues
            if contents:
                if action == 'create':
                    params["encodedContents"] = base64.b64encode(contents.encode('utf-8')).decode('utf-8')
                    params["contentsEncoded"] = True
                else:
                    params["contents"] = contents

            params = {k: v for k, v in params.items() if v is not None}

            response = send_command_with_retry("manage_script", params)

            if isinstance(response, dict):
                if response.get("success"):
                    if response.get("data", {}).get("contentsEncoded"):
                        decoded_contents = base64.b64decode(response["data"]["encodedContents"]).decode('utf-8')
                        response["data"]["contents"] = decoded_contents
                        del response["data"]["encodedContents"]
                        del response["data"]["contentsEncoded"]

                    return {
                        "success": True,
                        "message": response.get("message", "Operation successful."),
                        "data": response.get("data"),
                    }
                return response

            return {"success": False, "message": str(response)}

        except Exception as e:
            return {
                "success": False,
                "message": f"Python error managing script: {str(e)}",
            }
