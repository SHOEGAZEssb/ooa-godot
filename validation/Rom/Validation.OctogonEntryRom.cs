using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateOctogonEntryRom()
    {
        foreach (bool batch in new[] {false,true})
        {
            ReinitializeGameplayForValidation();
            _inventory.GiveTreasure(TreasureId.Flippers,0);
            _inventory.GiveTreasure(TreasureId.MermaidSuit,0);
            _inventory.EquipA(0); _inventory.EquipB(0);
            // The bridge and boss-key consumers have separate native tests.
            // Begin with their completed flags and cross the actual doorway.
            _saveData.SetRoomFlag(5,0x38,0x41); _saveData.SetRoomFlag(5,0x36,4);
            LoadValidationRoom(5,0x38);
            var active=SomariaPrivate<List<IRoomEntity>>(_entities,"_activeEntities");
            var free=typeof(RoomEntityManager).GetMethod("FreeEntity",BindingFlags.Instance|BindingFlags.NonPublic)!;
            foreach(var actor in active.Where(actor => actor is not OctogonEncounterInitializerRoomEntity && actor is not DungeonSignalScriptRoomEntity).ToArray())
            { active.Remove(actor); free.Invoke(_entities,[actor]); }
            _player.WarpTo(new(120,40)); _player.Face(Vector2I.Up);
            FailIf(_collision.Collides(_player.Position),"Octogon entry must start on room$5:$38's original south approach.");
            StepGameplayUpdates(1,Vector2.Zero,batched:batch);
            var seed=_random.CaptureState();
            var destination=_world.LoadRoom(5,0x36);
            var rom=new EnemyStatusRom(destination,_saveData,seed.Calls);
            var outgoingSlots=SomariaPrivate<HashSet<int>>(_entities,"_reservedEnemySlots").ToArray();
            foreach(int slot in outgoingSlots)
            { rom[0xd080+slot*256]=2; rom[0xd081+slot*256]=0x32; }
            rom[0xff94]=seed.Rng1; rom[0xff95]=seed.Rng2;
            rom.GeneratePlacementBuffer();
            byte[] headers=[0xc8,0xc9,0xca,0x8e,0xa5,0x8f,0xa7];
            for(int i=0;i<headers.Length;i++) rom[0xcc08+i*2]=headers[i];
            byte[] initial=[0,0,0xff,0x28,0x28,0x78,0xff,0];
            for(int i=0;i<initial.Length;i++) rom[0xcfd0+i]=initial[i];
            rom[0xcc39]=0x0c; rom[0xcd00]=8;
            rom[0xd000]=1; rom[0xd004]=1; rom[0xd024]=0x80; rom[0xd029]=0x38;
            rom[0xd026]=rom[0xd027]=6;
            // Original crossed-shutter substitution opens only the entered
            // south tile before parseObjectData/state0, keeping the east closed.
            rom[0xcfa7]=0xa0; rom[0xcea7]=0;
            var incomingSlots=Enumerable.Range(0,16).Where(slot => !outgoingSlots.Contains(slot)).ToArray();
            for(int i=0;i<2;i++)
            {
                int drop=0xd080+incomingSlots[i]*256;
                rom[drop]=1; rom[drop+1]=0x59; rom[drop+2]=6;
                rom[drop+11]=0x58; rom[drop+13]=(byte)(i==0 ? 0x58 : 0x98);
            }
            int bodySlot=incomingSlots[2];
            rom[0xd080+bodySlot*256]=1; rom[0xd081+bodySlot*256]=0x7d;
            rom[0xd240]=1; rom[0xd241]=0x1e; rom[0xd242]=0x0a; rom[0xd24b]=0xa7;
            rom[0xd340]=1; rom[0xd341]=0x1e; rom[0xd342]=9; rom[0xd34b]=0x5e;
            rom[0xd440]=1; rom[0xd441]=0x20; rom[0xd44b]=0x58; rom[0xd44d]=0x78;
            for(int walk=0;!IsTransitioning && walk<80;walk++) StepGameplayUpdates(1,Vector2.Up,batched:batch);
            FailIf(!IsTransitioning || _entities.Entities<OctogonCharacter>().Count != 2,
                "Walking through the opened boss door must preload Octogon's fresh body and shell.");
            var sounds=_sound.AttachPlayRequestAudit();
            rom.Update(_entities.FrameCounter,_player.Position);
            var body=_entities.Entities<OctogonCharacter>().Single(actor => !actor.ShellForm);
            var incoming=SomariaPrivate<OracleRoomData>(_entities,"_roomForActiveEntities");
            var enemySlots=SomariaPrivate<Dictionary<IRoomEntity,int>>(_entities,"_enemySlots");
            void Compare(string phase)
            {
                foreach(var pair in enemySlots.Where(pair => pair.Key.Node is OctogonCharacter))
                {
                    var actor=(OctogonCharacter)pair.Key.Node; int s=0xd080+pair.Value*256;
                    FailIf(rom[s]==0 || actor.State != rom[s+4] || actor.Depth != rom[s+3] || actor.Health != rom[s+41] ||
                        actor.Counter1 != rom[s+6] || actor.Counter2 != rom[s+7] || actor.Direction != rom[s+8] ||
                        actor.Position != new Vector2(rom.Word(s+12)/256f,rom.Word(s+10)/256f),
                        $"Octogon {phase}, batch={batch}: slot${s:x4} runtime={actor.State}/{actor.Depth}/{actor.Health}/{actor.Counter1}/{actor.Counter2}/{actor.Direction}/{actor.Position}; native={rom[s+4]}/{rom[s+3]}/{rom[s+41]}/{rom[s+6]}/{rom[s+7]}/{rom[s+8]}/{rom.Word(s+12)/256f},{rom.Word(s+10)/256f}.");
                }
                for(int i=0;i<8;i++) FailIf(_runtimeState.ReadWramByte(0xcfd0+i) != rom[0xcfd0+i],
                    $"Octogon {phase}: shared byte${0xcfd0+i:x4} differs.");
                var rng=_random.CaptureState();
                FailIf(_sound.NativeActiveMusic != rom[0xcc35],
                    $"Octogon {phase}: wActiveMusic gate differs: runtime=${_sound.NativeActiveMusic:x2}, ROM=${rom[0xcc35]:x2}.");
                FailIf(rng.Rng1 != rom[0xff94] || rng.Rng2 != rom[0xff95] || rng.Calls-seed.Calls != rom.RandomCalls ||
                    _entities.BossEntrySignal != rom[0xcc93] || _entities.RoomEnemyCount != rom[0xcdd1],
                    $"Octogon {phase}: RNG/count/shutter differs: RNG={rng.Calls-seed.Calls}/{rom.RandomCalls}, count={_entities.RoomEnemyCount}/{rom[0xcdd1]}, signal=${_entities.BossEntrySignal:x2}/${rom[0xcc93]:x2}, Link={_player.Position}, nativeDoor={rom[0xd244]}/{rom[0xd245]}/{rom[0xd246]}, frame={_entities.FrameCounter}.");
                foreach(byte tile in new byte[]{0xa7,0x5e}) FailIf(incoming.GetPackedStorageMetatile(tile) != rom[0xcf00+tile],
                    $"Octogon {phase}: shutter tile${tile:x2} differs.");
            }
            Compare("preload");
            FailIf(body.State != 8 || _entities.BossEntrySignal != 0x80 || !rom.Sounds.SequenceEqual(new[]{SoundId.SndCtrlStopMusic}),
                "Fresh Octogon must stop music and await deferred entrance-shutter initialization in state8.");
            int nativeCues=rom.Sounds.Count;
            for(int wait=0;IsTransitioning && wait<180;wait++) StepGameplayUpdates(1,Vector2.Zero,batched:batch,afterUpdate:() => {
                if(IsTransitioning) { rom.Update(_entities.FrameCounter,_player.Position); Compare("scroll freeze"); }
            });
            FailIf(IsTransitioning || _currentRoom.Id != 0x36,"The actual north scroll must arrive in boss room$5:$36.");
            rom[0xcd00]=1;
            foreach(int slot in outgoingSlots) rom[0xd080+slot*256]=0; // clearObjectsWithEnabled2 at scroll completion.
            void Step(int count,Vector2 movement) => StepGameplayUpdates(count,movement,batched:batch,afterUpdate:() => {
                var camera=-_entities.ToScreen(Vector2.Zero);
                rom[0xffaa]=unchecked((byte)camera.Y); rom[0xffac]=unchecked((byte)camera.X);
                rom.Update(_entities.FrameCounter,_player.Position); Compare("entry/shutter");
                // Native actors sample the actual gameplay Link position;
                // the fixture omits Link's swim/audio dispatch, covered by
                // MermaidSwimmingRom. Compare the encounter-owned cues here.
                FailIf(!sounds.Requests.Where(cue => cue is SoundId.SndDoorClose or SoundId.MusBoss or SoundId.SndCtrlStopMusic)
                    .SequenceEqual(rom.Sounds.Skip(nativeCues)),
                    $"Fresh Octogon door/music cues differ: runtime=[{string.Join(',',sounds.Requests)}], native=[{string.Join(',',rom.Sounds.Skip(nativeCues))}], Link={_player.Position}.");
            });
            // Link must clear the actual south shutter; the source boss waits
            // for the complete shared signal, then starts music next enemy pass.
            var entryStart=_player.Position;
            for(int walk=0;body.State==8 && walk<80;walk++) Step(1,Vector2.Zero);
            FailIf(_player.Position.Y >= entryStart.Y,"Native queued boss entry must move Link north automatically after scrolling.");
            FailIf(body.State != 9 || _entities.BossEntrySignal != 0 || _currentRoom.GetPackedStorageMetatile(0xa7) == 0xa0 ||
                _sound.PlayRequestsFor(SoundId.MusBoss) != 1,"Closing the real entrance must release Octogon and start boss music exactly once.");
            Step(8,Vector2.Zero);
        }
        GD.Print("Validated clean-US fresh Octogon initialization, actual boss-door approach/scroll, frozen body/shell, entrance-shutter signal and boss-music release in split/batched gameplay.");
    }
}
