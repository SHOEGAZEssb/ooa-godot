using Godot;
using System;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateKingMoblinFight()
    {
        RunKingMoblinFight(false);
        RunKingMoblinFight(true);
    }
    private void ValidateKingMoblinCancellation()
    {
        const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
        var input=(ApplicationInputBuffer)typeof(GameRoot).GetField("_applicationInput",flags)!.GetValue(this)!;
        var scheduler=(ApplicationFixedUpdateScheduler)typeof(GameRoot).GetField("_applicationUpdates",flags)!.GetValue(this)!;
        var update=(Action)typeof(GameRoot).GetMethod("AdvanceApplicationUpdate",flags)!.CreateDelegate(typeof(Action),this);
        void Step(Vector2 movement=default,bool fire=false)
        {
            input.CaptureForValidation(fire?["attack"]:[],fire?["attack"]:[],movement);
            scheduler.Advance(1.0/60,update);
        }
        _saveData.SetGlobalFlag(0x1a,false);
        LoadValidationRoom(2,0xaf); _player.WarpTo(new Vector2(24,88));
        _inventory.GiveTreasure(TreasureDatabase.TreasureBracelet,1);
        _inventory.EquipA(InventoryState.ItemBracelet);
        var slots=(System.Collections.Generic.HashSet<int>)typeof(RoomEntityManager).GetField("_reservedEnemySlots",flags)!.GetValue(_entities)!;
        for(int i=1;i<15;i++) slots.Add(i);
        int rng=_entities.RandomCalls;
        Step(); Step();
        var boss=_entities.Entities<KingMoblinBoss>().Single();
        FailIf(boss.State!=0 || _entities.Entities<KingMoblinMinion>().Count!=0 || _entities.RandomCalls!=rng+2,
            "King Moblin state0 must retry two-slot allocation and consume enemyStandardUpdate RNG each time.");
        for(int i=1;i<15;i++) slots.Remove(i);
        Step();
        FailIf(boss.State!=8 || boss.Minions.Any(m=>m is not {State:1}),"King Moblin later minion slots must initialize in their creation update.");
        for(int i=0;boss.State==8 && i<100;i++) Step(Vector2.Right);
        LoadValidationRoom(2,0xaf); _player.WarpTo(new Vector2(24,88)); Step();
        FailIf(_entities.PlayerMovementDisabled || _dialogue.IsOpen || _saveData.HasGlobalFlag(0x1a),
            "Cancelling King Moblin's intro must release controls without completing the keep.");
        boss=_entities.Entities<KingMoblinBoss>().Single();
        for(int i=0;!_dialogue.IsOpen && i<150;i++) Step(boss.State==8?Vector2.Right:Vector2.Zero);
        FailIf(!_dialogue.IsOpen,"King Moblin intro must repeat after cancellation.");
        _dialogue.Close();
        for(int i=0;boss.Bomb is not {State:4} && i<250;i++) Step();
        var bomb=boss.Bomb!;
        FailIf(bomb is null || bomb.State!=4,"King Moblin cancellation fixture did not receive a landed bomb.");
        for(int i=0;i<180 && !_player.IsCarryingObject;i++)
        {
            Vector2 delta=bomb!.Position+new Vector2(0,12)-_player.Position;
            if(delta.Length()>2) Step(delta.Normalized());
            else {_player.Face(Vector2I.Up); Step(fire:true);}
        }
        FailIf(!_player.IsCarryingObject,"King Moblin cancellation fixture could not pick up its bomb.");
        LoadValidationRoom(2,0xae); Step();
        FailIf(_player.IsCarryingObject || _entities.Entities<KingMoblinBomb>().Count!=0 ||
            _entities.PlayerMovementDisabled || _saveData.HasGlobalFlag(0x1a),
            "Leaving with PART $3f held must discard its reserved-item owner without setting the completion flag.");
    }
    private void RunKingMoblinFight(bool batch)
    {
        const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
        var input=(ApplicationInputBuffer)typeof(GameRoot).GetField("_applicationInput",flags)!.GetValue(this)!;
        var scheduler=(ApplicationFixedUpdateScheduler)typeof(GameRoot).GetField("_applicationUpdates",flags)!.GetValue(this)!;
        var update=(Action)typeof(GameRoot).GetMethod("AdvanceApplicationUpdate",flags)!.CreateDelegate(typeof(Action),this);
        void Step(int count=1,Vector2 movement=default,bool fire=false)
        {
            input.CaptureForValidation(fire?["attack"]:[],fire?["attack"]:[],movement);
            if(batch) scheduler.Advance(count/60.0,update);
            else for(int i=0;i<count;i++) scheduler.Advance(1.0/60,update);
        }
        _saveData.SetGlobalFlag(0x1a,false); _saveData.SetGlobalFlag(0x16,false);
        _saveData.WriteWramByte(0xc612,(byte)(batch?1:0));
        _saveData.SetRoomFlag(0,0x09,1,false);
        LoadValidationRoom(2,0xaf);
        _player.WarpTo(new Vector2(0x18,0x58)); _player.Face(Vector2I.Right);
        _inventory.GiveTreasure(TreasureDatabase.TreasureBracelet,1);
        _inventory.EquipA(InventoryState.ItemBracelet);
        for(int i=0;i<11;i++) _inventory.GiveTreasure(TreasureDatabase.TreasureHeartContainer,4);
        _inventory.RefillHealth();
        FailIf(_currentRoom.IsSolid(_player.Position),"King Moblin entrance 2:af/$61 must be reachable floor.");
        Step();
        var boss=_entities.Entities<KingMoblinBoss>().Single();
        for(int x=3;x<=6;x++)
            FailIf(_currentRoom.GetMetatile(new Vector2(x*16,48))!=0x0a || !_currentRoom.IsSolid(new Vector2(x*16,56)),
                "roomTileChangesAfterLoad05 must replace logical $33-$36 with $0a while retaining wall collision.");
        FailIf(boss.State!=8 || boss.Health!=6 || boss.Position!=new Vector2(0x50,0x20) ||
            _entities.Entities<KingMoblinMinion>().Count!=2,"King Moblin $7f/$56 source initialization mismatch.");
        FailIf(!boss.Data.Bytes("pickup").SequenceEqual(new byte[]{10,20,28,45,45,45}) ||
            !boss.Data.Bytes("flashes").SequenceEqual(new byte[]{6,7,8,9,10,12}),"King Moblin health-indexed source timing tables differ.");
        for(int i=0;boss.State==8 && i<100;i++) Step(movement:Vector2.Right);
        FailIf(boss.State!=9 || boss.Counter!=24 || !_entities.PlayerMovementDisabled,
            $"King Moblin reachable intro failed: state=${boss.State:x2}, Link={_player.Position}.");
        Step(23); FailIf(_dialogue.IsOpen || boss.Counter!=1,"King Moblin TX_2f19 opened before update $18.");
        Step(); FailIf(!_dialogue.IsOpen || boss.State!=10,"King Moblin TX_2f19 missing at update $18.");
        FailIf(!_dialogue.CurrentMessage.StartsWith(batch?"Hey! You're that":"Who enters my",StringComparison.Ordinal) ||
            _dialogue.Position.Y!=96,"King Moblin linked/unlinked intro must preserve its source text and bottom position command $02.");
        Step(5); FailIf(boss.State!=10,"King Moblin must freeze during dialogue.");
        _dialogue.Close(); Step();
        FailIf(boss.State!=11 || boss.Counter!=30 || _entities.PlayerMovementDisabled,"King Moblin intro did not release Link and start fight.");
        Step(29); FailIf(boss.Bomb is not null,"King Moblin spawned its bomb before 30 updates.");
        Step();
        FailIf(boss.State!=12 || boss.Counter!=45 || boss.Bomb is not {State:1},"King Moblin first bomb pickup timing mismatch.");
        var bomb=boss.Bomb!;
        FailIf(bomb.Fuse is not (120 or 135 or 160 or 180),"PART $3f fuse must come from the four native random values.");
        Step(44); FailIf(boss.State!=12 || boss.Counter!=1,"King Moblin raised bomb early.");
        Step(); FailIf(boss.State!=13 || boss.Counter!=15 || bomb.Position!=new Vector2(0x42,0x10),"King Moblin overhead offset/timer mismatch.");
        Step(15); FailIf(boss.State!=14 || bomb.State!=3,"King Moblin failed to throw after 15 updates.");
        for(int i=0;bomb.State!=4 && i<120;i++) Step();
        FailIf(bomb.State!=4,$"King Moblin thrown bomb did not settle: ${bomb.State:x2}.");
        // Approach the landed object over real room geometry and lift with equipped A.
        for(int i=0;i<180 && !_player.IsCarryingObject;i++)
        {
            Vector2 target=bomb.Position+new Vector2(0,12);
            Vector2 delta=target-_player.Position;
            if(delta.Length()>2) Step(movement:delta.Normalized());
            else {_player.Face(Vector2I.Up); Step(fire:true);}
        }
        FailIf(!_player.IsCarryingObject || !bomb.Held,$"Bracelet could not reach PART $3f: Link={_player.Position}, bomb={bomb.Position}, state=${bomb.State:x2}.");
        int fuse=bomb.Fuse;
        Step(24);
        FailIf(!bomb.Held || bomb.Fuse>=fuse,"PART $3f must keep its fuse while carried.");
        for(int hit=0;hit<6;hit++)
        {
            if(hit!=0)
            {
                for(int i=0;i<800 && boss.Bomb is not {State:4,IsDead:false};i++) Step();
                bomb=boss.Bomb!;
                FailIf(bomb.State!=4 || bomb.IsDead,"King Moblin did not repeat its bomb attack.");
                for(int i=0;i<180 && !_player.IsCarryingObject;i++)
                {
                    Vector2 delta=bomb.Position+new Vector2(0,12)-_player.Position;
                    if(delta.Length()>2) Step(movement:delta.Normalized());
                    else {_player.Face(Vector2I.Up); Step(fire:true);}
                }
                FailIf(!bomb.Held,$"Repeat bracelet pickup {hit} failed: bomb={bomb.Position}/${bomb.State:x2}, Link={_player.Position}.");
                Step(24);
            }
            for(int i=0;i<90 && Math.Abs(_player.Position.X-80)>1;i++)
                Step(movement:new Vector2(Math.Sign(80-_player.Position.X),0));
            for(int i=0;i<90 && _player.Position.Y>72;i++) Step(movement:Vector2.Up);
            int flashes=new[]{12,10,9,8,7,6}[hit];
            for(int i=0;i<500 && (bomb.Fuse!=0 || bomb.Flashes<Math.Max(0,flashes-6));i++) Step();
            FailIf(!bomb.Held,$"PART $3f exploded before return {hit}: fuse={bomb.Fuse}, flashes={bomb.Flashes}, Link={_player.Position}.");
            // Minion bombs now travel through the room. A native hurt update
            // can reject A; recover before issuing the bracelet throw.
            for(int i=0;i<20 && _player.KnockbackFrames>0;i++) Step();
            _player.Face(Vector2I.Up);
            Step(movement:Vector2.Up,fire:true); Step();
            FailIf(bomb.Held || _player.IsCarryingObject,$"Equipped bracelet failed to throw PART $3f on hit {hit}, knockback={_player.KnockbackFrames}, Link={_player.Position}.");
            int previousHealth=boss.Health;
            for(int i=0;i<250 && boss.Health==previousHealth;i++) Step();
            FailIf(boss.Health!=previousHealth-1,
                $"Returned bomb {hit} missed King Moblin: hp={boss.Health}, boss={boss.Position}/${boss.State:x2}, bomb={bomb.Position}/${bomb.State:x2}, flashes={bomb.Flashes}, Link={_player.Position}.");
            FailIf(boss.InvincibilityCounter!=30,"PART $3f must write $1e invincibility in its later part slot.");
            _inventory.RefillHealth();
            if(hit!=5) Step(35);
        }
        Step(); FailIf(boss.State>=18,"King Moblin must consume zero-health JUST_HIT before death.");
        Step(); FailIf(boss.State!=18 || !_entities.PlayerMovementDisabled,"King Moblin death did not lock controls.");
        for(int i=0;i<700 && boss.State!=21;i++) Step();
        // Either minion writes var33 when it exits. Their independent attack
        // phases need not finish the escape on the same update.
        FailIf(boss.State!=21 || boss.Counter!=98 || !boss.EscapeSignal || !boss.Minions.Any(m=>m!.IsDead),
            $"King Moblin minions failed to signal escape: boss=${boss.State:x2}, minions={string.Join(',',boss.Minions.Select(m=>m!.State))}.");
        Step(97); FailIf(_saveData.HasGlobalFlag(0x1a),"King Moblin keep flag was set before explosion counter98.");
        FailIf(_entities.Entities<KingMoblinMinion>().Count!=0,"Both King Moblin minions must finish escaping before the final warp.");
        Step();
        // checkDisplayEraOrSeasonInfo consumes GLOBALFLAG_16 during destination loading.
        FailIf(!_saveData.HasGlobalFlag(0x1a) || _saveData.HasGlobalFlag(0x16) || !_saveData.HasRoomFlag(0,0x09,1),
            $"King Moblin defeat flags: keep={_saveData.HasGlobalFlag(0x1a)}, $16={_saveData.HasGlobalFlag(0x16)}, room09={_saveData.HasRoomFlag(0,0x09,1)}, state={boss.State}, counter={boss.Counter}, room={_currentRoom.Group}:{_currentRoom.Id:x2}, batch={batch}.");
        for(int i=0;IsTransitioning && i<180;i++) Step();
        FailIf(_currentRoom.Id!=0x09 || _currentRoom.Group!=0,"King Moblin defeat did not warp to room0:09.");
        FailIf(!_roomEvents.Get<DefeatedMoblinEvent>().HasState,
            "King Moblin's actual defeat warp must arm INTERAC $72 in room 0:09.");
        // Returning directly through the debug loader recreates the source enemy stream;
        // normal access is removed by the destroyed keep's world layout.
        LoadValidationRoom(2,0xaf); Step();
        FailIf(_player.IsCarryingObject || _entities.PlayerMovementDisabled,"King Moblin room cancellation retained a carried bomb or control restriction.");
    }
}
