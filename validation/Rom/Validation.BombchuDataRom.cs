using Godot;
using System;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateBombchuDataRom()
    {
        var data = new BombchuDatabase();
        CompareBombCollisionDataRom();
        var nativeAnimation = new ObjectAnimationRom(3, 0x0d);
        var node = new Node2D();
        var playback = new EnemyAnimationPlayer(node, 7);
        using var sheet = Image.CreateEmpty(256, 256, false, Image.Format.Rgba8);
        playback.Load(sheet, Enumerable.Range(0, 7).Select(index => data.Graphic(index).Animation).ToArray(), 0, 0);
        var counter = typeof(EnemyAnimationPlayer).GetField("_frameCounter", BindingFlags.Instance | BindingFlags.NonPublic)!;
        int comparisons = 0;
        try
        {
            for (int animation = 0; animation < 7; animation++)
            {
                var frames = OracleGraphicsCache.GetAnimationDefinition(data.Graphic(animation).Animation).Frames;
                nativeAnimation.Set(animation); playback.SetAnimation(animation);
                int start = nativeAnimation.Pointer - 3;
                // Walking: two complete loops. Explosion: stop exactly when
                // its native handler would delete; its stream has no loop.
                int updates = animation < 6 ? 24 : 34;
                for (int update = 0; update <= updates; update++)
                {
                    var frame = frames[playback.FrameIndex];
                    FailIf(nativeAnimation.Counter != (int)counter.GetValue(playback)! ||
                        nativeAnimation.Parameter != playback.CurrentParameter ||
                        nativeAnimation.Pointer != start + playback.FrameIndex * 3 + 3 ||
                        nativeAnimation.Duration != frame.Duration || nativeAnimation.Oam != frame.EncodedOam,
                        $"ITEM$0d animation ${animation:x2}, update {update}: native clock/OAM differs.");
                    comparisons++;
                    if (update != updates) { nativeAnimation.Advance(); playback.Advance(); }
                }
                FailIf(animation == 6 && nativeAnimation.Parameter != 0xff,
                    "ITEM$0d explosion must reach its $ff terminal after exactly 34 animation updates.");
                // Same-index reset and the counter-zero wrap use the native
                // setter/advancer; no long terminal-pose run is needed.
                nativeAnimation.Set(animation); playback.SetAnimation(animation);
                nativeAnimation.Counter = 0; playback.SetFrameCounter(0);
                nativeAnimation.Advance(); playback.Advance();
                FailIf(nativeAnimation.Counter != 0xff || (int)counter.GetValue(playback)! != 0xff,
                    $"ITEM$0d animation ${animation:x2}: zero counter must wrap to $ff.");
            }
        }
        finally { node.Free(); }

        ReinitializeGameplayForValidation(); LoadValidationRoom(4, 0x91); _entities.Clear();
        var rom = new SomariaRom(_saveData, _random.CaptureState(), _currentRoom, 1, 120, 112);
        rom[0xc689] = 0x0d; rom[0xc688] = 0; rom[0xc6b3] = 0x10;
        rom[0xd01a] = 0x80;
        rom.Update(1, 1, 0);
        const int item = 0xd700;
        var graphic = data.Graphic(0);
        var offset = data.PlacementOffset(1, false);
        FailIf(rom[item] == 0 || rom[item + 1] != 0x0d || rom[item + 4] != 1 ||
            rom[item + 6] != data.Constant("initial-wait") || rom[item + 7] != data.Constant("initial-fuse") ||
            rom[item + 0x11] != data.Constant("search-speed") || rom[item + 0x30] != 0xd0 ||
            rom[item + 0x31] != data.Constant("initial-turn") ||
            rom[item + 0x26] != data.Constant("initial-vision") || rom[item + 0x27] != data.Constant("initial-vision") ||
            rom[item + 0xb] != (byte)(112 + offset.Y) || rom[item + 0xd] != (byte)(120 + offset.X) ||
            rom[item + 0x24] != graphic.Collision || rom[item + 0x28] != graphic.RawDamage ||
            rom[item + 0x29] != graphic.Health || rom[item + 0x1d] != graphic.TileBase ||
            rom[item + 0x1c] != graphic.OamFlags || rom[0xc6b3] != 0x09,
            $"ITEM$0d native child initialization differs: enabled/id/state={rom[item]:x2}/{rom[item+1]:x2}/{rom[item+4]}, " +
            $"counters={rom[item+6]}/{rom[item+7]}, speedTmp={rom[item+0x11]}, XY={rom[item+0xd]},{rom[item+0xb]}, " +
            $"tile/flags={rom[item+0x1d]:x2}/{rom[item+0x1c]:x2}, ammo={rom[0xc6b3]:x2}.");

        void PrepareScan(int id, bool enabled = true, bool visible = true)
        {
            rom[item + 4] = 2; rom[item + 0x30] = 0xd0;
            rom[item + 0x26] = rom[item + 0x27] = 0x18;
            rom[0xd080] = (byte)(enabled ? 1 : 0); rom[0xd081] = (byte)id;
            rom[0xd09a] = (byte)(visible ? 0x80 : 0);
            rom[0xd08b] = rom[item + 0xb]; rom[0xd08d] = rom[item + 0xd];
            rom[0xd0a6] = rom[0xd0a7] = 4;
            // These are deliberately zero/nonzero: the native target scan
            // does not gate on health, Z, collision enable or invincibility.
            rom[0xd0a9] = rom[0xd0a4] = 0;
            rom[0xd08f] = 0xf0; rom[0xd0ab] = 0x40;
        }
        for (int id = 0; id < 128; id++)
        {
            PrepareScan(id); rom.ScanBombchuTarget(item);
            bool found = rom[item + 4] == 3;
            FailIf(found != data.CanTarget(id), $"bombchuTargets native enemy ID ${id:x2} differs.");
            if (found)
                FailIf(rom.Word(item + 0x18) != 0xd080 || rom[item + 6] != data.Constant("target-wait") ||
                    rom[item + 0x11] != data.Constant("target-speed") ||
                    rom[item + 0x26] != data.Constant("target-radius") || rom[item + 0x27] != data.Constant("target-radius"),
                    $"ITEM$0d target ${id:x2}: native slot reference, wait, speed or radius differs.");
            else FailIf(rom[item + 0x30] != 0xd1, "ITEM$0d must inspect one enemy slot per search call.");
        }
        foreach (bool enabled in new[] { false, true })
        foreach (bool visible in new[] { false, true })
        {
            PrepareScan(0x32, enabled, visible); rom.ScanBombchuTarget(item);
            FailIf((rom[item + 4] == 3) != (enabled && visible), "ITEM$0d target enabled/visible gates differ.");
        }
        PrepareScan(0x32, false);
        int radius = 0x18;
        for (int cycle = 0; cycle < 6; cycle++)
        {
            for (int slot = 0; slot < 16; slot++)
            {
                FailIf(rom[item + 0x30] != 0xd0 + slot, "ITEM$0d enemy-slot scan order differs.");
                rom.ScanBombchuTarget(item);
            }
            radius += data.Constant("vision-step");
            if (radius >= data.Constant("vision-limit")) radius = data.Constant("vision-reset");
            FailIf(rom[item + 0x30] != 0xd0 || rom[item + 0x26] != radius || rom[item + 0x27] != radius,
                $"ITEM$0d vision cycle {cycle}: native radius/slot wrap differs.");
        }
        FailIf(rom.RandomCalls != 0 || rom.Sounds.Count != 0, "ITEM$0d initialization/target scan must consume no RNG or cues.");
        for (int packed = 0; packed < 256; packed++) rom[0xce00 + packed] = 0;
        foreach (bool sideGroup in new[] { false, true })
        foreach (int direction in Enumerable.Range(0, 4))
        foreach (bool solid in new[] { false, true })
        {
            rom[0xcc2d] = (byte)(sideGroup ? 6 : 4);
            // Placement selects by active group, independently of the flag
            // which selects the physical top-down/side-view state machine.
            rom[0xcc34] = (byte)(sideGroup ? 0 : 0x20);
            rom[0xd008] = (byte)direction;
            offset = data.PlacementOffset(direction, sideGroup);
            int packed = ((112 + offset.Y) & 0xf0) | ((120 + offset.X) >> 4);
            rom[0xce00 + packed] = (byte)(solid ? 0x0f : 0);
            rom.PlaceBombchuInFrontOfLink(item);
            FailIf(rom[item + 0xb] != 112 + (solid ? 0 : offset.Y) ||
                rom[item + 0xd] != 120 + (solid ? 0 : offset.X),
                $"ITEM$0d placement group={sideGroup}, dir={direction}, solid={solid}: native offset/fallback differs.");
            rom[0xce00 + packed] = 0;
        }
        CompareBombchuSteeringRom(data);
        foreach (bool timed in new[] { false, true })
        {
            var explosion = new SomariaRom(_saveData, _random.CaptureState(), _currentRoom, 1, 120, 112);
            explosion[0xc688] = explosion[0xc689] = 0;
            explosion[item] = 3; explosion[item + 1] = 0x0d; explosion[item + 4] = 4;
            explosion[item + 7] = (byte)(timed ? 1 : 2);
            explosion[item + 0xb] = 64; explosion[item + 0xd] = 80;
            explosion.Word(item + 0x18, 0xd080); // Dead target, read by its native slot reference.
            explosion[item + 0x24] = (byte)graphic.Collision;
            explosion[item + 0x28] = (byte)graphic.RawDamage;
            explosion[item + 0x26] = explosion[item + 0x27] = 6;
            explosion[item + 0x3c] = 0xff;
            explosion[WramAddress.wActiveRing] = 0xff;
            explosion.Update(0, 0, 0);
            int duration = OracleGraphicsCache.GetAnimationDefinition(data.Graphic(6).Animation).Frames[0].Duration;
            FailIf(explosion[item + 4] != 0xff || explosion[item + 7] != 0 ||
                explosion[item + 0x20] != duration - (timed ? 1 : 0) ||
                explosion[item + 6] != (timed ? 7 : 8) || explosion[item + 0x21] != 6 ||
                explosion[item + 0x24] != 0x98 || explosion[item + 0x28] != graphic.RawDamage ||
                explosion[item + 0x1c] != 0x0a || explosion[item + 0x1d] != 0x0c || explosion[item] != 1 ||
                !explosion.Sounds.SequenceEqual(new[] { SoundId.SndExplosion }) || explosion.RandomCalls != 0,
                $"ITEM$0d explosion timed={timed}: native start/update boundary, attributes, probe, cue or RNG differs.");
        }
        GD.Print($"Validated ITEM$0d data against clean-US code: seven animations ({comparisons} bounded clock/OAM checks), native child/ammo initialization, all 128 target bits, ordered one-slot scans and six vision cycles. Gameplay integration remains open.");
    }
}
