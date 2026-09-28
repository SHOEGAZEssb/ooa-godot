"""Strict reader for the published cold-boot US Ages Gambatte BK2 profile."""
import hashlib
import json
from pathlib import Path
import zipfile

ROM_SHA1 = "880374fb978b18af4aa529e2e32f7ffb4d7dd2f4"
BUTTONS = [64, 128, 32, 16, 8, 4, 2, 1]
LOG_KEY = "LogKey:#Up|Down|Left|Right|Start|Select|B|A|Power|"


def read_movie(path):
    try:
        archive = zipfile.ZipFile(path)
    except zipfile.BadZipFile as exc:
        raise ValueError("Expected a valid BK2 ZIP container.") from exc
    with archive:
        names = archive.namelist()
        required = {"Header.txt", "SyncSettings.json", "Input Log.txt"}
        if not required <= set(names) or len(names) != len(set(names)):
            raise ValueError("Expected an extracted BK2, not the TASVideos download ZIP; missing/duplicate movie members.")
        if set(names) - required - {"Comments.txt", "Subtitles.txt"}:
            raise ValueError("Unsupported extra BK2 members; embedded states/SRAM are not supported.")
        if any(archive.getinfo(n).file_size > 32_000_000 for n in required):
            raise ValueError("Movie member exceeds the 32 MB safety bound.")
        header = dict(line.split(" ", 1) for line in archive.read("Header.txt").decode("utf-8-sig").splitlines() if " " in line)
        sync = json.loads(archive.read("SyncSettings.json")).get("o")
        if not isinstance(sync, dict):
            raise ValueError("Missing movie sync-settings object.")
        rows = [line for line in archive.read("Input Log.txt").decode("utf-8-sig").splitlines() if line]
    if header.get("SHA1", "").lower() != ROM_SHA1 or header.get("Core") != "Gambatte" or header.get("emuVersion") != "Version 1.11.5":
        raise ValueError("Movie requires a different ROM/core/version; supported profile is clean-US Ages / Gambatte / BizHawk 1.11.5.")
    if header.get("Platform") != "GB" or header.get("IsCGBMode") != "1" or any(
            header.get(k, "0") != "0" for k in ("StartsFromSavestate", "StartsFromSaveRam")):
        raise ValueError("This adapter requires a cold-boot CGB movie, without embedded SRAM/savestate.")
    expected = {"ForceDMG": False, "GBACGB": True, "MulticartCompat": False,
                "RealTimeRTC": False, "RTCInitialTime": 0, "EqualLengthFrames": True}
    if {k: v for k, v in sync.items() if k != "$type"} != expected:
        raise ValueError("Unsupported Gambatte sync settings; refusing an approximate replay.")
    if rows[:2] != ["[Input]", LOG_KEY] or rows[-1] != "[/Input]":
        raise ValueError("Unsupported BK2 input log/header/terminator.")
    inputs = bytearray()
    for index, row in enumerate(rows[2:-1]):
        if len(row) != 11 or row[0] != "|" or row[-1] != "|" or any(c not in (".", expected) for c, expected in zip(row[1:-1], "UDLRSsBAP")):
            raise ValueError(f"Malformed or unsupported input at movie frame {index}: {row!r}")
        inputs.extend((sum(bit for char, bit in zip(row[1:9], BUTTONS) if char != "."), int(row[9] != ".")))
    if not 1 <= len(inputs) // 2 <= 2_000_000:
        raise ValueError("Movie length must be 1..2,000,000 frames.")
    return bytes(inputs), {"sha256": hashlib.sha256(Path(path).read_bytes()).hexdigest(),
                           "header": header, "syncSettings": sync, "frames": len(inputs) // 2}
