
namespace Oracles;

public sealed partial class ValidationRoot
{
    private static void CheckSharedArchers(FrontendValidationRoot root, bool batched)
    {
        var rooms = root._gameplaySession!.Rooms;
        var entities = root._seasonsOpening?.Entities ?? root._entities;
        var player = root._scene.Player;
        var memory = rooms.RuntimeState;
        var game = rooms.SaveData.Game;
        var app = new ApplicationValidationFixture(root);
        var initialRandom = entities.Random.CaptureState();
        var projection = entities.WorldToScreen;
        int originalGroup = rooms.ActiveGroup, originalRoom = rooms.CurrentRoom.Id;
        var slots = SomariaPrivate<Dictionary<IRoomEntity,int>>(entities,"_partSlots");
        void Load(int group, int room)
        {
            root._scene.Dialogue.Close(); memory.SetField("wDisabledObjects",0);
            if (game == OracleGame.Ages) root.LoadDebugRoom(group,room);
            else root._seasonsOpening!.DebugTravel(group,room);
            entities.Clear(); entities.DisableLinkCollisionsAndMenu(); player.BeginCutsceneControl();
            player.Inventory.EquipA(0); player.Inventory.EquipB(0); player.Inventory.SetScriptedActiveRing(0xff);
        }
        void Floor()
        {
            Load(0,game == OracleGame.Ages ? 0x98 : 0xc9);
            byte tile = game == OracleGame.Ages ? (byte)0xa0 : (byte)0x04;
            for (int y=0; y<rooms.CurrentRoom.HeightInTiles; y++)
            for (int x=0; x<rooms.CurrentRoom.WidthInTiles; x++)
                rooms.CurrentRoom.SetPositionTileAndCollision(new(x*16+8,y*16+8),tile,0,0);
            player.WarpTo(new(120,24));
        }
        void Targets(BraceletRom native)
        {
            int target = game == OracleGame.Ages ? 0xffb0 : 0xffae;
            native[target] = (byte)player.Position.Y; native[target+1] = (byte)player.Position.X;
            native[target+2] = (byte)player.Position.Y; native[target+3] = (byte)player.Position.X;
            native[0xd00b] = (byte)player.Position.Y; native[0xd00d] = (byte)player.Position.X;
        }
        void Parts(BraceletRom native, string context)
        {
            for (int slot=0; slot<16; slot++)
            {
                int at=0xd0c0+slot*256;
                var entry = slots.FirstOrDefault(pair => pair.Value==slot && pair.Key is not IRoomEntityLifetime { Finished:true }).Key;
                FailIf((entry is null)!=(native[at]==0),context+$": PART${0xd0+slot:x2} membership differs.");
                if (entry is null) continue;
                var arrow=(EnemyArrowProjectile)entry.PresentationNode!;
                FailIf(native[at+1]!=0x1a || arrow.AssetRoot!=rooms.AssetRoot || arrow.NativeState!=native[at+4] ||
                    arrow.Counter!=native[at+6] || arrow.Angle!=native[at+9] ||
                    arrow.Position!=new Vector2(native.Word(at+12)/256f,native.Word(at+10)/256f) ||
                    (ushort)arrow.ZFixed!=native.Word(at+14) || arrow.Visible!=((native[at+0x1a]&128)!=0) ||
                    arrow.AnimationCounter!=native[at+0x20] || arrow.AnimationParameter!=native[at+0x21] ||
                    arrow.State==HostileProjectileState.Bouncing && arrow.AnimationFrame!=((32-arrow.Counter)/6)%4 ||
                    arrow.CollisionBounds.Size!=new Vector2(native[at+0x27]*2,native[at+0x26]*2),
                    context+$": PART${0xd0+slot:x2} native state/counter/angle/XYZ/visibility/radii differ; " +
                    $"runtime={arrow.NativeState}/{arrow.Counter}/{arrow.Position}/{arrow.CollisionBounds.Size}/{arrow.AnimationCounter}, " +
                    $"native={native[at+4]}/{native[at+6]}/{native.Word(at+12):x4},{native.Word(at+10):x4}/{native[at+0x26]:x2},{native[at+0x27]:x2}/{native[at+0x20]:x2}.");
            }
        }
        try
        {
            foreach (int id in new[] {0x0c,0x20,0x22})
            foreach (int sub in new[] {0,1})
            {
                Floor();
                entities.Random.RestoreState(initialRandom with { Rng1=0x37,Rng2=0x0d });
                var seed=entities.Random.CaptureState();
                var entry=new DebugObjectCatalog(rooms.AssetRoot).Enemies.Single(row=>row.Id==id && row.SubId==sub);
                FailIf(!entry.Supported || new DebugObjectPreview(rooms.AssetRoot).GetTexture(entry,false) is null || entities.Random.Calls!=seed.Calls,
                    $"{game}: native archer ${id:x2}:${sub:x2} preview lost its own pack or consumed RNG.");
                FailIf(!entities.TrySpawnDebugEnemy(id,sub,new(80,72),out string error),error);
                var actor=entities.Entities<ArrowMoblinCharacter>().Single(); actor.Position=new(80.25f,72.5f);
                FailIf(actor.Visible || !actor.InitializationPending || entities.Random.Calls!=seed.Calls,
                    $"{game}: archer construction ran state zero.");
                var native=SharedNativeEnemyReference(rooms,player,id);
                native.SetResidentObjectGraphics(3,game==OracleGame.Ages ? 0x8e : 0x73);
                native[0xff94]=seed.Rng1; native[0xff95]=seed.Rng2;
                native[0xd080]=1; native[0xd081]=(byte)id; native[0xd082]=(byte)sub;
                native.Word(0xd08a,0x4880); native.Word(0xd08c,0x5040);
                int updates=0,births=0;
                var seen=new HashSet<EnemyArrowProjectile>();
                var sounds=root._sound.AttachPlayRequestAudit();
                void Compare()
                {
                    Targets(native); native.AdvanceEnemies(entities.FrameCounter); native.AdvanceParts(); updates++;
                    string context=$"{game}: archer${id:x2}:${sub:x2}/batch{batched}/update{updates}";
                    var rng=entities.Random.CaptureState();
                    FailIf((int)actor.State!=native[0xd084] || actor.Counter!=native[0xd086] || actor.Angle!=native[0xd089] ||
                        actor.MoveCycles!=native[0xd0b0] || actor.ScentAttractionCounter!=native[0xd0bd] || actor.Health!=native[0xd0a9] ||
                        actor.Position!=new Vector2(native.Word(0xd08c)/256f,native.Word(0xd08a)/256f) ||
                        actor.Visible!=((native[0xd09a]&128)!=0) || rng.Rng1!=native[0xff94] || rng.Rng2!=native[0xff95] ||
                        rng.Calls!=seed.Calls+native.RandomCalls || sounds.RequestsFor(SoundId.SndThrow)!=0,
                        context+": native parent state/route/XY/health/visibility/RNG or absent throw cue differs.");
                    if (updates==1)
                        FailIf(actor.Record.RadiusY!=native[0xd0a6] || actor.Record.RadiusX!=native[0xd0a7] ||
                            actor.Record.RawDamage!=native[0xd0a8] || actor.Record.Palette!=(native[0xd09b]&7),
                            context+": native variant properties differ.");
                    Parts(native,context);
                    foreach (var arrow in entities.Entities<EnemyArrowProjectile>()) if (seen.Add(arrow)) births++;
                    // Explicit next-update Link input makes eligible natural turns face him.
                    if (actor.State==ArrowMoblinState.Turning && actor.Counter==1)
                    {
                        var prediction=new OracleRandom(); prediction.RestoreState(rng);
                        Vector2 target=actor.Position+OracleObjectMath.StrictCardinalVector(prediction.NextCardinalAngle())*32;
                        player.WarpTo(new(Mathf.Clamp(target.X,8,rooms.CurrentRoom.Width-8),Mathf.Clamp(target.Y,8,rooms.CurrentRoom.Height-8)));
                    }
                }
                void Step(int count)=>app.Step(count,Vector2.Zero,batched:batched,afterUpdate:Compare);
                root._scene.Dialogue.ShowMessage("Native archer initialization hold.",player.Position.Y); native[0xcba0]=1;
                Step(3); root._scene.Dialogue.Close(); native[0xcba0]=0;
                Step(400);
                while (births<2 && updates<2048) Step(64);
                FailIf(births<2,$"{game}: archer ${id:x2}:${sub:x2} did not repeat natural arrow production.");
                entities.Clear(); FailIf(entities.Entities<EnemyArrowProjectile>().Count!=0,"Room cancellation retained archer arrows.");
            }

            foreach (int sub in new[] {0,1})
            foreach (int angle in new[] {0,8,16,24})
            {
                Floor(); var room=rooms.CurrentRoom;
                Vector2 start=new(80.75f,72.5f);
                var arrow=entities.Spawn<EnemyArrowProjectile>(new EnemyArrowSpawn(start,angle,sub));
                FailIf(arrow.Visible || arrow.Position!=start.Floor() || arrow.NativeState!=0,
                    $"{game}: PART$1a construction must copy high XY and defer visibility/offset to state zero.");
                var native=SharedNativeEnemyReference(rooms,player,0x0c);
                native.SetResidentObjectGraphics(3,game==OracleGame.Ages ? 0x8e : 0x73);
                native[0xd0c0]=1; native[0xd0c1]=0x1a; native[0xd0c2]=(byte)sub; native[0xd0c9]=(byte)angle;
                native.Word(0xd0ca,72*256); native.Word(0xd0cc,80*256);
                int updates=0; bool bounced=false;
                // Arrow code has no Octorok-style camera-boundary kill.
                Vector2 camera=new(128,80); entities.WorldToScreen=point=>point-camera;
                void Step(int count)=>app.Step(count,Vector2.Zero,batched:batched,afterUpdate:()=>
                {
                    native.AdvanceEnemies(entities.FrameCounter); native.AdvanceParts(); updates++;
                    Parts(native,$"{game}: PART$1a:${sub:x2}/angle${angle:x2}/batch{batched}/update{updates}");
                    if (!arrow.Finished && arrow.State==HostileProjectileState.Bouncing) bounced=true;
                });
                root._scene.Dialogue.ShowMessage("Native arrow initialization hold.",player.Position.Y); native[0xcba0]=1;
                Step(3); root._scene.Dialogue.Close(); native[0xcba0]=0;
                FailIf(arrow.Finished || !arrow.Visible,"PART$1a state zero/text hold must survive the displaced camera.");
                Vector2 wall=arrow.Position+OracleObjectMath.StrictCardinalVector(angle)*24;
                room.SetPositionTileAndCollision(wall,game==OracleGame.Ages?(byte)0xa0:(byte)0x04,0x0f,0);
                native.CopyRoom(room);
                Step(64);
                FailIf(!bounced || !arrow.Finished,$"{game}: native arrow must bounce on the current tile and retire after its common counter.");
                entities.Clear(); entities.WorldToScreen=projection;
            }
            CheckSharedArcherCapacity(root,batched);
        }
        finally
        {
            root._scene.Dialogue.Close(); memory.SetField("wDisabledObjects",0); entities.WorldToScreen=projection;
            player.EndCutsceneControl(); Load(originalGroup,originalRoom); player.EndCutsceneControl();
            entities.EnableLinkCollisionsAndMenu(); entities.Random.RestoreState(initialRandom);
        }
    }
}
