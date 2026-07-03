#!/usr/bin/env python3
"""Upload contraband item icons to Wiki.js assets folder."""

from __future__ import annotations

import json
import os
import subprocess
import sys
import urllib.error
import urllib.request
from concurrent.futures import ThreadPoolExecutor, as_completed
from pathlib import Path

REPO = Path(__file__).resolve().parents[1]
ICONS_DIR = REPO / "docs" / "wiki" / "ss14" / "contraband-icons"
DEFAULT_BASE_URL = "https://backmen.ru"
DEFAULT_FOLDER_ID = 3  # ss14/contraband-icons


def graphql(base_url: str, token: str, query: str) -> dict:
    body = json.dumps({"query": query}).encode("utf-8")
    request = urllib.request.Request(
        f"{base_url}/graphql",
        data=body,
        headers={
            "Authorization": f"Bearer {token}",
            "Content-Type": "application/json",
        },
        method="POST",
    )
    with urllib.request.urlopen(request) as response:
        payload = json.loads(response.read().decode("utf-8"))
    if payload.get("errors"):
        raise RuntimeError(json.dumps(payload["errors"], ensure_ascii=False))
    return payload["data"]


def list_existing_filenames(base_url: str, token: str, folder_id: int) -> set[str]:
    data = graphql(
        base_url,
        token,
        f'{{ assets {{ list(folderId: {folder_id}, kind: IMAGE) {{ filename }} }} }}',
    )
    return {item["filename"] for item in data["assets"]["list"]}


def upload_icon(base_url: str, token: str, folder_id: int, icon_path: Path) -> tuple[str, str]:
    wiki_name = icon_path.name.lower()
    result = subprocess.run(
        [
            "curl",
            "-s",
            "-X",
            "POST",
            f"{base_url}/u",
            "-H",
            f"Authorization: Bearer {token}",
            "-F",
            f'mediaUpload={{"folderId":{folder_id}}}',
            "-F",
            f"mediaUpload=@{icon_path};type=image/png",
        ],
        capture_output=True,
        text=True,
        check=False,
    )
    output = (result.stdout or result.stderr).strip()
    if output != "ok":
        raise RuntimeError(f"{icon_path.name}: {output or 'upload failed'}")
    return icon_path.name, wiki_name


def main() -> int:
    base_url = os.environ.get("WIKIJS_BASE_URL", DEFAULT_BASE_URL).rstrip("/")
    token = os.environ.get("WIKIJS_API_TOKEN")
    folder_id = int(os.environ.get("WIKIJS_ICON_FOLDER_ID", DEFAULT_FOLDER_ID))
    workers = int(os.environ.get("WIKIJS_UPLOAD_WORKERS", "8"))

    if not token:
        print("Set WIKIJS_API_TOKEN.", file=sys.stderr)
        return 1
    if not ICONS_DIR.is_dir():
        print(f"Missing icons dir: {ICONS_DIR}. Run export_wiki_from_game.py first.", file=sys.stderr)
        return 1

    icons = sorted(ICONS_DIR.glob("*.png"))
    if not icons:
        print("No PNG icons found.", file=sys.stderr)
        return 1

    existing = list_existing_filenames(base_url, token, folder_id)
    pending = [icon for icon in icons if icon.name.lower() not in existing]
    print(f"Icons: {len(icons)} total, {len(existing)} already on wiki, {len(pending)} to upload")

    if not pending:
        print("Nothing to upload.")
        return 0

    uploaded = 0
    failed: list[str] = []
    with ThreadPoolExecutor(max_workers=workers) as pool:
        futures = {
            pool.submit(upload_icon, base_url, token, folder_id, icon): icon
            for icon in pending
        }
        for index, future in enumerate(as_completed(futures), start=1):
            icon = futures[future]
            try:
                future.result()
                uploaded += 1
            except Exception as exc:  # noqa: BLE001
                failed.append(f"{icon.name}: {exc}")
            if index % 50 == 0 or index == len(pending):
                print(f"Progress: {index}/{len(pending)}")

    print(f"Uploaded: {uploaded}")
    if failed:
        print(f"Failed: {len(failed)}", file=sys.stderr)
        for line in failed[:20]:
            print(line, file=sys.stderr)
        if len(failed) > 20:
            print("...", file=sys.stderr)
        return 1

    print("Done.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
