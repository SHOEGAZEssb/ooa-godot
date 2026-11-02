
namespace Oracles;

public sealed partial class ValidationRoot
{
    private static void CheckSharedSideScrollData(OracleGame game)
    {
        bool ages = game == OracleGame.Ages;
        string pack = OracleAssetPack.ForGame(game);
        var data = SideScrollPlayerDatabase.For(pack);
        var parameters = data.Parameters;
        var native = ValidationRom.LoadCleanUs(game);
        int Byte(int address) => native.Span[address];
        int Word(int address) => Byte(address) | Byte(address + 1) << 8;
        int Bank6(int pointer) => 6 * 0x4000 + (pointer & 0x3fff);
        int animations = Bank6(ages ? 0x5cb1 : 0x5a21);
        int graphics = Bank6(ages ? 0x59b1 : 0x5739);
        int oams = Bank6(ages ? 0x60a7 : 0x5d9e);
        // Native @applyGravity operands and shared speed-table rows, rather
        // than another generated table consumed by the same runtime reader.
        int gravity = 5 * 0x4000 + (ages ? 0x1c34 : 0x1b23);
        int speeds = 5 * 0x4000 + (ages ? 0x1d3b : 0x1c34);
        int swimming = 5 * 0x4000 + (ages ? 0x1778 : 0x16dc);
        FailIf(parameters.Gravity != Byte(gravity + 1) || parameters.ReducedGravity != Byte(gravity + 10) ||
            parameters.NormalSpeed != Byte(speeds) || parameters.IceVelocityInterval != Byte(speeds + 9) ||
            parameters.SwimSpeed != Byte(swimming + 10) || parameters.FastSwimSpeed != Byte(swimming + 6),
            $"{game}: side-view gravity or native movement/swimming operands differ from the clean US ROM.");
        int jump = Bank6(Word(animations + 0x18 * 2));
        FailIf(!parameters.AnimationPhaseDurations.SequenceEqual(new[] { Byte(jump),Byte(jump + 3),Byte(jump + 6) }) ||
            Byte(jump + 9) != 0x7f || Byte(jump + 11) != 0xff,
            $"{game}: side-view jumping lost the native $18 finite animation boundaries.");
        foreach (bool mermaid in ages ? new[] { false,true } : new[] { false })
        {
            int animation = Bank6(Word(animations + (mermaid ? 0x11 : 0x17) * 2));
            FailIf(Byte(animation + 6) != 0xff || Byte(animation + 7) != 0xf9,
                $"{game}: native side-view swimming requires its two-frame relative loop.");
            for (int frame = 0; frame < 2; frame++)
            for (int direction = 0; direction < 4; direction++)
            {
                var record = data.SwimmingFrame(mermaid,frame,direction);
                int graphic = graphics + (Byte(animation + frame * 3 + 1) + direction) * 3;
                int oam = (ages ? 0x13 : 0x12) * 0x4000 + (Word(oams + Byte(graphic) * 2) & 0x3fff);
                string encoded = string.Join(';',Enumerable.Range(0,Byte(oam)).Select(part =>
                    string.Join(',',Enumerable.Range(0,4).Select(column => Byte(oam + 1 + part * 4 + column)))));
                FailIf(record.Duration != Byte(animation + frame * 3) || record.Sprite != "spr_link" ||
                    record.SourceOffset != (Word(graphic + 1) & ~31) - 0x4000 || record.OamIndex != Byte(graphic) ||
                    (Word(graphic + 1) & 31) != 4 || record.Oam != encoded,
                    $"{game}: side-view Mermaid={mermaid}, frame{frame}, direction{direction} differs from native graphics/OAM pointers.");
            }
        }
        var sprites = new PlayerSpriteLibrary(pack);
        FailIf(sprites.SideScrollSwimTexture.GetSize() != new Vector2(32,64) ||
            sprites.DamageSideScrollSwimTexture.GetSize() != new Vector2(32,64),
            $"{game}: native side-view Flippers frames must compose with the selected pack's sprite sheet.");
        if (ages)
        {
            var mermaid = MermaidSwimmingDatabase.For(pack).Parameters;
            FailIf(mermaid.MermaidTargetSpeed != Byte(speeds + 26) || mermaid.MermaidVelocityInterval != Byte(speeds + 25) ||
                sprites.SideScrollMermaidTexture.GetSize() != new Vector2(32,64),
                "Ages: top-down and side-view Mermaid Suit must share the native velocity operands and keep their own frames.");
        }
        else
        {
            bool rejected = false;
            try { _ = MermaidSwimmingDatabase.For(pack); }
            catch (NotSupportedException error) { rejected = error.Message.Contains("ROM_SEASONS",StringComparison.Ordinal); }
            FailIf(!rejected,"Seasons must reject the absent Mermaid Suit source branch before reading Ages-only assets.");
            rejected = false;
            try { _ = data.SwimmingFrame(true,0,0); }
            catch (KeyNotFoundException) { rejected = true; }
            FailIf(!rejected || data.TileType(0x0c) != SideScrollTileType.Hole ||
                data.TileType(0x0d) != SideScrollTileType.Lava || data.TileType(0x22) != SideScrollTileType.Ice,
                "Seasons: retain native side-view $0c first-match HOLE, $0d LAVA, $22 ICE and reject unimported Mermaid frames.");
        }
    }
}
