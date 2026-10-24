using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    // Keep each fixture at one mechanism's handoff; enemy combat and the
    // reused items have their own regressions. No dungeon traversal here.
    private void IsolateHerosCave(Func<IRoomEntity, bool> keep)
    {
        var active = SomariaPrivate<List<IRoomEntity>>(_entities, "_activeEntities");
        var free = typeof(RoomEntityManager).GetMethod("FreeEntity", BindingFlags.Instance | BindingFlags.NonPublic)!;
        foreach (var entity in active.Where(entity => !keep(entity)).ToArray())
        { active.Remove(entity); free.Invoke(_entities, [entity]); }
    }

    private static Vector2 CavePoint(int packed) => new((packed & 15) * 16 + 8, (packed >> 4) * 16 + 8);

    private void ValidateHerosCavePuzzles()
    {
        foreach (bool batch in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            LoadValidationRoom(4, 0xc2);
            IsolateHerosCave(entity => entity is ColoredCubeRoomEntity or HerosCavePuzzleRoomEntity);
            _player.WarpTo(CavePoint(0x11));
            void Step(int count = 1) => StepGameplayUpdates(count, Vector2.Zero, batched: batch);
            var cube = _entities.Entities<ColoredCubeRoomEntity>().Single();
            var puzzle = cube.ColoredCubePuzzleState;
            Step();
            FailIf(cube.Position != CavePoint(0x83) || cube.Orientation != 4 || puzzle.CubeColor != 2 ||
                _entities.Entities<HerosCavePuzzleRoomEntity>().Count != 4,
                "Hero's Cave $4:$c2 lost cube$19:$04 or its three sensors/stair controller.");
            int[] pads = [0x1c, 0x33, 0x85];
            for (int color = 0; color < 3; color++)
            {
                FailIf(_currentRoom.GetPackedStorageMetatile((byte)pads[color]) != 0xad + color,
                    "Hero's Cave original target colors/order changed.");
                // Stage only the cube's published output, then exercise the
                // real ordered interaction pass and its downstream tiles.
                puzzle.CubePosition = pads[color]; puzzle.CubeColor = (color + 1) % 3;
                Step();
                FailIf(_entities.ActiveTriggers != (1 << color) - 1, "Mismatched Hero's Cave cube color set a trigger.");
                puzzle.CubeColor = color;
                var text = _entities.TextActiveSource;
                try { _entities.TextActiveSource = () => true; Step(); }
                finally { _entities.TextActiveSource = text; }
                FailIf(_entities.ActiveTriggers != (1 << color) - 1, "Initialized Hero's Cave sensor ran under text.");
                Step();
                FailIf(_entities.ActiveTriggers != (1 << (color + 1)) - 1 ||
                    _currentRoom.GetPackedStorageMetatile((byte)pads[color]) != 0xa3 ||
                    _currentRoom.GetTerrainInfo(CavePoint(pads[color])).Collision != 0x0f,
                    $"Hero's Cave pad${pads[color]:x2} lost trigger/tile/solid-cube handoff.");
            }
            FailIf(!_saveData.HasRoomFlag(4, 0xc2, 0x80) || _currentRoom.GetPackedStorageMetatile(0x15) != 0x45,
                "Hero's Cave exact trigger$07 did not create stairs$45 and persist flag$80 in the same pass.");
            LoadValidationRoom(4, 0xc2); Step();
            FailIf(_entities.Entities<HerosCavePuzzleRoomEntity>().Count != 0 || _currentRoom.GetPackedStorageMetatile(0x15) != 0x45,
                "Completed Hero's Cave cube sensors/stairs did not resolve correctly on re-entry.");

            LoadValidationRoom(4, 0xcd);
            IsolateHerosCave(entity => entity is HerosCavePuzzleRoomEntity);
            _player.WarpTo(CavePoint(0x49)); Step();
            _currentRoom.SetPositionTileAndCollision(CavePoint(0x95), 0x2a, null, 0);
            _currentRoom.SetPositionTileAndCollision(CavePoint(0x5d), 0xa0, null, 0);
            Step();
            FailIf(_entities.Entities<GroundTreasurePickup>().Count != 0, "Hero's Cave statue key accepted only one statue.");
            _currentRoom.SetPositionTileAndCollision(CavePoint(0x5d), 0x2a, null, 0);
            Step();
            FailIf(_entities.Entities<GroundTreasurePickup>().Single().Position != CavePoint(0x49) ||
                _entities.Entities<HerosCavePuzzleRoomEntity>().Count != 0,
                "Hero's Cave statues$95/$5d did not create the falling Small Key at $49.");
            LoadValidationRoom(4, 0xcd);
            _saveData.SetRoomFlag(4, 0xcd, OracleSaveData.RoomFlagItem); Step();
            FailIf(_entities.Entities<HerosCavePuzzleRoomEntity>().Count != 0, "Collected Hero's Cave statue key repeated.");

            LoadValidationRoom(4, 0xcf);
            IsolateHerosCave(entity => entity is DungeonPuzzleChestRoomEntity);
            _player.WarpTo(CavePoint(0x57));
            int[] cells = [0x4a, 0x4b, 0x4c, 0x5a, 0x5c, 0x6a, 0x6c];
            int[] tiles = [0x2c, 0x2c, 0x2c, 0x2d, 0x2d, 0x2e, 0x2e];
            var pattern = new DungeonChestPatternDatabase().Cells(0x90, 0x1a);
            FailIf(!pattern.Select(cell => cell.Position).SequenceEqual(cells) ||
                !pattern.Select(cell => cell.MinimumTile).SequenceEqual(tiles) ||
                !pattern.Select(cell => cell.MaximumTile).SequenceEqual(tiles),
                "Hero's Cave $90:$1a lost its seven source-ordered exact tile conditions.");
            for (int i = 0; i < cells.Length; i++)
                _currentRoom.SetPositionTileAndCollision(CavePoint(cells[i]), (byte)tiles[i], null, 0);
            _currentRoom.SetPositionTileAndCollision(CavePoint(0x6c), 0x2d, null, 0); Step();
            FailIf(_currentRoom.GetPackedStorageMetatile(0x57) == 0xf1, "Hero's Cave pattern accepted a wrong blue block.");
            _currentRoom.SetPositionTileAndCollision(CavePoint(0x6c), 0x2e, null, 0); Step();
            FailIf(_currentRoom.GetPackedStorageMetatile(0x57) != 0xf1 || _entities.Entities<DungeonPuzzleChestRoomEntity>().Count != 0,
                "Hero's Cave seven-cell pattern did not create its single chest.");
            Step(20);
            LoadValidationRoom(0, 0x60);
        }
    }

    private void ValidateHerosCaveSignals()
    {
        foreach (bool batch in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            void Step(int count = 1) => StepGameplayUpdates(count, Vector2.Zero, batched: batch);
            LoadValidationRoom(4, 0xc6);
            IsolateHerosCave(entity => entity is DungeonTriggerChestScriptRoomEntity);
            _player.WarpTo(CavePoint(0x77));
            _runtimeState.SetWramByte(0xcfc1, 0xa5); _runtimeState.SetWramByte(0xcfc2, 0x5a);
            Step();
            FailIf(_runtimeState.ReadWramByte(0xcfc1) != 0 || _runtimeState.ReadWramByte(0xcfc2) != 0,
                "Hero's Cave $20:$01 did not clear shared script scratch bytes.");
            foreach (int triggers in new[] { 0x07, 0x0e, 0x1f })
            {
                for (int bit = 0; bit < 5; bit++) _entities.SetTrigger(bit, (triggers & (1 << bit)) != 0);
                Step();
                FailIf(_currentRoom.GetPackedStorageMetatile(0x47) == 0xf1, "Hero's Cave chest requires exact trigger$0f.");
            }
            _entities.SetTrigger(4, false); Step(); // checkmemoryeq yields.
            Step(3); // sound, puff, wait15.
            Step(14);
            FailIf(_currentRoom.GetPackedStorageMetatile(0x47) == 0xf1, "Hero's Cave chest appeared before wait15 expired.");
            Step();
            FailIf(_currentRoom.GetPackedStorageMetatile(0x47) != 0xf1, "Hero's Cave chest missed its exact wait15 boundary.");

            LoadValidationRoom(4, 0xc9);
            IsolateHerosCave(entity => entity is DungeonSignalScriptRoomEntity);
            _player.WarpTo(CavePoint(0x86)); Step();
            _entities.SetTrigger(0, true); Step();
            FailIf(_saveData.HasRoomFlag(4, 0xc9, 0x40), "Hero's Cave bridge accepted trigger bit0 instead of bit1.");
            _entities.SetTrigger(1, true); Step();
            FailIf(!_saveData.HasRoomFlag(4, 0xc9, 0x40) || _entities.Entities<BridgeSpawnerRoomEntity>().Count != 1,
                "Hero's Cave bridge must publish flag$40 before its one PART allocation.");
            Step(64);
            foreach (int position in new[] { 0x27, 0x28, 0x29, 0x2a })
                FailIf(_currentRoom.GetPackedStorageMetatile((byte)position) != 0x6d, "Hero's Cave bridge did not finish four westward tiles.");
            LoadValidationRoom(4, 0xc9); Step();
            FailIf(_entities.Entities<BridgeSpawnerRoomEntity>().Count != 0 || _currentRoom.GetPackedStorageMetatile(0x27) != 0x6d,
                "Hero's Cave bridge re-entry failed to restore its layout without replaying.");

            LoadValidationRoom(4, 0xc5);
            IsolateHerosCave(entity => entity is DungeonTileScriptRoomEntity);
            _entities.SetNativeRoomEnemyCount(1); _player.WarpTo(CavePoint(0x67)); Step(2);
            FailIf(_currentRoom.GetPackedStorageMetatile(0x57) == 0x52, "Hero's Cave stairs appeared with an enemy remaining.");
            _sound.ClearPlayRequestAudit();
            _entities.ClearRoomEnemyCount(); Step();
            FailIf(_currentRoom.GetPackedStorageMetatile(0x57) == 0x52 || _sound.PlayRequestsFor(SoundId.SndSolvePuzzle) != 0,
                "Hero's Cave checknoenemies must yield before settilehere and its sound.");
            Step();
            FailIf(_currentRoom.GetPackedStorageMetatile(0x57) != 0x52 || _sound.PlayRequestsFor(SoundId.SndSolvePuzzle) != 1 ||
                _saveData.HasRoomFlag(4, 0xc5, OracleSaveData.RoomFlagItem),
                "Hero's Cave north stairs must appear without setting the item flag.");
            Step();
            LoadValidationRoom(0, 0x60);
        }
    }

    private void ValidateHerosCavePortals()
    {
        // Independent minibossPortal.s table, including the two different
        // bit7 item gates and packed-position construction operands.
        int[] rooms = [0xc2, 0xc3, 0xc4, 0xc5, 0xc6, 0xc9, 0xce, 0xcf];
        int[] positions = [0x11, 0x2c, 0x11, 0x2c, 0x7a, 0x86, 0x57, 0x91];
        int[] destinations = [2, 3, 0, 1, 7, 6, 5, 4];
        var data = new HerosCaveDatabase();
        foreach (bool batch in new[] { false, true })
        for (int index = 0; index < rooms.Length; index++)
        {
            ReinitializeGameplayForValidation();
            int room = rooms[index], target = destinations[index];
            FailIf(data.Portal(index) != (rooms[index], positions[index]), "Hero's Cave source portal destination table changed.");
            LoadValidationRoom(4, room);
            IsolateHerosCave(entity => entity is MinibossPortalRoomEntity);
            var portal = _entities.Entities<MinibossPortal>().Single();
            Vector2 point = CavePoint(positions[index]);
            Vector2 approach = new[] { Vector2.Down, Vector2.Up, Vector2.Left, Vector2.Right }.FirstOrDefault(direction =>
                Enumerable.Range(1, 16).All(distance => !_collision.Collides(point + direction * distance)));
            FailIf(approach == Vector2.Zero, $"Hero's Cave portal$4:${room:x2} has no clear 16-pixel approach at {point}.");
            _player.WarpTo(point + approach * 16);
            void Step(int count = 1, Vector2 move = default) => StepGameplayUpdates(count, move, batched: batch);
            Step();
            if (index is 4 or 5 or 6)
            {
                FailIf(portal.Visible, $"Hero's Cave room$4:${room:x2} portal ignored its item gate.");
                _saveData.SetRoomFlag(4, room, OracleSaveData.RoomFlagItem); Step();
            }
            FailIf(!portal.Visible || portal.Position != point, "Hero's Cave portal failed to decode its packed Y operand.");
            Warp? warp = null;
            void Capture(Warp value) => warp = value;
            _entities.RoomWarpRequested += Capture;
            try
            {
                for (int i = 0; !_player.CutsceneControlled && i < 40; i++) Step(move: -approach);
                FailIf(!_player.CutsceneControlled || _player.Position != point, "Hero's Cave portal was unreachable from original collision-free floor.");
                Step(47);
                FailIf(warp.HasValue, "Hero's Cave portal warped before spin update$30.");
                Step();
                FailIf(warp is not { DestinationGroup: 4, DestinationTransition: 0, DirectFadeOut: true } ||
                    warp.Value.DestinationRoom != rooms[target] || warp.Value.DestinationPosition != positions[target],
                    $"Hero's Cave portal from$4:${room:x2} did not request its original destination on spin update$30.");
                for (int i = 0; IsTransitioning && i < 180; i++) Step();
                FailIf(IsTransitioning || _currentRoom.Id != rooms[target] || _player.CutsceneControlled,
                    "Hero's Cave portal arrival retained transition/control ownership.");
                Step(4);
                FailIf(_player.CutsceneControlled, "Hero's Cave arrival overlap immediately retriggered its portal.");
                // The first four endpoints form two ungated reciprocal pairs.
                if (index < 4)
                {
                    Vector2 destinationPoint = CavePoint(positions[target]);
                    Vector2 leave = new[] { Vector2.Down, Vector2.Up, Vector2.Left, Vector2.Right }.First(direction =>
                        Enumerable.Range(1, 16).All(distance => !_collision.Collides(destinationPoint + direction * distance)));
                    Step(16, leave);
                    for (int i = 0; !_player.CutsceneControlled && i < 40; i++) Step(move: -leave);
                    FailIf(!_player.CutsceneControlled, "Hero's Cave portal failed a second floor approach.");
                    _entities.Clear();
                    FailIf(_player.CutsceneControlled, "Cancelling Hero's Cave portal retained Link control.");
                }
            }
            finally { _entities.RoomWarpRequested -= Capture; }
            LoadValidationRoom(0, 0x60);
        }
    }

    private void ValidateHerosCavePortalScroll()
    {
        foreach (bool batch in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            void Step(int count = 1, Vector2 move = default, bool press = false) =>
                StepGameplayUpdates(count, move, press ? ["attack"] : [], press ? ["attack"] : [], batched: batch);
            void EnterFromEast()
            {
                // Begin after the independently covered Small Key door opens.
                _saveData.SetRoomFlag(4, 0xc7, 0x08);
                _saveData.SetRoomFlag(4, 0xc6, 0x02);
                LoadValidationRoom(4, 0xc7);
                IsolateHerosCave(_ => false);
                _player.WarpTo(new(40, 88));
                FailIf(_collision.Collides(_player.Position), "Hero's Cave east approach must start on original floor.");
                for (int i = 0; !_transitions.ScrollActive && i < 64; i++) Step(move: Vector2.Left);
                FailIf(!_transitions.ScrollActive || _currentRoom.Id != 0xc6,
                    "Hero's Cave portal fixture must enter $4:$c6 through its actual east doorway.");
            }
            void FinishScroll()
            {
                for (int i = 0; _transitions.ScrollActive && i < 160; i++) Step();
                FailIf(_transitions.ScrollActive, "Hero's Cave room scroll did not finish.");
            }

            EnterFromEast();
            var portal = _entities.Entities<MinibossPortal>().Single();
            FailIf(portal.Visible, "Uncollected Hero's Cave $4:$c6 portal must remain hidden during arrival.");
            Step(2);
            FailIf(!_entities.Entities<MinibossPortal>().Contains(portal),
                "Incoming Hero's Cave $7e:$01 was deleted as an outgoing enabled$02 object while waiting for its chest.");
            FinishScroll();
            IsolateHerosCave(entity => entity is MinibossPortalRoomEntity or DungeonTriggerChestScriptRoomEntity);
            // Arrange only the solved eyes; exercise the real chest consumer,
            // item flag, text freeze, portal birth and room warp below.
            _player.WarpTo(new(120, 104)); _player.Face(Vector2I.Up);
            FailIf(_collision.Collides(_player.Position), "Hero's Cave chest approach must remain south of its solid statue.");
            for (int bit = 0; bit < 4; bit++) _entities.SetTrigger(bit, true);
            Step(20);
            FailIf(_currentRoom.GetPackedStorageMetatile(0x47) != 0xf1 || portal.Visible,
                "Solving Hero's Cave eyes must create the chest before enabling its portal.");
            Step(8, Vector2.Up); ApproachTileWall(batch); Step(press: true); Step(60);
            FailIf(!_saveData.HasRoomFlag(4, 0xc6, OracleSaveData.RoomFlagItem) || !_dialogue.IsOpen || !portal.Visible,
                "Opening Hero's Cave's actual chest must enable its retained portal, including while pickup text is active.");
            _dialogue.Close(); Step(3);
            for (int i = 0; _player.Position.X < 168 && i < 64; i++) Step(move: Vector2.Right);
            for (int i = 0; !_player.CutsceneControlled && i < 64; i++) Step(move: Vector2.Down);
            FailIf(!_player.CutsceneControlled || _player.Position != new Vector2(168, 120),
                "Hero's Cave newly enabled portal must be reachable around the original statues.");
            Step(48);
            for (int i = 0; IsTransitioning && i < 180; i++) Step();
            FailIf(IsTransitioning || _currentRoom.Id != 0xcf || _player.CutsceneControlled,
                "Hero's Cave chest-to-portal handoff must arrive at $4:$cf and release control.");

            EnterFromEast(); Step(2);
            FailIf(_entities.Entities<MinibossPortal>().Count != 1 || !_entities.Entities<MinibossPortal>().Single().Visible,
                "Collected Hero's Cave portal must also survive repeated incoming scroll initialization.");
            FinishScroll();
            LoadValidationRoom(0, 0x60);
        }
    }

    private void ValidateHerosCaveRewards()
    {
        foreach (bool batch in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            void Step(int count = 1, Vector2 move = default, bool press = false) =>
                StepGameplayUpdates(count, move, press ? ["attack"] : [], press ? ["attack"] : [], batched: batch);
            foreach (byte bit in new byte[] { 0, 1 })
            {
                _runtimeState.SetWramByte(OracleRuntimeState.SwitchStateAddress, bit);
                LoadValidationRoom(4, 0xc7);
                FailIf(_currentRoom.GetPackedStorageMetatile(0x27) != (bit == 0 ? 0x6d : 0xf4) ||
                    _currentRoom.GetPackedStorageMetatile(0x3d) != (bit == 0 ? 0xf4 : 0x6a),
                    "Hero's Cave $4:$c7 room-load bridge ignored switch bit0.");
            }

            LoadValidationRoom(4, 0xc0);
            IsolateHerosCave(entity => entity is HerosCavePuzzleRoomEntity);
            _inventory.GiveTreasure(TreasureId.Feather, 1); _inventory.EquipA(TreasureId.Feather);
            _player.WarpTo(new(152, 72)); Step();
            var reward = _entities.Entities<GroundTreasurePickup>().Single();
            FailIf(reward.Position != new Vector2(184, 72) || reward.Record.TreasureObject != "TREASURE_OBJECT_RUPEES_16" ||
                _entities.Entities<HerosCavePuzzleRoomEntity>().Count != 0,
                "Hero's Cave sidescroll producer lost its original $90:$20 position/reward/deletion.");
            int rupees = _inventory.Rupees;
            Step(move: Vector2.Right, press: true); Step(24, Vector2.Right); Step(50);
            FailIf(!_saveData.HasRoomFlag(4, 0xc0, OracleSaveData.RoomFlagItem) || _inventory.Rupees != rupees + 200,
                $"Hero's Cave reachable side-view reward failed its 200-Rupee grant: Link={_player.Position}, rupees={_inventory.Rupees}, text={_dialogue.IsOpen}.");
            _dialogue.Close(); Step(2);
            LoadValidationRoom(4, 0xc0); Step();
            FailIf(_entities.Entities<GroundTreasurePickup>().Count != 0, "Hero's Cave collected Rupee reward respawned.");

            _saveData.SetRoomFlag(4, 0xc9, OracleSaveData.RoomFlagItem);
            LoadValidationRoom(4, 0xce); Step();
            FailIf(!_saveData.HasRoomFlag(4, 0xce, OracleSaveData.RoomFlagItem),
                "Hero's Cave entrance did not copy the completed $4:$c9 item flag.");
            Step();
            FailIf(!_entities.Entities<MinibossPortal>().Single().Visible,
                "Hero's Cave entrance's earlier portal slot failed to observe the copied flag next update.");

            LoadValidationRoom(4, 0xc5);
            IsolateHerosCave(entity => entity is HerosCavePuzzleRoomEntity);
            _player.WarpTo(new(120, 72)); _player.Face(Vector2I.Up);
            FailIf(_collision.Collides(_player.Position), "Hero's Cave final chest must be approached on original floor.");
            Step(); Step(8, Vector2.Up); ApproachTileWall(batch); Step(press: true); Step(60);
            FailIf(!_saveData.HasRoomFlag(4, 0xc5, OracleSaveData.RoomFlagItem) || !_dialogue.IsOpen || IsTransitioning,
                "Hero's Cave final chest must grant its ring and retain the exit until text closes.");
            Warp? exit = null;
            void CaptureExit(Warp value) => exit = value;
            _entities.RoomWarpRequested += CaptureExit;
            try { _dialogue.Close(); Step(3); }
            finally { _entities.RoomWarpRequested -= CaptureExit; }
            FailIf(exit is not { SourceGroup: 4, SourceRoom: 0xc5, SourceTransition: 3,
                DestinationGroup: 0, DestinationRoom: 0x48, DestinationPosition: 0x28, DestinationTransition: 1, DirectFadeOut: true },
                "Hero's Cave final chest lost its source hardcoded exit bytes.");
            FailIf(!IsTransitioning, $"Hero's Cave final chest completion failed to request its exit warp after text: state={_entities.Entities<HerosCavePuzzleRoomEntity>().FirstOrDefault()?.State}, mask=${_runtimeState.ReadWramByte(WramAddress.wDisabledObjects):x2}, text={_entities.IsTextActive()}, freeze={_entities.RoomEntityFreezeActive()}.");
            for (int i = 0; IsTransitioning && i < 180; i++) Step();
            FailIf(IsTransitioning || _rooms.ActiveGroup != 0 || _currentRoom.Id != 0x48 || _player.CutsceneControlled ||
                _entities.LinkCollisionsAndMenuDisabled || _runtimeState.ReadWramByte(WramAddress.wDisabledObjects) != 0,
                "Hero's Cave exit failed to arrive at $0:$48 and release control.");
            LoadValidationRoom(4, 0xc5); Step();
            FailIf(_entities.Entities<HerosCavePuzzleRoomEntity>().Count != 0 || IsTransitioning,
                "Hero's Cave completed exit replayed on re-entry.");
            LoadValidationRoom(0, 0x60);
        }
    }
}
