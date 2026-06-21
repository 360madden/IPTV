#!/usr/bin/env python3
"""Validate UI smoke screenshots and optionally compare dimensions to a baseline manifest."""

from __future__ import annotations

import argparse
import json
import struct
from pathlib import Path


EXPECTED_FILES = (
    "01-initial-no-channel-placeholder.png",
    "02-channel-sort-dropdown.png",
    "03-theme-dropdown.png",
    "04-ui-scale-dropdown.png",
)


def read_png_size(path: Path) -> tuple[int, int]:
    with path.open("rb") as stream:
        header = stream.read(24)
    if len(header) < 24 or header[:8] != b"\x89PNG\r\n\x1a\n" or header[12:16] != b"IHDR":
        raise ValueError(f"{path} is not a valid PNG file.")
    width, height = struct.unpack(">II", header[16:24])
    return width, height


def build_manifest(directory: Path) -> dict[str, dict[str, int]]:
    manifest: dict[str, dict[str, int]] = {}
    for name in EXPECTED_FILES:
        path = directory / name
        if not path.exists():
            raise FileNotFoundError(f"Missing screenshot: {path}")
        if path.stat().st_size < 16_384:
            raise ValueError(f"Screenshot is unexpectedly small: {path}")
        width, height = read_png_size(path)
        if width < 800 or height < 600:
            raise ValueError(f"Screenshot is too small for layout review: {path} ({width}x{height})")
        manifest[name] = {"width": width, "height": height, "bytes": path.stat().st_size}
    return manifest


def compare_to_baseline(current: dict[str, dict[str, int]], baseline_path: Path) -> list[str]:
    if not baseline_path.exists():
        return []
    baseline = json.loads(baseline_path.read_text(encoding="utf-8"))
    differences: list[str] = []
    for name in EXPECTED_FILES:
        expected = baseline.get(name)
        actual = current[name]
        if not expected:
            differences.append(f"{name}: missing from baseline")
            continue
        if expected.get("width") != actual["width"] or expected.get("height") != actual["height"]:
            differences.append(
                f"{name}: size {actual['width']}x{actual['height']} differs from "
                f"baseline {expected.get('width')}x{expected.get('height')}"
            )
    return differences


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--screenshots",
        default="artifacts/ui-smoke/dropdowns",
        help="Directory produced by scripts/smoke-ui-dropdowns.ps1.",
    )
    parser.add_argument(
        "--baseline",
        default="artifacts/ui-smoke/dropdowns-baseline.json",
        help="Optional JSON baseline manifest to compare dimensions against.",
    )
    parser.add_argument(
        "--write-baseline",
        action="store_true",
        help="Write/update the baseline manifest from the current screenshots.",
    )
    args = parser.parse_args()

    screenshots_dir = Path(args.screenshots)
    baseline_path = Path(args.baseline)
    current = build_manifest(screenshots_dir)

    if args.write_baseline:
        baseline_path.parent.mkdir(parents=True, exist_ok=True)
        baseline_path.write_text(json.dumps(current, indent=2, sort_keys=True) + "\n", encoding="utf-8")
        print(f"Wrote screenshot baseline: {baseline_path}")
        return 0

    differences = compare_to_baseline(current, baseline_path)
    if differences:
        print("Screenshot comparison failed:")
        for difference in differences:
            print(f"- {difference}")
        return 1

    print(f"Validated {len(current)} UI smoke screenshots in {screenshots_dir}.")
    if not baseline_path.exists():
        print(f"No baseline found at {baseline_path}; dimensions and PNG integrity only were checked.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
