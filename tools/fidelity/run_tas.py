"""Replay a pinned BK2 through original Gambatte and Godot, stopping at first drift."""
import argparse
from collections import deque
from datetime import datetime, timezone
import hashlib
import json
import os
from pathlib import Path
import queue
import re
import shutil
import subprocess
import sys
import threading
import time

from tas_movie import read_movie
from tas_profile import BOUNDARY, COVERAGE, PROFILE, capture_reference, differences, owner
from tas_presentation import (PRESENTATION_PROFILE, PRESENTATION_BOUNDARY, SCREEN_NAMES, OVERLAY_NAMES,
                              PresentationMapper, presentation_differences)

CLEAN_MD5 = "c4639cc61c049e5a085526bb6cac03bb"
# code/bank0.s:_mainLoop -> call pollInput, then hIntroInputsEnabled.
# Unique signatures are checked against the pinned clean image before hooking.
HOOKS = {"_mainLoop": (0x933, "cd6d02f0b9872808fa81c4d60fca6901"),
         "pollInput": (0x26d, "0e003e20e2f2f2f2473e10e278e60fcb3747")}
CORE_SHA256 = "61f0553b4078667c962b46abcd5ec7c13b4403bbcb94c653a53b795f377b47eb"
SOURCES = ["code/bank0.s", "code/bank1.s", "code/bank2.s", "code/bank3Cutscenes.s",
           "code/audio.s", "include/wram.s", "include/hram.s", "include/structs.s"]


def sha(data):
    return hashlib.sha256(data).hexdigest()


class Child:
    def __init__(self, args, cwd, log):
        self.log = log.open("w", encoding="utf-8")
        self.process = subprocess.Popen([str(a) for a in args], cwd=cwd, stdin=subprocess.PIPE,
            stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True, encoding="utf-8", errors="replace",
            creationflags=subprocess.CREATE_NO_WINDOW)
        self.lines = queue.Queue()
        def read():
            for line in self.process.stdout:
                if line.startswith("TAS_"):
                    self.lines.put(line.rstrip())
                else:
                    self.log.write(line)
                    self.log.flush()
            self.lines.put(None)
        self.reader = threading.Thread(target=read, daemon=True)
        self.reader.start()

    def receive(self, deadline):
        try:
            value = self.lines.get(timeout=max(0, deadline - time.monotonic()))
        except queue.Empty as exc:
            raise TimeoutError("Replay deadline exceeded; inspect retained engine logs.") from exc
        if value is None:
            raise RuntimeError(f"Replay process exited early ({self.process.poll()}); inspect retained logs.")
        if value.startswith("TAS_ERROR "):
            raise RuntimeError(value)
        return value

    def send(self, value):
        self.process.stdin.write(value + "\n")
        self.process.stdin.flush()

    def close(self):
        if self.process.poll() is None:
            try:
                self.send("stop")
                self.process.wait(timeout=5)
            except (OSError, subprocess.TimeoutExpired):
                self.process.kill()
                self.process.wait(timeout=5)
        self.reader.join(timeout=2)
        self.log.close()


def command(args, cwd, timeout=120):
    result = subprocess.run([str(a) for a in args], cwd=cwd, stdout=subprocess.PIPE,
                            stderr=subprocess.STDOUT, encoding="utf-8", errors="replace", timeout=timeout)
    if result.returncode:
        raise RuntimeError(f"Command failed: {args}\n{result.stdout[-6000:]}")
    return result.stdout.strip()


def write(path, data):
    path.write_text(json.dumps(data, indent=2) + "\n", encoding="utf-8")


def check_sources(disassembly):
    text = "\n".join((disassembly / p).read_text(encoding="utf-8") for p in ("include/wram.s", "include/hram.s"))
    symbols = {"wIntroStage": 0xc2e6, "wIntroVar": 0xc2e7, "wGameState": 0xc2ee,
               "wActiveGroup": 0xcc2d, "wActiveRoom": 0xcc30, "wLinkHealth": 0xc6aa,
               "wLinkIDOverride": 0xcc57, "hRng1": 0xff94, "hRng2": 0xff95,
               "wChannelsEnabled": 0xc06d, "wChannelWaitCounters": 0xc075}
    for name, address in symbols.items():
        if not re.search(r"(?m)^.*\b" + name + r"\b[^\n]*;\s*\$" + f"{address:04x}" + r"\b", text):
            raise ValueError(f"Changed source mapping: {name}=${address:04x}")
    return symbols


