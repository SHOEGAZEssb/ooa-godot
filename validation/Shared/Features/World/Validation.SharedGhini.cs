
namespace Oracles;

public sealed partial class ValidationRoot
{
    private static void CheckSharedGhini(FrontendValidationRoot root,bool batched)
    {
        var rooms=root._gameplaySession!.Rooms;
        var entities=root._seasonsOpening?.Entities ?? root._entities;
        var player=root._scene.Player;
        var memory=rooms.RuntimeState;
        var game=memory.Game;
        var app=new ApplicationValidationFixture(root);
        int originalGroup=rooms.ActiveGroup,originalRoom=rooms.CurrentRoom.Id;
        var originalRandom=entities.Random.CaptureState();
        void Load()
        {
            root._scene.Dialogue.Close();
            if(game==OracleGame.Ages)root.LoadDebugRoom(0,0x98);else root._seasonsOpening!.DebugTravel(0,0xc9);
            entities.Clear();player.BeginCutsceneControl();entities.DisableLinkCollisionsAndMenu();
            player.Inventory.EquipA(0);player.Inventory.EquipB(0);player.Inventory.SetScriptedActiveRing(0xff);
            player.WarpTo(new(136,112));player.Face(Vector2I.Down);
        }
        try
        {
            foreach(int subid in new[]{0,1,2})
            {
                Load();
                var seed=entities.Random.CaptureState();
                var entry=new DebugObjectCatalog(rooms.AssetRoot).Enemies.Single(item=>item.Id==0x17 && item.SubId==subid);
                FailIf(!entry.Supported || new DebugObjectPreview(rooms.AssetRoot).GetTexture(entry,false) is null || entities.Random.Calls!=seed.Calls,
                    $"{game}: all three native Ghini variants require selected-pack previews without gameplay RNG or actor allocation.");
                FailIf(!entities.TrySpawnDebugEnemy(0x17,subid,new(64,56),out string error),error);
                var actor=subid==2?(EnemyCharacter)entities.Entities<TargetGhiniCharacter>().Single():entities.Entities<GhiniCharacter>().Single();
                var native=SharedNativeEnemyReference(rooms,player,0x17);
                native[0xff94]=seed.Rng1;native[0xff95]=seed.Rng2;
                native[0xd080]=1;native[0xd081]=0x17;native[0xd082]=(byte)subid;
                native.Word(0xd08c,64*256);native.Word(0xd08a,56*256);
                int updates=0;var visited=new System.Collections.Generic.HashSet<int>();
                void Step(int count)=>app.Step(count,Vector2.Zero,batched:batched,afterUpdate:()=>
                {
                    native.AdvanceEnemies(entities.FrameCounter);updates++;
                    int state,counter,angle,speed,z;
                    if(actor is TargetGhiniCharacter target)
                    {state=target.State;counter=target.Counter;angle=target.Angle;speed=target.Speed;z=target.ZFixed;}
                    else
                    {
                        var ghini=(GhiniCharacter)actor;
                        state=ghini.State switch{GhiniState.Uninitialized=>0,GhiniState.Choosing=>8,GhiniState.Moving=>9,_=>(int)ghini.State};
                        counter=ghini.Counter;angle=ghini.Angle;speed=ghini.Speed;z=ghini.ZFixed;
                    }
                    visited.Add(state);var rng=entities.Random.CaptureState();
                    if(state!=0)
                    {
                        var record=actor is TargetGhiniCharacter targetActor?targetActor.Record:((GhiniCharacter)actor).Record;
                        FailIf(record.AssetRoot!=rooms.AssetRoot || record.RadiusY!=native[0xd0a6] || record.RadiusX!=native[0xd0a7] ||
                            record.RawDamage!=native[0xd0a8] || record.Palette!=(native[0xd09b]&7),
                            $"{game}/Ghini$17:${subid:x2}: native variant collision and selected palette properties differ.");
                    }
                    FailIf(state!=native[0xd084] || counter!=native[0xd086] || angle!=native[0xd089] || speed!=native[0xd090] ||
                        actor.Position!=new Vector2(native.Word(0xd08c)/256f,native.Word(0xd08a)/256f) || (ushort)z!=native.Word(0xd08e) ||
                        actor.Visible!=((native[0xd09a]&128)!=0) || actor.CollisionEnabled!=((native[0xd0a4]&128)!=0) ||
                        actor.AnimationParameter!=native[0xd0a1] || SomariaPrivate<int>(actor.Animation,"_frameCounter")!=native[0xd0a0] ||
                        rng.Rng1!=native[0xff94] || rng.Rng2!=native[0xff95] || rng.Calls-seed.Calls!=native.RandomCalls || entities.RoomEnemyCount!=0,
                        $"{game}/Ghini$17:${subid:x2}/batch{batched}/update{updates}: native state{state}/{native[0xd084]}, counter{counter}/{native[0xd086]}, angle{angle}/{native[0xd089]}, speed{speed}/{native[0xd090]}, XY{actor.Position}/{native.Word(0xd08c)/256f},{native.Word(0xd08a)/256f}, Z{z}/{(short)native.Word(0xd08e)}, visible{actor.Visible}/{(native[0xd09a]&128)!=0}, collision{actor.CollisionEnabled}/{(native[0xd0a4]&128)!=0}, anim{actor.AnimationParameter}/{native[0xd0a1]}, timer{SomariaPrivate<int>(actor.Animation,"_frameCounter")}/{native[0xd0a0]}, RNG{rng.Rng1:x2}{rng.Rng2:x2}/{native[0xff94]:x2}{native[0xff95]:x2}, calls{rng.Calls-seed.Calls}/{native.RandomCalls}, count{entities.RoomEnemyCount}.");
                });
                root._scene.Dialogue.ShowMessage("Ghini native initialization hold.",104);native[0xcba0]=1;Step(3);
                root._scene.Dialogue.Close();native[0xcba0]=0;Step(subid==1?1100:450);
                if(subid==1)FailIf(!new[]{8,9,10,11,12}.All(visited.Contains),$"{game}: slow Ghini must complete its native fade, wander, slow, rest and acceleration phases.");
                memory.SetField("wDisabledObjects",4);native[0xcc8a]=4;Step(3);
                memory.SetField("wDisabledObjects",0);native[0xcc8a]=0;Step(3);
            }
            foreach(int parentSubid in new[]{0,1,2})
            {
                Load();
                foreach(int subid in new[]{parentSubid,1,1,0})
                    FailIf(!entities.TryCreateEnemy(new GhiniSpawn(new(40+entities.RoomEnemyCount*24,56),subid,"CountedGhini","ghini.s:counted constructor proof")),
                        "Common Ghini checked constructor unexpectedly exhausted the enemy pool.");
                var seed=entities.Random.CaptureState();var native=SharedNativeEnemyReference(rooms,player,0x17);
                native[0xff94]=seed.Rng1;native[0xff95]=seed.Rng2;native[0xcdd1]=4;
                int slot=0;
                foreach(int subid in new[]{parentSubid,1,1,0})
                {
                    int at=0xd080+slot*256;native[at]=1;native[at+1]=0x17;native[at+2]=(byte)subid;
                    native.Word(at+0xc,(40+slot*24)*256);native.Word(at+0xa,56*256);slot++;
                }
                app.Step(2,Vector2.Zero,batched:batched,afterUpdate:()=>native.AdvanceEnemies(entities.FrameCounter));
                FailIf(entities.RoomEnemyCount!=4,"Native scripted/grave Ghini allocation must be counted, unlike standalone debug spawning.");
                ((EnemyCharacter)entities.ResolveEnemySlot(0)!.Node).Health=0;native[0xd0a9]=0;
                app.Step(3,Vector2.Zero,batched:batched,afterUpdate:()=>
                {
                    native.AdvanceEnemies(entities.FrameCounter);native.AdvanceParts();
                    for(int index=0;index<4;index++)
                        FailIf((entities.ResolveEnemySlot(index) is null)!=(native[0xd080+index*256]==0),
                            $"{game}: parent$17:${parentSubid:x2} death must kill all slow Ghinis and preserve the other normal Ghini in physical slot order.");
                    FailIf(entities.RoomEnemyCount!=native[0xcdd1],$"{game}: parent-death enemy count {entities.RoomEnemyCount}/{native[0xcdd1]} differs from native getFreeEnemySlot/decNumEnemies.");
                });
                bool killsChildren=parentSubid!=1;
                FailIf(entities.ResolveEnemySlot(3) is null || (entities.ResolveEnemySlot(1) is null)!=killsChildren ||
                    (entities.ResolveEnemySlot(2) is null)!=killsChildren,
                    "Normal/target Ghini deaths kill their slow children; a slow Ghini dies alone, retaining the normal sibling.");
            }
        }
        finally
        {
            player.EndCutsceneControl();entities.EnableLinkCollisionsAndMenu();memory.SetField("wDisabledObjects",0);
            if(game==OracleGame.Ages)root.LoadDebugRoom(originalGroup,originalRoom);else root._seasonsOpening!.DebugTravel(originalGroup,originalRoom);
            entities.Random.RestoreState(originalRandom);
        }
    }
}
