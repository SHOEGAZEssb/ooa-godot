using Godot;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateLynelPlacementsAndContacts()
    {
        var database = new EnemyDatabase();
        // objects/ages/enemyData.s: the six Tomb approach streams contain
        // six Lynels and eight Arrow Darknuts, including both subids.
        foreach (var spec in new[] { (Room:0x5b,Subid:0,Darknuts:1), (Room:0x5d,Subid:1,Darknuts:1),
            (Room:0x6b,Subid:0,Darknuts:2), (Room:0x6d,Subid:1,Darknuts:2),
            (Room:0x7b,Subid:0,Darknuts:1), (Room:0x7d,Subid:0,Darknuts:1) })
        {
            ReinitializeGameplayForValidation(); LoadValidationRoom(1,spec.Room);
            var lynels = _entities.Entities<LynelCharacter>();
            FailIf(lynels.Count != 1 || lynels[0].Record.SubId != spec.Subid ||
                _entities.Entities<ArrowDarknutCharacter>().Count != spec.Darknuts ||
                database.GetRoomObjects(1,spec.Room).Any(row => row.Kind is RoomObjectKind.RandomEnemy or RoomObjectKind.FixedEnemy &&
                    !database.EnemyHandlers.ResolveHandler(row).SupportsOrderedConstruction),
                $"Room1:${spec.Room:x2} lost its complete source Lynel/Darknut placements.");
            FailIf(!lynels[0].InitializationPending, "Lynel placement must leave state zero for the object pass.");
        }
        ReinitializeGameplayForValidation(); LoadValidationRoom(1,0x7c);
        FailIf(_entities.Entities<LynelCharacter>().Count != 0 || _entities.Entities<ArrowDarknutCharacter>().Count != 0,
            "Room1:$7c itself has no enemy placements.");
        _entities.BeginScreenTransition(1,_world.LoadRoom(1,0x7b),Vector2.Left*160,_player);
        var incoming = _entities.Entities<LynelCharacter>().Single();
        FailIf(incoming.InitializationPending || !incoming.Visible || incoming.State != 9,
            "Scrolling preload must dispatch Lynel state zero with the live Link target.");
        var before = (incoming.Position,incoming.Counter,_entities.RandomCalls);
        for (int i=0;i<8;i++) _entities.Update(1.0/60,_player);
        FailIf(before != (incoming.Position,incoming.Counter,_entities.RandomCalls), "Destination Lynel advanced during scrolling.");
        _entities.FinishScreenTransition();
        _entities.Update(1.0/60,_player);
        FailIf(incoming.Counter != before.Counter-1, "Destination Lynel did not resume after scrolling.");

        foreach (int shield in new[] { 1,2,3 })
        {
            var inventory = new InventoryState(_treasures,OracleSaveData.CreateStandardGame());
            inventory.GiveTreasure(_treasures.GetObject($"TREASURE_OBJECT_SHIELD_0{shield-1}"));
            inventory.EquipA(TreasureId.Shield);
            var player = new Player(); AddChild(player);
            player.Initialize(new ValidationRingPlayerWorld(),inventory,new(72,72),new OracleRandom());
            player.Face(Vector2I.Right); player.UpdateShieldForValidation(true,false);
            Vector2 center = new(player.ShieldCollisionBounds.End.X+6,player.ShieldCollisionBounds.GetCenter().Y);
            var beam = new LynelBeamRoomEntity(database.LynelBeam,new(center-new Vector2(-8,2),0x18),_entities);
            var frame = new RoomEntityFrame(player,0,false);
            var spawns = new List<RoomEntitySpawn>();
            beam.UpdateFrame(frame,spawns);
            FailIf(player.OverlapsEnemyCollision(beam.CollisionBounds), "Lynel shield fixture must exclude Link's body.");
            beam.HandleLinkContact(player);
            FailIf(beam.Finished || player.PendingContactDamageRaw != 0 ||
                player.KnockbackFrames != (shield==3?16:0),
                $"PART$1b shield${shield:x2} lost its Mirror-only LINKDMG_28 recoil and delayed deletion.");
            beam.UpdateFrame(frame,spawns);
            FailIf(beam.Finished != (shield==3), $"PART$1b shield${shield:x2}: only Mirror Shield stops the beam.");
            beam.Free(); player.Free();
        }
        var bodyPlayer = new Player(); AddChild(bodyPlayer);
        bodyPlayer.Initialize(new ValidationRingPlayerWorld(),new InventoryState(_treasures,OracleSaveData.CreateStandardGame()),new(72,72),new OracleRandom());
        var bodyBeam = new LynelBeamRoomEntity(database.LynelBeam,new(new(80,70),0x18),_entities);
        var bodyFrame = new RoomEntityFrame(bodyPlayer,0,false);
        var effects = new List<RoomEntitySpawn>(); int health = bodyPlayer.HealthQuarters;
        bodyBeam.UpdateFrame(bodyFrame,effects); bodyBeam.HandleLinkContact(bodyPlayer);
        FailIf(bodyPlayer.HealthQuarters != health || (bodyPlayer.PendingContactDamageRaw&255) != 0xfa ||
            bodyPlayer.InvincibilityFrames != 42 || bodyPlayer.KnockbackFrames != 21 || bodyBeam.Finished,
            "PART$1b must publish raw damage$fa and LINKDMG_08 before deleting on its next dispatch.");
        bodyBeam.UpdateFrame(bodyFrame,effects);
        FailIf(!bodyBeam.Finished,"PART$1b survived its post-contact status dispatch.");
        bodyBeam.Free(); bodyPlayer.Free();
        var contactPlayer = new Player(); AddChild(contactPlayer);
        contactPlayer.Initialize(new ValidationRingPlayerWorld(),new InventoryState(_treasures,OracleSaveData.CreateStandardGame()),new(104,104),new OracleRandom());
        var contactEnemy = new LynelCharacter();
        contactEnemy.Initialize(database.ImportedEnemy(0x0d),_world.LoadRoom(1,0x5b),contactPlayer.Position,new OracleRandom());
        contactEnemy.UpdateFrame(contactPlayer.Position,null,0,()=>false);
        var placement = database.GetRoomObjects(1,0x5b).Single(row=>row.Id==0x0d);
        var contactAdapter = new LynelRoomEntity(contactEnemy,database.EnemyHandlers.ResolveHandler(placement).CombatSource(placement,1),_entities);
        var contactBefore = (contactEnemy.Position,contactEnemy.Counter);
        contactAdapter.HandleLinkContact(contactPlayer);
        FailIf(!contactEnemy.NativeHitPending || (contactPlayer.PendingContactDamageRaw&255)!=0xf8,
            "Lynel body contact must publish raw damage$f8 and its ENEMYDMG_1c status.");
        contactEnemy.UpdateFrame(contactPlayer.Position,null,1,()=>false);
        FailIf(contactBefore!=(contactEnemy.Position,contactEnemy.Counter),"Lynel must pause its first post-contact update.");
        contactEnemy.UpdateFrame(contactPlayer.Position,null,2,()=>false);
        FailIf(contactEnemy.Counter!=contactBefore.Counter-1,"Lynel must resume after its post-contact update.");
        contactEnemy.Free(); contactPlayer.Free();
        GD.Print("Validated complete Tomb-approach enemy placements, scrolling admission/freeze/resume, Mirror-only beam blocking and native contact damage/recoil/deletion.");
    }
}
