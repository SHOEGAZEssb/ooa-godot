using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateMermaidTorchOrderRom()
    {
        foreach (bool batch in new[] { false,true })
        {
            ReinitializeGameplayForValidation(); LoadValidationRoom(5,0x43);
            var controller=_entities.Entities<MermaidTorchOrderRoomEntity>().Single();
            var active=SomariaPrivate<List<IRoomEntity>>(_entities,"_activeEntities");
            var free=typeof(RoomEntityManager).GetMethod("FreeEntity",BindingFlags.Instance|BindingFlags.NonPublic)!;
            foreach (var entity in active.Where(actor => actor != controller).ToArray())
            { active.Remove(entity); free.Invoke(_entities,[entity]); }
            _inventory.GiveTreasure(TreasureId.Shooter,1); _inventory.GiveTreasure(0x20,0x50);
            _inventory.GiveTreasure(TreasureId.Flippers,0); _inventory.GiveTreasure(TreasureId.MermaidSuit,0);
            _inventory.SelectShooterSeeds(0); _inventory.EquipA(0); _inventory.EquipB(TreasureId.Shooter);
            _player.WarpTo(new(0x38,0x88)); _player.Face(Vector2I.Up);
            FailIf(_collision.Collides(_player.Position) || _currentRoom.GetTerrainInfo(_player.Position).Hazard != HazardType.None,
                "Torch order must begin on the original southern floor in room$5:$43.");
            byte[] cells=[0x31,0x33,0x53,0x35];
            foreach (byte cell in cells) FailIf(_currentRoom.GetPackedStorageMetatile(cell) != 8,
                $"Mermaid torch cell${cell:x2} must be an original unlit torch.");
            var seed=_random.CaptureState();
            var rom=new SomariaRom(_saveData,seed,_currentRoom,0,0x38,0x88)
                { HostilePartsEnabled=true, PostObjectCollisionsEnabled=false };
            rom.InitializeLinkGameplay(); rom[0xd009]=0xff; rom[0xc2ef]=1; rom[0xcc05]=0xff; rom[0xcc2f]=0x43;
            var menu=rom.CreateMenuView(); menu.LoadRoomTileset(); menu.LoadRoomMappings();
            menu.LoadDungeon(_rooms.CurrentDungeonIndex); rom.CopyRoom(_currentRoom);
            for (int tile=0;tile<256;tile++) FailIf(rom.TileCollision(tile) != _currentRoom.GetCollision((byte)tile),
                $"Torch native tileset collision${tile:x2} differs: ${rom.TileCollision(tile):x2}/${_currentRoom.GetCollision((byte)tile):x2}.");
            int slot=0xd040+_entities.InteractionSlot(controller)*256;
            rom[slot]=1; rom[slot+1]=0x90; rom[slot+2]=7;
            var retraction=_roomEvents.Get<WallRetractionEvent>();
            var sounds=_sound.AttachPlayRequestAudit(); int update=0,steps=0,previousHeld=0,cutsceneItemUpdates=0;
            void Step(int count=1,int angle=0xff,bool held=false,bool press=false)
            {
                Vector2 movement=angle == 0xff ? Vector2.Zero : angle == 4 ? new(1,-1) : OracleObjectMath.StrictCardinalVector(angle);
                int keys=angle switch { 0=>0x40,4=>0x50,8=>0x10,16=>0x80,24=>0x20,_=>0 };
                int sampledHeld=keys|(held?2:0);
                int edge=(sampledHeld&~previousHeld)|(press ? 2 : 0); previousHeld=sampledHeld;
                StepGameplayUpdates(count,movement,MenuRomActions(sampledHeld),MenuRomActions(edge),batch,() => {
                    bool cutscene=rom[0xc2ef] == 0x0b;
                    if (cutscene)
                    {
                        rom.AdvanceWallRetraction();
                    }
                    rom.UpdateGameplay(edge,keys|(held?2:0),angle,_entities.FrameCounter); edge=0;
                    if ((rom[0xcd00]&5) != 0) menu.UpdateScreenShake();
                    rom.AdvanceTileGraphics();
                    if (!cutscene) rom.SelectToggleCutscene();
                    if (!cutscene && rom[0xc2ef] == 1) { rom.CheckTileWarps(); rom.AdvanceEnemyAndPartCollisions(); }
                    string context=$"Mermaid torch update{++update}, batch={batch}";
                    CompareSomariaMotionRom(rom,context);
                    FailIf(controller.Finished != (rom[slot] == 0) || !controller.Finished &&
                        (controller.Initialized != (rom[slot+4] != 0) || controller.Step != rom[slot+5] || controller.LastLitMask != rom[slot+6]),
                        context+": controller state/cache/order/lifetime differs.");
                    FailIf(_runtimeState.ReadWramByte(WramAddress.wDisabledObjects) != rom[0xcc8a] ||
                        _runtimeState.ReadWramByte(WramAddress.wMenuDisabled) != rom[0xcc02] || retraction.HasState != (rom[0xc2ef] == 0x0b),
                        context+$": mask/menu/cutscene differs: masks=${_runtimeState.ReadWramByte(WramAddress.wDisabledObjects):x2}/${rom[0xcc8a]:x2}, cutscene={retraction.HasState}/${rom[0xc2ef]:x2}.");
                    if (cutscene)
                        FailIf(retraction.Counter != rom[0xcbb4] || retraction.Phase != rom[0xcbb3] || retraction.Retractions != rom[0xcbb7],
                            context+$": cutscene counter/phase/steps differ: {retraction.Counter}/{retraction.Phase}/{retraction.Retractions} vs {rom[0xcbb4]}/{rom[0xcbb3]}/{rom[0xcbb7]}.");
                    var torches=_entities.Entities<LightableTorchRoomEntity>().Where(actor => !actor.Finished).ToArray();
                    var nativeTorches=Enumerable.Range(0xd0,16).Select(page => (page<<8)+0xc0)
                        .Where(a => rom[a] != 0 && rom[a+1] == 6).ToArray();
                    FailIf(torches.Length != nativeTorches.Length,context+$": recreated torch count differs {torches.Length}/{nativeTorches.Length}; native interactions="+
                        string.Join(';',Enumerable.Range(0xd2,14).Select(page => page*256+0x40).Where(a => rom[a] != 0)
                            .Select(a => $"${a:x4}:${rom[a+1]:x2}:${rom[a+2]:x2}:state{rom[a+4]}:xy{rom[a+13]},{rom[a+11]}"))+"; parts="+
                        string.Join(';',Enumerable.Range(0xd0,16).Select(page => page*256+0xc0).Where(a => rom[a] != 0).Select(a => $"${a:x4}:${rom[a+1]:x2}")));
                    foreach (var torch in torches)
                    {
                        int a=0xd0c0+SomariaPrivate<Dictionary<IRoomEntity,int>>(_entities,"_partSlots")[torch]*256;
                        FailIf(rom[a+1] != 6 || torch.Initialized != (rom[a+4] != 0) ||
                            torch.Position != new Vector2(rom[a+13],rom[a+11]),context+": torch physical slot/position/init differs.");
                    }
                    var seeds=_entities.EntityAdapters<EmberSeedRoomEntity>().Where(actor => !actor.Finished).ToArray();
                    int nativeSeeds=Enumerable.Range(0xd7,5).Count(page => rom[page<<8] != 0 && rom[(page<<8)+1] == 0x20);
                    FailIf(seeds.Length != nativeSeeds || _saveData.ReadWramByte(WramAddress.wNumEmberSeeds) != rom[0xc6b9],
                        context+": live seed count or BCD ammo differs.");
                    foreach (var seedActor in seeds)
                    {
                        var item=(EmberSeedEffect)seedActor.Node; int a=_entities.DynamicItemSlotOf(seedActor)<<8;
                        int state=item.State == EmberState.Flying ? 1 : 3;
                        FailIf(state != rom[a+4] || item.PrecisePosition != new Vector2(rom.Word(a+12)/256f,rom.Word(a+10)/256f) ||
                            state == 3 && item.FlameCounter != rom[a+6],context+": seed state/position/flame counter differs across object masks.");
                    }
                    if (cutscene && seeds.Length != 0) cutsceneItemUpdates++;
                    byte[] collisions=_currentRoom.CaptureStorageCollisions();
                    for (int index=0;index<0xb0;index++)
                    {
                        FailIf(_currentRoom.GetPackedStorageMetatile((byte)index) != rom[0xcf00+index] ||
                            (index&15) < 15 && collisions[index] != rom[0xce00+index],context+$": layout/collision${index:x2} differs.");
                    }
                    for (int room=0;room<256;room++)
                        FailIf(_saveData.GetRoomFlags(5,room) != rom[0xca00+room],context+$": flag5:${room:x2} differs.");
                    int nativeSteps=rom.Sounds.Count(cue => cue == SoundId.SndDoorClose);
                    if (nativeSteps != steps)
                    {
                        steps=nativeSteps;
                        // Compare the complete native regenerated map, not the
                        // imported binary used by the runtime's copy helper.
                        for (int y=0;y<22;y++) for (int x=0;x<30;x++)
                            FailIf(_currentRoom.GetBackgroundSubtileForValidation(x,y) != rom.BackgroundTile(x,y) ||
                                _currentRoom.GetBackgroundAttributeForValidation(x,y) != rom.BackgroundAttribute(x,y),
                                context+$": wall step{steps} BG tile/attribute({x},{y}) differs.");
                    }
                    var rng=_random.CaptureState();
                    FailIf(rng.Rng1 != rom[0xff94] || rng.Rng2 != rom[0xff95] || rng.Calls-seed.Calls != rom.RandomCalls ||
                        !sounds.Requests.SequenceEqual(rom.Sounds),context+$": shared RNG or ordered sounds differ: rng={rng.Rng1:x2}/{rng.Rng2:x2}/{rng.Calls-seed.Calls} vs {rom[0xff94]:x2}/{rom[0xff95]:x2}/{rom.RandomCalls}; sounds="+
                        string.Join(',',sounds.Requests.Select(cue => cue.ToString("x2")))+" vs "+string.Join(',',rom.Sounds.Select(cue => cue.ToString("x2"))));
                    FailIf(_entities.ScreenShakeCounter != rom[0xcd18] || _entities.HorizontalScreenShakeCounter != rom[0xcd19],
                        context+": scroll-gated shared shake counters differ.");
                });
            }
            void WalkTo(int x,int y)
            {
                for (int axis=0;axis<2;axis++)
                {
                    int target=axis == 0 ? x : y;
                    int limit=0;
                    while (Math.Abs((axis == 0 ? _player.PrecisePosition.X : _player.PrecisePosition.Y)-target) > 1.5f)
                    {
                        float position=axis == 0 ? _player.PrecisePosition.X : _player.PrecisePosition.Y;
                        Step(angle:axis == 0 ? position < target ? 8 : 24 : position < target ? 16 : 0);
                        FailIf(++limit > 150,$"Actual torch approach cannot reach ({x},{y}) through original geometry; Link={_player.Position}.");
                    }
                }
                Step();
                FailIf(_collision.Collides(_player.Position),"Actual torch approach ended in solid geometry.");
            }
            void Shoot(int angle)
            { Step(angle:angle); Step(1,angle,held:true,press:true); Step(5,angle,held:true); Step(55); }
            Step(3);
            FailIf(_entities.Entities<LightableTorchRoomEntity>().Count != 4,"D6's pointer must scan four original torches.");
            Shoot(0); // Wrong first torch$53, actual Ember shot from the south.
            FailIf(controller.Step != 0 || controller.LastLitMask != 0 || rom[0xcc8f] != 1 ||
                _entities.Entities<LightableTorchRoomEntity>().Count != 4 || !sounds.Requests.Contains(SoundId.SndError),
                "Wrong first torch must reset all four tiles, preserve the cumulative counter and recreate four lightable parts.");
            WalkTo(0x18,0x88); Shoot(0);
            FailIf(controller.Step != 1,"Actual first torch$31 must advance the order once.");
            // The original pool forbids new item parents while swimming.
            // Shoot northeast from dry ground: the east wall reflects the
            // seed northwest to$33, bypassing the still-unlit torch$53.
            WalkTo(0x38,0x88); Shoot(4);
            FailIf(controller.Step != 2,"Actual second torch$33 must advance the order once.");
            Shoot(0);
            FailIf(controller.Step != 3,"Actual third torch$53 must advance the order once.");
            WalkTo(0x58,0x88); Shoot(0);
            FailIf(!controller.Finished || !retraction.HasState || rom[0xcc8f] != 5 ||
                !_saveData.HasRoomFlag(5,0x25,0x40),"Fourth actual torch must hand off the mask, cutscene and present room flag with five cumulative lights after retry.");
            Vector2 heldPosition=_player.PrecisePosition;
            // $6f freezes Link and initialized actors, but permits item
            // parents and children against the live collision scratch.
            Step(1,held:true,press:true); Step(5,held:true); Step();
            FailIf(_player.PrecisePosition != heldPosition,"Wall retraction must hold Link while item dispatch continues.");
            while (retraction.HasState) Step(1,angle:16);
            FailIf(steps != 15 || cutsceneItemUpdates == 0 || _runtimeState.ReadWramByte(WramAddress.wDisabledObjects) != 0 ||
                _runtimeState.ReadWramByte(WramAddress.wMenuDisabled) != 0 || !_saveData.HasRoomFlag(5,0x43,0x40) ||
                _player.PrecisePosition == heldPosition,"Fifteenth wall step must release input before Link's pass and retain both era flags.");
            Step(3,angle:16);
            LoadValidationRoom(0,0x60); LoadValidationRoom(5,0x43);
            StepGameplayUpdates(2,Vector2.Zero,batched:batch);
            FailIf(_entities.Entities<LightableTorchRoomEntity>().Count != 0 || _currentRoom.GetPackedStorageMetatile(0x17) == 0xa7 ||
                cells.Any(cell => _currentRoom.GetPackedStorageMetatile(cell) != 9),
                "Re-entry must retain the open wall and the source's permanently lit torches.");
        }
        GD.Print("Validated clean-US Mermaid actual Ember torch order, wrong-choice retry/shared counter, native wall retraction graphics/timing/masks, same-update release and re-entry through split/batched gameplay.");
    }
}
