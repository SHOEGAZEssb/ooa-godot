using Godot;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateSwoopFlyingMovementRom() => CompareSwoopMovementRom(0);
    private void ValidateSwoopStompTargetRom() => CompareSwoopMovementRom(1);
    private void ValidateSwoopBounceAngleRom() => CompareSwoopMovementRom(2);

    private void CompareSwoopMovementRom(int kind)
    {
        foreach (bool batched in new[] { false, true })
        for (int test = 0; test < (kind == 0 ? 4 : kind == 1 ? 2 : 1); test++)
        {
            ReinitializeGameplayForValidation(); LoadValidationRoom(4, 0x34);
            _inventory.SetScriptedEquippedItems(0, 0);
            var owner = _entities.EntityAdapters<IRoomEntity>().Single(entity => entity.Node is SwoopBoss);
            F1Retain(owner); var boss = (SwoopBoss)owner.Node;
            // Already-active source state and clear floor are explicit inputs.
            // $a2 also suppresses unrelated stomp debris/tile mutation.
            FillValidationRoom(0xa2);
            Vector2 link = kind == 0 ? test switch { 0 => new(72, 72), 1 => new(96, 96), 2 => new(112, 64), _ => new(120, 80) }
                : kind == 1 ? new(120, 80) : new(64, 112);
            _player.WarpTo(link); _player.BeginCutsceneControl();
            Vector2 start = kind == 1 && test == 1 ? new(134.5f, 80) : kind == 0 && test == 3 ? new(17, 24) : new(64.25f, 64.5f);
            if (kind == 0 && test is 0 or 1 or 2) start = new(64, 64);
            boss.Position = start; boss.Health = kind == 2 ? 8 : 12;
            F1Set(owner, "_initialized", true); F1Set(boss, "_introStarted", true); F1Set(boss, "_introActive", false);
            SwoopState initialState = kind == 0 ? SwoopState.Flying : kind == 1 ? test == 0 ? SwoopState.Telegraph : SwoopState.Stomping : SwoopState.Bouncing;
            F1Set(boss, "_state", initialState);
            F1Set(boss, "_counter", kind == 0 ? test == 2 ? 1 : 60 : 11);
            F1Set(boss, "_attackCounter", test < 2 ? 1 : 10);
            F1Set(boss, "_angle", kind == 2 ? 8 : kind == 0 && test == 3 ? 0x18 : 0x10);
            F1Set(boss, "_zFixed", kind == 2 ? -0x80 : -0x2000);
            F1Set(boss, "_speedZ", kind == 2 ? 0x80 : 8);
            F1Set(boss, "_bounces", 2); F1Set(boss, "_stompTarget", new Vector2(136, 80));
            boss.Animation.SetAnimation(0);
            if (kind == 0 && test == 3) _currentRoom.SetPositionTileAndCollision(new(8, 24), 0xa2, 0x0f, 0);
            var native = F1Native(0x71, 0, kind == 0 ? 0x0a : 0x0b, start);
            native.Word(0xd08a, (int)(start.Y * 256)); native.Word(0xd08c, (int)(start.X * 256));
            native[0xd085] = kind == 2 ? (byte)3 : kind == 1 && test == 1 ? (byte)1 : (byte)0;
            native[0xd086] = (byte)(kind == 0 ? test == 2 ? 1 : 60 : kind == 2 ? 2 : 11);
            native[0xd0b0] = (byte)(test < 2 ? 1 : 10); native[0xd0b2] = 80; native[0xd0b3] = 136;
            native[0xd089] = (byte)SomariaPrivate<int>(boss, "_angle");
            native[0xd090] = kind == 0 ? (byte)0x14 : kind == 1 ? (byte)0x50 : (byte)0x28;
            native[0xd0a9] = (byte)boss.Health; native.Word(0xd08e, boss.ZFixed);
            native.Word(0xd094, kind == 2 ? 0x80 : 8);
            int update = 0;
            void Step(int count = 1) => StepGameplayUpdates(count, Vector2.Zero, batched: batched, afterUpdate: () =>
            {
                F1Enemy(native); update++;
                SwoopState expected = native[0xd084] == 0x0a ? SwoopState.Flying : native[0xd085] switch
                {
                    0 => SwoopState.Telegraph, 1 => SwoopState.Stomping, 2 => SwoopState.Grounded, _ => SwoopState.Bouncing
                };
                FailIf(boss.State != expected || boss.Position != new Vector2(native.Word(0xd08c) / 256f, native.Word(0xd08a) / 256f) ||
                    (boss.ZFixed & 0xffff) != native.Word(0xd08e) || (SomariaPrivate<int>(boss, "_speedZ") & 0xffff) != native.Word(0xd094) ||
                    SomariaPrivate<int>(boss, "_angle") != native[0xd089],
                    $"Swoop $71 movement kind={kind} case={test} update={update} batch={batched}: " +
                    $"runtime={boss.State}/{boss.Position}/Z{boss.ZFixed & 0xffff:x4}/speedZ{SomariaPrivate<int>(boss, "_speedZ") & 0xffff:x4}/angle{SomariaPrivate<int>(boss, "_angle"):x2}; " +
                    $"native={native[0xd084]:x2}:{native[0xd085]:x2}/({native.Word(0xd08c):x4},{native.Word(0xd08a):x4})/Z{native.Word(0xd08e):x4}/speedZ{native.Word(0xd094):x4}/angle{native[0xd089]:x2}.");
                if (kind == 1 && test == 0 && native[0xd086] <= 10)
                    FailIf(SomariaPrivate<Vector2>(boss, "_stompTarget") != new Vector2(native[0xd0b3], native[0xd0b2]),
                        "Swoop $71 must capture tile-top Y, centred X at telegraph counter $0a.");
            });
            Step();
            Vector2 pausedPosition = boss.Position;
            int pausedZ = boss.ZFixed;
            _dialogue.ShowGameplayMessage("Swoop movement pause", 100);
            StepGameplayUpdates(3, Vector2.Zero, batched: batched);
            FailIf(boss.Position != pausedPosition || boss.ZFixed != pausedZ,
                "Initialized Swoop $71 motion/height must freeze during text before resuming the source branch.");
            _dialogue.Close();
            Step(kind == 1 && test == 0 ? 13 : kind == 2 ? 33 : 2);
            if (kind == 1)
            {
                for (int wait = 0; boss.State == SwoopState.Stomping && wait < 60; wait++) Step();
                FailIf(boss.State != SwoopState.Grounded,
                    "Swoop $71 must finish the captured/quantized stomp through its native landing handoff.");
            }
            if (kind == 2)
                FailIf(SomariaPrivate<int>(boss, "_angle") != 8 || boss.State != SwoopState.Grounded,
                    "Swoop $71 must retain its angle through the intermediate bounce and complete the final landing.");
            _entities.Clear();
        }
        GD.Print($"Validated clean-US Swoop $71 movement branch {kind} at declared active-state boundaries through split/batched gameplay.");
    }
}
