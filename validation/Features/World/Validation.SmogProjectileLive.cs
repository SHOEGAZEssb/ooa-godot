using Godot;
using System;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public partial class ValidationRoot
{
    private void ValidateSmogProjectileLive()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var input = (ApplicationInputBuffer)typeof(GameRoot).GetField("_applicationInput", flags)!.GetValue(this)!;
        var scheduler = (ApplicationFixedUpdateScheduler)typeof(GameRoot).GetField("_applicationUpdates", flags)!.GetValue(this)!;
        var update = (Action)typeof(GameRoot).GetMethod("AdvanceApplicationUpdate", flags)!.CreateDelegate(typeof(Action), this);
        foreach (bool batch in new[] { false, true })
        foreach (int subid in new[] { 0, 1 })
        {
            LoadValidationRoom(0, 0x60); _entities.Clear(); _player.WarpTo(new(120, 40));
            for (int y = 8; y < 128; y += 16)
                for (int x = 8; x < 160; x += 16) _currentRoom.SetPositionTileAndCollision(new(x,y), 0x0c, 0, 0);
            void Step(int count = 1)
            {
                input.CaptureForValidation([], [], Vector2.Zero);
                if (batch) scheduler.Advance(count / 60.0, update);
                else for (int i = 0; i < count; i++) scheduler.Advance(1.0 / 60.0, update);
            }
            var shot = _entities.Spawn<SmogProjectilePart>(new SmogProjectileSpawn(new(40,40), subid));
            var adapter = _entities.EntityAdapters<SmogProjectileRoomEntity>().Single();
            var text = _entities.TextActiveSource;
            try
            {
                _entities.TextActiveSource = () => true;
                Step();
                FailIf(shot.State != 1 || shot.Position != new Vector2(40 + subid,40), "Frozen PART$4a must initialize and execute state1 once.");
                Vector2 position = shot.Position;
                Step(3);
                FailIf(shot.Position != position, "Initialized PART$4a must freeze during dialogue.");
            }
            finally { _entities.TextActiveSource = text; }
            Step(4);
            foreach (var state in new[] { SwordActionState.Spin, SwordActionState.Held, SwordActionState.Charged })
            {
                adapter.SetLinkSwordState(state, 1);
                FailIf(adapter.ApplySwordHit(shot.CollisionBounds, Vector2.Zero, 1, default, []), "PART$4a source mask excludes spins and pokes.");
            }
            adapter.SetLinkSwordState(SwordActionState.Swing, 1);
            FailIf(!adapter.ApplySwordHit(shot.CollisionBounds, Vector2.Zero, 1, default, []), "Ordinary sword overlap must end PART$4a's collision scan.");
            FailIf(shot.InvincibilityCounter != (subid == 0 ? -28 : 0) || shot.ContactFlags != (subid == 0 ? 0x84 : 0) ||
                adapter.MeleeReportsContact != (subid == 0), "Small sword contact uses ENEMYDMG_34; large contact is a no-op.");
            try
            {
                _entities.TextActiveSource = () => true;
                Step(2);
                FailIf(shot.State != 1 || shot.InvincibilityCounter != (subid == 0 ? -28 : 0), "Frozen projectile must retain pending contact and invincibility.");
            }
            finally { _entities.TextActiveSource = text; }
            Step();
            FailIf(shot.State != (subid == 0 ? 2 : 1) || shot.PendingCollision || shot.InvincibilityCounter != (subid == 0 ? -27 : 0),
                "Eligible native update must decrement invincibility, consume small contact and clear the pending bit.");
            Step(10);
            FailIf(shot.Finished, "Small destruction remains alive through update11; large sword overlap leaves it moving.");
            Step();
            FailIf(shot.Finished != (subid == 0), "Small destruction completes on update12 only.");
            LoadValidationRoom(0, 0x60); _entities.Clear(); _player.WarpTo(new(72,72));
            _currentRoom.SetPositionTileAndCollision(new(72,72), 0x0c, 0, 0);
            int health = _player.HealthQuarters;
            shot = _entities.Spawn<SmogProjectilePart>(new SmogProjectileSpawn(new(72,72), subid));
            Step();
            FailIf(_player.HealthQuarters != health - 1 || shot.ContactFlags != 0x80 || shot.State != 1 ||
                _player.InvincibilityFrames != 34,
                "PART$4a effect02 must damage Link by one quarter-heart and defer small destruction until the next native update.");
            Step();
            FailIf(shot.State != (subid == 0 ? 2 : 1) || shot.PendingCollision,
                "Small projectile consumes Link contact next update; large projectile ignores it and clears the pending bit.");
        }
        LoadValidationRoom(0, 0x60); _entities.Clear();
        for (int slot = 0; slot < 16; slot++)
            _entities.Spawn<SmogProjectilePart>(new SmogProjectileSpawn(new(40,40), 1));
        bool rejected = false;
        try { _entities.Spawn<SmogProjectilePart>(new SmogProjectileSpawn(new(40,40), 1)); }
        catch (NotSupportedException error) { rejected = error.Message.Contains("PART$4a") && error.Message.Contains("full native PART pool"); }
        FailIf(!rejected || _entities.EntityAdapters<SmogProjectileRoomEntity>().Count() != 16,
            "Unrepresented unchecked Smog allocation into a full PART pool must diagnose without leaking another actor.");
        GD.Print("Validated Smog projectile room allocation, frozen initialization, sword masks and delayed destruction with single and batched gameplay updates.");
    }
}
