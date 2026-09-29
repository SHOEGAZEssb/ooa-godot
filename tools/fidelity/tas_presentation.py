"""Visible file-menu semantics, sampled from completed video frames, not pixels."""
import base64

PRESENTATION_PROFILE = "ages-menu-presentation-v1"
PRESENTATION_BOUNDARY = "movie-transport-end-last-published-video-v1"
SCREEN_NAMES = {0: "outside-file-menu-or-white", 1: "file-select", 2: "new-file-options",
                3: "name-entry", 4: "copy", 5: "secret-entry"}
OVERLAY_NAMES = {0: "none", 1: "message-speed"}


class PresentationMapper:
    def __init__(self, assets):
        self.templates = {}
        for name in ("file_menu_top", "file_menu_middle", "save_menu_middle", "name_entry_middle",
                     "file_menu_copy", "secret_entry_middle", "file_menu_message_speed"):
            self.templates[name] = tuple((assets / f"{prefix}_{name}.bin").read_bytes()
                                         for prefix in ("map", "flags"))

    def capture(self, raw):
        state = {"presentation.menu.visible": 0}
        video = raw["video"]
        diagnostics = {"videoFrame": raw["videoFrame"], "video": video, "coverage": "outside-file-menu"}
        result = {"movieFrame": raw["movieFrame"], "input": raw["input"], "cpuClocks": raw["cpuClocks"],
                  "state": state, "diagnostics": diagnostics}
        if video is None or video["blank"]:
            diagnostics["coverage"] = "no-content-or-lcd-blank"
            return result
        # Arrival retains the file-menu BG map behind the gameplay window:
        # LCDC=$ef, SCY=$f0. Those tile identities no longer describe the
        # visible screen. File menus use an unscrolled BG without a window.
        if video["scx"] != 0 or video["scy"] != 0 or video["lcd"] & 0x20:
            return result
        def decode(name, size):
            data = base64.b64decode(video[name], validate=True)
            if len(data) != size:
                raise ValueError(f"Invalid presentation {name} length: {len(data)}")
            return data
        tiles, attributes = decode("tiles", 1024), decode("attributes", 1024)
        bg, sprites = decode("bg", 64), decode("oam", 160)
        def matches(name, offset, rows):
            return all(actual[offset + y*32 + x] == expected[y*32 + x]
                       for actual, expected in zip((tiles, attributes), self.templates[name])
                       for y in range(rows) for x in range(20))
        # Static source tile identities; dynamic names/hearts/death counts are
        # deliberately outside the stencil. No image, movie-frame or RAM-mode
        # signature is used to decide what the viewer has actually received.
        page = 0
        if matches("file_menu_top", 0, 4):
            if matches("save_menu_middle", 0xa0, 4): page = 2
            elif matches("file_menu_copy", 0xa0, 4): page = 4
            elif matches("file_menu_middle", 0xa0, 4): page = 1
            else: raise ValueError("Unmapped visible bank2.s file-menu tilemap.")
        elif matches("name_entry_middle", 0xa0, 4): page = 3
        elif matches("secret_entry_middle", 0xa0, 4): page = 5
        if not page:
            return result
        palettes = {attributes[y*32+x] & 7 for y in range(18) for x in range(20)}
        components = []
        for palette in palettes:
            for color in range(4):
                offset = palette*8 + color*2
                rgb = bg[offset] | bg[offset+1] << 8
                components.extend((rgb & 31, rgb >> 5 & 31, rgb >> 10 & 31))
        # File menus use black components; their minimum records the applied
        # fade after the GBA brightness LUT. $1f makes the menu invisible.
        level = min(components)
        if level == 31:
            diagnostics["coverage"] = "white-file-menu"
            return result
        state.update({"presentation.menu.visible": 1, "presentation.menu.screen": page,
                      "presentation.menu.overlay": int(page == 1 and matches("file_menu_message_speed", 0x1c0, 4)),
                      "presentation.menu.blackLevelRgb5": level})
        diagnostics["coverage"] = "file-menu"
        objects = [sprites[i:i+4] for i in range(0, 160, 4)
                   if 0 < sprites[i] < 160 and 0 < sprites[i+1] < 168]
        if page in (1, 2, 4):
            acorns = [o for o in objects if o[2] == 0x28 and o[3] == 4]
            state["presentation.cursor.acorn.count"] = len(acorns)
            for i, (y, x, _, _) in enumerate(acorns):
                state[f"presentation.cursor.acorn.{i}.x"] = x - 8
                state[f"presentation.cursor.acorn.{i}.y"] = y - 16
        if page == 1:
            speed = [o for o in objects if o[2] == 0x2e and o[3] == 1]
            if len(speed) > 1: raise ValueError("Multiple bank2.s text-speed cursors in displayed OAM.")
            state["presentation.cursor.speed.visible"] = int(bool(speed))
            if speed:
                y, x, _, _ = speed[0]
                state.update({"presentation.cursor.speed.x": x - 8, "presentation.cursor.speed.y": y - 16})
        return result


def presentation_differences(reference, candidate):
    for key in ("movieFrame", "input", "cpuClocks"):
        if type(candidate.get(key)) is not int or candidate[key] != reference[key]:
            raise ValueError(f"Misaligned presentation {key}: {reference.get(key)} / {candidate.get(key)}")
    a, b = reference["state"], candidate["state"]
    if not isinstance(a, dict) or not isinstance(b, dict) or any(type(v) is not int for v in (*a.values(), *b.values())):
        raise ValueError("Presentation state must contain exact integers.")
    order = {"presentation.menu.visible": 0, "presentation.menu.screen": 1,
             "presentation.menu.overlay": 2, "presentation.menu.blackLevelRgb5": 3}
    return [{"field": key, "rom": a.get(key), "godot": b.get(key)}
            for key in sorted(a.keys() | b.keys(), key=lambda key: (order.get(key, 4), key))
            if a.get(key) != b.get(key)]
