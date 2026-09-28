"""Shared engine fields. No room, encounter, or TAS-route exceptions."""
import base64

PROFILE = "ages-shared-v1"
BOUNDARY = "mainLoop-before-pollInput-after-vblank-v1"
AUDIO = (0xc014, 0xc015, 0xc01b, 0xc022, 0xc023, 0xc024)
COVERAGE = {
    "supported": ["global RNG bytes", "frontend stage and non-cinematic substate", "room identity and Link fixed-point position/direction/control/health during gameplay",
                  "16 ordered native enemy-slot occupancy bits during gameplay", "live save payload $c5ba-$caff during gameplay",
                  "sound fade/enable/volume and eight channel enable/wait fields"],
    "unavailable": ["cinematic frontend substate mapping", "file-select/new-game internal states", "full Link native state/counter mapping", "enemy IDs/species states and child/part/item pools", "full RNG placement buffer mapping", "hardware Power input on the Godot side"],
    "outOfScope": ["pixels/VRAM, OAM, APU samples and hardware clocks", "ROM boot before the first _mainLoop", "unvisited TAS branches"]
}


def capture_reference(raw):
    ram = base64.b64decode(raw["wram"], validate=True)
    high = base64.b64decode(raw["hram"], validate=True)
    if len(ram) != 32768 or len(high) != 127:
        raise ValueError("Unexpected native memory sizes.")
    def r(address):
        return high[address - 0xff80] if address >= 0xff80 else ram[address - 0xc000]
    # Thread 0 exclusively owns frontend. It is stopped at file selection;
    # bit 7 suspends a thread while preserving its allocation.
    frontend = int(r(0xc2e0) != 0)
    state = {"rng.hRng1": r(0xff94), "rng.hRng2": r(0xff95), "frontend.active": frontend}
    if frontend:
        state["frontend.stage"] = r(0xc2e6)
        if r(0xc2e6) != 2:
            state["frontend.state"] = r(0xc2e7)
    for address in AUDIO:
        state[f"audio.${address:04x}"] = r(address)
    for channel in range(8):
        enabled = r(0xc06d + channel)
        state[f"audio.channel{channel}.enabled"] = enabled
        if enabled:
            state[f"audio.channel{channel}.wait"] = r(0xc075 + channel)
    # The ordinary Link special-object slot is enabled by room initialization.
    # Frontend cinematic actors use their own object dispatch and are excluded.
    gameplay = int(not frontend and r(0xd000) != 0)
    state["room.active"] = gameplay
    if gameplay:
        state.update({"room.group": r(0xcc2d), "room.id": r(0xcc30),
                      "link.x8_8": r(0xd00c) | r(0xd00d) << 8,
                      "link.y8_8": r(0xd00a) | r(0xd00b) << 8,
                      "link.direction": r(0xd008), "link.health": r(0xc6aa),
                      "link.controlled": int((r(0xcc57) & 0x7f if r(0xcc57) & 0x80 else r(0xd001)) == 8)})
        for slot in range(16):
            addr = 0xd080 + slot * 256
            state[f"enemy.${addr:04x}.occupied"] = int(r(addr) != 0)
        for address in range(0xc5ba, 0xcb00):
            state[f"save.${address:04x}"] = r(address)
    return {"update": raw["update"], "input": raw["input"], "pressed": raw["pressed"], "state": state,
            "diagnostics": {"movieFrame": raw["movieFrame"], "resetEpoch": raw["resetEpoch"],
                            "threadStates": ram[0x2e0:0x300].hex(), "link": ram[0x1000:0x1040].hex(),
                            "enemies": [ram[0x1080+i*256:0x10c0+i*256].hex() for i in range(16)],
                            "gameState": r(0xc2ee), "cutscene": r(0xc2ef), "frameCounter": r(0xcc00)}}


def differences(reference, candidate):
    for key in ("update", "input", "pressed"):
        if type(candidate.get(key)) is not int or candidate[key] != reference[key]:
            raise ValueError(f"Misaligned {key}: {reference.get(key)} / {candidate.get(key)}")
    a, b = reference["state"], candidate["state"]
    if not isinstance(a, dict) or not isinstance(b, dict) or any(type(v) is not int for v in list(a.values()) + list(b.values())):
        raise ValueError("Shared state must contain exact integers.")
    return [{"field": field, "rom": a.get(field), "godot": b.get(field)}
            for field in sorted(a.keys() | b.keys()) if a.get(field) != b.get(field)]


def owner(field):
    return {
        "frontend": ("code/bank3Cutscenes.s:runIntro", "FrontendIntroController"),
        "rng": ("code/bank0.s:getRandomNumber", "OracleRandom"),
        "audio": ("code/audio.s", "OracleSoundEngine / OracleSoundDriver"),
        "link": ("object_code/ages/specialObjects/link.s", "Player"),
        "enemy": ("code/bank0.s:updateEnemies", "RoomEntityManager"),
        "room": ("code/bank1.s:runGameLogic", "RoomSession / GameRoot"),
        "save": ("include/wram.s:wFileStart", "OracleSaveData")
    }.get(field.split(".")[0], (None, None))