def prepare(args, root, output):
    movie = args.movie.resolve()
    if not movie.is_file():
        raise ValueError("Missing -Movie. Download and extract the BK2 from https://tasvideos.org/3127M (Download .bk2).")
    inputs, metadata = read_movie(movie)
    rom = args.rom or root / "Legend of Zelda, The - Oracle of Ages (U) [C][!].gbc"
    if not rom.is_file():
        raise ValueError("Missing supported clean-US ROM. Supply -Rom; no ROM is downloaded.")
    data = rom.read_bytes()
    if len(data) != 1048576 or hashlib.md5(data).hexdigest() != CLEAN_MD5:
        raise ValueError("ROM must be the clean-US 1 MiB image, MD5 " + CLEAN_MD5)
    for name, (address, signature) in HOOKS.items():
        expected = bytes.fromhex(signature)
        if data[address:address+len(expected)] != expected or data.count(expected) != 1:
            raise ValueError(f"Clean-ROM hook signature changed: {name}")
    core = args.bizhawk.resolve() / "dll/libgambatte.dll"
    if not core.is_file() or sha(core.read_bytes()) != CORE_SHA256:
        raise ValueError("Supply -BizHawk with a directory containing dll/libgambatte.dll from the official BizHawk 1.11.5 release; native core hash must match. https://github.com/TASEmulators/BizHawk/releases/tag/1.11.5")
    if not Path(args.godot).is_file():
        raise ValueError("Missing Godot .NET console executable. Supply -Godot.")
    source = args.disassembly or root.parent / "oracles-disasm"
    symbols = check_sources(source)
    if not args.skip_build:
        (output / "build.log").write_text(command(["dotnet", "build"], root), encoding="utf-8")
    compiler = Path(os.environ["WINDIR"]) / "Microsoft.NET/Framework/v4.0.30319/csc.exe"
    if not compiler.is_file():
        raise ValueError("Missing Windows .NET Framework v4 C# compiler required by the pinned x86 core transport.")
    shutil.copyfile(core, output / "libgambatte.dll")
    native = output / "TasReference.exe"
    (output / "reference-build.log").write_text(command([compiler, "/nologo", "/platform:x86", "/r:System.Web.Extensions.dll",
        "/out:" + str(native), root / "tools/fidelity/TasReference.cs", root / "tools/fidelity/TasVideoCapture.cs"], root), encoding="utf-8")
    (output / "movie-inputs.bin").write_bytes(inputs)
    files = command(["git", "ls-files", "-co", "--exclude-standard"], root).splitlines()
    tree = {p: sha((root / p).read_bytes()) for p in sorted(set(files)) if (root / p).is_file()}
    assemblies = [root / ".godot/mono/temp/bin/Debug/oracle-of-ages.dll",
                  root / "validation/bin/Debug/net8.0/oracle-of-ages.validation.dll"]
    manifest = {"schemaVersion": 3, "profile": PROFILE, "boundary": BOUNDARY,
        "presentation": {"profile": PRESENTATION_PROFILE, "boundary": PRESENTATION_BOUNDARY,
                         "screens": SCREEN_NAMES, "overlays": OVERLAY_NAMES,
                         "captureSourceSha256": sha((root / "tools/fidelity/TasVideoCapture.cs").read_bytes()),
                         "mapperSourceSha256": sha((root / "tools/fidelity/tas_presentation.py").read_bytes())},
        "createdUtc": datetime.now(timezone.utc).isoformat(), "movie": metadata,
        "expandedMovieInputSha256": sha(inputs), "romSha256": sha(data),
        "reference": {"emulator": "BizHawk 1.11.5 native Gambatte", "coreSha256": CORE_SHA256,
                      "transportSha256": sha(native.read_bytes()), "transportSourceSha256": sha((root / "tools/fidelity/TasReference.cs").read_bytes()),
                      "startup": "native cold boot, default SRAM, GBA CGB registers; no gameplay memory writes",
                      "framePacing": "Gambatte.cs FrameAdvance: 35112 samples minus carried overflow"},
        "source": {"revision": command(["git", "-c", "safe.directory=" + source.resolve().as_posix(), "rev-parse", "HEAD"], source),
                   "files": {p: sha((source / p).read_bytes()) for p in SOURCES}, "symbols": symbols, "hooks": HOOKS},
        "port": {"revision": command(["git", "rev-parse", "HEAD"], root), "workingFiles": tree,
                 "workingTreeSha256": sha(json.dumps(tree, sort_keys=True).encode()),
                 "assemblies": {p.name: sha(p.read_bytes()) for p in assemblies},
                 "godotVersion": command([args.godot, "--version"], root, 15),
                 "generatedTablesSha256": sha((root / "assets/oracle/generated_tables.manifest.tsv").read_bytes())},
        "coverage": COVERAGE, "godotBatchSize": args.batch_size,
        "limits": {"movieFrames": args.max_frames or metadata["frames"], "updates": args.max_updates, "seconds": args.timeout}}
    write(output / "manifest.json", manifest)
    return rom.resolve(), native, manifest


