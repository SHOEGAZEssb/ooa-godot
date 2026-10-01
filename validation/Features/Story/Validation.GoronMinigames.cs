using Godot;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void AdvanceGoronDialogue(int frames,int choice=0)
    {
        for(int i=0;i<frames;i++)
        {
            if(_dialogue.IsOpen)
            { if(_dialogue.ChoiceActive) _dialogue.SubmitChoiceForValidation(choice); else _dialogue.Close(); }
            StepGameplayUpdates(1,Vector2.Zero);
        }
    }
    private void ValidateGoronBigBang()
    {
        // bank3.objectSpeedTable has 40 words per speed. Halving PART $49's
        // $2d byte yields $16, an offset into SPEED_80, not SPEED_60.
        var half=OracleObjectSpeedTable.Shared.GetRaw(0x16,ObjectAngle.Up);
        var quarter=OracleObjectSpeedTable.Shared.GetRaw(0x0b,ObjectAngle.Up);
        FailIf(half.YFixed!=0x80||half.XFixed!=0||quarter.YFixed!=0||quarter.XFixed!=0x40,
            "PART $49 raw halved speeds did not preserve the source's unaligned sine-table reads.");
        LoadValidationRoom(3,0x3e); StepGameplayUpdates(4,Vector2.Zero);
        var cave=_roomEvents.Get<GoronCaveEvent>();
        var host=cave.Actors.Single();
        TalkGoronFromFloor(host);
        FailIf(_saveData.HasRoomFlag(3,0x3e,0x40),"Big Bang attendant accepted absent Goronade.");
        _inventory.GiveTreasure(TreasureId.Goronade,0);
        _inventory.AddRupees(100);
        ApproachGoronFromFloor(host); StepGameplayUpdates(1,Vector2.Zero,["attack"],["attack"]);
        AdvanceToBigBangCheckingPrize(host,1600);
        FailIf(host.BigBang?.Playing!=true||_inventory.HasTreasure(TreasureId.Goronade)||!_saveData.HasRoomFlag(3,0x3e,0x40)||_inventory.Rupees!=100,
            "Big Bang first game did not consume Goronade, save its trade, and waive the fee.");
        var game=host.BigBang!;
        int health=_inventory.HealthQuarters;
        var spawner=game.Parts.Single();
        StepGameplayUpdates(1,Vector2.Zero);
        FailIf(spawner.State!=5||spawner.Counter!=60,"PART $49 spawner did not initialize its 60-update first wave.");
        StepGameplayUpdates(59,Vector2.Zero);
        FailIf(game.Parts.Count!=1||spawner.Counter!=1,"Big Bang spawned before the first 60-update boundary.");
        StepGameplayUpdates(1,Vector2.Zero);
        FailIf(game.Parts.Count!=2||spawner.Wave!=1||game.Parts[1].State!=1,
            "Big Bang wave did not initialize its later PART slot in the same update.");
        var firstBomb=game.Parts[1];
        for(int i=0;i<1800&&game.Playing;i++)
        {
            AdvanceGoronDialogue(1,1);
            if(firstBomb.Finished&&GodotObject.IsInstanceValid(firstBomb.Node)&&firstBomb.Node.IsQueuedForDeletion())
                firstBomb.Node.Free();
        }
        AdvanceGoronDialogue(400,1);
        FailIf(game.Playing||_inventory.HealthQuarters!=health||!_inventory.HasTreasure(TreasureId.OldMermaidKey)||cave.BlocksGameplay||
            _rooms.CurrentRoom.GetMetatile(new(0x18,0x18))!=0xef,
            $"Big Bang completion: playing={game.Playing}, health={_inventory.HealthQuarters}/{health}, key={_inventory.HasTreasure(TreasureId.OldMermaidKey)}, blocked={cave.BlocksGameplay}, tile={_rooms.CurrentRoom.GetMetatile(new(0x18,0x18)):x2}, command={host.CommandIndex}, counter={host.Counter}, invincibility={_player.InvincibilityFrames}, parts={game.Parts.Count}, status={_entities.RuntimeState.ReadWramByte(WramAddress.wTmpcfc0)}.");
        ApproachGoronFromFloor(host); StepGameplayUpdates(1,Vector2.Zero,["attack"],["attack"]);
        AdvanceGoronDialogue(100,1);
        FailIf(_inventory.Rupees!=100||cave.BlocksGameplay,"Declining a repeat Big Bang game charged money or locked input.");
        ApproachGoronFromFloor(host); StepGameplayUpdates(1,Vector2.Zero,["attack"],["attack"]);
        AdvanceToBigBangCheckingPrize(host,1200);
        FailIf(!game.Playing||_inventory.Rupees!=90,"Repeat Big Bang game did not charge 10 rupees.");
        // Follow a settled bomb through the real part/collision/script phases.
        GoronBombRoomEntity? grounded=null;
        for(int i=0;i<400&&grounded is null;i++)
        {
            StepGameplayUpdates(1,Vector2.Zero);
            grounded=game.Parts.FirstOrDefault(p=>p.State==3);
        }
        FailIf(grounded is null,"Big Bang never produced a grabbable bomb.");
        _player.WarpTo(grounded!.Node.Position);
        for(int i=0;i<30&&_player.InvincibilityFrames==0;i++) StepGameplayUpdates(1,Vector2.Zero);
        FailIf(_player.InvincibilityFrames!=0x22||_inventory.HealthQuarters!=health||!game.Playing,
            "Big Bang collision did not preserve health and defer the loss script until the next object update.");
        AdvanceGoronDialogue(500,1);
        FailIf(game.Playing||cave.BlocksGameplay||_inventory.Rupees!=90,
            "Big Bang loss did not clear its bombs and release Link without another fee.");
    }
    private void AdvanceToBigBangCheckingPrize(GoronCaveScriptHost host,int limit)
    {
        NpcCharacter? display=null;
        for(int i=0;i<limit&&host.BigBang?.Playing!=true;i++)
        {
            AdvanceGoronDialogue(1);
            var prize=_entities.Entities<NpcCharacter>().SingleOrDefault(n=>n.Active&&n.Record.Id==0x60);
            if(prize is null) continue;
            display=prize;
            // scriptHelper.s goron_bigBang_spawnPrize: Y=$38, X=$50,
            // Z=$f0. Every displayed treasure retains visiblec2 priority.
            FailIf(!prize.Visible||!prize.IsVisibleInTree()||prize.CurrentAnimationOpaquePixels==0||
                prize.Position!=new Vector2(0x50,0x38)||prize.ScriptDrawOffset!=new Vector2(0,-16)||prize.ZIndex!=9,
                "Room $3:$3e Big Bang display prize was hidden or lost its source position/Z/visiblec2 priority.");
        }
        FailIf(display is null||display.Active||display.Visible,
            "Room $3:$3e Big Bang did not display and remove its prize before play, including repeat entry.");
    }
    private void ValidateGoronBigBangPartLifetime()
    {
        var traces=new List<string>();
        foreach(bool batched in new[]{false,true})
        {
            ReinitializeGameplayForValidation();
            _saveData.SetRoomFlag(3,0x3e,0x40,true);
            _inventory.AddRupees(100);
            LoadValidationRoom(3,0x3e); StepGameplayUpdates(4,Vector2.Zero);
            var cave=_roomEvents.Get<GoronCaveEvent>();
            var host=cave.Actors.Single();
            ApproachGoronFromFloor(host); StepGameplayUpdates(1,Vector2.Zero,["attack"],["attack"]);
            AdvanceToBigBangCheckingPrize(host,1200);
            var game=host.BigBang!;
            FailIf(!game.Playing||_inventory.Rupees!=90,"Room $3:$3e lifetime regression did not start the paid game.");
            var observed=new HashSet<GoronBombRoomEntity>(game.Parts);
            var trace=new List<string>();
            int disposed=0;
            bool wasPlaying=true,clearedUnderWhite=false;
            void AfterUpdate()
            {
                foreach(var part in _entities.EntityAdapters<GoronBombRoomEntity>()) observed.Add(part);
                // Synchronous validations share one host frame. Reproduce the
                // engine's end-of-frame destruction only after the manager has
                // retired and queued the naturally completed PART $49 actor.
                foreach(var part in observed)
                    if(part.Finished&&GodotObject.IsInstanceValid(part.Node)&&part.Node.IsQueuedForDeletion())
                    { part.Node.Free(); disposed++; }
                if(wasPlaying&&!game.Playing)
                {
                    // scripts.s goron_bigBang_loadNormalRoomLayout calls
                    // clearParts while white, after unhide/initLinkPosition.
                    clearedUnderWhite=_warpFade.Color.A==1;
                    FailIf(!clearedUnderWhite||!host.Actor.Visible||_player.Position!=new Vector2(0x50,0x48)||
                        observed.Any(p=>!p.Finished)||game.Parts.Count!=0,
                        "Room $3:$3e clearParts did not retire every live bomb under white at source Link position ($50,$48).");
                }
                wasPlaying=game.Playing;
                trace.Add($"{game.Playing}:{game.Parts.Count}:{host.CommandIndex}:{host.Counter}:{_warpFade.Color.A}:{_player.CutsceneControlled}:{disposed}");
                if(_dialogue.IsOpen)
                { if(_dialogue.ChoiceActive) _dialogue.SubmitChoiceForValidation(1); else _dialogue.Close(); }
            }
            void Advance(int updates)=>StepGameplayUpdates(updates,Vector2.Zero,batched:batched,afterUpdate:AfterUpdate);
            Advance(240);
            FailIf(disposed==0||!game.Playing,"Room $3:$3e did not dispose a naturally expired bomb before the loss.");
            GoronBombRoomEntity? grounded=null;
            for(int i=0;i<400&&grounded is null;i++)
            {
                Advance(1);
                grounded=_entities.EntityAdapters<GoronBombRoomEntity>().FirstOrDefault(p=>p.State==3&&!p.Finished);
            }
            FailIf(grounded is null,"Room $3:$3e did not produce a settled bomb after retiring its first wave.");
            int health=_inventory.HealthQuarters;
            _player.WarpTo(grounded!.Node.Position);
            for(int i=0;i<30&&_player.InvincibilityFrames==0;i++) Advance(1);
            FailIf(_player.InvincibilityFrames!=0x22||_inventory.HealthQuarters!=health||!game.Playing,
                "Room $3:$3e PART $49 contact did not defer the no-health-loss result until the next update.");
            Advance(1);
            FailIf(!cave.BlocksGameplay||!game.Playing,"Room $3:$3e loss did not acquire input before its collapsed animation and fade.");
            Advance(500);
            FailIf(!clearedUnderWhite||game.Playing||cave.BlocksGameplay||_player.CutsceneControlled||cave.PaletteBusy||
                _warpFade.Color.A!=0||_inventory.Rupees!=90||_inventory.HasTreasure(TreasureId.OldMermaidKey)||
                _rooms.CurrentRoom.GetMetatile(new(0x18,0x18))!=0xef||
                _rooms.CurrentRoom.GetMetatile(new(0x38,0x78))!=0xb5,
                "Room $3:$3e loss retained white/input/bombs, changed the fee/prize, or failed to restore the normal floor and exit.");
            Advance(8);
            traces.Add(string.Join("\n",trace));
            // The restored attendant must remain reachable through the room's
            // floor geometry. Start again, retire more bombs, then cancel.
            ApproachGoronFromFloor(host); StepGameplayUpdates(1,Vector2.Zero,["attack"],["attack"]);
            AdvanceToBigBangCheckingPrize(host,1200);
            FailIf(!game.Playing||_inventory.Rupees!=80,"Room $3:$3e could not start another paid game after the loss fade.");
            Advance(240);
            var remaining=game.Parts.ToArray();
            cave.Cancel();
            FailIf(game.Playing||game.Parts.Count!=0||remaining.Any(p=>!p.Finished)||_player.CutsceneControlled||
                cave.PaletteBusy||_warpFade.Color.A!=0||_rooms.CurrentRoom.GetMetatile(new(0x38,0x78))!=0xb5,
                "Room $3:$3e cancellation failed after bomb actors had been disposed.");
            wasPlaying=false;
            Advance(8);
        }
        FailIf(traces[0]!=traces[1],"Room $3:$3e bomb retirement/loss/fade differs between individual and batched application updates.");
    }
    private void ValidateGoronBigBangExplosion()
    {
        // Independent partAnimation5ba9f and partOamData5334a/53353/
        // 53856/53877/53898. PART $49 writes flags $0a (bank 1, palette 2)
        // and tile base $0c, then publishes objectSetVisible83.
        string spark="8,0,12,0;8,8,12,32";
        string[] oam=[spark,"8,0,12,7;8,8,12,39",spark,
            "2,250,12,0;2,2,12,32;2,6,12,0;2,14,12,32;10,250,12,0;10,2,12,32;10,6,12,0;10,14,12,32",
            "0,248,0,0;0,0,2,0;0,8,2,32;0,16,0,32;16,248,0,64;16,0,2,64;16,8,2,96;16,16,0,96",
            "0,248,14,0;0,0,14,32;0,8,14,0;0,16,14,32;16,248,14,0;16,0,14,32;16,8,14,0;16,16,14,32"];
        int[] durations=[4,4,3,7,8,8],parameters=[2,6,6,10,15,0];
        Vector2I[] sizes=[new(16,16),new(16,16),new(16,16),new(28,24),new(32,32),new(32,32)];
        Vector2[] offsets=[new(-8,-8),new(-8,-8),new(-8,-8),new(-14,-14),new(-16,-16),new(-16,-16)];
        var source=OracleGraphicsCache.LoadImage("res://assets/oracle/gfx/spr_common_sprites.png");
        ulong HashPixels(string cells)
        {
            using var image=NpcCharacter.BuildPositionedOamTexture(source,cells,0x0c,2,null,true,0).Texture.GetImage();
            ulong hash=14695981039346656037UL;
            foreach(byte pixel in image.GetData()) { hash^=pixel; hash*=1099511628211UL; }
            return hash;
        }
        ulong[] hashes=oam.Select(HashPixels).ToArray();
        var traces=new List<string>();
        foreach(bool batched in new[]{false,true})
        {
            ReinitializeGameplayForValidation();
            _saveData.SetRoomFlag(3,0x3e,0x40,true); _inventory.AddRupees(100);
            LoadValidationRoom(3,0x3e); StepGameplayUpdates(4,Vector2.Zero);
            var host=_roomEvents.Get<GoronCaveEvent>().Actors.Single();
            ApproachGoronFromFloor(host); StepGameplayUpdates(1,Vector2.Zero,["attack"],["attack"]);
            AdvanceToBigBangCheckingPrize(host,1200);
            var observed=new HashSet<GoronBombRoomEntity>();
            var ages=new Dictionary<GoronBombRoomEntity,int>();
            var positions=new Dictionary<GoronBombRoomEntity,Vector2>();
            var trace=new List<string>();
            int completed=0;
            void CheckExplosions()
            {
                foreach(var part in _entities.EntityAdapters<GoronBombRoomEntity>()) observed.Add(part);
                foreach(var part in observed)
                {
                    if(part.State!=4) continue;
                    int age=ages.GetValueOrDefault(part);
                    if(part.Finished)
                    {
                        if(age==35) continue;
                        FailIf(age!=34,"Room $3:$3e PART $49 explosion did not delete on update 34 at parameter $ff.");
                        completed++; ages[part]=35;
                        if(GodotObject.IsInstanceValid(part.Node)&&part.Node.IsQueuedForDeletion()) part.Node.Free();
                        continue;
                    }
                    int frame=0,remaining=age;
                    while(frame<durations.Length&&remaining>=durations[frame]) remaining-=durations[frame++];
                    FailIf(frame>=durations.Length,"Room $3:$3e PART $49 explosion remained active after its 34 visible updates.");
                    var actor=(NpcCharacter)part.Node;
                    if(age==0) positions.Add(part,actor.Position);
                    FailIf(!actor.Visible||!actor.IsVisibleInTree()||actor.ZIndex!=8||actor.Record.SpriteName!="spr_common_sprites"||
                        actor.Record.TileBase!=0x0c||actor.Record.Palette!=2||actor.CurrentAnimationFrame!=frame||
                        actor.CurrentAnimationParameter!=parameters[frame]||actor.CurrentAnimationTextureSize!=sizes[frame]||
                        actor.CurrentAnimationOffset!=offsets[frame]||actor.CurrentAnimationPixelHash!=hashes[frame]||
                        actor.Position!=positions[part]||actor.ScriptDrawOffset!=Vector2.Zero,
                        $"Room $3:$3e PART $49 explosion update {age}, frame {frame}: visible={actor.Visible}/{actor.IsVisibleInTree()}, " +
                        $"sprite={actor.Record.SpriteName}, tile=${actor.Record.TileBase:x2}, palette={actor.Record.Palette}, priority={actor.ZIndex}, " +
                        $"animation={actor.CurrentAnimationFrame}/{actor.CurrentAnimationParameter}, size={actor.CurrentAnimationTextureSize}/{sizes[frame]}, " +
                        $"offset={actor.CurrentAnimationOffset}/{offsets[frame]}, hash={actor.CurrentAnimationPixelHash:x16}/{hashes[frame]:x16}, " +
                        $"position={actor.Position}/{positions[part]}, Z offset={actor.ScriptDrawOffset}; expected source OAM/palette/position and visible83.");
                    trace.Add($"{age}:{frame}:{actor.CurrentAnimationPixelHash}:{actor.Position}");
                    ages[part]=age+1;
                }
            }
            for(int updates=0;updates<400&&completed<2;updates+=4)
                StepGameplayUpdates(4,Vector2.Zero,batched:batched,afterUpdate:CheckExplosions);
            FailIf(completed<2,"Room $3:$3e did not render two complete natural bomb explosions through deletion.");
            traces.Add(string.Join("\n",trace));
        }
        FailIf(traces[0]!=traces[1],"Room $3:$3e explosion pixels/timing differ between individual and batched application updates.");
    }
    private void ValidateGoronTargetCarts()
    {
        _inventory.GiveTreasure(TreasureId.Shooter,0); _inventory.AddRupees(100);
        LoadValidationRoom(5,0xd8); StepGameplayUpdates(4,Vector2.Zero);
        // Exercise the same debug-state entry boundary as the reported ride.
        RestoreDebugSavestate(CaptureDebugSavestate());
        StepGameplayUpdates(4,Vector2.Zero);
        var cave=_roomEvents.Get<GoronCaveEvent>();
        var left=cave.Actors.Single(a=>a.Actor.Record.Var03==0);
        var right=cave.Actors.Single(a=>a.Actor.Record.Var03==1);
        TalkGoronFromFloor(right);
        int b=_inventory.EquippedB,a=_inventory.EquippedA,seeds=_inventory.ScentSeeds;
        ApproachGoronFromFloor(left); StepGameplayUpdates(1,Vector2.Zero,["attack"],["attack"]);
        ValidateTargetCartPrizeDisplay(left,batched:false);
        for(int i=0;i<1300&&!_saveData.HasRoomFlag(5,0xd8,0x80);i++) AdvanceGoronDialogue(1);
        AdvanceGoronDialogue(2);
        FailIf(!_saveData.HasRoomFlag(5,0xd8,0x80)||_inventory.Rupees!=90||_inventory.ScentSeeds!=0x99||
            _entities.EntityAdapters<TargetCartCrystalRoomEntity>().Count()!=5||_entities.Entities<MinecartRoomEntity>().Count!=1,
            $"Target carts setup: flag={_saveData.HasRoomFlag(5,0xd8,0x80)}, rupees={_inventory.Rupees}, seeds={_inventory.ScentSeeds:x2}, crystals={_entities.EntityAdapters<TargetCartCrystalRoomEntity>().Count()}, carts={_entities.Entities<MinecartRoomEntity>().Count}, command={left.CommandIndex}, counter={left.Counter}.");
        Vector2[] sourcePositions=[new(0x38,0x18),new(0x58,0x48),new(0x98,0x28),new(0xc8,0x48),new(0xb8,0x18)];
        FailIf(!_entities.EntityAdapters<TargetCartCrystalRoomEntity>().Select(c=>c.Node.Position).SequenceEqual(sourcePositions),
            "Target-cart configuration zero differs from its independent source positions.");
        StepGameplayUpdates(20,Vector2.Up,["move_up"],["move_up"]);
        var arrivingCart=_entities.Entities<MinecartRoomEntity>().Single();
        // targetCartCrystal.s configuration0, subids $05-$0b. Check the
        // actual mounted scroll before gameplay can repair a zero-position spawn.
        Vector2[] secondRoomPositions=[new(0x38,0x58),new(0x98,0x28),new(0xd8,0x28),
            new(0xd8,0x58),new(0xd8,0x98),new(0x90,0x98),new(0x58,0x98)];
        int scrollChecks=0, returnScrollChecks=0;
        for(int i=0;i<2600&&!_dialogue.IsOpen;i++)
        {
            StepGameplayUpdates(1,Vector2.Zero);
            ValidateOnlyArrivingTargetCart(arrivingCart);
            if(!IsTransitioning) continue;
            var crystals=_entities.EntityAdapters<TargetCartCrystalRoomEntity>().ToArray();
            if(_rooms.CurrentRoom.Id==0xd8)
            {
                FailIf(crystals.Length!=5||crystals.Any(c=>!c.Node.Visible||c.Node.Position!=sourcePositions[c.SubId]),
                    "Goron $66:$09 must restore the five unhit ENEMY $63 crystals during the return-scroll initialization.");
                returnScrollChecks++;
                continue;
            }
            if(_rooms.CurrentRoom.Id!=0xd9) continue;
            FailIf(crystals.Length!=7||crystals.Any(c=>!c.Node.Visible||
                c.Node.Position!=secondRoomPositions[c.SubId-5]),
                "ENEMY $63 in $5:d9 must load configuration positions during scroll preload and remain frozen throughout scrolling.");
            scrollChecks++;
        }
        FailIf(scrollChecks<2,"Target-cart regression did not observe the $5:d8 -> $5:d9 scroll.");
        FailIf(returnScrollChecks<2,"Target-cart regression did not observe the $5:d9 -> $5:d8 return scroll.");
        // The source route crosses $5:d9 and returns before the right-hand script scores it.
        FailIf(!_dialogue.IsOpen||_rooms.ActiveGroup!=5||_rooms.CurrentRoom.Id!=0xd8,
            $"Target-cart ride did not return to its scoring attendant: {_rooms.ActiveGroup}:{_rooms.CurrentRoom.Id:x2}, Link {_player.Position}.");
        left=cave.Actors.Single(h=>h.Actor.Record.Var03==0);
        for(int i=0;i<600&&!_dialogue.ChoiceActive;i++) AdvanceGoronDialogue(1);
        FailIf(_inventory.EquippedB!=b||_inventory.EquippedA!=a||_inventory.ScentSeeds!=seeds||_saveData.HasRoomFlag(5,0xd8,0x80)||
            _inventory.HasTreasure(TreasureId.RockBrisket)||!_dialogue.ChoiceActive,
            "Target-cart zero-hit result did not restore inventory, clear play state, and withhold Rock Brisket.");
        // scripts.s: B@selectedYes leaves input disabled and signals A; A's
        // enableallobjects must release that lock when the next game starts.
        FailIf(!_player.CutsceneControlled,"Target-cart retry prompt released input before a choice.");
        _dialogue.SubmitChoiceForValidation(0);
        ValidateTargetCartPrizeDisplay(left,batched:true);
        for(int i=0;i<1300&&!_saveData.HasRoomFlag(5,0xd8,0x80);i++) AdvanceGoronDialogue(1);
        StepGameplayUpdates(2,Vector2.Zero);
        FailIf(_player.CutsceneControlled||cave.BlocksGameplay||_inventory.Rupees!=80||
            _player.Position!=new Vector2(0x38,0x88),
            "Target-cart immediate retry retained the scoring attendant's input lock at $5:d8 platform ($38,$88).");
        var scent=new SeedSatchelDatabase().Scent;
        var shooter=SeedShooterRecord.Load();
        var fired=new System.Collections.Generic.HashSet<int>();
        void ShootCrystals()
        {
            if(IsTransitioning) return;
            foreach(var crystal in _entities.EntityAdapters<TargetCartCrystalRoomEntity>().ToArray())
                if(crystal.Node.Position!=Vector2.Zero&&_entities.DynamicItemSlotAvailable&&fired.Add(crystal.SubId))
                    _entities.Spawn<EmberSeedEffect>(new EmberSeedSpawn(
                        crystal.Node.Position+Vector2.Down*8-shooter.Offsets[0],Vector2I.Up,scent,4,SeedLaunchKind.Shooter));
        }
        ShootCrystals(); StepGameplayUpdates(8,Vector2.Zero);
        FailIf(_entities.RuntimeState.ReadWramByte(0xcfde)!=5||_entities.RuntimeState.ReadWramByte(0xcfdd)!=0x1f,
            "Scent projectiles did not destroy the first five crystals and publish their re-entry mask.");
        StepGameplayUpdates(20,Vector2.Up,["move_up"],["move_up"],batched:true);
        arrivingCart=_entities.Entities<MinecartRoomEntity>().Single();
        for(int i=0;i<2600&&!_dialogue.IsOpen;i++)
        {
            ShootCrystals(); StepGameplayUpdates(1,Vector2.Zero);
            ValidateOnlyArrivingTargetCart(arrivingCart);
        }
        FailIf(_entities.RuntimeState.ReadWramByte(0xcfde)!=12||fired.Count!=12,
            $"Target-cart return lost a crystal hit or respawned a destroyed first-room target: hits {_entities.RuntimeState.ReadWramByte(0xcfde)}, fired {fired.Count}, room {_rooms.CurrentRoom.Id:x2}, remaining {string.Join(',',_entities.EntityAdapters<TargetCartCrystalRoomEntity>().Select(c=>c.SubId))}.");
        AdvanceGoronDialogue(650,1);
        FailIf(!_inventory.HasTreasure(TreasureId.RockBrisket)||_inventory.EquippedB!=b||_inventory.EquippedA!=a||
            _inventory.ScentSeeds!=seeds||_saveData.HasRoomFlag(5,0xd8,0x80),
            "Twelve target hits did not award Rock Brisket and restore the pre-game inventory.");
        right=cave.Actors.Single(h=>h.Actor.Record.Var03==1); TalkGoronFromFloor(right);
        left=cave.Actors.Single(h=>h.Actor.Record.Var03==0);
        ApproachGoronFromFloor(left); StepGameplayUpdates(1,Vector2.Zero,["attack"],["attack"]);
        for(int i=0;i<1300&&!_saveData.HasRoomFlag(5,0xd8,0x80);i++) AdvanceGoronDialogue(1);
        FailIf(!_saveData.HasRoomFlag(5,0xd8,0x80),"Cancellation fixture never started target carts.");
        LoadValidationRoom(2,0xed);
        FailIf(_saveData.HasRoomFlag(5,0xd8,0x80)||_inventory.ScentSeeds!=seeds||
            _inventory.EquippedA!=a||_inventory.EquippedB!=b,
            "Cancelling outside the target-cart course leaked its temporary inventory or active flag.");
        ValidateTargetCartCrystalPreload();
    }
    private void ValidateOnlyArrivingTargetCart(MinecartRoomEntity arrivingCart)
    {
        if(!arrivingCart.Riding) return;
        // minecart.s creates the stationary INTERAC $16 only at
        // @minecartStopped; BeginRide already removed its static record.
        var carts=_entities.Entities<MinecartRoomEntity>()
            .Concat(_entities.OutgoingEntities<MinecartRoomEntity>()).ToArray();
        FailIf(carts.Length!=1||!ReferenceEquals(carts[0],arrivingCart),
            $"Target-cart arrival spawned a duplicate before dismount at $5:{_rooms.CurrentRoom.Id:x2}: carts={carts.Length}, positions={string.Join(';',carts.Select(c=>c.Position))}.");
        FailIf(MinecartRuntimeState.StationaryInRoom(_runtimeState,0xd8).Any(),
            "$5:d8 retained a stationary cart in wStaticObjects while Link was riding.");
    }
    private void ValidateTargetCartPrizeDisplay(GoronCaveScriptHost host,bool batched)
    {
        // scripts.s $66:$09 A: TX_24ac, wait 30, spawnPrize, wait 90,
        // fadeout, then goron_deleteTreasure. The first game and immediate
        // retry must both publish ROCK_BRISKET_01 at (yh,xh)=($78,$78).
        NpcCharacter[] Prizes()=>_entities.Entities<NpcCharacter>().Where(n=>
            n.Active&&n.Record.Id==InteractionId.Treasure).ToArray();
        for(int i=0;i<400&&!(_dialogue.IsOpen&&_dialogue.CurrentMessage.Contains("time is...this!",System.StringComparison.Ordinal));i++)
            AdvanceGoronDialogue(1);
        FailIf(!_dialogue.IsOpen||!_dialogue.CurrentMessage.Contains("time is...this!",System.StringComparison.Ordinal)||Prizes().Length!=0,
            "$5:d8 target carts did not reach TX_24ac before displaying its prize.");
        _dialogue.Close(); StepGameplayUpdates(1,Vector2.Zero);
        FailIf(host.Counter!=30,"$5:d8 TX_24ac did not initialize its source wait 30.");
        StepGameplayUpdates(29,Vector2.Zero,batched:batched);
        FailIf(host.Counter!=1||Prizes().Length!=0,"$5:d8 displayed the target-cart prize before wait 30 expired.");
        StepGameplayUpdates(1,Vector2.Zero);
        var prizes=Prizes();
        FailIf(prizes.Length!=1,"$5:d8 did not spawn exactly one INTERAC_TREASURE $60 display prize.");
        var prize=prizes.Single();
        // treasureObjectData5e: ROCK_BRISKET_01 selects graphic $4e.
        var visual=host.Context.Treasures.GetObjectVisual(0x4e);
        FailIf(prize.Record.SpriteName!=visual.Sprite||prize.Record.TileBase!=visual.TileBase||
            prize.Record.Palette!=visual.Palette||prize.Record.DownAnimation!=visual.Animation,
            "$5:d8 ROCK_BRISKET_01 display did not select the source graphic $4e.");
        void CheckDisplay()
        {
            FailIf(!prize.Active||!prize.Visible||!prize.IsVisibleInTree()||
                prize.Position!=new Vector2(0x78,0x78)||prize.ScriptDrawOffset!=Vector2.Zero||
                prize.ZIndex!=9||prize.CurrentAnimationOpaquePixels==0||
                _entities.RuntimeState.ReadWramByte(0xcfd6)!=0||_inventory.HasTreasure(TreasureId.RockBrisket),
                $"$5:d8 ROCK_BRISKET_01 display must be visible at ($78,$78), visiblec2, without awarding it; batched={batched}, visible={prize.Visible}, position={prize.Position}, priority={prize.ZIndex}.");
        }
        CheckDisplay();
        FailIf(host.Counter!=90,"$5:d8 target-cart prize did not initialize its source wait 90.");
        StepGameplayUpdates(89,Vector2.Zero,batched:batched); CheckDisplay();
        FailIf(host.Counter!=1,"$5:d8 target-cart prize wait 90 ended early.");
        StepGameplayUpdates(1,Vector2.Zero); CheckDisplay();
        FailIf(!_roomEvents.Get<GoronCaveEvent>().PaletteBusy,"$5:d8 prize wait 90 did not start fadeout on its zero update.");
        for(int i=0;i<100&&prize.Active;i++) StepGameplayUpdates(1,Vector2.Zero);
        FailIf(prize.Active||prize.Visible||Prizes().Length!=0||_inventory.HasTreasure(TreasureId.RockBrisket),
            "$5:d8 goron_deleteTreasure did not remove its display prize before configuring the ride.");
    }
    private void ValidateTargetCartCrystalPreload()
    {
        // Independent targetCartCrystal.s configuration1/2 and behaviourTable.
        Vector2[][] positions=[
            [new(0x48,0x18),new(0x68,0x58),new(0x88,0x18),new(0xd8,0x18),new(0xd8,0x58),new(0xd8,0x98),new(0x78,0x98)],
            [new(0x68,0x28),new(0x68,0x58),new(0xb8,0x18),new(0xd8,0x40),new(0xd8,0x80),new(0x90,0x98),new(0x50,0x98)]];
        int[][] behaviour=[[0,0,0,0,1,0,2],[0,0,2,1,1,2,2]];
        foreach(bool batched in new[]{false,true})
        for(int configuration=1;configuration<=2;configuration++)
        {
            LoadValidationRoom(5,0xd9);
            _entities.RuntimeState.SetWramByte(0xcfd4,(byte)configuration);
            var room=_rooms.CurrentRoom;
            _entities.BeginScreenTransition(5,room,Vector2.Up*room.Height);
            FailIf(_entities.TrySpawnDebugEnemy(EnemyId.TargetCartCrystal,0,new Vector2(8,8),out _),
                "Debug spawning must remain disabled during scrolling even though native state-zero spawns are eligible.");
            var crystals=_entities.EntityAdapters<TargetCartCrystalRoomEntity>().ToArray();
            FailIf(crystals.Length!=7,"Room $5:d9 did not preload seven ENEMY $63 crystals.");
            void CheckPositions(int updates)
            {
                foreach(var crystal in crystals)
                {
                    int index=crystal.SubId-5;
                    Vector2 direction=behaviour[configuration-1][index] switch
                    { 1=>Vector2.Up,2=>Vector2.Left,_=>Vector2.Zero };
                    // SPEED_80: 31 half-pixel steps, reversal on update $20.
                    float distance=updates==32?15:updates*0.5f;
                    Vector2 expected=positions[configuration-1][index]+direction*distance;
                    FailIf(!crystal.Node.Visible||crystal.Node.Position!=expected,
                        $"ENEMY $63:${crystal.SubId:x2} configuration {configuration}, update {updates}, batched={batched}: expected {expected}, got {crystal.Node.Position}.");
                }
            }
            CheckPositions(0);
            if(batched) _entities.Update(40.0/60,_player);
            else for(int i=0;i<40;i++) _entities.Update(1.0/60,_player);
            CheckPositions(0);
            FailIf(crystals.Any(c=>((NpcCharacter)c.Node).TransitionDrawOffset!=Vector2.Up*room.Height),
                "ENEMY $63 preload lost its room-relative transition draw offset.");
            _entities.FinishScreenTransition();
            CheckPositions(0);
            FailIf(crystals.Any(c=>((NpcCharacter)c.Node).TransitionDrawOffset!=Vector2.Zero),
                "ENEMY $63 retained a draw offset after scrolling.");
            if(batched) _entities.Update(31.0/60,_player);
            else for(int i=0;i<31;i++) _entities.Update(1.0/60,_player);
            CheckPositions(31);
            _entities.Update(1.0/60,_player);
            CheckPositions(32);
        }
    }
    private void ValidateGoronTunnel()
    {
        _inventory.GiveTreasure(TreasureId.Essence,4);
        LoadValidationRoom(0,0x0a);
        var maku=_roomEvents.Get<RemoteMakuFifthEssenceEvent>();
        FailIf(!maku.HasState,"Fifth Essence did not activate remote Maku in $0:0a.");
        for(int i=0;i<1000&&!_roomEvents.Get<GoronCaveEvent>().HasState;i++) AdvanceGoronDialogue(1);
        var cave=_roomEvents.Get<GoronCaveEvent>();
        FailIf(!cave.HasState||!_player.CutsceneControlled||maku.BlocksGameplay,
            "Remote Maku did not hand input directly to tunnel Goron $66:$03.");
        var goron=cave.Actors.Single();
        FailIf(goron.Actor.Position!=new Vector2(0xa8,0x58),"Tunnel Goron did not begin at source YX $58,$a8.");
        AdvanceGoronDialogue(350);
        FailIf(goron.Actor.Active||_player.CutsceneControlled||!_saveData.HasRoomFlag(0,0x0a,0x40),
            "Tunnel announcement did not delete its actor, release Link, and preserve the remote-Maku room flag.");
        LoadValidationRoom(0,0x0a); StepGameplayUpdates(4,Vector2.Zero);
        FailIf(maku.HasState||cave.HasState,"Fifth-Essence tunnel cutscene repeated on re-entry.");
    }
}
