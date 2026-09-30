using Godot;
using System;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateObjectAnimationRom()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var counterField = typeof(EnemyAnimationPlayer).GetField("_frameCounter", flags)!;
        var npcTicks = typeof(NpcCharacter).GetField("_animationTicks", flags)!;
        var enemies = new EnemyDatabase();
        int streams = 0, comparisons = 0;
        void CheckStreams(int kind, int id, string[] encoded)
        {
            var rom = new ObjectAnimationRom(kind, id);
            var owner = new Node2D();
            var playback = new EnemyAnimationPlayer(owner, encoded.Length);
            // Real OAM is checked below, directly against ROM bytes. A blank
            // sheet avoids coupling this clock test to species graphics loads.
            using var sheet = Image.CreateEmpty(256, 256, false, Image.Format.Rgba8);
            playback.Load(sheet, encoded, 0, 0);
            var npc = new NpcCharacter();
            var record = new NpcDatabase().GetRoomNpcs(0, 0x66).First();
            npc.Initialize(record);
            try
            {
                for (int animation = 0; animation < encoded.Length; animation++)
                {
                    AnimationDefinition definition = OracleGraphicsCache.GetAnimationDefinition(encoded[animation]);
                    int start = 0;
                    void Reset()
                    {
                        rom.Set(animation);
                        start = rom.Pointer - 3;
                        playback.SetAnimation(animation);
                        npc.SetScriptAnimation(encoded[animation]);
                    }
                    void Compare(string phase)
                    {
                        int frame = kind == 0 ? npc.CurrentAnimationFrame : playback.FrameIndex;
                        var imported = definition.Frames[frame];
                        int counter = kind == 0 ? imported.Duration - (int)(double)npcTicks.GetValue(npc)! : (int)counterField.GetValue(playback)!;
                        int parameter = kind == 0 ? npc.CurrentAnimationParameter : playback.CurrentParameter;
                        FailIf(counter != rom.Counter || parameter != rom.Parameter ||
                            start + frame * 3 + 3 != rom.Pointer || imported.Duration != rom.Duration || imported.EncodedOam != rom.Oam,
                            $"ROM animation kind={kind}, id=${id:x2}, animation=${animation:x2}, {phase}: " +
                            $"frame={frame}, counter={counter}/${rom.Counter:x2}, parameter=${parameter:x2}/${rom.Parameter:x2}, pointer=${rom.Pointer:x4}.");
                        comparisons++;
                    }
                    void Advance(int count)
                    {
                        for (int tick = 0; tick < count; tick++) rom.Advance();
                        if (kind == 0) npc.AdvanceAnimationUpdates(count);
                        else for (int tick = 0; tick < count; tick++) playback.Advance();
                    }
                    Reset();
                    Compare("set");
                    // Two complete stream lengths revisit interior loops and
                    // single-frame held poses, including flags $01/$70/$80.
                    int length = definition.Frames.Sum(f => f.Duration);
                    for (int tick = 0; tick < length * 2 + 1; tick++)
                    {
                        Advance(1);
                        Compare($"update={tick + 1}");
                    }
                    // Same-index setters explicitly restart; then mixed-size
                    // calls must consume the same number of native updates.
                    Reset(); Compare("same-index reset");
                    foreach (int count in new[] { 1, 7, 32, 129 })
                    {
                        Advance(count); Compare($"batch={count}");
                    }
                    for (int remaining = 0; remaining < 256; remaining++)
                    {
                        Reset();
                        rom.Counter = remaining;
                        if (kind == 0) npc.SetAnimationCounter(remaining);
                        else playback.SetFrameCounter(remaining);
                        Advance(1); Compare($"counter=${remaining:x2}");
                    }
                    if (kind != 0)
                    {
                        Reset();
                        for (int tick = 0; tick < length * 2; tick++)
                        {
                            FailIf(playback.ConsumeParameter() != rom.Parameter || playback.ConsumeParameter() != 0,
                                $"ROM animation parameter consumption kind={kind}, id=${id:x2}, animation=${animation:x2}.");
                            rom.Parameter = 0;
                            Compare("consumed");
                            Advance(1); Compare("after consumption");
                        }
                    }
                    streams++;
                }
            }
            finally { npc.Free(); owner.Free(); }
        }
        foreach (int id in new[] { EnemyId.Rope, EnemyId.Spark, EnemyId.LikeLike, EnemyId.Smog })
            CheckStreams(1, id, enemies.ImportedEnemy(id).Animations);
        var patch = new PatchDatabase();
        CheckStreams(0, 0x94, Enumerable.Range(0, 13).Select(patch.Animation).ToArray());
        var burning = GeneratedTable.Load("res://assets/oracle/effects/burning_enemy.tsv",
            new GeneratedTableSchema("ROM burning animation", GeneratedTableKeySemantics.Unique,
                ["sprite", "tile-base", "palette", "frames", "gravity", "animation", "source"], ["source"], headerRequired: true));
        CheckStreams(2, 0x12, [burning.SingleRow().RequiredString(5)]);
        GD.Print($"Validated {streams} imported enemy/interaction/part animation streams and {comparisons} ROM comparisons: OAM, durations, loops, reset, parameters, batched calls and all counter bytes.");
    }
}
