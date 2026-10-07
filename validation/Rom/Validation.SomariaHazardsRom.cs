using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateSomariaHazardsRom()
    {
        // Exhaust the small lookup directly, then exercise each destructive
        // terrain class through the real equipped Cane and item/object loop.
        var data = new SomariaPlacementDatabase();
        var lookup = new LinkCollisionRom();
        lookup[0xd70b] = 0x40; lookup[0xd70d] = 0x48;
        int hazards = 0;
        for (int mode = 0; mode < 6; mode++)
        for (int tile = 0; tile < 256; tile++)
        {
            lookup[0xcc33] = (byte)mode; lookup[0xcf44] = (byte)tile;
            lookup.Call(LinkCollisionRom.OverHazard, bank: 0, objectPage: 0xd7);
            int expected = (lookup[0xc201] & 0x10) != 0 ? lookup[0xc200] : 0;
            FailIf(data.Hazard(mode, tile) != expected,
                $"Somaria hazard mode=${mode:x2} tile=${tile:x2}: native class=${expected:x2}, imported=${data.Hazard(mode, tile):x2}.");
            if (expected != 0) hazards++;
        }
        FailIf(hazards != 84 || data.Hazard(0, 0xe4) != 4 || data.Hazard(2, 0xf3) != 2 || data.Hazard(3, 0x1a) != 1,
            "Ages hazard source must retain all 84 mode/tile pairs and distinct water/hole/lava classes.");
        int fixture = 0;
        foreach (byte tile in new byte[] { 0xfa, 0xf3, 0x61 })
        foreach (bool batched in RomHostSchedules(fixture++))
        {
            bool primary = tile != 0xf3;
            SomariaRom rom = PrepareSomariaMotionRom(0, primary: primary);
            Vector2 target = data.Align(data.CreationPosition(_player.Position, 0));
            _currentRoom.SetPositionTileAndCollision(target, tile, 0, 0);
            rom[0xcf00 + _currentRoom.GetPackedPosition(target)] = tile;
            rom[0xce00 + _currentRoom.GetPackedPosition(target)] = 0;
            var seed = _random.CaptureState();
            var sounds = _sound.AttachPlayRequestAudit();
            int button = primary ? 1 : 2, update = 0;
            void Step(int count = 1, bool press = false)
            {
                int edge = press ? button : 0;
                StepGameplayUpdates(count, Vector2.Zero, MenuRomActions(edge), MenuRomActions(edge), batched, () =>
                {
                    rom.UpdateGameplay(edge, press ? button : 0, 0xff, _entities.FrameCounter); edge = 0;
                    string context = $"Cane hazard ${tile:x2} update={++update}";
                    CompareSomariaMotionRom(rom, context);
                    FailIf(!sounds.Requests.Where(cue => cue != SoundId.SndText).SequenceEqual(rom.Sounds), context + ": cue order differs.");
                    var random = _random.CaptureState();
                    FailIf(random.Rng1 != rom[0xff94] || random.Rng2 != rom[0xff95] || random.Calls - seed.Calls != rom.RandomCalls,
                        context + ": shared RNG differs.");
                });
            }
            for (int repeat = 0; repeat < 2; repeat++)
            {
                Step(1, true); Step(14);
                FailIf(rom.Blocks.Length != 1 || rom[rom.Blocks[0] + 4] != 1,
                    "Hazard placement must retain the entire source phase-in before the alignment/hazard check.");
                _dialogue.ShowMessage("Hazard phase-in pause.", _player.Position.Y); rom[0xcba0] = 1;
                Step(3); _dialogue.Close(); rom[0xcba0] = 0;
                Step(8); FailIf(rom.Blocks.Length != 1, "Hazard block disappeared before the phase-in terminal update.");
                Step();
                FailIf(rom.Blocks.Length != 0 || _currentRoom.GetMetatile(target) != tile || _currentRoom.GetTerrainInfo(target).Collision != 0,
                    "Water/hole/lava must reject aligned block placement without overwriting terrain.");
                var puffs = _entities.Entities<PuzzlePuffEffect>();
                int[] native = Enumerable.Range(0xd0, 16).Select(page => page * 256 + 0x40)
                    .Where(slot => rom[slot] != 0 && rom[slot + 1] == 0x05).ToArray();
                FailIf(puffs.Count != 1 || native.Length != 1 || puffs[0].Position != target ||
                    puffs[0].Position != new Vector2(rom[native[0] + 0xd], rom[native[0] + 0xb]),
                    $"Hazard ${tile:x2}: rejected block puff runtime=[{string.Join(',', puffs.Select(puff => puff.Position))}], native=[{string.Join(',', Enumerable.Range(0xd0, 16).Select(page => page * 256 + 0x40).Where(slot => rom[slot] != 0).Select(slot => $"${rom[slot + 1]:x2}@{rom[slot + 0xd]},{rom[slot + 0xb]}"))}], target={target}.");
                Step(20);
            }
        }
        ValidateSomariaHazardThrowsRom();
        GD.Print("Validated all 1536 native Somaria hazard lookup results and 84 source pairs, plus actual water/hole/lava phase-in rejection and throws, aligned effects, text freeze, terrain retention and repeated A/B use through split/batched gameplay.");
    }

    private void ValidateSomariaHazardThrowsRom()
    {
        int fixture = 0;
        foreach (byte tile in new byte[] { 0xfa, 0xf3, 0x61 })
        foreach (bool batched in RomHostSchedules(fixture++))
        {
            bool primary = tile != 0xf3;
            SomariaRom rom = PrepareSomariaMotionRom(1, primary: primary, pushRoute: true);
            var seed = _random.CaptureState();
            var sounds = _sound.AttachPlayRequestAudit();
            int button = primary ? 1 : 2;
            void Step(int count, int angle = 0xff, int held = 0, int pressed = 0) =>
                StepSomariaMotionRom(rom, count, batched, angle, held, pressed, () =>
                {
                    var random = _random.CaptureState();
                    FailIf(random.Rng1 != rom[0xff94] || random.Rng2 != rom[0xff95] || random.Calls - seed.Calls != rom.RandomCalls ||
                        !sounds.Requests.Where(cue => cue != SoundId.SndText).SequenceEqual(rom.Sounds),
                        $"Somaria throw hazard ${tile:x2}: sound or shared RNG order differs.");
                });
            for (int repeat = 0; repeat < 2; repeat++)
            {
                EquipSomariaMotionItem(rom, TreasureId.CaneOfSomaria, primary);
                Step(1, held: button, pressed: button); Step(24);
                EquipSomariaMotionItem(rom, TreasureId.Bracelet, primary);
                Step(12, 8); Step(1, held: button, pressed: button); Step(13, held: button);
                FailIf(rom[0xcc5a] != 0x83 || _currentRoom.IsSolid(_player.Position),
                    "Hazard throw requires a completed collision-reachable lift.");
                // Supply only the terrain ahead of the reachable carrier;
                // both gameplay loops still own movement, flight and contact.
                var changed = Enumerable.Range(1, _currentRoom.HeightInTiles - 2)
                    .SelectMany(y => Enumerable.Range(1, _currentRoom.WidthInTiles - 2)
                        .Select(x => new Vector2(x * 16 + 8, y * 16 + 8)))
                    .Where(point => point.X >= _player.Position.X + 16 &&
                        System.Math.Abs(point.Y - _player.Position.Y) <= 24 &&
                        _currentRoom.GetTerrainInfo(point).Collision == 0)
                    .Select(point => (Point: point, Tile: _currentRoom.GetMetatile(point))).ToArray();
                foreach (var cell in changed)
                {
                    _currentRoom.SetPositionTileAndCollision(cell.Point, tile, 0, 0);
                    rom[0xcf00 + _currentRoom.GetPackedPosition(cell.Point)] = tile;
                }
                Step(1); Step(1, 8, button, button);
                int updates = 0;
                while (rom.Blocks.Length != 0 && updates++ < 60) Step(1);
                FailIf(rom.Blocks.Length != 0 || updates >= 60,
                    $"Thrown Somaria must retire on native hazard ${tile:x2} contact.");
                FailIf(tile != 0xf3 && !sounds.Requests.Contains(SoundId.SndSplash),
                    $"Thrown Somaria hazard ${tile:x2} must publish its source effect cue.");
                foreach (var cell in changed)
                {
                    _currentRoom.SetPositionTileAndCollision(cell.Point, cell.Tile, 0, 0);
                    rom[0xcf00 + _currentRoom.GetPackedPosition(cell.Point)] = cell.Tile;
                }
                Step(20);
                _player.Face(Vector2I.Right); rom[0xd008] = 1;
            }
        }
    }
}
