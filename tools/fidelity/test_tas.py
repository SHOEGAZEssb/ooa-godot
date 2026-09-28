import base64
import copy
import json
from pathlib import Path
import tempfile
import unittest
import zipfile

from tas_movie import LOG_KEY, read_movie
from tas_profile import capture_reference, differences


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


if __name__ == "__main__":
    unittest.main()
