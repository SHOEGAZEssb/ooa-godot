using Godot;
using System;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateSharedLinkItemData()
    {
        foreach (OracleGame game in new[] { OracleGame.Ages, OracleGame.Seasons })
        {
            CheckSharedSideScrollData(game);
            bool ages = game == OracleGame.Ages;
            var data = LinkItemDatabase.For(OracleAssetPack.ForGame(game));
            CheckSharedLinkFrames(game,data.GaleFrames,3);
            CheckSharedLinkFrames(game,data.FallFrames,4);
            var flute=new FluteDatabase(OracleAssetPack.ForGame(game));
            CheckSharedLinkFrames(game,flute.Frames,0x1b);
            FailIf(flute.Duration(0)!=95 || Enumerable.Range(1,3).Any(icon=>flute.Duration(icon)!=255) ||
                Enumerable.Range(0,4).Any(icon=>flute.Sound(icon)!=new[]{0x8b,0x9d,0x9e,0x9f}[icon]),
                $"{game}: native Flute terminal parameter boundaries or four sound selections differ.");
            var landingRom = ValidationRom.LoadCleanUs(game);
            int landingAt = 5 * 0x4000 + (ages ? 0x0ba1 : 0x0b5a);
            bool readsTile = landingRom.Span[landingAt + 9] == 0xcd;
            FailIf(data.FallReadsLandingTile != readsTile || readsTile &&
                (data.FallTrampolineTile != landingRom.Span[landingAt + 13] || data.FallTrampolineGroupMask != 6 || data.FallTrampolineGroupValue != 4),
                $"{game}: native warpTransition5_01 tile/trampoline dispatch differs from clean US code.");
            // Native LINK_ANIM_MODE_22/26 and DIG_2, read independently from
            // the two source pointer tables and their finite frame streams.
            FailIf(data.Constants.SwordSwingFrames != 17 || data.Constants.MinecartSwordSwingFrames != 14 ||
                data.Constants.ShovelActionFrames != 23 || data.Constants.ShovelDigFrame != 4 ||
                data.Constants.ShovelSecondPoseFrame != 8 || data.Constants.SwordChargeCounter != 0x28 ||
                !data.Constants.MinecartSwingArcPhases.SequenceEqual(new[] { 0,1,3,3 }),
                $"{game}: native Link/item update boundaries changed.");
            CheckGraphics("attack", [0xac,0xb0,0xb4]);
            CheckGraphics("minecart-attack", ages ? [0xc8,0xcc,0xcc,0x58] : [0xbc,0xc0,0xc0,0x58]);
            CheckGraphics("shovel", ages ? [0xf8,0xfc] : [0xf0,0xf4]);
            var air = TopDownAirDatabase.For(OracleAssetPack.ForGame(game)).Parameters;
            FailIf(air.Gravity != 0x20 || air.JumpSpeedZ != -0x1e0 || air.MaximumFallSpeed != 0x300 ||
                !air.AnimationPhaseDurations.SequenceEqual(new[] { 9,9,6 }) ||
                air.CapeLevel != (ages ? 0 : 2) || air.CapeSpeedZ != (ages ? 0 : -0x80) ||
                air.CapeMinimumSpeedZ != (ages ? 0 : 0x100) ||
                !air.CapeAnimationDurations.SequenceEqual(ages ? new[] { 0,0 } : new[] { 6,40 }),
                $"{game}: configured Feather/Cape source branches changed.");
            if (!ages)
            {
                CheckGraphics("cape",[0xe4,0xe4,0x80]);
                // Native graphicsHeaderTable and common OAM rows, including
                // the terminal standing frame rather than a third Cape pose.
                int[] offsets = [0x18c0,0x1900,0x18e0,0x1900];
                int[] standing = [0,0xc0,0x200,0xc0];
                string[] oam = ["8,0,0,0;8,8,0,32","8,0,2,32;8,8,0,32","8,0,0,0;8,8,0,32","8,0,0,0;8,8,2,0"];
                for (int phase = 0; phase < 3; phase++)
                for (int direction = 0; direction < 4; direction++)
                {
                    var graphic = data.Graphic("cape",0,phase,direction);
                    FailIf(graphic.ByteOffset != (phase < 2 ? offsets[direction] : standing[direction]) ||
                        phase < 2 && graphic.Oam != oam[direction],
                        $"Seasons: Cape phase {phase}/{direction} lost native graphics/OAM.");
                }
            }
            int[] lift = ages ? [0xdc,0xe0,0xb0] : [0xd0,0xd4,0xb0];
            for (int pose = 0; pose < lift.Length; pose++)
            for (int direction = 0; direction < 4; direction++)
                FailIf(data.Graphic("bracelet", pose, 0, direction).GraphicsIndex != lift[pose] + direction,
                    $"{game}: native lift/strain/throw graphic {pose}/{direction} changed.");
            Vector2[] shovel = [new(0,-8),new(6,4),new(0,7),new(-7,4)];
            for (int direction = 0; direction < 4; direction++)
                FailIf(data.ShovelOffset(direction) != shovel[direction], $"{game}: native Shovel child geometry changed.");
            FailIf(data.SwordArc(0) != new SwordArc(9,6,-2,16) || data.SwordArc(27) != new SwordArc(5,5,0,-12) ||
                !data.SwordOam(0).SequenceEqual(new[] { new SwordPart(8,4,4) }),
                $"{game}: native Sword arcs or OAM pointer resolution changed.");

            // Seasons' overworld silent list reaches Subrosia's first $00;
            // Subrosia's second list reaches the Maku tree's first $00.
            byte[][] bombable = ages
                ? [[0xc1,0xc2,0xc4,0xd1,0xcf], [0x1f,0x30,0x31,0x32,0x33,0x38,0x39,0x3a,0x3b,0x68,0x69],
                   [0x1f,0x30,0x31,0x32,0x33,0x38,0x39,0x3a,0x3b,0x68,0x69], [0x12],
                   [0xc1,0xc2,0xc4,0xd1,0xcf], [0x1f,0x30,0x31,0x32,0x33,0x38,0x39,0x3a,0x3b,0x68,0x69]]
                : [[0xc1,0xc2,0xe2,0xcb], [], [], [0x1f,0x30,0x31,0x32,0x33,0x38,0x39,0x3a,0x3b],
                   [0x1f,0x30,0x31,0x32,0x33,0x38,0x39,0x3a,0x3b], [0x12]];
            byte[][] silent = ages
                ? [[0xfd,0xfe,0xff],[0x0a,0x0b],[0x0a,0x0b],[],[0xfd,0xfe,0xff],[0x0a,0x0b]]
                : [[0xfd,0xfe,0xff,0xd9,0xda,0x20,0xd7],[0xfd],[],[0x0a,0x0b],[0x0a,0x0b],[]];
            for (int collision = 0; collision < 6; collision++)
            {
                foreach (bool isSilent in new[] { false, true })
                {
                    byte[] expected = isSilent ? silent[collision] : bombable[collision];
                    var rows = data.ClinkRows.Where(row => row.CollisionSet == collision && row.Kind == (isSilent ? "silent" : "bombable")).ToArray();
                    FailIf(rows.Length != expected.Length + 1 || !rows[^1].Terminal || rows[^1].Tile != 0 ||
                        rows.Take(expected.Length).Any(row => row.Terminal) ||
                        !rows.Take(expected.Length).Select(row => row.Tile).SequenceEqual(expected),
                        $"{game}: clink set ${collision:x2} lost its native {(isSilent ? "silent" : "bombable")} content/fallthrough/terminator.");
                    for (int tile = 0; tile < 256; tile++)
                        FailIf((isSilent ? data.IsSilentClinkTile(collision,(byte)tile) : data.IsBombableClinkTile(collision,(byte)tile)) != expected.Contains((byte)tile),
                            $"{game}: clink set ${collision:x2} classified native tile ${tile:x2} incorrectly.");
                }
            }
            if (!ages)
            {
                bool rejected = false;
                try { data.Graphic("underwater-attack",0,0,0); }
                catch (ArgumentOutOfRangeException) { rejected = true; }
                FailIf(!rejected, "Seasons must not substitute unrelated graphics for Ages' underwater Sword mode.");
            }
            // Both native pointer sets select these common spr_link/spr_swords
            // pictures. Reuse independently established pixel goldens, not a
            // second composition from the generated records under test.
            var sprites = new PlayerSpriteLibrary(OracleAssetPack.ForGame(game));
            CheckPixels(sprites.AttackTexture, 0xd97fede215433c60UL, "Sword body");
            CheckPixels(sprites.MinecartAttackTexture, 0x64a25aeb9b7db45aUL, "cart Sword body");
            CheckPixels(sprites.ShovelLinkTexture, 0x5d009ee6640952eeUL, "Shovel body");
            CheckPixels(sprites.SwordTexture, 0x61e9abb0e1173ec7UL, "Sword OAM");
            CheckPixels(sprites.ShieldLinkTexture, 0xe4598edf4d896e5cUL, "Shield body");
            if (!ages)
            {
                // Independent goldens from the original common spr_link PNG,
                // literal native Cape/standing cells and standard palettes 0/5.
                ulong[][] goldens =
                [
                    [0x0d4e4191d1c1e170UL,0xdef1418fa3cadbe3UL,0x40cabd5e59c00a78UL,0x30dadaa48a30f6d3UL,
                     0xddeed47cbb459db5UL,0x057b1c21dd7f255dUL,0x5f6668116fd8bcc8UL,0x93586f0088aee39dUL],
                    [0xd2482f84b5ea82f0UL,0x96a1b40f3b45d7aeUL,0xa30fe88809ac877cUL,0x76d6adc8e41befbaUL,
                     0x8b8ecac60648985fUL,0x15ca92b9bbabc49bUL,0x0bd35e68ac5372c8UL,0x152344c4e3199fdfUL]
                ];
                for (int damage = 0; damage < 2; damage++)
                for (int phase = 0; phase < 3; phase++)
                for (int direction = 0; direction < 4; direction++)
                {
                    var frame = sprites.CapeFrame(phase,direction,damage != 0);
                    FailIf(frame.Offset != new Vector2(-8,-8),"Native Cape OAM must stay centered on Link.");
                    CheckPixels(frame.Texture,goldens[damage][direction + (phase == 2 ? 4 : 0)],
                        $"Cape {phase}/{direction}, damage={damage}");
                }
            }
            void CheckPixels(Texture2D texture, ulong expected, string action)
            {
                using Image image = texture.GetImage();
                ulong actual = OracleGraphicsCache.PixelHash(image);
                FailIf(actual != expected, $"{game}: native shared {action} pixels changed: ${actual:x16}, expected ${expected:x16}.");
            }
            void CheckGraphics(string kind, int[] bases)
            {
                for (int phase = 0; phase < bases.Length; phase++)
                for (int direction = 0; direction < 4; direction++)
                    FailIf(data.Graphic(kind,0,phase,direction).GraphicsIndex != bases[phase] + direction,
                        $"{game}: native {kind} frame {phase}/{direction} changed.");
            }
        }
    }
    private static void CheckSharedLinkFrames(OracleGame game, ObjectSpriteFrame[] frames, int mode)
    {
        var rom = ValidationRom.LoadCleanUs(game);
        bool ages = game == OracleGame.Ages;
        int bank6 = 6 * 0x4000;
        int gfx = bank6 + (ages ? 0x19b1 : 0x1739);
        int pointers = bank6 + (ages ? 0x1cb1 : 0x1a21);
        int oamPointers = bank6 + (ages ? 0x20a7 : 0x1d9e);
        int oamBank = (ages ? 0x13 : 0x12) * 0x4000;
        int Word(int at) => rom.Span[at] | rom.Span[at + 1] << 8;
        int animation = bank6 + (Word(pointers + mode * 2) & 0x3fff);
        var vram = Enumerable.Repeat(-1,256).ToArray();
        int frameCount = mode switch {3=>8,4=>3,0x1b=>7,_=>throw new ArgumentOutOfRangeException(nameof(mode))}, end = frameCount * 3;
        FailIf(frames.Length != frameCount || mode!=0x1b && (rom.Span[animation + end] != 0xff ||
            end + 1 + unchecked((sbyte)rom.Span[animation + end + 1]) != 0) ||
            mode==0x1b && (rom.Span[animation+18]!=0x7f || rom.Span[animation+20]!=0xff),
            $"{game}: Link mode ${mode:x2} must retain its native loop or terminal frame.");
        for (int index = 0; index < frames.Length; index++)
        {
            int at = animation + index * 3;
            int graphic = rom.Span[at + 1];
            if (mode == 4 && graphic >= 0x54) graphic += 2;
            int packed = Word(gfx + graphic * 3 + 1);
            int count = packed & 31;
            int sourceTile = ((packed & ~31) - 0x4000) / 16;
            for (int tile = 0; tile < count; tile++) vram[tile] = sourceTile + tile;
            int oamIndex = rom.Span[gfx + graphic * 3];
            int oam = oamBank + (Word(oamPointers + oamIndex * 2) & 0x3fff);
            var expected = new ObjectSpritePart[rom.Span[oam]];
            for (int cell = 0; cell < expected.Length; cell++)
            {
                int part = oam + 1 + cell * 4;
                int tile = rom.Span[part + 2] & 0xfe;
                FailIf(vram[tile] < 0 || vram[tile + 1] != vram[tile] + 1,
                    $"{game}: native Gale frame ${index:x2} references an unloaded partial-upload tile.");
                expected[cell] = new(rom.Span[part],rom.Span[part + 1],vram[tile],rom.Span[part + 3]);
            }
            FailIf(frames[index].Duration != rom.Span[at] || frames[index].SourceOffset != 0 ||
                frames[index].BasePalette != 0 || !frames[index].Parts.SequenceEqual(expected),
                $"{game}: Gale frame ${index:x2} must retain clean-ROM duration, OAM order/flips and prior partial graphics.");
        }
    }
}
