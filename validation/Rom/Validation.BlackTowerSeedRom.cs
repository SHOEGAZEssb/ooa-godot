using Godot;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateBlackTowerSeedRom()
    {
        foreach (bool batch in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            _inventory.GiveTreasure(TreasureId.MakuSeed, 0);
            LoadValidationRoom(4, 0xea);
            _inventory.EquipA(0); _inventory.EquipB(0);
            Vector2 floor = new(0x08, 0x88);
            FailIf(_collision.Collides(floor), "Black Tower $4:ea requires a reachable entrance floor.");
            _player.WarpTo(floor); _player.Face(Vector2I.Up);
            _transitions.ResetCamera();
            var random = _random.CaptureState();
            var rom = new SomariaRom(_saveData, random, _currentRoom, 0, (int)floor.X, (int)floor.Y)
                { CompanionDispatchEnabled = true };
            rom.InitializeLinkGameplay();
            rom[0xcd16] = 0; rom[0xcd17] = 0xd0;
            rom[0xffaa] = 48; rom[0xffac] = 0;
            rom[0xd240] = 1; rom[0xd241] = 0xc6;
            int update = 0, lastState = 0, lastSubstate = 0, essences = 0;
            bool comparedTiles = false;
            void Compare()
            {
                rom.AdvanceWarpPalette();
                rom.UpdateGameplay(0, 0, 0xff, ++update);
                rom.AdvanceCameraPosition();
                var actors = _entities.EntityAdapters<BlackTowerSeedRoomEntity>().ToArray();
                var slots = SomariaPrivate<Dictionary<IRoomEntity, int>>(_entities, "_interactionSlots");
                foreach (var actor in actors)
                {
                    int address = 0xd040 + slots[actor] * 256;
                    FailIf(rom[address] == 0 || rom[address + 1] != actor.Id || rom[address + 2] != actor.SubId ||
                        actor.State != rom[address + 4] || actor.Substate != rom[address + 5] ||
                        actor.Counter != rom[address + 6] || actor.Counter2 != rom[address + 7] ||
                        actor.ZHigh != unchecked((sbyte)rom[address + 15]) ||
                        actor.Id == 0xd7 && actor.PrecisePosition != new Vector2(rom.Word(address + 12) / 256f, rom.Word(address + 10) / 256f),
                        $"Black Tower INTERAC ${actor.Id:x2}:${actor.SubId:x2} update {update}: " +
                        $"port={actor.State}/{actor.Substate}/{actor.Counter}/{actor.Counter2}/{actor.PrecisePosition}, " +
                        $"ROM=${address:x4}:{rom[address + 4]}/{rom[address + 5]}/{rom[address + 6]}/{rom[address + 7]}/" +
                        $"{rom.Word(address + 12) / 256f},{rom.Word(address + 10) / 256f}.");
                    if (actor is { Id: 0x84, SubId: 4 })
                        FailIf(((NpcCharacter)actor.Node).GraphicsSourceOffset != 0x1c00 ||
                            ((NpcCharacter)actor.Node).CurrentAnimationTextureSize.X < 48,
                            "Black Tower $84:$04 must use spr_link's $1c00 sparkle slice and the full source OAM.");
                    if (actor.Id == 0xd7 && actor.SubId == 0) { lastState = actor.State; lastSubstate = actor.Substate; }
                }
                essences = System.Math.Max(essences, actors.Count(actor => actor.Id == 0xd7 && actor.SubId != 0));
                FailIf(_player.PrecisePosition != new Vector2(rom.Word(0xd00c) / 256f, rom.Word(0xd00a) / 256f) ||
                    _entities.RuntimeState.ReadWramByte(WramAddress.wDisabledObjects) != rom[0xcc8a] ||
                    _entities.RuntimeState.ReadWramByte(WramAddress.wMenuDisabled) != rom[0xcc02] ||
                    _entities.RuntimeState.ReadWramByte(0xcfc0) != rom[0xcfc0] ||
                    _entities.RuntimeState.ReadWramByte(0xcfc1) != rom[0xcfc1],
                    $"Black Tower $4:ea Link/control/signal update {update}: Link={_player.PrecisePosition}/" +
                    $"{rom.Word(0xd00c) / 256f},{rom.Word(0xd00a) / 256f}.");
                FailIf(_saveData.HasRoomFlag(4, 0xea, 0x40) != ((rom[0xc900 + 0xea] & 0x40) != 0),
                    $"Black Tower $4:ea completion flag differs on update {update}.");
                if (!comparedTiles && lastState == 4 && lastSubstate == 1)
                {
                    comparedTiles = true;
                    byte[] positions = [0x33, 0x34, 0x35, 0x43, 0x44, 0x45, 0x53, 0x54, 0x55,
                        0x39, 0x3a, 0x3b, 0x49, 0x4a, 0x4b, 0x59, 0x5a, 0x5b];
                    byte[] expected = [0xa3, 0xa3, 0xa3, 0xb7, 0xb7, 0xb7, 0x88, 0x88, 0x88,
                        0xa3, 0xa3, 0xa3, 0xb7, 0xb7, 0xb7, 0x88, 0x88, 0x88];
                    for (int index = 0; index < positions.Length; index++)
                        FailIf(_currentRoom.GetPackedStorageMetatile(positions[index]) != expected[index] ||
                            rom[0xcf00 + positions[index]] != expected[index],
                            $"Black Tower $d7 must remove false-door tile ${positions[index]:x2} with tile ${expected[index]:x2}.");
                }
            }
            for (int bound = 0; bound < 1600 && !_saveData.HasRoomFlag(4, 0xea, 0x40); bound += 4)
                StepGameplayUpdates(4, Vector2.Zero, batched: batch, afterUpdate: Compare);
            FailIf(!_saveData.HasRoomFlag(4, 0xea, 0x40) || !comparedTiles || essences != 8 || lastState != 4 || lastSubstate != 3,
                $"Black Tower $4:ea seed reveal failed to finish all eight essences: {lastState}/{lastSubstate}, {essences}.");
            StepGameplayUpdates(4, Vector2.Zero, batched: batch, afterUpdate: Compare);
            FailIf(_player.CutsceneControlled || _entities.PlayerMenusDisabled ||
                _currentRoom.GetPackedStorageMetatile(0x47) != 0x44,
                "Black Tower $c6/$d7 must release Link and install the central staircase after completion.");
            LoadValidationRoom(4, 0xe9); LoadValidationRoom(4, 0xea);
            StepGameplayUpdates(2, Vector2.Zero);
            FailIf(_entities.EntityAdapters<BlackTowerSeedRoomEntity>().Any() ||
                _currentRoom.GetPackedStorageMetatile(0x44) != 0xb7 || _currentRoom.GetPackedStorageMetatile(0x4a) != 0xb7 ||
                _currentRoom.GetPackedStorageMetatile(0x47) != 0x44,
                "Black Tower $4:ea re-entry must preserve both false-door removals without replaying $d7.");
        }
    }

    private void ValidateBlackTowerSeedGates()
    {
        LoadValidationRoom(4, 0xea);
        StepGameplayUpdates(1, Vector2.Zero);
        FailIf(_entities.RuntimeState.ReadWramByte(WramAddress.wDisableWarps) != 0x44 ||
            new byte[] { 0x44, 0x47, 0x4a }.Any(position => _currentRoom.GetPackedStorageMetatile(position) != 0x44) ||
            _entities.EntityAdapters<BlackTowerSeedRoomEntity>().Any(),
            "Black Tower $c6 without treasure $36 must replace only door behavior, set wDisableWarps=$44 and delete.");
        _player.WarpTo(new(0x78, 0x58));
        FailIf(_collision.Collides(_player.Position), "Black Tower false-door fixture must start on the actual floor below $47.");
        for (int repeat = 0; repeat < 2; repeat++)
        {
            if (repeat != 0) StepGameplayUpdates(16, Vector2.Down);
            for (int wait = 0; !IsTransitioning && wait < 32; wait++) StepGameplayUpdates(1, Vector2.Up);
            FailIf(!IsTransitioning, "Black Tower without $36 must reach the original door through room geometry.");
            Warp warp = SomariaPrivate<Warp>(_transitions, "_pendingWarp");
            FailIf(warp.DestinationGroup != 4 || warp.DestinationRoom != 0xea || warp.DestinationPosition != 0x47 ||
                warp.DestinationTransition != 0,
                "wDisableWarps must select setWarpDestDefault: $4:ea/$47, transition $00, rather than the source's $4:d0.");
            for (int wait = 0; IsTransitioning && wait < 200; wait++) StepGameplayUpdates(1, Vector2.Zero);
            FailIf(IsTransitioning || _rooms.ActiveGroup != 4 || _currentRoom.Id != 0xea,
                "The no-seed Black Tower door must return to $4:ea on repeated reachable entry.");
        }
        LoadValidationRoom(4, 0xe9);
        FailIf(_entities.RuntimeState.ReadWramByte(WramAddress.wDisableWarps) != 0,
            "Leaving Black Tower $4:ea must release its wDisableWarps gate.");
        foreach (int cancelAt in new[] { 1, 180, 700, 1200 })
        {
            ReinitializeGameplayForValidation();
            _inventory.GiveTreasure(TreasureId.MakuSeed, 0);
            LoadValidationRoom(4, 0xea);
            _player.WarpTo(new(8, 0x88));
            StepGameplayUpdates(cancelAt, Vector2.Zero);
            _roomEvents.Get<BlackTowerSeedEvent>().Cancel();
            Vector2 stopped = _player.PrecisePosition;
            StepGameplayUpdates(1, Vector2.Zero);
            FailIf(_player.CutsceneControlled || _entities.PlayerMenusDisabled ||
                _player.PrecisePosition != stopped || _entities.EntityAdapters<BlackTowerSeedRoomEntity>().Any() ||
                _saveData.HasRoomFlag(4, 0xea, 0x40) || _transitions.NativePaletteFadeActive,
                $"Cancel Black Tower seed reveal update {cancelAt} retained control/fade or completed room $4:ea.");
        }
    }
}
