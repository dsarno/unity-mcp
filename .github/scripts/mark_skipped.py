#!/usr/bin/env python3
"""Convert permission-related failures in JUnit reports to skipped tests.

The Claude NL/T suite can emit failures with messages like "Test skipped..." when
MCP tool approval blocks execution. GitHub dashboards should treat these as
skipped, not failed. This script rewrites the JUnit XML in-place, replacing any
<failure> element containing "approval required" or "MCP not usable" with
<skipped>.
"""
import sys
import pathlib
import xml.etree.ElementTree as ET

def main(path: str) -> None:
    p = pathlib.Path(path)
    if not p.exists():
        return
    tree = ET.parse(p)
    root = tree.getroot()
    changed = False
    for case in root.iter("testcase"):
        failure = case.find("failure")
        if failure is None:
            continue
        msg = (failure.get("message") or "") + (failure.text or "")
        if "approval required" in msg.lower() or "mcp not usable" in msg.lower():
            case.remove(failure)
            skipped = ET.Element("skipped")
            if failure.get("message"):
                skipped.set("message", failure.get("message"))
            case.append(skipped)
            changed = True
    if changed:
        tree.write(p, encoding="utf-8", xml_declaration=True)

if __name__ == "__main__":
    if len(sys.argv) > 1:
        main(sys.argv[1])
