namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateSeasonsRootDungeonEnemiesRom()
    {
        foreach(bool batched in new[]{false,true})
        {
            var root=StartSeasonsAfterImpa(batched);
            try
            {
                var s=root._seasonsOpening!;var entities=s.Entities;var app=new ApplicationValidationFixture(root);
                foreach(var spec in new[]{(Id:0x0a,Sub:0,Updates:640),(Id:0x0e,Sub:0,Updates:320),
                    (Id:0x0e,Sub:1,Updates:320),(Id:0x0e,Sub:2,Updates:320),(Id:0x0e,Sub:5,Updates:320)})
                {
                    if(batched && spec.Id==0x0e && spec.Sub!=1)continue;
                    s.DebugTravel(4,0x0e);entities.Clear();
                    s.Link.WarpTo(new(56,120));s.Link.BeginCutsceneControl();entities.DisableLinkCollisionsAndMenu();
                    s.Inventory.EquipA(0);s.Inventory.EquipB(0);
                    var initial=spec.Id==0x0e?new Vector2(56,24):new Vector2(88,56);
                    FailIf(s.Collision.Collides(initial)||s.Collision.Collides(s.Link.Position),"Native enemy fixture must use original room$0e floor.");
                    var seed=entities.Random.CaptureState();
                    FailIf(!entities.TrySpawnDebugEnemy(spec.Id,spec.Sub,initial,out var error),error);
                    var actor=(EnemyCharacter)entities.ResolveEnemySlot(0)!.Node;
                    var native=SharedNativeEnemyReference(s.Rooms,s.Link,spec.Id);
                    native[0xff94]=seed.Rng1;native[0xff95]=seed.Rng2;
                    native[0xd080]=1;native[0xd081]=(byte)spec.Id;native[0xd082]=(byte)spec.Sub;
                    native.Word(0xd08c,(int)initial.X*256);native.Word(0xd08a,(int)initial.Y*256);
                    s.Sound.AttachPlayRequestAudit();s.Sound.ClearPlayRequestAudit();native.ClearSounds();
                    var visited=new HashSet<int>();bool sawPart=false,sawReturn=false;int update=0;
                    app.Step(spec.Updates,Vector2.Zero,batched:batched,afterUpdate:()=>
                    {
                        native.AdvanceEnemies(entities.FrameCounter);native.AdvanceParts();update++;
                        int state,counter,angle,speed;
                        if(actor is BoomerangMoblinCharacter moblin)
                        {
                            state=(int)moblin.State;counter=SomariaPrivate<int>(moblin,"_counter");
                            angle=SomariaPrivate<int>(moblin,"_angle");speed=BoomerangMoblinDatabase.For(s.AssetRoot).SpeedRaw;
                        }
                        else
                        {
                            var trap=(BladeTrapCharacter)actor;state=(int)trap.State;counter=trap.Counter;angle=trap.Angle;speed=trap.SpeedRaw;
                        }
                        visited.Add(state);var rng=entities.Random.CaptureState();
                        string context=$"Seasons ENEMY${spec.Id:x2}:${spec.Sub:x2}/batch{batched}/update{update}";
                        FailIf(state!=native[0xd084] || counter!=native[0xd086] || angle!=native[0xd089] || speed!=native[0xd090] ||
                            actor.Position!=new Vector2(native.Word(0xd08c)/256f,native.Word(0xd08a)/256f) ||
                            actor.Animation.RemainingUpdates!=native[0xd0a0] || actor.AnimationParameter!=native[0xd0a1] ||
                            actor.Visible!=((native[0xd09a]&128)!=0) || actor.CollisionEnabled!=((native[0xd0a4]&128)!=0) ||
                            rng.Rng1!=native[0xff94] || rng.Rng2!=native[0xff95] || rng.Calls-seed.Calls!=native.RandomCalls,
                            $"{context}: state{state}/{native[0xd084]}, counter{counter}/{native[0xd086]}, angle${angle:x2}/${native[0xd089]:x2}, speed${speed:x2}/${native[0xd090]:x2}, XY{actor.Position}/{native.Word(0xd08c)/256f},{native.Word(0xd08a)/256f}, anim{actor.Animation.RemainingUpdates}/{native[0xd0a0]}, parameter{actor.AnimationParameter}/{native[0xd0a1]}, RNG{rng.Rng1:x2}{rng.Rng2:x2}/{native[0xff94]:x2}{native[0xff95]:x2}, calls{rng.Calls-seed.Calls}/{native.RandomCalls}.");
                        var parts=entities.Entities<MoblinBoomerangProjectile>();
                        int nativeParts=Enumerable.Range(0,8).Count(slot=>native[0xd0c0+slot*0x100]!=0 && native[0xd0c1+slot*0x100]==0x21);
                        FailIf(parts.Count!=nativeParts,$"{context}: PART$21 allocation/lifetime differs.");
                        foreach(var part in parts)
                        {
                            var adapter=entities.EntityAdapters<MoblinBoomerangRoomEntity>().Single(candidate=>ReferenceEquals(candidate.Node,part));
                            int slot=Enumerable.Range(0,8).Single(index=>ReferenceEquals(entities.ResolvePartSlot(index),adapter));
                            int at=0xd0c0+slot*0x100;sawPart=true;sawReturn|=part.Returning;
                            FailIf(native[at]==0 || native[at+1]!=0x21 || part.Counter!=native[at+6] || part.SpeedRaw!=native[at+0x10] ||
                                part.Returning!=(native[at+4]==2) || part.Position!=new Vector2(native.Word(at+0x0c)/256f,native.Word(at+0x0a)/256f),
                                $"{context}: boomerang slot${slot:x2}, counter{part.Counter}/{native[at+6]}, speed{part.SpeedRaw}/{native[at+0x10]}, return{part.Returning}/{native[at+4]}, XY{part.Position}/{native.Word(at+0x0c)/256f},{native.Word(at+0x0a)/256f}.");
                        }
                        foreach(int cue in new[]{SoundId.SndUnknown5,SoundId.SndClink,SoundId.SndMoveBlock})
                            FailIf(s.Sound.PlayRequestsFor(cue)!=native.Sounds.Count(sound=>sound==cue),$"{context}: cue${cue:x2} differs.");
                    });
                    FailIf(spec.Id==0x0a? !sawPart||!sawReturn||!new[]{8,9,10}.All(visited.Contains):
                        !new[]{8,9,10,11}.All(visited.Contains)||spec.Sub!=0&&!visited.Contains(12),
                        $"ENEMY${spec.Id:x2}:${spec.Sub:x2} must exercise its original movement/attack/return states.");
                    s.Link.EndCutsceneControl();entities.EnableLinkCollisionsAndMenu();
                }
            }
            finally {root.Free();}
        }
    }
}
