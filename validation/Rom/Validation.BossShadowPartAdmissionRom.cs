using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void CompareBossShadowAdmissionRom()
    {
        foreach (int id in new[] { 0x70, 0x78, 0x7a })
        foreach (int free in new[] { 0, 1 })
        foreach (bool batched in new[] { false, true })
        {
            ReinitializeGameplayForValidation(); LoadValidationRoom(4, id == 0x70 ? 0x18 : id == 0x78 ? 0x13 : 0x4a);
            _inventory.SetScriptedEquippedItems(0, 0); _player.WarpTo(new(160, 112)); _player.BeginCutsceneControl();
            var parent = _entities.EntityAdapters<IRoomEntity>().Single(actor =>
                id == 0x70 ? actor.Node is GiantGhiniBoss : id == 0x78 ? actor.Node is PumpkinHeadBoss : actor.Node is ShadowHagBoss);
            F1Retain(parent); var boss = parent.Node; boss.Position = new(80, 72);
            F1Set(boss, "_initialized", true); parent.GetType().GetField("_initialized", F1Hidden)?.SetValue(parent, true);
            if (id == 0x70) { F1Set(boss, "_introDoorsClosed", true); F1Set(boss, "_counter", 1); }
            var native = F1Native(id, id == 0x78 ? 1 : 0, 8, boss.Position);
            native[0xffb0] = 112; native[0xffb1] = 160;
            if (id == 0x70) { native[0xd085] = 1; native[0xd086] = 1; }
            F1Fill(free, native);
            void Compare()
            {
                F1Enemy(native);
                FailIf(_entities.Entities<BossShadowEffect>().Count != F1Count(native, 7),
                    $"ENEMY${id:x2} shadow continuation: free={free}, runtime={_entities.Entities<BossShadowEffect>().Count}, native={F1Count(native, 7)}, state={native[0xd084]}/{native[0xd085]}.");
                bool waiting = boss is PumpkinHeadBoss pumpkin ? pumpkin.State == BossState.WaitingForDoors :
                    boss is ShadowHagBoss hag ? hag.State == ShadowHagState.IntroWaitingForDoors : false;
                FailIf(id != 0x70 && waiting != (native[0xd084 + (id == 0x7a ? 1 : 0)] == (id == 0x7a ? 0 : 8)),
                    $"ENEMY${id:x2} must retry its shadow before committing the following intro state.");
                if (id == 0x7a)
                    FailIf(boss.Position != new Vector2(native[0xd08d], native[0xd08b]),
                        "Shadow Hag must copy Link's position only after its shadow allocation succeeds.");
            }
            StepGameplayUpdates(1, Vector2.Zero, batched: batched, afterUpdate: Compare);
            if (free == 0)
            {
                _entities.RetireEntity(_entities.ResolvePartSlot(0)!);
                for (int i = 0; i < 64; i++) native[0xd0c0 + i] = 0;
                StepGameplayUpdates(1, Vector2.Zero, batched: batched, afterUpdate: Compare);
                FailIf(_entities.Entities<BossShadowEffect>().Count != (id == 0x70 ? 0 : 1),
                    "Giant Ghini skips its failed optional shadow; Pumpkin Head and Shadow Hag retry.");
            }
            _entities.Clear();
        }
    }
}
