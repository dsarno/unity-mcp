"""
Port discovery utility for Unity MCP Server.

What changed and why:
- Unity now writes a per-project port file named like
  `~/.unity-mcp/unity-mcp-port-<hash>.json` to avoid projects overwriting
  each other's saved port. The legacy file `unity-mcp-port.json` may still
  exist.
- This module now scans for both patterns, prefers the most recently
  modified file, and verifies that the port is actually a Unity MCP listener
  (quick socket connect + ping) before choosing it.
"""

import json
import os
import logging
from pathlib import Path
from typing import Optional, List
import glob
import socket
import struct

logger = logging.getLogger("unity-mcp-server")

FRAME_HEADER_SIZE = 8
# Keep small; we're only looking for a tiny pong. 1 MiB is generous for probes.
MAX_FRAME_SIZE = 1 << 20


# Module-level helper to avoid duplication and per-call redefinition
def _read_exact(sock: socket.socket, count: int) -> bytes:
    buf = bytearray()
    while len(buf) < count:
        chunk = sock.recv(count - len(buf))
        if not chunk:
            raise ConnectionError("Connection closed before reading expected bytes")
        buf.extend(chunk)
    return bytes(buf)

class PortDiscovery:
    """Handles port discovery from Unity Bridge registry"""
    REGISTRY_FILE = "unity-mcp-port.json"  # legacy single-project file
    DEFAULT_PORT = 6400
    CONNECT_TIMEOUT = 0.3  # seconds, keep this snappy during discovery
    
    @staticmethod
    def get_registry_path() -> Path:
        """Get the path to the port registry file"""
        return Path.home() / ".unity-mcp" / PortDiscovery.REGISTRY_FILE
    
    @staticmethod
    def get_registry_dir() -> Path:
        return Path.home() / ".unity-mcp"
    
    @staticmethod
    def list_candidate_files() -> List[Path]:
        """Return candidate registry files, newest first.
        Includes hashed per-project files and the legacy file (if present).
        """
        base = PortDiscovery.get_registry_dir()
        hashed = sorted(
            (Path(p) for p in glob.glob(str(base / "unity-mcp-port-*.json"))),
            key=lambda p: p.stat().st_mtime,
            reverse=True,
        )
        legacy = PortDiscovery.get_registry_path()
        if legacy.exists():
            # Put legacy at the end so hashed, per-project files win
            hashed.append(legacy)
        return hashed
    
    @staticmethod
    def _try_probe_unity_mcp(port: int) -> bool:
        """Quickly check if a Unity MCP listener is on this port.
        Performs a short TCP connect and ping/pong exchange. If the
        server advertises ``FRAMING=1`` in its greeting, the ping and
        pong are sent/received with an 8-byte big-endian length prefix.
        """

        try:
            with socket.create_connection(("127.0.0.1", port), PortDiscovery.CONNECT_TIMEOUT) as s:
                s.settimeout(PortDiscovery.CONNECT_TIMEOUT)
                try:
                    greeting = s.recv(256)
                    text = greeting.decode('ascii', errors='ignore') if greeting else ''
                    payload = b"ping"
                    if 'FRAMING=1' in text:
                        header = struct.pack('>Q', len(payload))
                        s.sendall(header + payload)
                        resp_header = _read_exact(s, FRAME_HEADER_SIZE)
                        resp_len = struct.unpack('>Q', resp_header)[0]
                        # Defensive cap against unreasonable frame sizes
                        if resp_len > MAX_FRAME_SIZE:
                            return False
                        data = _read_exact(s, resp_len)
                    else:
                        s.sendall(payload)
                        # Read a small bounded amount looking for pong
                        chunks = []
                        total = 0
                        data = b""
                        while total < 1024:
                            try:
                                part = s.recv(512)
                            except socket.timeout:
                                break
                            if not part:
                                break
                            chunks.append(part)
                            total += len(part)
                            if b'"message":"pong"' in part:
                                break
                        if chunks:
                            data = b"".join(chunks)
                    # Minimal validation: look for a success pong response
                    if data and b'"message":"pong"' in data:
                        return True
                except Exception:
                    return False
        except Exception:
            return False
        return False

    @staticmethod
    def _read_latest_status() -> Optional[dict]:
        try:
            base = PortDiscovery.get_registry_dir()
            status_files = sorted(
                (Path(p) for p in glob.glob(str(base / "unity-mcp-status-*.json"))),
                key=lambda p: p.stat().st_mtime,
                reverse=True,
            )
            if not status_files:
                return None
            with status_files[0].open('r') as f:
                return json.load(f)
        except Exception:
            return None
    
    @staticmethod
    def discover_unity_port() -> int:
        """
        Discover Unity port by scanning per-project and legacy registry files.
        Prefer the newest file whose port responds; fall back to first parsed
        value; finally default to 6400.
        
        Returns:
            Port number to connect to
        """
        # Prefer the latest heartbeat status if it points to a responsive port
        status = PortDiscovery._read_latest_status()
        if status:
            port = status.get('unity_port')
            if isinstance(port, int) and PortDiscovery._try_probe_unity_mcp(port):
                logger.info(f"Using Unity port from status: {port}")
                return port

        candidates = PortDiscovery.list_candidate_files()

        first_seen_port: Optional[int] = None

        for path in candidates:
            try:
                with open(path, 'r') as f:
                    cfg = json.load(f)
                unity_port = cfg.get('unity_port')
                if isinstance(unity_port, int):
                    if first_seen_port is None:
                        first_seen_port = unity_port
                    if PortDiscovery._try_probe_unity_mcp(unity_port):
                        logger.info(f"Using Unity port from {path.name}: {unity_port}")
                        return unity_port
            except Exception as e:
                logger.warning(f"Could not read port registry {path}: {e}")

        if first_seen_port is not None:
            logger.info(f"No responsive port found; using first seen value {first_seen_port}")
            return first_seen_port

        # Fallback to default port
        logger.info(f"No port registry found; using default port {PortDiscovery.DEFAULT_PORT}")
        return PortDiscovery.DEFAULT_PORT
    
    @staticmethod
    def get_port_config() -> Optional[dict]:
        """
        Get the most relevant port configuration from registry.
        Returns the most recent hashed file's config if present,
        otherwise the legacy file's config. Returns None if nothing exists.
        
        Returns:
            Port configuration dict or None if not found
        """
        candidates = PortDiscovery.list_candidate_files()
        if not candidates:
            return None
        for path in candidates:
            try:
                with open(path, 'r') as f:
                    return json.load(f)
            except Exception as e:
                logger.warning(f"Could not read port configuration {path}: {e}")
        return None