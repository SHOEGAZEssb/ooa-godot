using Godot;
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
        for(int i=0;i<1600&&host.BigBang?.Playing!=true;i++) AdvanceGoronDialogue(1);
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
        for(int i=0;i<1800&&game.Playing;i++) AdvanceGoronDialogue(1,1);
        AdvanceGoronDialogue(400,1);
        FailIf(game.Playing||_inventory.HealthQuarters!=health||!_inventory.HasTreasure(TreasureId.OldMermaidKey)||cave.BlocksGameplay||
            _rooms.CurrentRoom.GetMetatile(new(0x18,0x18))!=0xef,
            $"Big Bang completion: playing={game.Playing}, health={_inventory.HealthQuarters}/{health}, key={_inventory.HasTreasure(TreasureId.OldMermaidKey)}, blocked={cave.BlocksGameplay}, tile={_rooms.CurrentRoom.GetMetatile(new(0x18,0x18)):x2}, command={host.CommandIndex}, counter={host.Counter}, invincibility={_player.InvincibilityFrames}, parts={game.Parts.Count}, status={_entities.RuntimeState.ReadWramByte(WramAddress.wTmpcfc0)}.");
        ApproachGoronFromFloor(host); StepGameplayUpdates(1,Vector2.Zero,["attack"],["attack"]);
        AdvanceGoronDialogue(100,1);
        FailIf(_inventory.Rupees!=100||cave.BlocksGameplay,"Declining a repeat Big Bang game charged money or locked input.");
        ApproachGoronFromFloor(host); StepGameplayUpdates(1,Vector2.Zero,["attack"],["attack"]);
        for(int i=0;i<1200&&!game.Playing;i++) AdvanceGoronDialogue(1);
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
    private void ValidateGoronTargetCarts()
    {
        _inventory.GiveTreasure(TreasureId.Shooter,0); _inventory.AddRupees(100);
        LoadValidationRoom(5,0xd8); StepGameplayUpdates(4,Vector2.Zero);
        var cave=_roomEvents.Get<GoronCaveEvent>();
        var left=cave.Actors.Single(a=>a.Actor.Record.Var03==0);
        var right=cave.Actors.Single(a=>a.Actor.Record.Var03==1);
        TalkGoronFromFloor(right);
        int b=_inventory.EquippedB,a=_inventory.EquippedA,seeds=_inventory.ScentSeeds;
        ApproachGoronFromFloor(left); StepGameplayUpdates(1,Vector2.Zero,["attack"],["attack"]);
        for(int i=0;i<1300&&!_saveData.HasRoomFlag(5,0xd8,0x80);i++) AdvanceGoronDialogue(1);
        AdvanceGoronDialogue(2);
        FailIf(!_saveData.HasRoomFlag(5,0xd8,0x80)||_inventory.Rupees!=90||_inventory.ScentSeeds!=0x99||
            _entities.EntityAdapters<TargetCartCrystalRoomEntity>().Count()!=5||_entities.Entities<MinecartRoomEntity>().Count!=1,
            $"Target carts setup: flag={_saveData.HasRoomFlag(5,0xd8,0x80)}, rupees={_inventory.Rupees}, seeds={_inventory.ScentSeeds:x2}, crystals={_entities.EntityAdapters<TargetCartCrystalRoomEntity>().Count()}, carts={_entities.Entities<MinecartRoomEntity>().Count}, command={left.CommandIndex}, counter={left.Counter}.");
        Vector2[] sourcePositions=[new(0x38,0x18),new(0x58,0x48),new(0x98,0x28),new(0xc8,0x48),new(0xb8,0x18)];
        FailIf(!_entities.EntityAdapters<TargetCartCrystalRoomEntity>().Select(c=>c.Node.Position).SequenceEqual(sourcePositions),
            "Target-cart configuration zero differs from its independent source positions.");
        StepGameplayUpdates(20,Vector2.Up,["move_up"],["move_up"]);
        // targetCartCrystal.s configuration0, subids $05-$0b. Check the
        // actual mounted scroll before gameplay can repair a zero-position spawn.
        Vector2[] secondRoomPositions=[new(0x38,0x58),new(0x98,0x28),new(0xd8,0x28),
            new(0xd8,0x58),new(0xd8,0x98),new(0x90,0x98),new(0x58,0x98)];
        int scrollChecks=0, returnScrollChecks=0;
        for(int i=0;i<2600&&!_dialogue.IsOpen;i++)
        {
            StepGameplayUpdates(1,Vector2.Zero);
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
        AdvanceGoronDialogue(600,1);
        FailIf(_inventory.EquippedB!=b||_inventory.EquippedA!=a||_inventory.ScentSeeds!=seeds||_saveData.HasRoomFlag(5,0xd8,0x80)||
            _inventory.HasTreasure(TreasureId.RockBrisket)||_roomEvents.Active,
            "Target-cart zero-hit result did not restore inventory, clear play state, and withhold Rock Brisket.");
        right=cave.Actors.Single(h=>h.Actor.Record.Var03==1); TalkGoronFromFloor(right);
        left=cave.Actors.Single(h=>h.Actor.Record.Var03==0);
        ApproachGoronFromFloor(left); StepGameplayUpdates(1,Vector2.Zero,["attack"],["attack"]);
        for(int i=0;i<1300&&!_saveData.HasRoomFlag(5,0xd8,0x80);i++) AdvanceGoronDialogue(1);
        StepGameplayUpdates(2,Vector2.Zero);
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
        StepGameplayUpdates(20,Vector2.Up,["move_up"],["move_up"]);
        for(int i=0;i<2600&&!_dialogue.IsOpen;i++)
        { ShootCrystals(); StepGameplayUpdates(1,Vector2.Zero); }
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
