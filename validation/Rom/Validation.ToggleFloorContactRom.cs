using Godot;
using System.Linq;

namespace oracleofages;

public partial class ValidationRoot
{
    private void CompareToggleFloorContactRom()
    {
        foreach (bool batch in new[] { false,true })
        foreach (bool fullPool in new[] { false,true })
        {
            ReinitializeGameplayForValidation();
            _runtimeState.SetWramByte(OracleRuntimeState.ToggleBlocksStateAddress,0);
            LoadValidationRoom(4,0xa1); _entities.Clear();
            var data = new DungeonMechanicDatabase();
            var record = data.GetRoomRecords(4,0xa1).Single();
            FailIf(record is not { Id:3,SubId:0,Order:0,PackedPosition:0x35 },"Crown$4:$a1 must place PART$03:$00 first at$35.");
            var orb = new DungeonOrbRoomEntity(record,data,new DungeonInteractionVisualDatabase().Visual("grotto-orb"),
                _currentRoom,_runtimeState,() => (long)_animationTicks,_sound.PlaySound);
            _entities.AddEntity(orb);
            _inventory.GiveTreasure(TreasureId.Sword,1); _inventory.EquipA(TreasureId.Sword); _inventory.EquipB(0);
            _inventory.ApplyDamage(4);
            _player.WarpTo(new(120.25f,56.5f)); _player.Face(Vector2I.Left);
            FailIf(_collision.Collides(_player.Position),"Crown orb Sword must approach from the original east floor$37.");
            var seed = _random.CaptureState();
            var rom = new SomariaRom(_saveData,seed,_currentRoom,3,120,56)
                { HostilePartsEnabled = true,PostObjectCollisionsEnabled = false };
            var menu = rom.CreateMenuView(); menu.LoadRoomTileset(); menu.LoadRoomMappings(); menu.EnableVramDmaTransfers();
            menu.LoadToggleGraphics();
            rom.Word(0xd00a,56*256+128); rom.Word(0xd00c,120*256+64);
            rom.InitializeLinkGameplay(); rom.InitializeLinkWalkingAnimation(); rom[0xcc39] = 5; rom[0xc2ef] = 1;
            menu.CopyToggleRoom(_currentRoom);
            rom[0xd0c0] = 1; rom[0xd0c1] = 3; rom[0xd0cb] = 56; rom[0xd0cd] = 88;
            var toggle = _entities.FloorToggle!;
            var sounds = _sound.AttachPlayRequestAudit(); int update = 0, lastPhase = int.MinValue;
            void Step(int count = 1,bool attack = false,int angle = 0xff)
            {
                int tick = 0;
                int keys = angle switch { 8 => 0x10,24 => 0x20,_ => 0 };
                StepGameplayUpdates(count,angle == 0xff ? Vector2.Zero : OracleObjectMath.StrictCardinalVector(angle),
                    attack ? ["attack"] : [],attack ? ["attack"] : [],batch,() => {
                    bool toggling = rom[0xc2ef] == 2;
                    if (toggling) menu.AdvanceToggleCutscene();
                    rom.UpdateGameplay(attack && tick++ == 0 ? 1 : 0,(attack ? 1 : 0)|keys,angle,_entities.FrameCounter);
                    rom.AdvanceTileGraphics();
                    if (!toggling)
                    {
                        rom.SelectToggleCutscene();
                        if (rom[0xc2ef] != 2) rom.AdvanceEnemyAndPartCollisions();
                    }
                    CompareSomariaMotionRom(rom,$"Crown orb/toggle update{++update}, full={fullPool}, batch={batch}");
                    // Selection has not installed counter6 yet. On a repeat,
                    // native$cbb4 still holds the preceding terminal$1e.
                    FailIf(toggle.Active != (rom[0xc2ef] == 2) || toggle.Active &&
                        (toggle.State != rom[0xcc03] || toggle.State > 0 && toggle.Counter != rom[0xcbb4]) ||
                        _runtimeState.ReadWramByte(WramAddress.wLastToggleBlocksState) != rom[0xcd2c] ||
                        _runtimeState.ReadWramByte(OracleRuntimeState.ToggleBlocksStateAddress) != rom[0xcdd2] ||
                        orb.Palette != rom[0xd0db] || orb.PendingHit != ((rom[0xd0ea]&0x80) != 0) ||
                        orb.HitLockout != -unchecked((sbyte)rom[0xd0eb]) || !sounds.Requests.SequenceEqual(rom.Sounds),
                        $"Crown update{update}: cutscene={toggle.State}/{rom[0xc2ef]:x2}:{rom[0xcc03]}, counter={toggle.Counter}/{rom[0xcbb4]}, orb={orb.Palette}:{orb.PendingHit}:{orb.HitLockout}/{rom[0xd0db]}:{rom[0xd0ea]:x2}:{rom[0xd0eb]:x2}, cues=[{string.Join(',',sounds.Requests)}]/[{string.Join(',',rom.Sounds)}].");
                    int phase = toggle.State;
                    if (phase != lastPhase)
                    {
                        lastPhase = phase;
                        using Image uploaded = _currentRoom.CaptureLiveGraphics();
                        for (int tile = 0; tile < 4; tile++)
                        for (int y = 0; y < 8; y++)
                        for (int x = 0; x < 8; x++)
                        {
                            int address = 0x8cc0+tile*16+y*2;
                            int shade = ((menu.MapGfx(1,address)>>(7-x))&1)|(((menu.MapGfx(1,address+1)>>(7-x))&1)<<1);
                            int pixel = Mathf.RoundToInt((1-uploaded.GetPixel((0x4c+tile)%16*8+x,(0x4c+tile)/16*8+y).R)*3);
                            FailIf(pixel != shade,$"Toggle phase{phase} native header pixel differs at tile${0x4c+tile:x2}, ({x},{y}): {pixel}/{shade}.");
                        }
                    }
                    var random = _random.CaptureState();
                    FailIf(random.Rng1 != rom[0xff94] || random.Rng2 != rom[0xff95] || random.Calls-seed.Calls != rom.RandomCalls,
                        "Actual orb/toggle handoff must preserve original RNG order.");
                });
            }
            Step(16,angle:24); Step(attack:true);
            for (int wait = 0; !orb.PendingHit && wait < 40; wait++) Step();
            FailIf(!orb.PendingHit,"Actual Sword must reach the Crown orb through original collision geometry.");
            Step(); FailIf(toggle.State != 0 || toggle.Frozen || !orb.IsOn,"PART publication must select the toggle after objects before state0 freeze.");
            _currentRoom.SetPositionTileAndCollision(new(72,72),0x10,null,(long)_animationTicks);
            rom.SetTile(0x44,0x10); rom.AdvanceTileGraphics(); menu.CopyToggleRoom(_currentRoom);
            if (fullPool)
                for (int index = 0; index < 14; index++)
                {
                    _entities.Spawn<PuzzlePuffEffect>(new PuzzlePuffSpawn(new(200,120),SoundId.MusNone));
                    int slot = (0xd2+index)*256+0x40;
                    rom[slot] = 1; rom[slot+1] = 5; rom[slot+2] = 0x80; rom[slot+0xb] = 120; rom[slot+0xd] = 200;
                }
            Vector2 held = _player.PrecisePosition; int lockout = orb.HitLockout;
            Step(7,angle:8);
            FailIf(toggle.Counter != 1 || _player.PrecisePosition != held || orb.HitLockout != lockout,
                "Toggle initialization/intermediate upload and five decrements must retain Link and the orb lockout.");
            Step(angle:8);
            FailIf(toggle.Active || orb.HitLockout != lockout-1 || _currentRoom.GetMetatile(new(88,72)) != 0x28 ||
                _currentRoom.GetMetatile(new(72,72)) != 0x0f || _entities.Entities<RockDebrisEffect>().Count != (fullPool ? 0 : 1),
                "Wait zero must change both floor colors, restore object updates and honor debris capacity.");
            for (int wait = 0; (_player.IsAttacking || orb.HitLockout > 0) && wait < 60; wait++) Step();
            Vector2 released = _player.PrecisePosition; Step(angle:8);
            FailIf(_player.PrecisePosition.X <= released.X,"Link must regain actual movement after the toggle and Sword release.");
            Step(4,angle:24); Step(attack:true);
            for (int wait = 0; !orb.PendingHit && wait < 40; wait++) Step();
            FailIf(!orb.PendingHit,"A repeated actual Sword must still contact the same Crown orb.");
            Step(); Step(8);
            FailIf(toggle.Active || orb.IsOn || _currentRoom.GetMetatile(new(88,72)) != 0x0e || _currentRoom.GetMetatile(new(72,72)) != 0x29,
                "The repeated orb must restore both floor colors and complete its reverse toggle.");
        }
    }
}
