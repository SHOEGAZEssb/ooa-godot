using Godot;
using System;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateAncientTombEntrance()
    {
        CompareAncientTombEntranceRom();
        var data = new DungeonMechanicDatabase();
        var record = data.GetRoomRecords(1, 0x8c).Single();
        int[] positions = [0x04, 0x05, 0x14, 0x15];
        int[] tiles = [0x30, 0x32, 0x3a, 0x3a];
        static Vector2 Point(int packed) => new((packed & 15) * 16 + 8, (packed >> 4) * 16 + 8);
        FailIf(record is not { Id: 0xdc, SubId: 0x0e, PackedPosition: 0x34, Order: 0 } ||
            data.AncientTombGraveTile != 0x02 || data.AncientTombSolveWait != 45 ||
            !data.TilePattern(0xdc, 0x0e).Select(row => row.PackedPosition).SequenceEqual(positions) ||
            !data.TilePattern(0xdc, 0x0e).Select(row => row.Tile).SequenceEqual(tiles),
            "Room 1:8c $dc:$0e lost its source placement, grave $02, 45-update wait or ordered tile writes.");

        foreach (bool batch in new[] { false, true })
        {
            void Step(int count = 1, Vector2 movement = default) =>
                StepGameplayUpdates(count, movement, [], [], batched: batch);
            AncientTombEntranceRoomEntity Enter()
            {
                _saveData.SetRoomFlag(1, 0x8c, 0x80, false);
                LoadValidationRoom(1, 0x8c);
                _player.WarpTo(Point(0x25));
                FailIf(_currentRoom.IsSolid(_player.Position) || _currentRoom.GetMetatile(Point(0x35)) != 0xd3 ||
                    _currentRoom.GetMetatile(Point(0x34)) != 0x3a,
                    "1:8c must start on clear ground north of grave $d3, with empty target $34.");
                var entrance = _entities.Entities<AncientTombEntranceRoomEntity>().Single();
                FailIf(entrance.State != 0 || entrance.Visible || _entities.InteractionSlot(entrance) != 2,
                    "$dc:$0e must retain state0 and first dynamic interaction slot $02.");
                Step();
                FailIf(entrance.State != 1 || entrance.Counter != 0 || _entities.PlayerUpdatesFrozen,
                    "$dc:$0e state0 must only check room flag $80 and advance state.");
                return entrance;
            }
            void PushGrave(AncientTombEntranceRoomEntity entrance, int? freePuffSlots = null)
            {
                // Approach through the room's actual eastern half-tile aisle.
                Step(10, Vector2.Right);
                Step(13, Vector2.Down);
                FailIf(_currentRoom.IsSolid(_player.Position),
                    $"1:8c grave approach entered solid geometry at {_player.Position}.");
                bool filled = false;
                for (int i = 0; i < 100 && entrance.State != 2; i++)
                {
                    if (!filled && freePuffSlots.HasValue && _pushBlocks.Active && _pushBlocks.BlockTopLeft.X <= 65)
                    {
                        // Fill immediately before reserved $14 completes. The
                        // earlier logical allocation keeps these slots live
                        // until the placed $dc controller attempts its puffs.
                        for (int slot = 0; slot < 13 - freePuffSlots.Value; slot++)
                            _entities.Spawn<PuzzlePuffEffect>(new PuzzlePuffSpawn(new(200, 104), SoundId.MusNone));
                        filled = true;
                    }
                    Step(1, Vector2.Left);
                }
                FailIf(freePuffSlots.HasValue && !filled, "1:8c puff exhaustion setup missed the grave's final movement update.");
                FailIf(entrance.State != 2 || entrance.Counter != 45 ||
                    _currentRoom.GetMetatile(Point(0x34)) != 0x02 || _currentRoom.GetMetatile(Point(0x35)) != 0x3a,
                    $"1:8c real grave push failed to trigger $dc:$0e: state={entrance.State}, counter={entrance.Counter}, Link={_player.Position}, target=${_currentRoom.GetMetatile(Point(0x34)):x2}.");
            }

            var entrance = Enter();
            Step(8);
            FailIf(entrance.State != 1 || _saveData.HasRoomFlag(1, 0x8c, 0x80),
                "$dc:$0e triggered without a stationary grave at $34.");
            _sound.ClearPlayRequestAudit();
            PushGrave(entrance);
            var puffs = _entities.Entities<PuzzlePuffEffect>();
            FailIf(positions.Where((p, i) => _currentRoom.GetMetatile(Point(p)) != tiles[i]).Any() ||
                puffs.Count != 4 || !puffs.Select(p => p.Position).SequenceEqual(positions.Select(Point)) ||
                _runtimeState.ReadWramByte(AgesWramAddress.wDisabledObjects) != 0x81 ||
                _runtimeState.ReadWramByte(AgesWramAddress.wMenuDisabled) != 0x81 ||
                !_entities.PlayerUpdatesFrozen || !_entities.PlayerMenusDisabled || !_entities.WarpTilesDisabled ||
                _saveData.HasRoomFlag(1, 0x8c, 0x80) || _sound.PlayRequestsFor(SoundId.SndSolvePuzzle) != 0,
                "$dc:$0e trigger must open four tiles, allocate ordered puffs and write both $81 masks before flag/sound.");
            Vector2 held = _player.Position;
            var textSource = _entities.TextActiveSource;
            try
            {
                _entities.TextActiveSource = () => true;
                Step(3);
                FailIf(entrance.Counter != 45, "Initialized $dc:$0e must pause its counter during text.");
            }
            finally { _entities.TextActiveSource = textSource; }
            Step(44, Vector2.Down);
            FailIf(entrance.Counter != 1 || _player.Position != held || _saveData.HasRoomFlag(1, 0x8c, 0x80) ||
                _sound.PlayRequestsFor(SoundId.SndSolvePuzzle) != 0 || !_entities.PlayerUpdatesFrozen,
                "$dc:$0e must hold Link through delay update44 without flag $80 or solve sound.");
            Step(1, Vector2.Down);
            FailIf(!entrance.Finished || _entities.Entities<AncientTombEntranceRoomEntity>().Count != 0 ||
                !_saveData.HasRoomFlag(1, 0x8c, 0x80) || _sound.PlayRequestsFor(SoundId.SndSolvePuzzle) != 1 ||
                _runtimeState.ReadWramByte(AgesWramAddress.wDisabledObjects) != 0 ||
                _runtimeState.ReadWramByte(AgesWramAddress.wMenuDisabled) != 0 || _player.Position != held,
                "$dc:$0e update45 must chime, persist flag $80, clear both masks and delete after Link's pass.");
            Step(4, Vector2.Down);
            FailIf(_player.Position == held, "Link must resume on the update after $dc:$0e completion.");
            Step(30, Vector2.Up);
            Step(30, Vector2.Left);
            FailIf(_sound.PlayRequestsFor(SoundId.SndSolvePuzzle) != 1 || _entities.PlayerUpdatesFrozen,
                "Repeated grave contact restarted the completed 1:8c puzzle.");
            FailIf(!OracleSaveData.TryDeserialize(_saveData.Serialize(), out var restored) ||
                !restored!.HasRoomFlag(1, 0x8c, 0x80), "1:8c flag $80 did not survive explicit save serialization.");
            LoadValidationRoom(1, 0x8c);
            _sound.ClearPlayRequestAudit();
            Step(2);
            FailIf(_entities.Entities<AncientTombEntranceRoomEntity>().Count != 0 ||
                positions.Where((p, i) => _currentRoom.GetMetatile(Point(p)) != tiles[i]).Any() ||
                _currentRoom.GetMetatile(Point(0x34)) != 0x02 || _currentRoom.GetMetatile(Point(0x35)) != 0x3a ||
                _sound.PlayRequestsFor(SoundId.SndSolvePuzzle) != 0,
                "Completed 1:8c re-entry must restore all six source tiles and delete $dc:$0e without replay.");

            entrance = Enter();
            PushGrave(entrance);
            Step(10);
            LoadValidationRoom(1, 0x8b);
            FailIf(_saveData.HasRoomFlag(1, 0x8c, 0x80) || _entities.PlayerUpdatesFrozen || _entities.PlayerMenusDisabled,
                "Cancelling $dc:$0e must release both masks without persisting unfinished flag $80.");
            entrance = Enter();
            PushGrave(entrance);
            Step(45);
            FailIf(!_saveData.HasRoomFlag(1, 0x8c, 0x80), "Cancelled 1:8c must allow a fresh grave push and completion.");

            _saveData.SetRoomFlag(1, 0x8c, 0x80, false);
            LoadValidationRoom(1, 0x8b);
            _player.WarpTo(new(159, 72), recordSafe: false);
            _transitions.BeginScroll(_player, Vector2I.Right, 0x8c);
            entrance = _entities.Entities<AncientTombEntranceRoomEntity>().Single();
            FailIf(!IsTransitioning || entrance.State != 1 || entrance.Visible,
                "Scrolling into 1:8c must admit only $dc:$0e state0 and keep its controller hidden.");
            // Stage a satisfied watched tile after preload so a running state1
            // would expose an erroneous destination update during scrolling.
            _rooms.TrySetTile(0x34, 0x02);
            Step(8);
            FailIf(!IsTransitioning || entrance.State != 1 || entrance.Counter != 0 ||
                _runtimeState.ReadWramByte(AgesWramAddress.wDisabledObjects) != 0 || _saveData.HasRoomFlag(1, 0x8c, 0x80),
                "Destination $dc:$0e must remain frozen throughout scrolling even with its grave gate satisfied.");
            _rooms.TrySetTile(0x34, 0x3a);
            for (int i = 0; i < 90 && IsTransitioning; i++) Step();
            FailIf(IsTransitioning || _rooms.ActiveGroup != 1 || _currentRoom.Id != 0x8c || entrance.State != 1,
                "1:8c scrolling must preserve the controller's first live state1 update.");

            foreach (int freeSlots in new[] { 0, 1, 2, 3 })
            {
                entrance = Enter();
                PushGrave(entrance, freeSlots);
                var created = _entities.Entities<PuzzlePuffEffect>().Where(puff => puff.Position.X < 160).ToArray();
                FailIf(created.Length != freeSlots || !created.Select(puff => puff.Position).SequenceEqual(positions.Take(freeSlots).Select(Point)) ||
                    positions.Where((p, i) => _currentRoom.GetMetatile(Point(p)) != tiles[i]).Any(),
                    $"1:8c {freeSlots} free slots must create only the first available puffs after all four tile writes.");
                _sound.ClearPlayRequestAudit();
                Step(45);
                FailIf(!entrance.Finished || !_saveData.HasRoomFlag(1, 0x8c, 0x80) ||
                    _sound.PlayRequestsFor(SoundId.SndSolvePuzzle) != 1 || _entities.PlayerUpdatesFrozen,
                    $"1:8c puff allocation failures ({freeSlots} free slots) must not retry or prevent completion.");
            }
        }
    }
}
