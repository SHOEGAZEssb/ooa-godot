using Godot;
using System;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void CompareLegacyBossDeathAdmissionRom()
    {
        foreach (var test in new (int Id, int Room, Type Type)[] { (0x70, 0x18, typeof(GiantGhiniBoss)),
            (0x71, 0x34, typeof(SwoopBoss)), (0x72, 0x4d, typeof(SubterrorBoss)),
            (0x78, 0x13, typeof(PumpkinHeadBoss)), (0x79, 0x2b, typeof(HeadThwompBoss)),
            (0x7a, 0x4a, typeof(ShadowHagBoss)) })
        foreach (bool batched in new[] { false, true })
        foreach (int free in new[] { 0, 1 })
        {
            ReinitializeGameplayForValidation(); LoadValidationRoom(4, test.Room);
            _inventory.SetScriptedEquippedItems(0, 0); _player.WarpTo(new(16, 112)); _player.BeginCutsceneControl();
            var parent = _entities.EntityAdapters<IRoomEntity>().Single(actor => actor.Node.GetType() == test.Type);
            F1Retain(parent);
            var boss = parent.Node; boss.Position = new(80, 72);
            test.Type.GetField("_initialized", F1Hidden)?.SetValue(boss, true);
            parent.GetType().GetField("_initialized", F1Hidden)?.SetValue(parent, true);
            if (boss is PumpkinHeadBoss)
            { F1Set(boss, "_state", BossState.Dying); F1Set(boss, "_counter", 2); }
            else
            {
                F1Set(boss, "_dying", true); F1Set(boss, "_deathCounter", 2);
                ((EnemyCharacter)boss).Health = 1;
            }
            var native = F1Native(test.Id, test.Id == 0x78 ? 2 : 0, 8, boss.Position);
            native[0xd0a9] = 0; native[0xd0a4] = 0; native[0xd086] = 2;
            F1Fill(free, native);
            // commonBossCode.s is included in both original boss banks. Execute the
            // shared terminal allocator directly; each runtime species reaches
            // it through its real application update, not a whole boss fight.
            var bytes = ValidationRom.LoadCleanUs().Span;
            FailIf(!bytes.Slice(0x0f * 0x4000 + 0x4f0, 4).SequenceEqual(new byte[] { 0x62, 0x2e, 0xa4, 0x7e }),
                "Clean-US enemyBoss_dead must retain its source entry at $0f:$44f0.");
            byte[] caller = [0x16, 0xd0, 0xcd, 0xf0, 0x44, 0xc9];
            for (int i = 0; i < caller.Length; i++) native[0xc100 + i] = caller[i];
            bool Dead() => boss is PumpkinHeadBoss pumpkin ? pumpkin.IsDead : ((EnemyCharacter)boss).IsDead;
            int updates = 0;
            void Compare()
            {
                updates++;
                if (native[0xd080] != 0) SomariaPrivate<FrontendRom>(native, "_rom").Call(0xc100, 0x0f);
                FailIf(Dead() != (native[0xd080] == 0) || _entities.EntityAdapters<BossDeathExplosionRoomEntity>().Count() != F1Count(native, 4),
                    $"ENEMY${test.Id:x2} boss-death admission must retain the parent until PART$04 succeeds, free={free}, update={updates}.");
                if (!Dead())
                {
                    int timer = boss is PumpkinHeadBoss ? SomariaPrivate<int>(boss, "_counter") : SomariaPrivate<int>(boss, "_deathCounter");
                    FailIf(timer != native[0xd086], "Boss explosion failure must reload the source counter1=$01 retry.");
                }
            }
            void Step(int count) => StepGameplayUpdates(count, Vector2.Zero, batched: batched, afterUpdate: Compare);
            Step(1); FailIf(Dead(), "Boss cannot retire before the terminal death update.");
            Step(1); Step(2);
            if (free == 0)
            {
                FailIf(Dead() || _entities.EntityAdapters<BossDeathExplosionRoomEntity>().Count() != 0,
                    "Full PART capacity must preserve a live retrying boss without an explosion.");
                _entities.RetireEntity(_entities.ResolvePartSlot(0)!);
                for (int offset = 0; offset < 64; offset++) native[0xd0c0 + offset] = 0;
                Step(1); Step(2);
            }
            FailIf(!Dead() || _entities.EntityAdapters<BossDeathExplosionRoomEntity>().Count() != 1,
                "Released capacity must admit exactly one boss explosion and complete the deferred teardown.");
            _entities.Clear();
        }
    }
}