def replay(args, root, output, rom, native, manifest):
    children = []
    report = {"status": "error", "matchedSnapshots": 0, "matchedPresentationFrames": 0,
              "coveredPresentationFrames": 0, "firstDivergence": None, "completeMovie": False}
    context = deque(maxlen=4)
    deadline = time.monotonic() + args.timeout
    try:
        godot = Child([args.godot, "--headless", "--path", root, "--log-file", output / "godot-engine.log",
                       "--", "--validate", "--tas-replay", f"--tas-batch-size={args.batch_size}"], root, output / "godot.log")
        children.append(godot)
        if godot.receive(deadline) != "TAS_READY":
            raise RuntimeError("Godot did not enter TAS replay mode.")
        # A separate isolated application retains the physical input timeline.
        # The update adapter must remain independent of reference elapsed time.
        presentation = Child([args.godot, "--headless", "--path", root, "--log-file", output / "presentation-engine.log",
                              "--", "--validate", "--tas-replay", "--tas-presentation", f"--tas-batch-size={args.batch_size}"], root, output / "presentation.log")
        children.append(presentation)
        if presentation.receive(deadline) != "TAS_READY":
            raise RuntimeError("Godot did not enter presentation replay mode.")
        mapper = PresentationMapper(root / "assets/oracle/menu")
        reference = Child([native, rom, output / "movie-inputs.bin", manifest["limits"]["movieFrames"]], output, output / "reference.log")
        children.append(reference)
        with (output / "rom.jsonl").open("w", encoding="utf-8", buffering=1) as ref_file, \
             (output / "godot.jsonl").open("w", encoding="utf-8", buffering=1) as port_file, \
             (output / "rom-presentation.jsonl").open("w", encoding="utf-8", buffering=1) as video_ref_file, \
             (output / "godot-presentation.jsonl").open("w", encoding="utf-8", buffering=1) as video_port_file:
            while True:
                line = reference.receive(deadline)
                if line.startswith("TAS_END "):
                    end = json.loads(line[8:])
                    if not report["matchedSnapshots"] or end["snapshots"] != report["matchedSnapshots"]:
                        raise ValueError("Reference ended without complete snapshot coverage.")
                    if end["movieFrames"] != report["matchedPresentationFrames"]:
                        raise ValueError("Reference ended without complete presentation coverage.")
                    report.update(status="match", completeMovie=end["complete"], movieFrames=end["movieFrames"])
                    break
                if line.startswith("TAS_FRAME "):
                    raw = json.loads(line[10:])
                    if raw["resetEpoch"]:
                        raise ValueError("Hardware Power input reached: Godot power-cycle adaptation is not yet supported.")
                    if raw["movieFrame"] != report["matchedPresentationFrames"]:
                        raise ValueError("Nonconsecutive presentation movie frames.")
                    ref = mapper.capture(raw)
                    video_ref_file.write(json.dumps(ref, separators=(",", ":")) + "\n")
                    presentation.send(json.dumps({k: ref[k] for k in ("movieFrame", "input", "cpuClocks")}))
                    response = presentation.receive(deadline)
                    if not response.startswith("TAS_PRESENTATION "):
                        raise ValueError("Unexpected Godot presentation response.")
                    port = json.loads(response[17:])
                    video_port_file.write(json.dumps(port, separators=(",", ":")) + "\n")
                    diff = presentation_differences(ref, port)
                    context.append({"layer": "presentation", "movieFrame": ref["movieFrame"],
                                    "cpuClocks": ref["cpuClocks"], "rom": ref["state"], "godot": port["state"]})
                    if diff:
                        report.update(status="divergence", firstDivergence={"layer": "presentation",
                            "update": port["diagnostics"]["update"], "movieFrame": ref["movieFrame"],
                            "cpuClocks": ref["cpuClocks"], "input": ref["input"], "differences": diff,
                            "source": "code/bank2.s file-menu tilemaps/OAM and code/loadGraphics.s palettes",
                            "runtimeOwner": "MainMenuScreen / OracleVideoPresentation"}, context=list(context))
                        break
                    report["matchedPresentationFrames"] += 1
                    report["coveredPresentationFrames"] += ref["state"]["presentation.menu.visible"]
                    reference.send("continue")
                    continue
                if not line.startswith("TAS_REFERENCE "):
                    raise ValueError("Unexpected reference protocol response.")
                raw = json.loads(line[14:])
                if raw["resetEpoch"]:
                    raise ValueError("Hardware Power input reached: Godot power-cycle adaptation is not yet supported.")
                ref = capture_reference(raw)
                ref_file.write(json.dumps(ref, separators=(",", ":")) + "\n")
                godot.send(json.dumps({k: ref[k] for k in ("update", "input", "pressed")}))
                response = godot.receive(deadline)
                if not response.startswith("TAS_SNAPSHOT "):
                    raise ValueError("Unexpected Godot protocol response.")
                port = json.loads(response[13:])
                port_file.write(json.dumps(port, separators=(",", ":")) + "\n")
                diff = differences(ref, port)
                context.append({"update": ref["update"], "movieFrame": raw["movieFrame"], "input": ref["input"], "pressed": ref["pressed"],
                                "rom": ref["state"], "godot": port["state"]})
                if diff:
                    source, runtime = owner(diff[0]["field"])
                    report.update(status="divergence", firstDivergence={"layer": "shared-state", "update": ref["update"], "movieFrame": raw["movieFrame"],
                        "input": ref["input"], "pressed": ref["pressed"], "room": {"group": ref["state"].get("room.group"), "id": ref["state"].get("room.id")},
                        "differences": diff, "source": source, "runtimeOwner": runtime}, context=list(context))
                    break
                report["matchedSnapshots"] += 1
                if args.max_updates is not None and ref["update"] >= args.max_updates:
                    report.update(status="match", limit="max-updates")
                    break
                if ref["update"] % 1000 == 0:
                    print(f"Matched snapshot {ref['update']} (movie frame {raw['movieFrame']}).", flush=True)
                reference.send("continue")
    except Exception as exc:
        report.update(status="error", error=str(exc), context=list(context))
    finally:
        for child in reversed(children):
            child.close()
        write(output / "comparison.json", report)
    return report


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--movie", type=Path, required=True)
    parser.add_argument("--bizhawk", type=Path, required=True)
    parser.add_argument("--rom", type=Path)
    parser.add_argument("--disassembly", type=Path)
    parser.add_argument("--godot", default=r"E:\Stuff\Gamedev\Godot\Godot_v4.7.1-stable_mono_win64_console.exe")
    parser.add_argument("--output", type=Path)
    parser.add_argument("--skip-build", action="store_true")
    parser.add_argument("--max-frames", type=int)
    parser.add_argument("--max-updates", type=int)
    parser.add_argument("--timeout", type=int, default=600)
    parser.add_argument("--batch-size", type=int, default=1)
    args = parser.parse_args()
    if not 1 <= args.batch_size <= 1024:
        parser.error("Batch size must be 1..1024.")
    if args.timeout < 1 or args.max_frames is not None and args.max_frames < 1 or args.max_updates is not None and args.max_updates < 0:
        parser.error("Positive frame/time bounds and nonnegative update bounds are required.")
    root = Path(__file__).resolve().parents[2]
    output = (args.output or root / "local-audits" / ("tas-" + datetime.now().strftime("%Y%m%d-%H%M%S"))).resolve()
    output.mkdir(parents=True, exist_ok=True)
    if any(output.iterdir()):
        raise ValueError("Output directory is not empty; choose a fresh -Output.")
    print("Artifacts: " + str(output), flush=True)
    rom, native, manifest = prepare(args, root, output)
    report = replay(args, root, output, rom, native, manifest)
    if report["status"] == "divergence":
        d = report["firstDivergence"]
        first = d["differences"][0]
        def value(v):
            if v is None: return "<absent>"
            labels = {"presentation.menu.screen": SCREEN_NAMES, "presentation.menu.overlay": OVERLAY_NAMES}.get(first["field"], {})
            return f"{labels[v]} ({v})" if v in labels else f"${v:x} ({v})"
        print(f"DIVERGENCE {d['layer']} update {d['update']}, movie frame {d['movieFrame']}, input ${d['input']:02x}: {first['field']} ROM {value(first['rom'])} / Godot {value(first['godot'])}")
        return 1
    if report["status"] == "error":
        print(report["error"], file=sys.stderr)
        return 2
    print(f"MATCH {report['matchedSnapshots']} snapshots and {report['matchedPresentationFrames']} presentation observations "
          f"({report['coveredPresentationFrames']} visible file-menu frames); complete movie: {report['completeMovie']}.")
    return 0


if __name__ == "__main__":
    try:
        sys.exit(main())
    except (OSError, ValueError, RuntimeError, subprocess.SubprocessError) as exc:
        print("TAS replay failed: " + str(exc), file=sys.stderr)
        sys.exit(2)
