import base64
import copy
import json
from pathlib import Path
import tempfile
import unittest
import zipfile

from tas_movie import LOG_KEY, read_movie
from tas_profile import capture_reference, differences
from tas_presentation import PresentationMapper, presentation_differences
from tas_movie_profile import movie_differences, CLOCK_ORIGIN, FRAME_CLOCKS


class TasTests(unittest.TestCase):
    def movie(self, folder, rows, header="", settings=None):
        path = Path(folder) / "test.bk2"
        with zipfile.ZipFile(path, "w") as z:
            z.writestr("Header.txt", "emuVersion Version 1.11.5\nPlatform GB\nCore Gambatte\nIsCGBMode 1\nSHA1 880374FB978B18AF4AA529E2E32F7FFB4D7DD2F4\n" + header)
            z.writestr("SyncSettings.json", json.dumps({"o": settings or {
                "ForceDMG": False, "GBACGB": True, "MulticartCompat": False,
                "RealTimeRTC": False, "RTCInitialTime": 0, "EqualLengthFrames": True}}))
            z.writestr("Input Log.txt", "\r\n".join(["[Input]", LOG_KEY, *rows, "[/Input]"]))
        return path

    def test_bk2_physical_buttons_and_power(self):
        with tempfile.TemporaryDirectory() as folder:
            inputs, metadata = read_movie(self.movie(folder, ["|.........|", "|U..R...A.|", "|........P|"]))
            self.assertEqual(inputs, bytes([0, 0, 0x51, 0, 0, 1]))
            self.assertEqual(metadata["frames"], 3)

    def test_reject_incompatible_movie_instead_of_resyncing(self):
        with tempfile.TemporaryDirectory() as folder:
            for header in ("Core GBHawk\n", "emuVersion Version 2.9\n", "SHA1 BAD\n", "StartsFromSavestate 1\n", "StartsFromSaveRam 1\n"):
                with self.subTest(header=header), self.assertRaises(ValueError):
                    read_movie(self.movie(folder, ["|.........|"], header))
            for rows in ([], ["|........|"], ["|X........|"], ["|........R|"]):
                with self.subTest(rows=rows), self.assertRaises(ValueError):
                    read_movie(self.movie(folder, rows))
            with self.assertRaises(ValueError):
                read_movie(self.movie(folder, ["|.........|"], settings={"EqualLengthFrames": False}))

    def reference(self):
        # Independent clean-US WRAM/struct addresses: fixed low bank, bank 1
        # object pages, and bank 4 at a distinct offset. Deliberately different
        # values expose wrong bank, row stride, endian, and slot-order mapping.
        ram, high = bytearray(32768), bytearray(127)
        ram[0x1000] = 1
        ram[0xc2d], ram[0xc30] = 4, 0x4a
        ram[0x100c:0x100e] = bytes([0x69, 0x37])
        ram[0x100a:0x100c] = bytes([0xb1, 0x26])
        ram[0x6aa] = 12
        ram[0x1080] = ram[0x1f80] = 1
        ram[0x4080] = 99
        ram[0xc57] = 0x88
        ram[0x6d0] = 0xa5
        high[0x14:0x16] = bytes([0x69, 0x51])
        return {"update": 1, "movieFrame": 44, "resetEpoch": 0, "input": 0x10, "pressed": 0,
                "wram": base64.b64encode(ram).decode(), "hram": base64.b64encode(high).decode()}

    def test_shared_memory_mapping_retains_bytes_and_slot_order(self):
        snapshot = capture_reference(self.reference())
        state = snapshot["state"]
        self.assertEqual(state["link.x8_8"], 0x3769)
        self.assertEqual(state["link.y8_8"], 0x26b1)
        self.assertEqual(state["link.health"], 12)
        self.assertEqual(state["link.controlled"], 1)
        self.assertEqual(state["save.$c6d0"], 0xa5)
        self.assertEqual(state["rng.hRng1"], 0x69)
        self.assertEqual(state["rng.hRng2"], 0x51)
        self.assertEqual([state[f"enemy.${0xd080+i*256:04x}.occupied"] for i in range(16)], [1]+[0]*14+[1])
        self.assertNotIn("save.$c5b0", state)
        self.assertEqual(snapshot["input"], 0x10)
        self.assertEqual(snapshot["pressed"], 0)

    def test_mutations_and_missing_fields_fail_exactly(self):
        reference = capture_reference(self.reference())
        self.assertEqual(differences(reference, copy.deepcopy(reference)), [])
        for field in ("link.x8_8", "rng.hRng1", "audio.$c014", "enemy.$df80.occupied", "save.$c6d0"):
            changed = copy.deepcopy(reference)
            changed["state"][field] += 1
            self.assertEqual(differences(reference, changed), [{"field": field, "rom": reference["state"][field], "godot": changed["state"][field]}])
        changed = copy.deepcopy(reference)
        del changed["state"]["link.x8_8"]
        self.assertEqual(differences(reference, changed)[0]["field"], "link.x8_8")
        for key in ("input", "pressed", "update"):
            changed = copy.deepcopy(reference)
            changed[key] += 1
            with self.assertRaises(ValueError): differences(reference, changed)

    def test_phase_gates_keep_inactive_counters_uncompared(self):
        raw = self.reference()
        ram = bytearray(base64.b64decode(raw["wram"]))
        ram[0x2e0], ram[0x2e6], ram[0x2e7] = 1, 2, 99
        ram[0x75] = 230
        raw["wram"] = base64.b64encode(ram).decode()
        state = capture_reference(raw)["state"]
        self.assertEqual(state["room.active"], 0)
        self.assertNotIn("link.x8_8", state)
        self.assertNotIn("frontend.state", state)
        self.assertNotIn("audio.channel0.wait", state)
        ram[0x6d] = 1
        raw["wram"] = base64.b64encode(ram).decode()
        self.assertEqual(capture_reference(raw)["state"]["audio.channel0.wait"], 230)

    def test_movie_catches_lag_and_input_poll_drift_without_resync(self):
        snapshot = capture_reference(self.reference())
        original = {"movieFrame": 44, "input": 0x10, "updates": [snapshot]}
        self.assertEqual(movie_differences(original, copy.deepcopy(original)), ([], []))
        lagged = dict(original, updates=[])
        self.assertEqual(movie_differences(original, lagged)[0],
            [{"field": "timing.completedUpdates", "rom": 1, "godot": 0}])
        # Same physical button, different preceding input poll: Godot must
        # derive its own edge and fail rather than accept the ROM's edge.
        changed = copy.deepcopy(original)
        changed["updates"][0]["pressed"] = 0x10
        self.assertEqual(movie_differences(original, changed)[0],
            [{"field": "timing.pressed", "rom": 0, "godot": 0x10}])
        self.assertEqual(movie_differences(lagged, dict(lagged)), ([], []))
        # The independently observed first two pinned-core movie endpoints.
        self.assertEqual(CLOCK_ORIGIN + FRAME_CLOCKS, 205916)
        self.assertEqual(CLOCK_ORIGIN + 2 * FRAME_CLOCKS, 346364)

    def test_movie_retains_audio_diagnostics_and_strict_gameplay(self):
        original = {"movieFrame": 44, "input": 0x10,
                    "updates": [capture_reference(self.reference())]}
        changed = copy.deepcopy(original)
        changed["updates"][0]["state"]["audio.$c014"] += 1
        failure, audio = movie_differences(original, changed)
        self.assertEqual(failure, [])
        self.assertEqual(audio, [{"field": "audio.$c014", "rom": 0, "godot": 1, "update": 1}])
        changed["updates"][0]["state"]["link.x8_8"] += 1
        self.assertEqual(movie_differences(original, changed)[0],
            [{"field": "link.x8_8", "rom": 0x3769, "godot": 0x376a, "update": 1}])


class PresentationTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        folder = Path(__file__).resolve().parent
        cls.mapper = PresentationMapper(folder.parents[1] / "assets/oracle/menu")
        fixture = json.loads((folder / "fixtures/menu-presentation.json").read_text())
        cls.raw = {row["movieFrame"]: row for row in fixture["frames"]}

    def state(self, frame):
        return self.mapper.capture(self.raw[frame])["state"]

    def test_executed_rom_panel_and_cursor_have_distinct_display_boundaries(self):
        # Independent native observations: the panel reaches the display one
        # frame before its cursor. Live fileSelect.mode2 is already 2 earlier.
        for frame, overlay, cursor in ((358, 0, 0), (359, 1, 0), (360, 1, 1)):
            state = self.state(frame)
            self.assertEqual(state["presentation.menu.screen"], 1)
            self.assertEqual(state["presentation.menu.overlay"], overlay)
            self.assertEqual(state["presentation.cursor.speed.visible"], cursor)
        self.assertEqual(self.state(360)["presentation.cursor.speed.x"], 89)
        self.assertEqual(self.state(360)["presentation.cursor.speed.y"], 128)
        reference = self.mapper.capture(self.raw[358])
        early = copy.deepcopy(reference)
        early["state"]["presentation.menu.overlay"] = 1
        self.assertEqual(presentation_differences(reference, early), [
            {"field": "presentation.menu.overlay", "rom": 0, "godot": 1}])

    def test_executed_rom_screen_fade_and_blanking_mapping(self):
        self.assertEqual(self.state(326)["presentation.menu.screen"], 1)
        options = self.state(332)
        self.assertEqual(options["presentation.menu.screen"], 2)
        self.assertEqual((options["presentation.cursor.acorn.0.x"], options["presentation.cursor.acorn.0.y"]), (32, 56))
        self.assertEqual(self.state(339)["presentation.menu.screen"], 3)
        self.assertEqual(self.state(380)["presentation.menu.blackLevelRgb5"], 26)
        for frame in (331, 338, 390):
            self.assertEqual(self.state(frame), {"presentation.menu.visible": 0})

    def test_mapping_uses_displayed_memory_not_movie_indices_or_logical_mode(self):
        raw = copy.deepcopy(self.raw[358])
        raw.update(movieFrame=12345, input=0xff, logicalMenu="TextSpeed")
        self.assertEqual(self.mapper.capture(raw)["state"], self.state(358))
        # Name/health/death-counter uploads are outside the static page stencil.
        video = raw["video"]
        tiles = bytearray(base64.b64decode(video["tiles"]))
        tiles[0x128] ^= 0xff
        video["tiles"] = base64.b64encode(tiles).decode()
        self.assertEqual(self.mapper.capture(raw)["state"], self.state(358))
        # A tilemap already prepared behind a blank frame is not visible.
        raw["video"]["blank"] = True
        self.assertEqual(self.mapper.capture(raw)["state"], {"presentation.menu.visible": 0})

    def test_missing_fields_clock_drift_and_malformed_video_fail(self):
        reference = self.mapper.capture(self.raw[360])
        for field in reference["state"]:
            changed = copy.deepcopy(reference)
            del changed["state"][field]
            self.assertEqual(presentation_differences(reference, changed)[0]["field"], field)
        for key in ("movieFrame", "input", "cpuClocks"):
            changed = copy.deepcopy(reference)
            changed[key] += 1
            with self.assertRaises(ValueError): presentation_differences(reference, changed)
        changed = copy.deepcopy(reference)
        changed["cpuClocks"] += 4
        self.assertEqual(presentation_differences(reference, changed, compare_clock=False), [])
        changed["state"]["presentation.menu.overlay"] = 0
        self.assertEqual(presentation_differences(reference, changed, compare_clock=False)[0]["field"],
                         "presentation.menu.overlay")
        raw = copy.deepcopy(self.raw[358])
        raw["video"]["tiles"] = "AA=="
        with self.assertRaises(ValueError): self.mapper.capture(raw)

    def test_gameplay_window_does_not_expose_retained_menu_bg(self):
        raw = copy.deepcopy(self.raw[358])
        # Executed arrival scanout uses LCDC=$ef and SCY=$f0 while the menu
        # tilemap is still intact. Recognition must use the display layout.
        raw["video"].update(lcd=0xef, scy=0xf0)
        self.assertEqual(self.mapper.capture(raw)["state"], {"presentation.menu.visible": 0})


if __name__ == "__main__":
    unittest.main()
