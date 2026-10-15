using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateAncientTombScriptsRom()
    {
        int fixture = 0;
        foreach (var spec in new[] { (Room:0x87, Subid:2, Tile:0x50, Flag:0x40),
            (Room:0x9c, Subid:3, Tile:0x52, Flag:0x40), (Room:0x9b, Subid:5, Tile:0x45, Flag:0x80),
            (Room:0x7a, Subid:6, Tile:0x6a, Flag:0) })
        foreach (bool batch in RomHostSchedules(fixture++))
        {
            ReinitializeGameplayForValidation(); LoadValidationRoom(5, spec.Room); _entities.Clear();
            _inventory.SetScriptedEquippedItems(0, 0);
            var data = new AncientTombDatabase();
            var record = data.GetRoomRecords(5, spec.Room).Single(r => r.Kind == DungeonObjectKind.TileScript);
            var profile = data.Script(spec.Subid);
            int torches = 0, enemies = 1;
            var script = new DungeonTileScriptRoomEntity(record, profile, _runtimeState,
                () => _entities.ActiveTriggers, () => torches, () => enemies,
                () => _saveData.HasRoomFlag(5, spec.Room, profile.Flag), () => _saveData.SetRoomFlag(5, spec.Room, profile.Flag),
                _entities.IsScriptTextActive, tile => { if (_rooms.TrySetTile((byte)_currentRoom.GetPackedPosition(record.Position),tile)) _entities.OnRoomTileChanged(); },
                _entities.OnNativeSoundRequested, _entities.TryCreatePuzzlePuff, () => throw new InvalidOperationException("Tile-script fixture cannot start a wall."));
            _entities.AddEntity(script);
            Vector2 point = Enumerable.Range(0, 0xb0).Select(p => new Vector2((p & 15) * 16 + 8, (p >> 4) * 16 + 8))
                .First(p => !_collision.Collides(p) && _currentRoom.GetTerrainInfo(p).Hazard == HazardType.None);
            _player.WarpTo(point); _player.Face(Vector2I.Down);
            var seed = _random.CaptureState(); var rom = new SomariaRom(_saveData,seed,_currentRoom,2,(int)point.X,(int)point.Y);
            rom.InitializeLinkGameplay(); rom.InitializeLinkWalkingAnimation(); rom[0xcc39] = 8;
            int s = 0xd040 + _entities.InteractionSlot(script) * 256;
            rom[s] = 1; rom[s+1] = 0x20; rom[s+2] = (byte)spec.Subid;
            rom[s+11] = (byte)record.Y; rom[s+13] = (byte)record.X;
            int update = 0, packed = _currentRoom.GetPackedPosition(record.Position);
            var sounds = _sound.AttachPlayRequestAudit();
            void Step(int count = 1) => StepGameplayUpdates(count, Vector2.Zero, batched:batch, afterUpdate:() => {
                rom[0xcdd1] = (byte)enemies; rom[0xcc8f] = (byte)torches;
                rom.UpdateGameplay(0,0,0xff,_entities.FrameCounter); rom.AdvanceTileGraphics(); update++;
                CompareSomariaMotionRom(rom,$"D8 script${spec.Subid:x2} update{update}");
                FailIf(script.Finished != (rom[s] == 0) || !script.Finished &&
                    (script.Initialized != (rom[s+4] != 0) || script.Counter != rom[s+6]),
                    $"D8 script${spec.Subid:x2} update{update}: state/timer/lifetime differs, {script.Step}/{script.Counter} vs native state{rom[s+4]}/timer{rom[s+6]}.");
                FailIf(_saveData.GetRoomFlags(5,spec.Room) != rom[0xca00+spec.Room] ||
                    _currentRoom.GetPackedStorageMetatile((byte)packed) != rom[0xcf00+packed] ||
                    !sounds.Requests.SequenceEqual(rom.Sounds), $"D8 script${spec.Subid:x2} update{update}: flags {_saveData.GetRoomFlags(5,spec.Room):x2}/{rom[0xca00+spec.Room]:x2}, tile {_currentRoom.GetPackedStorageMetatile((byte)packed):x2}/{rom[0xcf00+packed]:x2}, sounds {string.Join(',',sounds.Requests)}/{string.Join(',',rom.Sounds)}, step {script.Step}.");
            });
            Step(4);
            if (spec.Subid is 2 or 3)
            {
                _entities.SetTrigger(0,true); _entities.SetTrigger(1,true); rom[0xcca0] = 3;
                Step(2); FailIf(script.Step != 1, "Ancient Tomb stairs require triggers EXACTLY$01.");
                _entities.SetTrigger(1,false); rom[0xcca0] = 1;
            }
            else if (spec.Subid == 6)
            { torches = 2; Step(2); FailIf(script.Step != 1, "Vertical bridge requires exactly one lit torch."); torches = 1; }
            else { Step(27); FailIf(script.Step != 1, "Enemy stairs must reach checknoenemies after their thirty-update wait."); enemies = 0; }
            for (int wait = 0; !script.Finished && wait < 12; wait++) Step();
            FailIf(!script.Finished || _currentRoom.GetPackedStorageMetatile((byte)packed) != spec.Tile ||
                spec.Flag != 0 && !_saveData.HasRoomFlag(5,spec.Room,(byte)spec.Flag),
                $"D8 script${spec.Subid:x2} must publish source tile${spec.Tile:x2}/flag${spec.Flag:x2}.");
            Step(3);
            if (spec.Flag != 0)
            {
                LoadValidationRoom(5,spec.Room); StepGameplayUpdates(3,Vector2.Zero);
                FailIf(_entities.Entities<DungeonTileScriptRoomEntity>().Any(), "Completed stairs script must retire on re-entry.");
            }
        }
        GD.Print("Validated clean-US D8 south/north stairs, enemy-clear wait and exact torch bridge gates, command yields, cues, flags and reuse.");
    }

    private void ValidateAncientTombWallRom()
    {
        foreach (bool batch in new[] { false, true })
        {
            ReinitializeGameplayForValidation(); LoadValidationRoom(5,0x95);
            var script = _entities.Entities<DungeonTileScriptRoomEntity>().Single();
            var active = SomariaPrivate<List<IRoomEntity>>(_entities,"_activeEntities");
            var free = typeof(RoomEntityManager).GetMethod("FreeEntity",BindingFlags.Instance|BindingFlags.NonPublic)!;
            foreach (var actor in active.Where(e => e != script).ToArray()) { active.Remove(actor); free.Invoke(_entities,[actor]); }
            _inventory.SetScriptedEquippedItems(0,0);
            Vector2 point = Enumerable.Range(0x60,0x40).Select(p => new Vector2((p & 15)*16+8,(p>>4)*16+8))
                .First(p => !_collision.Collides(p) && _currentRoom.GetTerrainInfo(p).Hazard == HazardType.None);
            _player.WarpTo(point); _player.Face(Vector2I.Down);
            var seed = _random.CaptureState(); var rom = new SomariaRom(_saveData,seed,_currentRoom,2,(int)point.X,(int)point.Y);
            rom.InitializeLinkGameplay(); rom.InitializeLinkWalkingAnimation(); rom[0xcc39] = 8;
            rom[0xc2ef] = 1; rom[0xcc05] = 0xff; rom[0xcc2f] = 0x95;
            var menu = rom.CreateMenuView(); menu.LoadRoomTileset(); menu.LoadRoomMappings();
            menu.LoadDungeon(_rooms.CurrentDungeonIndex); rom.CopyRoom(_currentRoom);
            int s = 0xd040 + _entities.InteractionSlot(script)*256;
            rom[s] = 1; rom[s+1] = 0x20; rom[s+2] = 4;
            var wall = _roomEvents.Get<WallRetractionEvent>();
            var sounds = _sound.AttachPlayRequestAudit(); int update = 0, rectangles = 0;
            void Step(int count = 1) => StepGameplayUpdates(count,Vector2.Zero,batched:batch,afterUpdate:() => {
                bool cutscene = rom[0xc2ef] == 0x0b;
                if (cutscene) rom.AdvanceWallRetraction();
                rom.UpdateGameplay(0,0,0xff,_entities.FrameCounter);
                if (!wall.CameraUpdatesDisabled) rom.AdvanceScreenShake();
                rom.AdvanceTileGraphics(); rom.SelectTriggeredCutscene(); update++;
                CompareSomariaMotionRom(rom,$"D8 wall update{update}");
                FailIf(script.Finished != (rom[s] == 0) || !script.Finished && script.Counter != rom[s+6] ||
                    wall.HasState != (rom[0xc2ef] == 0x0b) ||
                    _runtimeState.ReadWramByte(WramAddress.wDisabledObjects) != rom[0xcc8a] ||
                    _runtimeState.ReadWramByte(WramAddress.wMenuDisabled) != rom[0xcc02],
                    $"D8 wall update{update}: script/lifetime/hand-off/masks differ: counter{script.Counter}/{rom[s+6]}, wall={wall.HasState}/${rom[0xc2ef]:x2}, masks={_runtimeState.ReadWramByte(WramAddress.wDisabledObjects):x2}/{rom[0xcc8a]:x2}, menu={_runtimeState.ReadWramByte(WramAddress.wMenuDisabled):x2}/{rom[0xcc02]:x2}.");
                if (cutscene) FailIf(wall.Counter != rom[0xcbb4] || wall.Phase != rom[0xcbb3] || wall.Retractions != rom[0xcbb7],
                    $"D8 wall update{update}: native waits/rectangle count differ.");
                byte[] collisions = _currentRoom.CaptureStorageCollisions();
                for (int p = 0;p<0xb0;p++)
                    FailIf(_currentRoom.GetPackedStorageMetatile((byte)p) != rom[0xcf00+p] ||
                        (p&15)<15 && collisions[p] != rom[0xce00+p],$"D8 wall update{update}: layout/collision${p:x2} differs.");
                int steps = rom.Sounds.Count(c => c == SoundId.SndDoorClose);
                if (steps != rectangles)
                {
                    rectangles = steps;
                    for (int y=0;y<22;y++) for (int x=0;x<30;x++)
                        FailIf(_currentRoom.GetBackgroundSubtileForValidation(x,y) != rom.BackgroundTile(x,y) ||
                            _currentRoom.GetBackgroundAttributeForValidation(x,y) != rom.BackgroundAttribute(x,y),
                            $"D8 wall rectangle{rectangles}: native BG({x},{y}) differs.");
                }
                var rng = _random.CaptureState();
                FailIf(_saveData.GetRoomFlags(5,0x95) != rom[0xca95] || rng.Rng1 != rom[0xff94] || rng.Rng2 != rom[0xff95] ||
                    rng.Calls-seed.Calls != rom.RandomCalls || !sounds.Requests.SequenceEqual(rom.Sounds),
                    $"D8 wall update{update}: flag/shared shake RNG or cues differ.");
            });
            Step(2); _entities.SetTrigger(0,true); rom[0xcca0] = 1;
            for (int wait=0;!wall.HasState && wait<40;wait++) Step();
            FailIf(!wall.HasState || !script.Finished,"Source trigger must wait thirty updates before the wall hand-off.");
            for (int wait=0;wall.HasState && wait<500;wait++) Step();
            FailIf(wall.HasState || rectangles != 11 || !_saveData.HasRoomFlag(5,0x95,0x40),
                "D8 wall must finish exactly eleven source rectangles and persist ROOMFLAG$40.");
            Step(3); LoadValidationRoom(5,0x95); StepGameplayUpdates(3,Vector2.Zero);
            FailIf(wall.HasState || _entities.Entities<DungeonTileScriptRoomEntity>().Any(), "Re-entry must retain the retracted wall without replaying its script.");
        }
        GD.Print("Validated clean-US D8 wall script/30-update hand-off, 60/60 waits, eleven native BG rectangles, scratch solidity, RNG/cues, release and re-entry.");
    }
}
