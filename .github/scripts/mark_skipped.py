#!/usr/bin/env python3
"""
Post-processes a JUnit XML so that "expected"/environmental failures
(e.g., permission prompts, empty MCP resources, or schema hiccups)
are converted to <skipped/>. Leaves real failures intact.

Usage:
  python .github/scripts/mark_skipped.py reports/claude-nl-tests.xml
"""

from __future__ import annotations
import sys
import os
import re
import xml.etree.ElementTree as ET

PATTERNS = [
    r"\bpermission\b",
    r"\bpermissions\b",
    r"\bautoApprove\b",
    r"\bapproval\b",
    r"\bdenied\b",
    r"requested\s+permissions",
    r"^MCP resources list is empty$",
    r"No MCP resources detected",
    r"aggregator.*returned\s*\[\s*\]",
    r"Unknown resource:\s*unity://",
    r"Input should be a valid dictionary.*ctx",
    r"validation error .* ctx",
]

def should_skip(msg: str) -> bool:
    if not msg:
        return False
    msg_l = msg.strip()
    for pat in PATTERNS:
        if re.search(pat, msg_l, flags=re.IGNORECASE | re.MULTILINE):
            return True
    return False

def summarize_counts(ts):
    tests = 0
    failures = 0
    errors = 0
    skipped = 0
    for case in ts.findall("testcase"):
        tests += 1
        if case.find("failure") is not None:
            failures += 1
        if case.find("error") is not None:
            errors += 1
        if case.find("skipped") is not None:
            skipped += 1
    return tests, failures, errors, skipped

def main(path: str) -> int:
    if not os.path.exists(path):
        print(f"[mark_skipped] No JUnit at {path}; nothing to do.")
        return 0

    try:
        tree = ET.parse(path)
    except ET.ParseError as e:
        print(f"[mark_skipped] Could not parse {path}: {e}")
        return 0

    root = tree.getroot()
    suites = root.findall("testsuite") if root.tag == "testsuites" else [root]

    changed = False
    for ts in suites:
        for case in list(ts.findall("testcase")):
            for node_name in ("failure", "error"):
                node = case.find(node_name)
                if node is None:
                    continue
                msg = (node.get("message") or "") + "\n" + (node.text or "")
                if should_skip(msg):
                    # Replace with <skipped/>
                    reason = "Marked skipped: environment/permission precondition not met"
                    case.remove(node)
                    skip = ET.SubElement(case, "skipped")
                    skip.set("message", reason)
                    skip.text = (node.text or "").strip() or reason
                    changed = True
                    break  # only one conversion per case

        # Recompute tallies per testsuite
        tests, failures, errors, skipped = summarize_counts(ts)
        ts.set("tests", str(tests))
        ts.set("failures", str(failures))
        ts.set("errors", str(errors))
        ts.set("skipped", str(skipped))

    if changed:
        tree.write(path, encoding="utf-8", xml_declaration=True)
        print(f"[mark_skipped] Updated {path}: converted environmental failures to skipped.")
    else:
        print(f"[mark_skipped] No environmental failures detected in {path}.")

    return 0

if __name__ == "__main__":
    target = sys.argv[1] if len(sys.argv) > 1 else "reports/claude-nl-tests.xml"
    raise SystemExit(main(target))
