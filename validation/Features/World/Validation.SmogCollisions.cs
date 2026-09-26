using Godot;
using System.Collections.Generic;

namespace oracleofages;

public partial class ValidationRoot
{
    private void ValidateSmogCollisions()
    {
        var data = new SmogCollisionDatabase();
        var record = new EnemyDatabase().ImportedEnemy(0x7c,0);
        for (int collision = 0; collision < 32; collision++)
        {
            bool enabled = collision == 0 || collision is >= 4 and <= 9;
            FailIf(data.Enabled(collision) != enabled || enabled &&
                (data.Effect(7,collision) != (collision == 0 ? 0x3c : 0x1f) ||
                data.Effect(0x4d,collision) != (collision == 0 ? 0x36 : 0x21)),
                $"Smog source mask and mode07/4d enabled effects mismatch at${collision:x2}.");
        }
        SmogCharacter Create(int kind)
        {
            var actor = new SmogCharacter();
            if (kind == 2)
            {
                actor.InitializeSmallCloud(record,2,0,new(72,72),0,_ => 0);
                actor.UpdateSmallCloud(0,() => { },_ => { },_ => { },() => { },() => 0);
            }
            else
            {
                actor.InitializeMergedCloud(record,3,0,new(72,72),0,_ => 0);
                for (int i = 0; i < 5; i++) actor.UpdateMergedInitialization(kind == 4 ? 2 : 3,_ => { },(_,_) => { },() => 0);
            }
            return actor;
        }
        foreach (int kind in new[] { 2,3,4 })
        foreach (var (state, level, collision) in new[] {
            (SwordActionState.Swing, 1, 0x04), (SwordActionState.Swing, 2, 0x05),
            (SwordActionState.Swing, 3, 0x06), (SwordActionState.Spin, 1, 0x08),
            (SwordActionState.Spin, 2, 0x08), (SwordActionState.Spin, 3, 0x08),
            (SwordActionState.Held, 1, 0x09), (SwordActionState.Charged, 2, 0x09) })
        {
            var actor = Create(kind);
            var sounds = new List<int>();
            var adapter = new SmogRoomEntity(actor,data,sounds.Add);
            adapter.SetLinkSwordState(state, level);
            try
            {
                FailIf(!adapter.ApplySwordHit(actor.CollisionBounds,new(40,72),2,default,[]) ||
                    actor.Health != (kind == 4 ? 4 : 6) || actor.InvincibilityCounter != (kind == 4 ? 32 : -28) ||
                    actor.KnockbackCounter != 0 || actor.ContactFlags != (0x80 | collision) || sounds.Count != 1 ||
                    sounds[0] != (kind == 4 ? 0x63 : 0x58),
                    "Smog sword/spin/poke must clink without cloud damage, or damage large form with boss sound and zero recoil.");
                FailIf(adapter.ApplySwordHit(actor.CollisionBounds,new(40,72),2,default,[]), "Pending collision must prevent a second hit in the same scan.");
                actor.PublishCollision(0);
                foreach (int inv in new[] { -1,1 })
                {
                    actor.InvincibilityCounter = inv;
                    FailIf(adapter.ApplySwordHit(actor.CollisionBounds,new(40,72),2,default,[]), "Either sign of enemy invincibility blocks item collisions.");
                }
            }
            finally { actor.Free(); }
        }
        foreach (int damage in new[] { 0,6,7,255 })
        {
            var actor = Create(4);
            try
            {
                var adapter = new SmogRoomEntity(actor,data,_ => { });
                adapter.ApplySwordHit(actor.CollisionBounds,new(40,72),damage,default,[]);
                FailIf(actor.Health != 0 || actor.CollisionEnabled || actor.IsDead || actor.InvincibilityCounter != 32,
                    "ENEMYDMG_30 byte arithmetic must disable zero-health collisions and defer boss death to native status handling.");
            }
            finally { actor.Free(); }
        }
        foreach (int kind in new[] { 2,3,4 })
        foreach (bool ring in new[] { false,true })
        {
            var save = OracleSaveData.CreateStandardGame();
            if (ring) { save.WriteWramByte(0xc6cc,1); save.WriteWramByte(0xc6c6,(byte)RingId.GreenHoly); }
            var inventory = new InventoryState(_treasures,save);
            if (ring) FailIf(!inventory.EquipRingAt(0), "Smog fixture must equip Green Holy Ring.");
            var player = new Player(); AddChild(player);
            player.Initialize(new ValidationRingPlayerWorld(),inventory,new(72,72),new OracleRandom());
            var actor = Create(kind);
            actor.InvincibilityCounter = -28; // Enemy invincibility must not suppress Link contact.
            var adapter = new SmogRoomEntity(actor,data,_ => { });
            try
            {
                int health = player.HealthQuarters;
                player.ApplyInteractionInvincibility(4);
                adapter.HandleLinkContact(player);
                FailIf(player.HealthQuarters != health || player.ElectricShockActive || actor.ContactFlags != 0,
                    "Link's own invincibility must reject cloud contact before effects.");
                player.ClearInteractionKnockback(clearInvincibility:true);
                adapter.HandleLinkContact(player);
                int damage = kind == 4 ? (ring ? 0 : 4) : 2;
                FailIf(player.HealthQuarters != health-damage || player.ElectricShockActive != (kind == 4) ||
                    actor.ContactFlags != (kind == 4 ? 0xa0 : 0x80) || actor.CollisionEnabled != (kind != 4) ||
                    player.InvincibilityFrames != (kind == 4 ? 12 : 34),
                    "Smog contact ignores enemy invincibility: cloud damage2, large shock4 (Green Holy Ring prevents only its damage), with native pending flags.");
                adapter.HandleLinkContact(player);
                FailIf(player.HealthQuarters != health-damage, "Pending Smog contact must not apply twice.");
            }
            finally { actor.Free(); player.Free(); }
        }
        GD.Print("Validated isolated Smog source collision masks, sword damage/clink profiles, item versus Link invincibility gates and Green Holy Ring shock response.");
    }
}
