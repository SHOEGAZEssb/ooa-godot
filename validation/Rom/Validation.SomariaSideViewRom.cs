using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateSomariaSideViewRom()
    {
        int fixture = 0;
        foreach (int direction in new[] { 1, 3 })
        foreach (byte supportMask in new byte[] { 0x0f, 0, 3, 0x0c })
        foreach (bool batched in RomHostSchedules(fixture++))
        {
            ReinitializeGameplayForValidation(); LoadValidationRoom(6, 0x93); _entities.Clear();
            bool primary = (fixture & 1) != 0;
            _inventory.GiveTreasure(TreasureId.CaneOfSomaria, 1);
            _inventory.GiveTreasure(TreasureId.Bracelet, 1);
            _inventory.EquipA(primary ? TreasureId.CaneOfSomaria : 0);
            _inventory.EquipB(primary ? 0 : TreasureId.CaneOfSomaria);
            Vector2 forward = OracleObjectMath.StrictCardinalVector(direction * 8);
            Vector2 start = Enumerable.Range(1, _currentRoom.HeightInTiles - 2)
                .SelectMany(y => Enumerable.Range(2, _currentRoom.WidthInTiles - 4)
                    .Select(x => new Vector2(x * 16 + 8, y * 16 + 8)))
                .First(point => (_playerWorld.GetSideScrollTerrain(point).CombinedType &
                    (SideScrollTileType.Ladder | SideScrollTileType.LadderTop)) == 0 &&
                    !_collision.Collides(point) && !_collision.Collides(point + forward * 16) &&
                    _currentRoom.GetTerrainInfo(point + new Vector2(0, 16)).Collision == 0x0f &&
                    _currentRoom.GetTerrainInfo(point + forward * 16 + new Vector2(0, 16)).Collision == 0x0f);
            _player.WarpTo(start); StepGameplayUpdates(20, Vector2.Zero);
            _player.Face((Vector2I)forward);
            FailIf(!_player.IsGroundedForFloorButton || _currentRoom.IsSolid(_player.Position),
                "Cane side-view fixture must stand on room $6:$93's actual floor.");
            var seed = _random.CaptureState();
            var rom = new SomariaRom(_saveData, seed, _currentRoom, direction,
                (int)_player.Position.X, (int)_player.Position.Y);
            rom.Word(0xd00a, (int)(_player.PrecisePosition.Y * 256));
            rom.Word(0xd00c, (int)(_player.PrecisePosition.X * 256)); rom.InitializeLinkGameplay();
            rom.InitializeLinkWalkingAnimation();
            var sounds = _sound.AttachPlayRequestAudit();
            int button = primary ? 1 : 2, update = 0;
            Vector2 target = new SomariaPlacementDatabase().Align(_player.Position + forward * 19);
            Vector2 support = target + new Vector2(0, 16);
            byte supportTile = _currentRoom.GetMetatile(support);
            void SetSupport(byte mask)
            {
                _currentRoom.SetPositionTileAndCollision(support, supportTile, mask, 0);
                rom[0xce00 + _currentRoom.GetPackedPosition(support)] = mask;
            }
            void Motion(int count = 1, int angle = 0xff, int held = 0, int pressed = 0)
            {
                StepSomariaMotionRom(rom, count, batched, angle, held, pressed, () =>
                {
                    string context = $"Cane side-view dir={direction} support=${supportMask:x2} A={primary} update={++update}";
                    FailIf(!sounds.Requests.Where(cue => cue != SoundId.SndText).SequenceEqual(rom.Sounds),
                        context + ": full cue order differs.");
                    var random = _random.CaptureState();
                    FailIf(random.Rng1 != rom[0xff94] || random.Rng2 != rom[0xff95] || random.Calls - seed.Calls != rom.RandomCalls,
                        context + ": shared RNG differs.");
                });
            }
            void Step(int count = 1, bool press = false) => Motion(count, held: press ? button : 0, pressed: press ? button : 0);
            for (int repeat = 0; repeat < 2; repeat++)
            {
                SetSupport(supportMask); byte original = _currentRoom.GetMetatile(target);
                Step(1, true); Step(14);
                FailIf(rom.Blocks.Length != 1 || rom[rom.Blocks[0] + 4] != 1 || rom[rom.Blocks[0] + 0xf] != 0,
                    "Side-view Cane must create one phase-in block with merged Z on update15.");
                _dialogue.ShowMessage("Side-view block pause.", _player.Position.Y); rom[0xcba0] = 1;
                Step(3); _dialogue.Close(); rom[0xcba0] = 0;
                Step(9);
                FailIf(_entities.Entities<SomariaBlock>().Count != (supportMask == 0x0f ? 1 : 0) || _player.IsUsingSomaria,
                    "Only full $0f support may retain a side-view block after phase-in and parent completion.");
                if (supportMask == 0x0f)
                {
                    FailIf(_currentRoom.GetMetatile(target) != 0xda, "Native side-view block must publish tile $da.");
                    SetSupport(0); Step();
                }
                FailIf(_entities.Entities<SomariaBlock>().Count != 0 || _currentRoom.GetMetatile(target) != original,
                    "Unsupported side-view block must retire and restore its underlying tile.");
                SetSupport(0x0f); Step(20);
            }
            if (supportMask == 0x0f)
            foreach (string release in new[] { "drop", "throw", "water" })
            {
                EquipSomariaMotionItem(rom, TreasureId.CaneOfSomaria, primary);
                Step(1, true); Step(24);
                EquipSomariaMotionItem(rom, TreasureId.Bracelet, primary);
                Motion(12, direction * 8); Step(1, true); Motion(13, held: button);
                FailIf(rom[0xcc5a] != 0x83 || _currentRoom.IsSolid(_player.Position),
                    "Side-view Somaria lift must complete after approaching through the source floor.");
                var changed = Enumerable.Range(1, _currentRoom.HeightInTiles - 2)
                    .SelectMany(y => Enumerable.Range(1, _currentRoom.WidthInTiles - 2)
                        .Select(x => new Vector2(x * 16 + 8, y * 16 + 8)))
                    .Where(point => release == "water" && forward.Dot(point - _player.Position) >= 16 &&
                        Math.Abs(point.Y - _player.Position.Y) <= 32 && _currentRoom.GetTerrainInfo(point).Collision == 0)
                    .Select(point => (Point: point, Tile: _currentRoom.GetMetatile(point))).ToArray();
                foreach (var cell in changed)
                {
                    _currentRoom.SetPositionTileAndCollision(cell.Point, 0x1a, 0, 0);
                    rom[0xcf00 + _currentRoom.GetPackedPosition(cell.Point)] = 0x1a;
                }
                int splashBefore = sounds.Requests.Count(cue => cue == SoundId.SndSplash);
                Step(); Motion(1, release == "drop" ? 0xff : direction * 8, button, button);
                int flight = 0;
                while (rom.Blocks.Length != 0 && flight++ < 160) Step();
                FailIf(rom.Blocks.Length != 0 || flight > 160 || _player.IsCarryingObject,
                    $"Side-view Somaria {release} dir={direction} must retire on the native floor: updates={flight}, native=[{string.Join(',', rom.Blocks.Select(slot => $"{rom[slot + 0xd]},{rom[slot + 0xb]}"))}], Link={_player.Position}, carry={_player.IsCarryingObject}.");
                FailIf(release == "water" && sounds.Requests.Count(cue => cue == SoundId.SndSplash) == splashBefore,
                    "Native side-view water throw must actually enter water and emit its splash before floor contact.");
                foreach (var cell in changed)
                {
                    _currentRoom.SetPositionTileAndCollision(cell.Point, cell.Tile, 0, 0);
                    rom[0xcf00 + _currentRoom.GetPackedPosition(cell.Point)] = cell.Tile;
                }
                Step(20);
                _player.Face((Vector2I)forward); rom[0xd008] = (byte)direction;
            }
        }
        GD.Print("Validated native side-view Cane phase-in, exact $0f support versus empty/partial masks, merged Z, text freeze, support loss/restoration, reachable lifting/drop/throw, water entry/alternate gravity/cap/splash and repeated A/B/left/right use through split/batched gameplay.");
    }
}
