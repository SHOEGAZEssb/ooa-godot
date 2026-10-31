using Godot;
using System.Collections.Generic;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private static void CheckSharedOctorok(FrontendValidationRoot root, bool batched)
    {
        var rooms = root._gameplaySession!.Rooms;
        var entities = root._seasonsOpening?.Entities ?? root._entities;
        var player = root._scene.Player;
        var memory = rooms.RuntimeState;
        var game = rooms.SaveData.Game;
        var app = new ApplicationValidationFixture(root);
        var initialRandom = entities.Random.CaptureState();
        var originalProjection = entities.WorldToScreen;
        int originalGroup = rooms.ActiveGroup, originalRoom = rooms.CurrentRoom.Id;
        void Load(int group, int room)
        {
            root._scene.Dialogue.Close();
            if (game == OracleGame.Ages) root.LoadDebugRoom(group, room);
            else root._seasonsOpening!.DebugTravel(group, room);
            entities.Clear(); entities.DisableLinkCollisionsAndMenu(); player.BeginCutsceneControl();
            player.Inventory.EquipA(0); player.Inventory.EquipB(0); player.Inventory.SetScriptedActiveRing(0xff);
        }
        void Floor()
        {
            Load(0, game == OracleGame.Ages ? 0x98 : 0xc9);
            byte tile = game == OracleGame.Ages ? (byte)0xa0 : (byte)0x04;
            for (int y=0; y<rooms.CurrentRoom.HeightInTiles; y++)
            for (int x=0; x<rooms.CurrentRoom.WidthInTiles; x++)
                rooms.CurrentRoom.SetPositionTileAndCollision(new(x*16+8,y*16+8), tile, 0, 0);
            player.WarpTo(new(120,24));
        }
        try
        {
            var database = OctorokDatabase.For(rooms.AssetRoot);
            int wrappedVisits=0;
            var bounce = CommonEnemyBehavior.Shared.ProjectileBounce;
            FailIf(!database.CounterValues.Select(row => row.Value).SequenceEqual(new[] {30,45,60,75,45,60,75,90}) ||
                !database.WalkCounterValues.Select(row => row.Value).SequenceEqual(new[] {25,33,41,49}) ||
                database.Behavior.ShootDelayFrames != 16 || database.Behavior.PostShotWaitFrames != 32 ||
                bounce.Frames != 32 || bounce.Gravity != 14 || bounce.SpeedRaw != 10 || bounce.InitialSpeedZ != -224 ||
                database.Projectile.NormalAnimation != "127@8,0,0,0;8,8,0,32" ||
                database.Projectile.BounceAnimation != "127@8,0,0,0;8,8,0,32",
                $"{game}: original Octorok and common PART bounce operands differ.");
            foreach (int sub in new[] {0,1,2})
            foreach (bool pressure in new[] {false,true})
            {
                Floor();
                var room = rooms.CurrentRoom;
                entities.Random.RestoreState(initialRandom with { Rng1 = 0x37, Rng2 = 0x0d });
                var seed = entities.Random.CaptureState();
                var entry = new DebugObjectCatalog(rooms.AssetRoot).Enemies.Single(row => row.Id == 9 && row.SubId == sub);
                var texture = new DebugObjectPreview(rooms.AssetRoot).GetTexture(entry,false);
                FailIf(!entry.Supported || texture is null || entities.Random.Calls != seed.Calls,
                    $"{game}: Octorok $09:${sub:x2} preview must use its own pack without initialization/RNG.");
                FailIf(!entities.TrySpawnDebugEnemy(9,sub,new(80,72),out string error),error);
                var actor = entities.Entities<OctorokCharacter>().Single();
                // Native retained position bytes are explicit inputs; F4 accepts whole pixels.
                actor.Position=new(80.25f,72.5f);
                FailIf(!actor.InitializationPending || actor.Visible || entities.Random.Calls != seed.Calls ||
                    actor.Record.Health != (sub == 2 ? 3 : 2) || actor.Record.RawDamage != (sub == 2 ? 0xfc : 0xfe) ||
                    actor.Record.SpeedRaw != (sub == 1 ? 30 : 20) || actor.Record.CounterMask != (sub == 2 ? 3 : 7),
                    $"{game}: Octorok native construction/properties differ.");
                var native = SharedNativeEnemyReference(rooms,player,9);
                native.SetResidentObjectGraphics(2,game == OracleGame.Ages ? 0x8f : 0x74);
                native[0xff94] = seed.Rng1; native[0xff95] = seed.Rng2;
                native[0xd080] = 1; native[0xd081] = 9; native[0xd082] = (byte)sub;
                native.Word(0xd08a,0x4880); native.Word(0xd08c,0x5040);
                var slots = SomariaPrivate<Dictionary<IRoomEntity,int>>(entities,"_partSlots");
                if (pressure)
                {
                    memory.SetField("wDisabledObjects",8); native[0xcc8a] = 8;
                    for (int index=0; index<16; index++)
                    {
                        var rock = entities.Spawn<OctorokRockProjectile>(new OctorokRockSpawn(new(32,96),8));
                        int slot = slots.Single(pair => ReferenceEquals(pair.Key.PresentationNode,rock)).Value;
                        int at = 0xd0c0 + slot*256;
                        native[at] = 1; native[at+1] = 0x18; native[at+9] = 8;
                        native.Word(at+10,96*256); native.Word(at+12,32*256);
                    }
                }
                var sounds = root._sound.AttachPlayRequestAudit();
                int updates=0, failedBirths=0, visits=0;
                void Step(int count) => app.Step(count,Vector2.Zero,batched:batched,afterUpdate:() =>
                {
                    if (pressure && native[0xcc8a] == 8 && native[0xd084] == 11 && native[0xd086] == 1) failedBirths++;
                    native.AdvanceEnemies(entities.FrameCounter); native.AdvanceParts(); updates++;
                    string context=$"{game}: Octorok $09:${sub:x2}/pressure{pressure}/batch{batched}/update{updates}";
                    var rng=entities.Random.CaptureState();
                    FailIf((int)actor.State != native[0xd084] || actor.Counter1 != native[0xd086] || actor.Angle != native[0xd089] ||
                        SomariaPrivate<int>(actor,"_walkCounter") != native[0xd0b0] || actor.ScentAttractionCounter != native[0xd0bd] ||
                        actor.Position != new Vector2(native.Word(0xd08c)/256f,native.Word(0xd08a)/256f) ||
                        actor.Visible != ((native[0xd09a]&128) != 0) || rng.Rng1 != native[0xff94] || rng.Rng2 != native[0xff95] ||
                        rng.Calls != seed.Calls+native.RandomCalls || sounds.RequestsFor(SoundId.SndThrow) != native.Sounds.Count(sound => sound == 0x51),
                        context+": native parent state/counter/angle/walk/var3d/XY/visibility/RNG/cues differ.");
                    for (int slot=0; slot<16; slot++)
                    {
                        int at=0xd0c0+slot*256;
                        var entity=slots.FirstOrDefault(pair => pair.Value == slot && pair.Key is not IRoomEntityLifetime {Finished:true}).Key;
                        FailIf((entity is null) != (native[at] == 0),$"{context}: PART${0xd0+slot:x2} membership differs.");
                        if (entity is null) continue;
                        visits++;
                        var rock=(OctorokRockProjectile)entity.PresentationNode!;
                        var expectedWrap=new Vector2(native.Word(at+12) >= 0xf800 ? -256 : 0,native.Word(at+10) >= 0xf800 ? -256 : 0);
                        if (expectedWrap != Vector2.Zero) wrappedVisits++;
                        FailIf(rock.SourceOamWrapOffset != expectedWrap,
                            $"{context}: PART${0xd0+slot:x2} must project native edge bytes through its bound OAM wrap owner.");
                        int state=rock.State switch {HostileProjectileState.Initializing=>0,HostileProjectileState.Flying=>1,HostileProjectileState.CollisionPending=>2,_=>3};
                        FailIf(rock.AssetRoot != rooms.AssetRoot || native[at+1] != 0x18 || state != native[at+4] ||
                            rock.Counter != native[at+6] || rock.Angle != native[at+9] ||
                            rock.Position != new Vector2(native.Word(at+12)/256f,native.Word(at+10)/256f) ||
                            (ushort)rock.ZFixed != native.Word(at+14) || rock.Visible != ((native[at+0x1a]&128) != 0),
                            $"{context}: PART${0xd0+slot:x2} state/counter/angle/XYZ/visibility differs; " +
                            $"port={state}/{rock.Counter}/{rock.Position}, native={native[at+4]}/{native[at+6]}/{native.Word(at+12):x4},{native.Word(at+10):x4}.");
                    }
                });
                root._scene.Dialogue.ShowMessage("Native Octorok state zero hold.",player.Position.Y);
                native[0xcba0]=1; Step(3); root._scene.Dialogue.Close(); native[0xcba0]=0;
                Step(360);
                if (pressure)
                {
                    while (failedBirths == 0 && updates < 2048) Step(64);
                    FailIf(failedBirths == 0 || entities.Entities<OctorokRockProjectile>().Count != 16 || sounds.RequestsFor(SoundId.SndThrow) != 0,
                        $"{game}: full PART pool must suppress actual checked shots and their cue while retaining all16 allocations.");
                    memory.SetField("wDisabledObjects",0); native[0xcc8a]=0;
                }
                Step(720);
                while (sounds.RequestsFor(SoundId.SndThrow) < 2 && updates < 4096) Step(64);
                FailIf(visits == 0 || sounds.RequestsFor(SoundId.SndThrow) < 2,
                    $"{game}: native Octorok $09:${sub:x2}/pressure{pressure} fixture did not cover repeated successful firing in {updates} updates.");
            }
            FailIf(wrappedVisits == 0,$"{game}: Octorok fixture must exercise actual byte-wrapped edge presentation.");
            foreach (int angle in new[] {0,8,16,24})
            {
                Floor();
                var room=rooms.CurrentRoom;
                var start=new Vector2(80.75f,72.5f);
                Vector2 direction=angle switch {0=>Vector2.Up,8=>Vector2.Right,16=>Vector2.Down,_=>Vector2.Left};
                Vector2 wall=start+direction*16;
                room.SetPositionTileAndCollision(wall,game == OracleGame.Ages ? (byte)0xa0 : (byte)0x04,0x0f,0);
                var rock=entities.Spawn<OctorokRockProjectile>(new OctorokRockSpawn(start,angle));
                FailIf(rock.Visible || rock.State != HostileProjectileState.Initializing,
                    $"{game}: allocating a rock must leave its native state zero hidden until dispatch.");
                var native=SharedNativeEnemyReference(rooms,player,9);
                native.SetResidentObjectGraphics(2,game == OracleGame.Ages ? 0x8f : 0x74);
                native[0xd0c0]=1; native[0xd0c1]=0x18; native[0xd0c9]=(byte)angle;
                native.Word(0xd0ca,0x4880); native.Word(0xd0cc,0x50c0);
                int updates=0, pending=0, bouncing=0, nativePose=0;
                app.Step(64,Vector2.Zero,batched:batched,afterUpdate:() =>
                {
                    native.AdvanceEnemies(entities.FrameCounter); native.AdvanceParts(); updates++;
                    if (updates == 1) nativePose=native.Word(0xd0de);
                    pending+=native[0xd0c4] == 2 ? 1 : 0;
                    bouncing+=native[0xd0c4] == 3 && native[0xd0c0] != 0 ? 1 : 0;
                    if (bouncing == 1)
                    {
                        using Image normal=SomariaPrivate<Texture2D>(rock,"_normalTexture").GetImage();
                        using Image displayed=SomariaPrivate<Texture2D>(rock,"_bounceTexture").GetImage();
                        FailIf(nativePose == 0 || native.Word(0xd0de) != nativePose || !normal.GetData().SequenceEqual(displayed.GetData()),
                            $"{game}: PART18 bounce must retain its source animation-$00 OAM and pixels.");
                    }
                    int state=rock.State switch {HostileProjectileState.Initializing=>0,HostileProjectileState.Flying=>1,HostileProjectileState.CollisionPending=>2,_=>3};
                    FailIf(rock.Finished != (native[0xd0c0] == 0) || !rock.Finished &&
                        (state != native[0xd0c4] || rock.Counter != native[0xd0c6] || rock.Angle != native[0xd0c9] ||
                         rock.Position != new Vector2(native.Word(0xd0cc)/256f,native.Word(0xd0ca)/256f) ||
                         (ushort)rock.ZFixed != native.Word(0xd0ce)),
                        $"{game}: PART18 actual tile contact angle${angle:x2}/batch{batched}/update{updates}: " +
                        $"port={state}/{rock.Counter}/{rock.Position}, native={native[0xd0c4]}/{native[0xd0c6]}/{native.Word(0xd0cc):x4},{native.Word(0xd0ca):x4}.");
                });
                FailIf(pending != 1 || bouncing != 32 || !rock.Finished,
                    $"{game}: actual PART18 wall contact must move on its pending update, then bounce for32 eligible visits and retire.");
            }
            foreach (Vector2 camera in new[] {new Vector2(96,0),new Vector2(0,96)})
            {
                Floor();
                // Camera bytes are fixture inputs; the actual entity binding consumes them.
                entities.WorldToScreen=point=>point-camera;
                var rock=entities.Spawn<OctorokRockProjectile>(new OctorokRockSpawn(new(80,72),8));
                var native=SharedNativeEnemyReference(rooms,player,9);
                native.SetResidentObjectGraphics(2,game == OracleGame.Ages ? 0x8f : 0x74);
                int cameraY=game == OracleGame.Ages ? 0xffaa : 0xffa8;
                native[cameraY]=(byte)camera.Y; native[cameraY+2]=(byte)camera.X;
                native[0xd0c0]=1; native[0xd0c1]=0x18; native[0xd0c9]=8;
                native.Word(0xd0ca,72*256); native.Word(0xd0cc,80*256);
                root._scene.Dialogue.ShowMessage("Held camera boundary.",player.Position.Y);
                native[0xcba0]=1;
                int updates=0;
                void Step(int count) => app.Step(count,Vector2.Zero,batched:batched,afterUpdate:() =>
                {
                    native.AdvanceEnemies(entities.FrameCounter); native.AdvanceParts(); updates++;
                    FailIf(rock.Finished != (native[0xd0c0] == 0) || !rock.Finished &&
                        (rock.Position != new Vector2(native.Word(0xd0cc)/256f,native.Word(0xd0ca)/256f) ||
                         rock.SourceScreenPosition != rock.Position-camera),
                        $"{game}: held native camera{camera}/batch{batched}/update{updates}: PART18 must use its bound projection, independently of Link.");
                });
                Step(3);
                FailIf(rock.Finished,$"{game}: state zero must initialize outside the held camera, then text must retain it.");
                root._scene.Dialogue.Close(); native[0xcba0]=0; Step(1);
                FailIf(!rock.Finished,$"{game}: first eligible PART18 flight visit must reject the held-camera boundary.");
                Step(2); entities.WorldToScreen=originalProjection;
            }
        }
        finally
        {
            root._scene.Dialogue.Close(); memory.SetField("wDisabledObjects",0);
            entities.WorldToScreen=originalProjection;
            player.EndCutsceneControl(); Load(originalGroup,originalRoom); player.EndCutsceneControl();
            entities.EnableLinkCollisionsAndMenu(); entities.Random.RestoreState(initialRandom);
        }
    }
}
