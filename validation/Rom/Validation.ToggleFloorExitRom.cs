using Godot;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

public partial class ValidationRoot
{
    private void CompareToggleFloorExitRom()
    {
        foreach (bool batch in RomHostSchedules(0))
        {
            ReinitializeGameplayForValidation();
            _runtimeState.SetWramByte(OracleRuntimeState.ToggleBlocksStateAddress,0);
            _saveData.SetRoomFlag(4,0x9a,8,true);
            LoadValidationRoom(4,0x9a); _entities.Clear();
            _player.ApplicationUpdateOwned = true; _inventory.EquipA(0); _inventory.EquipB(0);
            var data = new DungeonMechanicDatabase();
            var record = data.GetRoomRecords(4,0x9a).Single(row => row.Id == 3);
            var orb = new DungeonOrbRoomEntity(record,data,new DungeonInteractionVisualDatabase().Visual("grotto-orb"),
                _currentRoom,_runtimeState,() => (long)_animationTicks,_sound.PlaySound);
            _entities.AddEntity(orb); _player.WarpTo(new(8,88)); _player.Face(Vector2I.Left);
            bool neighbor = _rooms.TryGetNeighbor(Vector2I.Left,out int target);
            FailIf(_collision.Collides(_player.Position) || !neighbor,
                "Crown exit must approach its original opened doorway and imported floor-layout neighbor.");
            var rom = new SomariaRom(_saveData,_random.CaptureState(),_currentRoom,3,8,88)
                { HostilePartsEnabled = true,PostObjectCollisionsEnabled = false };
            rom.InitializeLinkGameplay(); rom.InitializeLinkWalkingAnimation(); rom.CreateMenuView().LoadDungeon(5);
            rom[0xc2ef] = 1; rom[0xcd04] = 2; rom[0xcd0c] = 234; rom[0xcd0d] = 169;
            rom[0xd0c0] = 1; rom[0xd0c1] = 3; rom[0xd0cb] = (byte)orb.Position.Y; rom[0xd0cd] = (byte)orb.Position.X;
            var sounds = _sound.AttachPlayRequestAudit(); int update = 0;
            void Step(int count = 1,int angle = 0xff) => StepGameplayUpdates(count,
                angle == 0xff ? Vector2.Zero : OracleObjectMath.StrictCardinalVector(angle),batched:batch,afterUpdate:() => {
                bool toggling = rom[0xc2ef] == 2;
                if (toggling) rom.CreateMenuView().AdvanceToggleCutscene();
                rom.UpdateGameplay(0,angle == 24 ? 0x20 : 0,angle,_entities.FrameCounter);
                rom.AdvanceArrivalRoomControl(); // updateAllObjects' boundary pass precedes cutscene selection.
                rom.AdvanceTileGraphics();
                if (!toggling)
                {
                    rom.SelectToggleCutscene();
                    if (rom[0xc2ef] != 2)
                    {
                        rom.CheckTileWarps(); rom.SelectNextActiveRoom();
                        if (rom[0xcd00] == 1) rom.AdvanceEnemyAndPartCollisions();
                    }
                }
                FailIf(_currentRoom.Id != rom[0xcc30] || !IsTransitioning &&
                    _player.PrecisePosition != new Vector2(rom.Word(0xd00c)/256f,rom.Word(0xd00a)/256f),
                    $"Toggle/exit update{++update}, batch={batch}: room/Link runtime={_currentRoom.Id:x2}/{_player.PrecisePosition}, ROM={rom[0xcc30]:x2}/{rom.Word(0xd00c)/256f},{rom.Word(0xd00a)/256f}, scroll={rom[0xcd00]:x2}:{rom[0xcd04]}, cutscene={rom[0xc2ef]:x2}.");
                bool OrbCue(int cue) => cue is SoundId.SndSwitch or SoundId.SndDoorClose;
                FailIf(!sounds.Requests.Where(OrbCue).SequenceEqual(rom.Sounds.Where(OrbCue)),
                    "Deferred exit must preserve the original orb/floor cue order.");
            });
            Step(); Step(2,24);
            FailIf(IsTransitioning || _player.Position.X != 6,"The actual exit approach must stop before its original edge threshold.");
            FailIf(!orb.ApplySwordHit(orb.CollisionBounds,orb.Position,1,EnemyKnockbackStrength.Normal,new List<RoomEntitySpawn>()),
                "The original initialized orb must accept the declared pending collision.");
            rom[0xd0ea] |= 0x80; rom[0xd0eb] = unchecked((byte)-28);
            Step(1,24);
            FailIf(IsTransitioning || _entities.FloorToggle!.State != 0 || !orb.IsOn,
                "Toggle selection must defer the simultaneously requested room exit.");
            Step(7,24);
            FailIf(IsTransitioning || _entities.FloorToggle.Counter != 1,"The pending exit must survive the complete toggle delay.");
            Step(1,24); FailIf(IsTransitioning || _entities.FloorToggle.Active,"Toggle completion must skip normal exit selection.");
            Step(); FailIf(!IsTransitioning || _currentRoom.Id != target || rom[0xcd02] != 3,
                "The next normal update must consume the original left exit even after directional input is released.");
        }
    }
}
