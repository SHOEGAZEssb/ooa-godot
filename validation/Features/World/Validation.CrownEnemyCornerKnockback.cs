using Godot;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public partial class ValidationRoot
{
    private void ValidateCrownEnemyCornerKnockback()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        foreach (bool batch in new[] { false, true })
        foreach (var (id, room) in new[] { (0x0c,0xab), (0x21,0xaa), (0x21,0x9c),
            (0x3d,0xb0), (0x48,0x9c), (0x24,0x9f), (0x32,0xa1), (0x34,0x9d), (0x134,0x9d) })
        {
            LoadValidationRoom(4, room); _player.WarpTo(new(120,120));
            void Step(int n = 1) => StepGameplayUpdates(n, Vector2.Zero, batched: batch);
            Step(2);
            EnemyCharacter actor = id switch
            {
                0x0c or 0x21 => _entities.Entities<ArrowMoblinCharacter>().First(),
                0x3d or 0x48 => _entities.Entities<SwordEnemyCharacter>().First(e => e.Record.Id == id),
                0x24 => _entities.Entities<LikeLikeCharacter>().First(),
                0x34 or 0x134 => _entities.Entities<ZolCharacter>().First(e => e.Record.SubId == (id >> 8)),
                _ => _entities.Entities<KeeseCharacter>().First()
            };
            int State() => actor switch
            {
                ArrowMoblinCharacter arrow => (int)arrow.State,
                SwordEnemyCharacter sword => (int)sword.State,
                LikeLikeCharacter like => like.State,
                KeeseCharacter keese => (int)keese.State,
                ZolCharacter zol => (int)zol.State,
                _ => -1
            };
            if (actor is ZolCharacter initialZol)
                FailIf(initialZol.NativeSpeed != (id == 0x34 ? 0x1e : 0x0e),
                    "Clean-US Zol initialization uses SPEED_c0 for green and returned ROM bank$0e for red.");
            Vector2? corner = null;
            bool Solid(int x, int y) => _currentRoom.IsSolid(new(x,y));
            for (int y = 8; y < _currentRoom.Height - 8 && corner is null; y++)
            for (int x = 8; x < _currentRoom.Width - 8 && corner is null; x++)
                if (!Solid(x,y) && Solid(x+6,y-1) && !Solid(x+6,y+5) &&
                    _currentRoom.GetTerrainInfo(new(x-1,y+5)).Hazard == HazardType.None &&
                    _currentRoom.GetTerrainInfo(new(x+1,y+5)).Hazard == HazardType.None)
                    corner = new(x,y);
            FailIf(corner is null, $"Room4:{room:x2} must provide a native upper-only right-wall probe.");
            if (id == 0x32) corner = new(24,0); // Screen-boundary-only upper X probe.
            for (int repeat = 0; repeat < 2; repeat++)
            {
                actor.Position = corner!.Value;
                typeof(EnemyCharacter).GetProperty("KnockbackAngle",flags)!.SetValue(actor,8);
                typeof(EnemyCharacter).GetProperty("KnockbackCounter",flags)!.SetValue(actor,16);
                actor.InvincibilityCounter = 32;
                int state = State();
                var text = _entities.TextActiveSource;
                try
                {
                    _entities.TextActiveSource = () => true; Step(2);
                    FailIf(actor.Position != corner.Value || actor.KnockbackCounter != 16,
                        "Text must retain pending enemy recoil.");
                }
                finally { _entities.TextActiveSource = text; }
                Step(2);
                // ecom sideview probes find only the upper X wall; add $0060
                // to Y each update. These stored speeds keep hFF8D nonzero.
                FailIf(actor.Position != corner.Value + new Vector2(0,0.75f) ||
                    actor.KnockbackCounter != 14 || actor.InvincibilityCounter != 30 || State() != state,
                    $"Enemy${id:x2} must slide at a corner and retain recoil without running route AI: position={actor.Position}, start={corner.Value}, knock={actor.KnockbackCounter}, inv={actor.InvincibilityCounter}, state={State()}/{state}.");
            }
            if (actor is ZolCharacter { Record.SubId: 1 } red)
            {
                typeof(EnemyCharacter).GetProperty("KnockbackCounter",flags)!.SetValue(red,0);
                red.InvincibilityCounter = 0;
                // Isolate the two native speed writes without following a
                // room route. Waiting must preserve whichever was last written.
                var zolState = typeof(ZolCharacter).GetField("_state",flags)!;
                var zolCounter = typeof(ZolCharacter).GetField("_counter1",flags)!;
                zolState.SetValue(red,ZolState.RedShaking); zolCounter.SetValue(red,1);
                Step();
                FailIf(red.NativeSpeed != 0x28 || red.State != ZolState.RedHopping,
                    "Red hop entry must write SPEED_100.");
                zolState.SetValue(red,ZolState.RedWaiting); zolCounter.SetValue(red,24);
                Step(2);
                FailIf(red.NativeSpeed != 0x28, "Red waiting must retain its previous hop speed byte.");
                for (int attempt = 0; attempt < 32 && red.State != ZolState.RedSliding; attempt++)
                {
                    zolState.SetValue(red,ZolState.RedWaiting); zolCounter.SetValue(red,1); Step();
                }
                FailIf(red.State != ZolState.RedSliding || red.NativeSpeed != 0x14,
                    "Red slide selection must write SPEED_80.");
            }
        }
        LoadValidationRoom(0,0x60);
    }
}
