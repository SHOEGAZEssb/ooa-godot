using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateBraceletLeverRom()
    {
        int fixture = 0;
        foreach (int room in new[] { 0x83, 0x84, 0x7f })
        foreach (bool primary in new[] { false, true })
        foreach (bool batched in RomHostSchedules(fixture++))
        {
            ReinitializeGameplayForValidation(); LoadValidationRoom(4, room); _entities.Clear();
            var records = new SkullDungeonDatabase().GetRoomRecords(4, room);
            DungeonObjectRecord record = records.Single(row => row.Id == 0x61);
            LeverProfile profile = new LeverDatabase().Profile(record.SubId);
            var lever = new LeverRoomEntity(profile.ToNpcRecord(record),
                new LeverState(_runtimeState, 0xccab), profile.Behavior, _sound.PlaySound);
            _entities.AddEntity(lever);
            _entities.AddEntity(new LeverConnectionRoomEntity(profile.ToNpcRecord(record, connection: true),
                profile.Connections, lever, profile.Behavior.ConnectionStep));
            DungeonObjectRecord lavaRecord = records.Single(row => row.Id == 0xd8);
            var lava = new LeverLavaFillerRoomEntity(_currentRoom, new LeverLavaDatabase().Script(lavaRecord.SubId),
                new LeverState(_runtimeState, 0xccab), _random, _sound.PlaySound,
                (position, tile) => _rooms.TrySetTile(position, tile));
            _entities.AddEntity(lava);
            _inventory.GiveTreasure(TreasureId.Bracelet, primary ? 2 : 1);
            _inventory.GiveTreasure(TreasureId.Shield, 1);
            _inventory.EquipA(primary ? TreasureId.Bracelet : TreasureId.Shield);
            _inventory.EquipB(primary ? TreasureId.Shield : TreasureId.Bracelet);
            int sign = (record.SubId & 1) == 0 ? 1 : -1;
            int direction = sign == 1 ? 0 : 2;
            Vector2 start = new(record.X, record.Y + sign * 32);
            _player.WarpTo(start); _player.Face(sign == 1 ? Vector2I.Up : Vector2I.Down);
            var seed = _random.CaptureState();
            var rom = new SomariaRom(_saveData, seed, _currentRoom, direction, (int)start.X, (int)start.Y);
            rom.InitializeLinkGameplay();
            const int slot = 0xd240;
            rom[slot] = 1; rom[slot + 1] = 0x61; rom[slot + 2] = (byte)record.SubId;
            rom[slot + 0x0b] = (byte)record.Y; rom[slot + 0x0d] = (byte)record.X;
            const int lavaSlot = 0xd340;
            rom[lavaSlot] = 1; rom[lavaSlot + 1] = 0xd8; rom[lavaSlot + 2] = (byte)lavaRecord.SubId;
            var sounds = _sound.AttachPlayRequestAudit();
            int previous = 0, update = 0, button = primary ? 1 : 2;
            bool shieldHeld = false;
            void Step(int count = 1, bool held = false, int angle = 0xff)
            {
                Vector2 movement = angle == 0xff ? Vector2.Zero : OracleObjectMovement.Shared.Direction(angle);
                int buttons = (held ? button : 0) | (shieldHeld ? primary ? 2 : 1 : 0) |
                    (angle == 0 ? 0x40 : angle == 16 ? 0x80 : 0);
                int edge = buttons & ~previous; previous = buttons;
                StepGameplayUpdates(count, movement, MenuRomActions(buttons), MenuRomActions(edge), batched, () =>
                {
                    rom.UpdateGameplay(edge, buttons, angle, _entities.FrameCounter); edge = 0;
                    rom.AdvanceTileGraphics();
                    string context = $"Lever $4:${room:x2} A={primary} batch={batched} update={++update}";
                    FailIf(_player.PrecisePosition != new Vector2(rom.Word(0xd00c) / 256f, rom.Word(0xd00a) / 256f) ||
                        lever.Position != new Vector2(rom[slot + 0x0d], rom[slot + 0x0b]) || lever.PullDistance != rom[0xccab],
                        context + $": Link/lever/pull offset differs: runtime={_player.PrecisePosition}/{lever.Position}/${lever.PullDistance:x2}, native={rom.Word(0xd00c) / 256f},{rom.Word(0xd00a) / 256f}/{rom[slot + 0x0d]},{rom[slot + 0x0b]}/${rom[0xccab]:x2}.");
                    FailIf(lever.Grabbed != (rom[slot + 4] == 2 && rom[slot + 5] < 2) ||
                        _player.PatchCollisionsEnabled != ((rom[0xd024] & 0x80) != 0),
                        context + $": grabbed/collision runtime={lever.Grabbed}/{_player.BraceletLiftCollisionsDisabled}/{_bracelet.State}, native=${rom[slot + 4]:x2}/${rom[slot + 5]:x2}/collision=${rom[0xd024]:x2}/grab=${rom[0xcc5a]:x2}, parent=${rom[0xd201]:x2}/${rom[0xd204]:x2}.");
                    FailIf(lava.State != rom[lavaSlot + 4] ||
                        lava.State is 2 or 3 or 4 && lava.Counter != rom[lavaSlot + 6],
                        context + $": lava state/counter runtime={lava.State}/{lava.Counter}, native={rom[lavaSlot + 4]}/{rom[lavaSlot + 6]}.");
                    FailIf(_rooms.PendingTileGraphics != ((rom[0xcce0] - rom[0xccdf]) & 31),
                        context + ": queued tile graphics count differs.");
                    FailIf(_player.IsUsingShield != (rom[0xcc6f] != 0), context + ": Shield grab interruption differs.");
                    for (int packed = 0; packed < _currentRoom.Layout.Length; packed++)
                        if (_currentRoom.Layout[packed] != rom[0xcf00 + packed] ||
                            _currentRoom.GetUnderlyingStorageMetatile(packed) != rom.Underlying(packed))
                            FailIf(true, context + $": lava tile ${packed:x2} runtime=${_currentRoom.Layout[packed]:x2}/${_currentRoom.GetUnderlyingStorageMetatile(packed):x2}, native=${rom[0xcf00 + packed]:x2}/${rom.Underlying(packed):x2}, state={lava.State}, cursor={lava.Cursor}.");
                    FailIf(!sounds.Requests.Where(cue => cue != SoundId.SndText).SequenceEqual(rom.Sounds),
                        context + $": cue order runtime=[{string.Join(',', sounds.Requests.Where(cue => cue != SoundId.SndText).Select(cue => cue.ToString("x2")))}], native=[{string.Join(',', rom.Sounds.Select(cue => cue.ToString("x2")))}].");
                    var random = _random.CaptureState();
                    FailIf(random.Rng1 != rom[0xff94] || random.Rng2 != rom[0xff95] ||
                        random.Calls - seed.Calls != rom.RandomCalls, context + ": shared RNG differs.");
                });
            }
            Step(); Step(24, angle: direction * 8);
            FailIf(_currentRoom.IsSolid(_player.Position), "Lever approach must stop on the room's actual floor.");
            for (int repeat = 0; repeat < 2; repeat++)
            {
                shieldHeld = repeat == 0; Step(2);
                Step(1, true); Step(1, true);
                FailIf(!lever.Grabbed, "Collision-limited equipped Bracelet approach did not grab INTERAC_LEVER $61.");
                Step(8, true, (direction * 8) ^ 16);
                if (repeat == 1)
                {
                    shieldHeld = true; Step(2, true);
                    FailIf(rom[0xd500] != 0 || _player.IsUsingShield,
                        "Lever grab must reject new Shield allocation while retaining existing parents.");
                }
                _dialogue.ShowMessage("Lever pause.", _player.Position.Y); rom[0xcba0] = 1;
                Step(3, true, (direction * 8) ^ 16); _dialogue.Close(); rom[0xcba0] = 0;
                Step(4, true); Step(4, true, (direction * 8) ^ 16);
                if (repeat == 1)
                {
                    int remaining = 400;
                    while ((rom[0xccab] & 0x80) == 0 && remaining-- > 0) Step(1, true, (direction * 8) ^ 16);
                    FailIf(lever.PullDistance != 0xc0, "Native lever must publish full length $40 with bit7.");
                    Step(4, true, (direction * 8) ^ 16);
                    _dialogue.ShowMessage("Lava pause.", _player.Position.Y); rom[0xcba0] = 1;
                    Step(3, true); _dialogue.Close(); rom[0xcba0] = 0;
                    int dry = 300;
                    while (rom[lavaSlot + 4] == 2 && dry-- > 0) Step(1, true);
                    FailIf(lava.State != 3, "Fully pulled lever must finish the native drying script.");
                }
                Step();
                int retract = 260;
                while ((rom[0xccab] & 0x7f) != 0 && retract-- > 0) Step();
                FailIf(lever.PullDistance != 0, "Released lever must retract to its original base.");
                FailIf(!_player.IsUsingShield, "Released lever must restore held Shield on the eligible update.");
                if (repeat == 1)
                {
                    int refill = 300;
                    while (rom[lavaSlot + 4] == 4 && refill-- > 0) Step();
                    FailIf(lava.State != 1, "Released lever must finish ordered native lava refill.");
                }
                Step(24, angle: direction * 8);
            }
        }
        GD.Print("Validated native Bracelet levers/lava in $4:$83/$84/$7f: actual collision approach, pull/rest/dialogue, partial/full extension, release/regrab, all ordered drying/refill tile buffers, contact gates, sounds/RNG and split/batched gameplay.");
    }
}
