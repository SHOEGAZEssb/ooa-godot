using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateSubterrorDrillDistanceRom() => CompareSubterrorDrillMovementRom(distance: true);
    private void ValidateSubterrorDrillFractionsRom() => CompareSubterrorDrillMovementRom(distance: false);

    private void ValidateSubterrorPlanarPublicationRom()
    {
        foreach (bool batched in new[] { false, true })
        foreach (bool intro in new[] { false, true })
        foreach (float x in intro ? new[] { 64.25f } : new[] { 64.25f, 16.25f })
        {
            ReinitializeGameplayForValidation(); LoadValidationRoom(4, 0x4d);
            _inventory.SetScriptedEquippedItems(0, 0); _player.WarpTo(new(24, 112)); _player.BeginCutsceneControl();
            var owner = _entities.EntityAdapters<IRoomEntity>().Single(actor => actor.Node is SubterrorBoss); F1Retain(owner);
            var boss = (SubterrorBoss)owner.Node;
            F1Set(owner, "_initialized", true); F1Set(boss, "_initialized", true); F1Set(boss, "_introActive", intro);
            F1Set(boss, "_shuttersClosed", (System.Func<bool>)(() => true));
            F1Set(boss, "_state", intro ? SubterrorState.WaitingForDoors : SubterrorState.AboveGround);
            F1Set(boss, "_substate", intro ? 0 : 2); F1Set(boss, "_counter1", 3); F1Set(boss, "_counter2", intro ? 1 : 0);
            F1Set(boss, "_dirtEnabled", false); F1Set(boss, "_speed", intro ? 0x3c : 0x14); F1Set(boss, "_angle", intro ? 0x10 : 0x18);
            boss.Position = new(x, 64.5f); boss.Health = 12;
            var native = F1Native(0x72, 0, intro ? 8 : 12, boss.Position);
            native[0xd085] = intro ? (byte)0 : (byte)2; native[0xd086] = 3; native[0xd087] = intro ? (byte)1 : (byte)0;
            native[0xd089] = intro ? (byte)0x10 : (byte)0x18; native[0xd090] = intro ? (byte)0x3c : (byte)0x14;
            native[0xd0a9] = 12; native[0xcc93] = 0;
            native.Word(0xd08a, 0x4080); native.Word(0xd08c, (int)(x * 256));
            byte[] scratch = new byte[4];
            var probe = new PartMovementScratchProbe(() =>
            {
                for (int i = 0; i < 4; i++) scratch[i] = _runtimeState.ReadWramByte(0xcec0 + i);
            });
            _entities.RegisterEnemySlot(probe, 15); _entities.AddEntity(probe);
            int update = 0;
            StepGameplayUpdates(3, Vector2.Zero, batched: batched, afterUpdate: () =>
            {
                F1Enemy(native); update++;
                FailIf(boss.Position != new Vector2(native.Word(0xd08c) / 256f, native.Word(0xd08a) / 256f) ||
                    (int)boss.State != native[0xd084] || boss.Substate != native[0xd085] || boss.Counter1 != native[0xd086] ||
                    Enumerable.Range(0, 4).Any(i => scratch[i] != native[0xcec0 + i]),
                    $"Subterror intro={intro} x={x}, visit={update}: port={boss.Position}/${(int)boss.State:x2}.{boss.Substate}/counter{boss.Counter1}/scratch{string.Join(',', scratch)}; source=({native.Word(0xd08c):x4},{native.Word(0xd08a):x4})/${native[0xd084]:x2}.{native[0xd085]}/counter{native[0xd086]}/scratch{string.Join(',', Enumerable.Range(0,4).Select(i => native[0xcec0+i]))}.");
            });
            FailIf(!intro && boss.State != SubterrorState.Digging, "Roaming's terminal counter visit must retain its last planar step before digging.");
            _entities.Clear();
        }
    }

    private void CompareSubterrorDrillMovementRom(bool distance)
    {
        foreach (bool batched in new[] { false, true })
        foreach (var test in distance
            ? new[] { (X: 80, Y: 81, Drill: false), (X: 76, Y: 77, Drill: false), (X: 88, Y: 65, Drill: false), (X: 87, Y: 65, Drill: true) }
            : new[] { (X: 80, Y: 65, Drill: true) })
        {
            ReinitializeGameplayForValidation(); LoadValidationRoom(4, 0x4d);
            _inventory.SetScriptedEquippedItems(0, 0);
            _player.WarpTo(new(test.X, test.Y)); _player.BeginCutsceneControl();
            var owner = _entities.EntityAdapters<IRoomEntity>().Single(actor => actor.Node is SubterrorBoss);
            F1Retain(owner);
            var boss = (SubterrorBoss)owner.Node;
            // Declare the already-underground state boundary, retaining the
            // imported miniboss room and real application/ENEMY dispatch.
            // SPEED_80 moving down adds $0080 before the distance gate.
            boss.Position = new(64.25f, 64.5f); boss.Health = 12;
            F1Set(owner, "_initialized", true);
            F1Set(boss, "_initialized", true); F1Set(boss, "_introActive", false);
            F1Set(boss, "_state", SubterrorState.UndergroundMoving); F1Set(boss, "_substate", 1);
            F1Set(boss, "_counter1", 30); F1Set(boss, "_counter2", 1);
            F1Set(boss, "_speed", 0x14); F1Set(boss, "_angle", 0x10); F1Set(boss, "_dirtEnabled", false);
            var native = F1Native(0x72, 0, 0x0a, boss.Position);
            native[0xd085] = 1; native[0xd086] = 30; native[0xd087] = 1;
            native[0xd089] = 0x10; native[0xd090] = 0x14; native[0xd0a9] = 12;
            native.Word(0xd08a, 0x4080); native.Word(0xd08c, 0x4040);
            byte[] scratch = new byte[4];
            var probe = new PartMovementScratchProbe(() =>
            {
                for (int i = 0; i < 4; i++) scratch[i] = _runtimeState.ReadWramByte(0xcec0 + i);
            });
            _entities.RegisterEnemySlot(probe, 15); _entities.AddEntity(probe);
            int update = 0;
            void Step(int count = 1) => StepGameplayUpdates(count, Vector2.Zero, batched: batched, afterUpdate: () =>
            {
                F1Enemy(native); update++;
                FailIf(Enumerable.Range(0, 4).Any(i => scratch[i] != native[0xcec0 + i]),
                    "Subterror underground movement must publish native velocity before the later PART/INTERACTION phases.");
                FailIf((int)boss.State != native[0xd084] || boss.Substate != native[0xd085] ||
                    boss.Counter1 != native[0xd086] || boss.Counter2 != native[0xd087] ||
                    boss.Position != new Vector2(native.Word(0xd08c) / 256f, native.Word(0xd08a) / 256f),
                    $"Subterror $72 drill {(distance ? "distance" : "fraction")} Link=({test.X},{test.Y}), update={update}, batch={batched}: " +
                    $"runtime={(int)boss.State:x2}/{boss.Substate}/{boss.Counter1}/{boss.Counter2}/{boss.Position}; " +
                    $"native={native[0xd084]:x2}/{native[0xd085]}/{native[0xd086]}/{native[0xd087]}/({native.Word(0xd08c):x4},{native.Word(0xd08a):x4}).");
            });
            Step();
            FailIf((boss.State == SubterrorState.Drilling) != test.Drill,
                "Subterror $72 requires strict high-byte Manhattan distance < $18 after movement.");
            if (!distance)
            {
                FailIf(native.Word(0xd08c) != 0x5840 || native.Word(0xd08a) != 0x4800,
                    "Native Subterror drill copy must change coordinate high bytes only, retaining low X=$40.");
                Step(3);
                _dialogue.ShowGameplayMessage("Subterror drill pause", 100);
                int counter = boss.Counter2;
                StepGameplayUpdates(3, Vector2.Zero, batched: batched);
                FailIf(boss.Counter2 != counter || boss.Position != new Vector2(88.25f, 72),
                    "Paused Subterror drilling must preserve its wait and fractional coordinates.");
                _dialogue.Close(); Step();
            }
            _entities.Clear();
        }
        GD.Print($"Validated native Subterror $72 underground {(distance ? "strict Manhattan boundary" : "high-byte drill copy retaining fractions")} through split/batched application updates at a declared underground-state boundary.");
    }
}
