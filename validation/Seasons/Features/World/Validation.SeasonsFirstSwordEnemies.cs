using Godot;
using System;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateSeasonsFirstSwordEnemiesRom()
    {
        foreach(bool batched in new[]{false,true})
        {
            var root=StartSeasonsAfterImpa(batched);
            try
            {
                var session=root._seasonsOpening!;var entities=session.Entities;
                var app=new ApplicationValidationFixture(root);
                foreach(var spec in new[]{(Id:0x08,SubId:0,Group:0,Room:0xb4),(Id:0x34,SubId:0,Group:4,Room:4),
                    (Id:0x34,SubId:1,Group:4,Room:4)})
                {
                    session.DebugTravel(spec.Group,spec.Room);entities.Clear();
                    session.Link.WarpTo(new(88,64));session.Link.BeginCutsceneControl();
                    entities.DisableLinkCollisionsAndMenu();session.Inventory.EquipA(0);session.Inventory.EquipB(0);
                    var seed=entities.Random.CaptureState();
                    FailIf(!entities.TrySpawnDebugEnemy(spec.Id,spec.SubId,new(64,56),out var error),error);
                    var actor=(EnemyCharacter)entities.ResolveEnemySlot(0)!.Node;
                    var native=SharedNativeEnemyReference(session.Rooms,session.Link,spec.Id);
                    native[0xff94]=seed.Rng1;native[0xff95]=seed.Rng2;
                    native[0xd080]=1;native[0xd081]=(byte)spec.Id;native[0xd082]=(byte)spec.SubId;
                    native.Word(0xd08c,64*256);native.Word(0xd08a,56*256);
                    int updates=0;var visited=new System.Collections.Generic.HashSet<int>();
                    app.Step(600,Vector2.Zero,batched:batched,afterUpdate:()=>
                    {
                        native.AdvanceEnemies(entities.FrameCounter);
                        updates++;
                        int state,counter,counter2=0,speed=0,z=0;
                        if(actor is RiverZoraCharacter zora){state=zora.State;counter=zora.Counter;}
                        else
                        {
                            var zol=(ZolCharacter)actor;state=(int)zol.State;
                            if(state>=16)state-=8;
                            counter=zol.Counter1;counter2=zol.Counter2;speed=zol.NativeSpeed;z=zol.ZFixed;
                        }
                        visited.Add(state);var rng=entities.Random.CaptureState();
                        FailIf(state!=native[0xd084] || counter!=native[0xd086] || actor is ZolCharacter &&
                            (counter2!=native[0xd087] || speed!=native[0xd090] || (ushort)z!=native.Word(0xd08e)) ||
                            actor.Position!=new Vector2(native.Word(0xd08c)/256f,native.Word(0xd08a)/256f) ||
                            actor.Visible!=((native[0xd09a]&128)!=0) || actor.CollisionEnabled!=((native[0xd0a4]&128)!=0) ||
                            rng.Rng1!=native[0xff94] || rng.Rng2!=native[0xff95] || rng.Calls-seed.Calls!=native.RandomCalls,
                            $"Seasons ENEMY${spec.Id:x2}:${spec.SubId:x2}/batch{batched}/update{updates}: state{state}/{native[0xd084]}, counter{counter}/{native[0xd086]}, counter2{counter2}/{native[0xd087]}, speed{speed}/{native[0xd090]}, XY{actor.Position}/{native.Word(0xd08c)/256f},{native.Word(0xd08a)/256f}, Z{z}/{(short)native.Word(0xd08e)}, visible{actor.Visible}/{(native[0xd09a]&128)!=0}, collision{actor.CollisionEnabled}/{(native[0xd0a4]&128)!=0}, RNG{rng.Rng1:x2}{rng.Rng2:x2}/{native[0xff94]:x2}{native[0xff95]:x2}, calls{rng.Calls-seed.Calls}/{native.RandomCalls}.");
                    });
                    FailIf(spec.Id==0x08 && !new[]{8,9,10,11}.All(visited.Contains),
                        "Seasons River Zora must find native water, surface, fire and submerge during the executed ROM comparison.");
                    session.Link.EndCutsceneControl();entities.EnableLinkCollisionsAndMenu();
                }
            }
            finally {root.Free();}
        }
    }
}
