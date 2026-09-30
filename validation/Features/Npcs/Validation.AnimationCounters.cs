using Godot;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private static void ValidateAnimationCounterBoundaries()
    {
        // bank0 enemyAnimate/interactionAnimate use DEC (HL), including $00
        // -> $ff. Literal streams keep these boundaries covered without a ROM.
        const string cycle = "2,128@8,0,0,0|3,1@8,0,2,0";
        const string held = "3,112@8,0,0,0";
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var counter = typeof(EnemyAnimationPlayer).GetField("_frameCounter", flags)!;
        var ticks = typeof(NpcCharacter).GetField("_animationTicks", flags)!;
        var owner = new Node2D();
        var npc = new NpcCharacter();
        using var sheet = Image.CreateEmpty(32, 32, false, Image.Format.Rgba8);
        var enemy = new EnemyAnimationPlayer(owner, 2);
        enemy.Load(sheet, [cycle, held], 0, 0);
        npc.Initialize(new NpcDatabase().GetRoomNpcs(0, 0x66).First());
        try
        {
            enemy.SetAnimation(0); npc.SetScriptAnimation(cycle);
            enemy.SetFrameCounter(0); npc.SetAnimationCounter(0);
            enemy.ConsumeParameter();
            for (int update = 1; update <= 255; update++)
            {
                enemy.Advance(); npc.AdvanceAnimationUpdates(1);
                FailIf(enemy.FrameIndex != 0 || npc.CurrentAnimationFrame != 0 ||
                    (int)counter.GetValue(enemy)! != 256 - update ||
                    2 - (double)ticks.GetValue(npc)! != 256 - update || enemy.CurrentParameter != 0,
                    $"Zero animation counter must remain on frame zero through update {update}.");
            }
            enemy.Advance(); npc.AdvanceAnimationUpdates(1);
            FailIf(enemy.FrameIndex != 1 || npc.CurrentAnimationFrame != 1 ||
                (int)counter.GetValue(enemy)! != 3 || enemy.CurrentParameter != 1 || npc.CurrentAnimationParameter != 1,
                "Zero animation counter must enter frame one and reload its flags on update 256.");
            enemy.SetAnimation(1); npc.SetScriptAnimation(held);
            enemy.ConsumeParameter();
            enemy.Advance(); npc.AdvanceAnimationUpdates(1);
            FailIf((int)counter.GetValue(enemy)! != 2 || (double)ticks.GetValue(npc)! != 1 || enemy.CurrentParameter != 0,
                "Single-frame loop must decrement without reloading flags early.");
            enemy.Advance(); enemy.Advance(); npc.AdvanceAnimationUpdates(2);
            FailIf((int)counter.GetValue(enemy)! != 3 || (double)ticks.GetValue(npc)! != 0 || enemy.CurrentParameter != 112,
                "Single-frame loop must reset its counter and flags on the exact loop boundary.");
        }
        finally { npc.Free(); owner.Free(); }
        GD.Print("Validated animation counter $00 wrapping, 256-update boundary, parameter consumption and single-frame loop clocks without ROM input.");
    }
}
