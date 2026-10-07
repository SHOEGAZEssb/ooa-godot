using Godot;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

public partial class ValidationRoot
{
    private void CompareToggleFloorStairRom()
    {
        foreach (bool batch in RomHostSchedules(0))
        {
            ReinitializeGameplayForValidation();
            _runtimeState.SetWramByte(OracleRuntimeState.ToggleBlocksStateAddress,0);
            LoadValidationRoom(4,0xa1); _entities.Clear();
            _player.ApplicationUpdateOwned = true;
            _inventory.EquipA(0); _inventory.EquipB(0);
            var data = new DungeonMechanicDatabase();
            var record = data.GetRoomRecords(4,0xa1).Single(row => row.Id == 3);
            var orb = new DungeonOrbRoomEntity(record,data,new DungeonInteractionVisualDatabase().Visual("grotto-orb"),
                _currentRoom,_runtimeState,() => (long)_animationTicks,_sound.PlaySound);
            _entities.AddEntity(orb);
            _player.WarpTo(new(120,40)); _player.Face(Vector2I.Up);
            FailIf(_collision.Collides(_player.Position) || _currentRoom.GetMetatile(new(120,24)) != 0x44,
                "Crown stair fixture must approach original$17 from its south floor.");
            var rom = new SomariaRom(_saveData,_random.CaptureState(),_currentRoom,0,120,40)
                { HostilePartsEnabled = true,PostObjectCollisionsEnabled = false };
            rom.InitializeLinkGameplay(); rom.InitializeLinkWalkingAnimation(); rom.CreateMenuView().LoadDungeon(5);
            rom[0xc2ef] = 1; rom[0xcc05] = 0xff;
            rom[0xd0c0] = 1; rom[0xd0c1] = 3; rom[0xd0cb] = 56; rom[0xd0cd] = 88;
            var sounds = _sound.AttachPlayRequestAudit(); int update = 0; bool loaded = false;
            void Step(int count = 1,int angle = 0xff) => StepGameplayUpdates(count,
                angle == 0xff ? Vector2.Zero : OracleObjectMath.StrictCardinalVector(angle),batched:batch,afterUpdate:() => {
                rom.AdvanceWarpPalette();
                if (rom[0xc2ef] == 3)
                {
                    rom.UpdateFadeOutWarp(); loaded |= rom[0xcc30] != 0xa1;
                }
                else
                {
                    bool toggling = rom[0xc2ef] == 2;
                    if (toggling) rom.CreateMenuView().AdvanceToggleCutscene();
                    rom.UpdateGameplay(0,angle == 0 ? 0x40 : 0,angle,_entities.FrameCounter);
                    rom.AdvanceTileGraphics();
                    if (loaded) rom.AdvanceArrivalRoomControl();
                    else if (!toggling)
                    {
                        rom.SelectToggleCutscene();
                        if (rom[0xc2ef] != 2)
                        {
                            rom.CheckTileWarps();
                            if (rom[0xcc4b] != 0) rom.ApplyRequestedWarp();
                            if (rom[0xc2ef] == 1 && rom[0xcd00] != 0) rom.AdvanceEnemyAndPartCollisions();
                        }
                    }
                }
                FailIf(_rooms.ActiveGroup != rom[0xcc2d] || _currentRoom.Id != rom[0xcc30] ||
                    _player.PrecisePosition != new Vector2(rom.Word(0xd00c)/256f,rom.Word(0xd00a)/256f),
                    $"Toggle/stair update{++update}, loaded={loaded}, batch={batch}: room/Link runtime={_rooms.ActiveGroup:x1}:{_currentRoom.Id:x2}/{_player.PrecisePosition}, ROM={rom[0xcc2d]:x1}:{rom[0xcc30]:x2}/{rom.Word(0xd00c)/256f},{rom.Word(0xd00a)/256f}, cutscene={rom[0xc2ef]:x2}, Linkstate={rom[0xd004]:x2}, force={rom[0xcc50]:x2}.");
                FailIf(!sounds.Requests.Where(cue => cue is SoundId.SndEnterCave or SoundId.SndSwitch or SoundId.SndDoorClose)
                    .SequenceEqual(rom.Sounds.Where(cue => cue is SoundId.SndEnterCave or SoundId.SndSwitch or SoundId.SndDoorClose)),
                    "Toggle-before-stair selection must preserve original orb/door/entrance cue order.");
            });
            Step(14,0);
            FailIf(IsTransitioning || _player.Position != new Vector2(120,26),"Actual stair approach must remain outside its source centering window.");
            // Declare a producer-verified pending PART collision on the same
            // update as Link enters the stair's centering window.
            FailIf(!orb.ApplySwordHit(orb.CollisionBounds,orb.Position,1,EnemyKnockbackStrength.Normal,new List<RoomEntitySpawn>()),
                "The original orb must admit the staged pending collision.");
            rom[0xd0ea] |= 0x80; rom[0xd0eb] = unchecked((byte)-28);
            Step(1,0);
            FailIf(IsTransitioning || _entities.FloorToggle!.State != 0 || !orb.IsOn,
                "Toggle selection must suppress the simultaneous original stair lookup.");
            Step(8);
            FailIf(IsTransitioning || _entities.FloorToggle.Active || rom[0xc2ef] != 1,
                "Toggle completion must bypass the normal cutscene's stair lookup until the next update.");
            Step(); FailIf(!IsTransitioning,"The next normal update must admit the still-centered original stair.");
            for (int wait = 0; IsTransitioning && wait < 160; wait++) Step();
            FailIf(IsTransitioning || _currentRoom.Id != 0xb1 || _player.Position != new Vector2(120,24),
                "The deferred stair must preserve original Crown$4:$b1/$17 arrival.");
            Step(2); FailIf(IsTransitioning,"The destination stair must retain its original return suppression.");
        }
    }
}
