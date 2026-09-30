using Godot;
using System;

namespace oracleofages;

// Immutable imported graphics and per-player atlases. Pose selection and all
// gameplay state remain with Player; building a texture has no world inputs.
internal sealed class PlayerSpriteLibrary
{
    private LinkItemDatabase _linkItems => LinkItemDatabase.Shared;
    private SideScrollPlayerDatabase _sideScrollPlayerData => SideScrollPlayerDatabase.Shared;
    private TopDownSwimmingDatabase _topDownSwimmingData => TopDownSwimmingDatabase.Shared;
    internal Texture2D WalkTexture { get; private set; } = null!;

    internal void PrepareWalkTexture() => WalkTexture = BuildLinkTexture(damagePalette: false);

    // Build item and alternate-pose atlases only when presented. Their inputs
    // are immutable imported graphics; keep the result for this player node.
    private Texture2D? _damageTextureCache;
    internal Texture2D DamageTexture => _damageTextureCache ??= BuildLinkTexture(damagePalette: true);
    private Texture2D? _getItemOneHandTextureCache;
    internal Texture2D GetItemOneHandTexture => _getItemOneHandTextureCache ??= BuildGetItemOneHandTexture(damagePalette: false);
    private Texture2D? _damageGetItemOneHandTextureCache;
    internal Texture2D DamageGetItemOneHandTexture => _damageGetItemOneHandTextureCache ??= BuildGetItemOneHandTexture(damagePalette: true);
    private Texture2D? _getItemTwoHandTextureCache;
    internal Texture2D GetItemTwoHandTexture => _getItemTwoHandTextureCache ??= BuildGetItemTwoHandTexture(damagePalette: false);
    private Texture2D? _damageGetItemTwoHandTextureCache;
    internal Texture2D DamageGetItemTwoHandTexture => _damageGetItemTwoHandTextureCache ??= BuildGetItemTwoHandTexture(damagePalette: true);
    private Texture2D? _funnyJokeDanceLeftTextureCache;
    internal Texture2D FunnyJokeDanceLeftTexture => _funnyJokeDanceLeftTextureCache ??= BuildFunnyJokeDanceTexture( right: false, damagePalette: false);
    private Texture2D? _damageFunnyJokeDanceLeftTextureCache;
    internal Texture2D DamageFunnyJokeDanceLeftTexture => _damageFunnyJokeDanceLeftTextureCache ??= BuildFunnyJokeDanceTexture( right: false, damagePalette: true);
    private Texture2D? _funnyJokeDanceRightTextureCache;
    internal Texture2D FunnyJokeDanceRightTexture => _funnyJokeDanceRightTextureCache ??= BuildFunnyJokeDanceTexture( right: true, damagePalette: false);
    private Texture2D? _damageFunnyJokeDanceRightTextureCache;
    internal Texture2D DamageFunnyJokeDanceRightTexture => _damageFunnyJokeDanceRightTextureCache ??= BuildFunnyJokeDanceTexture( right: true, damagePalette: true);
    private Texture2D? _getItemOneHandRightTextureCache;
    internal Texture2D GetItemOneHandRightTexture => _getItemOneHandRightTextureCache ??= BuildGetItemOneHandRightTexture( damagePalette: false);
    private Texture2D? _damageGetItemOneHandRightTextureCache;
    internal Texture2D DamageGetItemOneHandRightTexture => _damageGetItemOneHandRightTextureCache ??= BuildGetItemOneHandRightTexture( damagePalette: true);
    private Texture2D? _carriedObjectTextureCache;
    internal Texture2D CarriedObjectTexture => _carriedObjectTextureCache ??= BuildCarriedObjectLinkTexture(damagePalette: false);
    private Texture2D? _damageCarriedObjectTextureCache;
    internal Texture2D DamageCarriedObjectTexture => _damageCarriedObjectTextureCache ??= BuildCarriedObjectLinkTexture(damagePalette: true);
    private Texture2D? _minecartLinkTextureCache;
    internal Texture2D MinecartLinkTexture => _minecartLinkTextureCache ??= BuildMinecartLinkTexture(damagePalette: false);
    private Texture2D? _damageMinecartLinkTextureCache;
    internal Texture2D DamageMinecartLinkTexture => _damageMinecartLinkTextureCache ??= BuildMinecartLinkTexture(damagePalette: true);
    private Texture2D? _minecartAttackTextureCache;
    internal Texture2D MinecartAttackTexture => _minecartAttackTextureCache ??= BuildMinecartAttackTexture(damagePalette: false);
    private Texture2D? _damageMinecartAttackTextureCache;
    internal Texture2D DamageMinecartAttackTexture => _damageMinecartAttackTextureCache ??= BuildMinecartAttackTexture(damagePalette: true);
    private Texture2D[,]? _braceletActionTexturesCache;
    internal Texture2D[,] BraceletActionTextures => _braceletActionTexturesCache ??= BuildBraceletActionTextures(damagePalette: false);
    private Texture2D[,]? _damageBraceletActionTexturesCache;
    internal Texture2D[,] DamageBraceletActionTextures => _damageBraceletActionTexturesCache ??= BuildBraceletActionTextures(damagePalette: true);
    private Texture2D? _shieldLinkTextureCache;
    internal Texture2D ShieldLinkTexture => _shieldLinkTextureCache ??= BuildShieldLinkTexture(damagePalette: false);
    private Texture2D? _damageShieldLinkTextureCache;
    internal Texture2D DamageShieldLinkTexture => _damageShieldLinkTextureCache ??= BuildShieldLinkTexture(damagePalette: true);
    private Texture2D? _pushTextureCache;
    internal Texture2D PushTexture => _pushTextureCache ??= BuildPushLinkTexture(damagePalette: false);
    private Texture2D? _damagePushTextureCache;
    internal Texture2D DamagePushTexture => _damagePushTextureCache ??= BuildPushLinkTexture(damagePalette: true);
    private Texture2D? _attackTextureCache;
    internal Texture2D AttackTexture => _attackTextureCache ??= BuildAttackLinkTexture(damagePalette: false);
    private Texture2D? _damageAttackTextureCache;
    internal Texture2D DamageAttackTexture => _damageAttackTextureCache ??= BuildAttackLinkTexture(damagePalette: true);
    private (Texture2D[,] LinkTextures, Texture2D[] WeaponTextures,
        Texture2D[,] Poses, Texture2D[,] DamagePoses, Vector2[,] Offsets)? _seedShooterTexturesCache;
    private (Texture2D[,] LinkTextures, Texture2D[] WeaponTextures,
        Texture2D[,] Poses, Texture2D[,] DamagePoses, Vector2[,] Offsets) SeedShooterTextures =>
        _seedShooterTexturesCache ??= BuildSeedShooterPoseTextures();
    internal Texture2D[,] SeedShooterLinkTextures => SeedShooterTextures.LinkTextures;
    internal Texture2D[] SeedShooterWeaponTextures => SeedShooterTextures.WeaponTextures;
    internal Texture2D[,] SeedShooterPoseTextures => SeedShooterTextures.Poses;
    internal Texture2D[,] DamageSeedShooterPoseTextures => SeedShooterTextures.DamagePoses;
    internal Vector2[,] SeedShooterPoseOffsets => SeedShooterTextures.Offsets;
    private Texture2D? _underwaterAttackTextureCache;
    internal Texture2D UnderwaterAttackTexture => _underwaterAttackTextureCache ??= BuildUnderwaterAttackLinkTexture( damagePalette: false);
    private Texture2D? _damageUnderwaterAttackTextureCache;
    internal Texture2D DamageUnderwaterAttackTexture => _damageUnderwaterAttackTextureCache ??= BuildUnderwaterAttackLinkTexture( damagePalette: true);
    private Texture2D? _swordTextureCache;
    internal Texture2D SwordTexture => _swordTextureCache ??= BuildSwordTexture(chargedPalette: false);
    private Texture2D? _chargedSwordTextureCache;
    internal Texture2D ChargedSwordTexture => _chargedSwordTextureCache ??= BuildSwordTexture(chargedPalette: true);
    private Texture2D? _shovelLinkTextureCache;
    internal Texture2D ShovelLinkTexture => _shovelLinkTextureCache ??= BuildShovelLinkTexture(damagePalette: false);
    private Texture2D? _damageShovelLinkTextureCache;
    internal Texture2D DamageShovelLinkTexture => _damageShovelLinkTextureCache ??= BuildShovelLinkTexture(damagePalette: true);
    private Texture2D? _drownTextureCache;
    internal Texture2D DrownTexture => _drownTextureCache ??= BuildDrownTexture(damagePalette: false);
    private Texture2D? _damageDrownTextureCache;
    internal Texture2D DamageDrownTexture => _damageDrownTextureCache ??= BuildDrownTexture(damagePalette: true);
    private Texture2D? _topDownSwimTextureCache;
    internal Texture2D TopDownSwimTexture => _topDownSwimTextureCache ??= BuildTopDownSwimTexture(damagePalette: false);
    private Texture2D? _damageTopDownSwimTextureCache;
    internal Texture2D DamageTopDownSwimTexture => _damageTopDownSwimTextureCache ??= BuildTopDownSwimTexture(damagePalette: true);
    private Texture2D? _topDownDiveTextureCache;
    internal Texture2D TopDownDiveTexture => _topDownDiveTextureCache ??= BuildTopDownDiveTexture(damagePalette: false);
    private Texture2D? _damageTopDownDiveTextureCache;
    internal Texture2D DamageTopDownDiveTexture => _damageTopDownDiveTextureCache ??= BuildTopDownDiveTexture(damagePalette: true);
    private Texture2D? _sideScrollSwimTextureCache;
    internal Texture2D SideScrollSwimTexture => _sideScrollSwimTextureCache ??= BuildSideScrollSwimTexture( mermaidSuit: false, damagePalette: false);
    private Texture2D? _damageSideScrollSwimTextureCache;
    internal Texture2D DamageSideScrollSwimTexture => _damageSideScrollSwimTextureCache ??= BuildSideScrollSwimTexture( mermaidSuit: false, damagePalette: true);
    private Texture2D? _sideScrollMermaidTextureCache;
    internal Texture2D SideScrollMermaidTexture => _sideScrollMermaidTextureCache ??= BuildSideScrollSwimTexture( mermaidSuit: true, damagePalette: false);
    private Texture2D? _damageSideScrollMermaidTextureCache;
    internal Texture2D DamageSideScrollMermaidTexture => _damageSideScrollMermaidTextureCache ??= BuildSideScrollSwimTexture( mermaidSuit: true, damagePalette: true);
    private Texture2D? _fallInHoleTextureCache;
    internal Texture2D FallInHoleTexture => _fallInHoleTextureCache ??= BuildFallInHoleTexture(damagePalette: false);
    private Texture2D? _damageFallInHoleTextureCache;
    internal Texture2D DamageFallInHoleTexture => _damageFallInHoleTextureCache ??= BuildFallInHoleTexture(damagePalette: true);
    private Texture2D? _ledgeJumpTextureCache;
    internal Texture2D LedgeJumpTexture => _ledgeJumpTextureCache ??= BuildLedgeJumpTexture(damagePalette: false);
    private Texture2D? _damageLedgeJumpTextureCache;
    internal Texture2D DamageLedgeJumpTexture => _damageLedgeJumpTextureCache ??= BuildLedgeJumpTexture(damagePalette: true);
    private Texture2D? _sideScrollSquishXTextureCache;
    internal Texture2D SideScrollSquishXTexture => _sideScrollSquishXTextureCache ??= BuildSideScrollSquishTexture( vertical: false);
    private Texture2D? _sideScrollSquishYTextureCache;
    internal Texture2D SideScrollSquishYTexture => _sideScrollSquishYTextureCache ??= BuildSideScrollSquishTexture( vertical: true);
    private Texture2D? _deathTextureCache;
    internal Texture2D DeathTexture => _deathTextureCache ??= BuildDeathTexture();

    internal static Texture2D BuildLinkTexture(bool damagePalette)
    {
        Image source = OracleGraphicsCache.LoadImage(
            "res://assets/oracle/gfx/spr_link.png");
        Image output = Image.CreateEmpty(32, 64, false, Image.Format.Rgba8);

        // LINK_ANIM_MODE_WALK uses base gfx indices $54 and $80, then adds
        // direction (UP, RIGHT, DOWN, LEFT). These resolve to the offsets and
        // OAM compositions below in specialObjectAnimationData.s. Up/down
        // alternate a mirrored composition of the same source tiles; they are
        // not neighboring 16x16 crops.
        WriteWalkFrame(output, source, Facing.Up, 0, 0x0000, false, damagePalette); // gfx $54, OAM $00
        WriteWalkFrame(output, source, Facing.Up, 1, 0x0000, true, damagePalette);  // gfx $80, OAM $01
        WriteWalkFrame(output, source, Facing.Right, 0, 0x0080, true, damagePalette); // gfx $55
        WriteWalkFrame(output, source, Facing.Right, 1, 0x00c0, true, damagePalette); // gfx $81
        WriteWalkFrame(output, source, Facing.Down, 0, 0x0200, false, damagePalette); // gfx $56
        WriteWalkFrame(output, source, Facing.Down, 1, 0x0200, true, damagePalette);  // gfx $82
        WriteWalkFrame(output, source, Facing.Left, 0, 0x0080, false, damagePalette); // gfx $57
        WriteWalkFrame(output, source, Facing.Left, 1, 0x00c0, false, damagePalette); // gfx $83

        return ImageTexture.CreateFromImage(output);
    }

    private static Texture2D BuildSideScrollSquishTexture(bool vertical)
    {
        Image source = OracleGraphicsCache.LoadImage(
            "res://assets/oracle/gfx/spr_link.png");
        // LINK_ANIM_MODE_SQUISHX/Y use graphics $32/$33. Their source
        // pointers are spr_link+$0ce0/$03c0 with special-object OAM $2d/$04.
        return NpcCharacter.BuildOamTexture(
            source,
            vertical
                ? "8,0,0,0;8,8,0,32"
                : "0,4,0,0;16,4,2,0",
            vertical ? 0x3c : 0xce,
            basePalette: 0);
    }

    private static Texture2D BuildGetItemOneHandTexture(bool damagePalette)
    {
        Image source = OracleGraphicsCache.LoadImage(
            "res://assets/oracle/gfx/spr_link.png");
        Image output = Image.CreateEmpty(16, 16, false, Image.Format.Rgba8);

        // LINK_ANIM_MODE_GETITEM1HAND ($0e) is the static graphics frame $05:
        // OAM $00, spr_link+$0da0, four tiles. The frame is below $54, so
        // loadLinkAndCompanionAnimationFrame_body does not add Link's direction.
        WriteLinkFrame(output, source, 0, 0, 0x0da0, false, damagePalette);
        return ImageTexture.CreateFromImage(output);
    }

    private static Texture2D BuildGetItemTwoHandTexture(bool damagePalette)
    {
        Image source = OracleGraphicsCache.LoadImage(
            "res://assets/oracle/gfx/spr_link.png");
        Image output = Image.CreateEmpty(16, 16, false, Image.Format.Rgba8);

        // LINK_ANIM_MODE_GETITEM2HAND ($0f) is static graphics frame $06:
        // OAM $04 mirrors the single spr_link+$0de0 cell into a 16-pixel body.
        WriteSymmetricLinkCell(output, source, 0, 0, 0x0de0, damagePalette);
        return ImageTexture.CreateFromImage(output);
    }

    private static Texture2D BuildFunnyJokeDanceTexture(
        bool right,
        bool damagePalette)
    {
        Image source = OracleGraphicsCache.LoadImage(
            "res://assets/oracle/gfx/spr_link.png");
        Image output = Image.CreateEmpty(16, 16, false, Image.Format.Rgba8);

        // LINK_ANIM_MODE_DANCELEFT/RIGHT ($08/$09) are static graphics
        // frames $1d/$1e: spr_link+$0d60 with OAM $00/$01.
        WriteLinkFrame(
            output, source, 0, 0, 0x0d60,
            mirroredOam: right,
            damagePalette: damagePalette);
        return ImageTexture.CreateFromImage(output);
    }

    private static Texture2D BuildGetItemOneHandRightTexture(bool damagePalette)
    {
        Image source = OracleGraphicsCache.LoadImage(
            "res://assets/oracle/gfx/spr_link.png");
        Image output = Image.CreateEmpty(16, 16, false, Image.Format.Rgba8);

        // LINK_ANIM_MODE_GETITEM1HAND_RIGHT ($1c) is static graphics frame
        // $07: the same spr_link+$0da0 cells as $0e, mirrored by OAM $01.
        WriteLinkFrame(
            output, source, 0, 0, 0x0da0,
            mirroredOam: true,
            damagePalette: damagePalette);
        return ImageTexture.CreateFromImage(output);
    }

    private static Texture2D BuildCarriedObjectLinkTexture(bool damagePalette)
    {
        Image source = OracleGraphicsCache.LoadImage(
            "res://assets/oracle/gfx/spr_link.png");
        Image output = Image.CreateEmpty(32, 64, false, Image.Format.Rgba8);

        // A finished grab leaves LINK_ANIM_MODE_WALK active. func_4553 adds
        // held-object variant $08 to its $54/$80 graphics frames, producing
        // the direction-aware $5c-$5f/$88-$8b frames below.
        WriteLinkFrame(output, source, 0, (int)Facing.Up * 16, 0x0040, false, damagePalette);       // $5c, OAM $00
        WriteLinkFrame(output, source, 0, (int)Facing.Right * 16, 0x01c0, true, damagePalette);    // $5d, OAM $01
        WriteLinkFrame(output, source, 0, (int)Facing.Down * 16, 0x0180, false, damagePalette);    // $5e, OAM $00
        WriteLinkFrame(output, source, 0, (int)Facing.Left * 16, 0x01c0, false, damagePalette);    // $5f, OAM $00
        WriteLinkFrame(output, source, 16, (int)Facing.Up * 16, 0x0040, true, damagePalette);      // $88, OAM $01
        WriteLinkFrame(output, source, 16, (int)Facing.Right * 16, 0x1140, true, damagePalette);  // $89, OAM $01
        WriteLinkFrame(output, source, 16, (int)Facing.Down * 16, 0x0180, true, damagePalette);   // $8a, OAM $01
        WriteLinkFrame(output, source, 16, (int)Facing.Left * 16, 0x1140, false, damagePalette);  // $8b, OAM $00

        return ImageTexture.CreateFromImage(output);
    }

    private Texture2D BuildMinecartLinkTexture(bool damagePalette)
    {
        Image source = OracleGraphicsCache.LoadImage(
            "res://assets/oracle/gfx/spr_link.png");
        Image output = Image.CreateEmpty(32, 64, false, Image.Format.Rgba8);

        // getLinkWalkingAnimation selects variant $01 while the main object is
        // SPECIALOBJECT_MINECART. Added to walk frames $54/$80, that resolves
        // to $58-$5b/$84-$87. Both animation phases intentionally use the
        // same seated pixels even though the cart and Link offset animate.
        for (int phase = 0; phase < 2; phase++)
        for (int facing = 0; facing < 4; facing++)
        {
            LinkGraphicRecord record =
                _linkItems.Graphic("minecart", 0, phase, facing);
            if (record.OamIndex == 0x04)
            {
                WriteSymmetricLinkCell(
                    output, source, phase * 16, facing * 16,
                    record.ByteOffset, damagePalette);
            }
            else
            {
                WriteLinkFrame(
                    output, source, phase * 16, facing * 16,
                    record.ByteOffset, record.MirrorX, damagePalette);
            }
        }
        return ImageTexture.CreateFromImage(output);
    }

    private Texture2D BuildMinecartAttackTexture(bool damagePalette)
    {
        Image source = OracleGraphicsCache.LoadImage(
            "res://assets/oracle/gfx/spr_link.png");
        Image output = Image.CreateEmpty(64, 64, false, Image.Format.Rgba8);

        // LINK_ANIM_MODE_26 resolves the Sword's four 3/3/8/terminal body
        // phases to $c8-$cb, $cc-$cf, $cc-$cf, and seated $58-$5b.
        for (int phase = 0; phase < 4; phase++)
        for (int facing = 0; facing < 4; facing++)
        {
            LinkGraphicRecord record =
                _linkItems.Graphic("minecart-attack", 0, phase, facing);
            if (record.OamIndex == 0x04)
            {
                WriteSymmetricLinkCell(
                    output, source, phase * 16, facing * 16,
                    record.ByteOffset, damagePalette);
            }
            else
            {
                WriteLinkFrame(
                    output, source, phase * 16, facing * 16,
                    record.ByteOffset, record.MirrorX, damagePalette);
            }
        }
        return ImageTexture.CreateFromImage(output);
    }

    private Texture2D[,] BuildBraceletActionTextures(bool damagePalette)
    {
        Image source = OracleGraphicsCache.LoadImage(
            "res://assets/oracle/gfx/spr_link.png");
        var result = new Texture2D[3, 4];
        for (int pose = 0; pose < result.GetLength(0); pose++)
        for (int direction = ObjectDirection.Up; direction < result.GetLength(1); direction++)
        {
            LinkGraphicRecord record =
                _linkItems.Graphic("bracelet", pose, 0, direction);
            result[pose, direction] = NpcCharacter.BuildOamTexture(
                source,
                record.Oam,
                record.ByteOffset / 16,
                basePalette: damagePalette ? 5 : 0);
        }
        return result;
    }

    private Texture2D BuildShieldLinkTexture(bool damagePalette)
    {
        Image source = OracleGraphicsCache.LoadImage(
            "res://assets/oracle/gfx/spr_link.png");
        Image output = Image.CreateEmpty(128, 64, false, Image.Format.Rgba8);

        // func_4553 selects variants $05/$06 while the shield is merely
        // equipped and $07/$08 while wUsingShield is nonzero. Added to walk
        // frames $54/$80, these are $68-$77 and $94-$a3. Every entry uses
        // special-object OAM $00, so each source pair retains its native order.
        for (int variant = 0; variant < 4; variant++)
        for (int facing = 0; facing < 4; facing++)
        for (int phase = 0; phase < 2; phase++)
        {
            LinkGraphicRecord record =
                _linkItems.Graphic("shield", variant, phase, facing);
            WriteLinkFrame(
                output, source,
                variant * 32 + phase * 16, facing * 16,
                record.ByteOffset, false, damagePalette);
        }
        return ImageTexture.CreateFromImage(output);
    }

    private static Texture2D BuildPushLinkTexture(bool damagePalette)
    {
        Image source = OracleGraphicsCache.LoadImage(
            "res://assets/oracle/gfx/spr_link.png");
        Image output = Image.CreateEmpty(32, 64, false, Image.Format.Rgba8);

        // The pushing walking variant adds $10 to LINK_ANIM_MODE_WALK's
        // gfx indices, producing frames $64-$67 and $90-$93. The source
        // offsets and compositions below come from specialObjectAnimationData.s.
        WriteLinkFrame(output, source, 0, (int)Facing.Up * 16, 0x0a00, false, damagePalette);       // $64, OAM $00
        WriteLinkFrame(output, source, 0, (int)Facing.Right * 16, 0x0b00, true, damagePalette);    // $65, OAM $01
        WriteSymmetricLinkCell(output, source, 0, (int)Facing.Down * 16, 0x0aa0, damagePalette);   // $66, OAM $04
        WriteLinkFrame(output, source, 0, (int)Facing.Left * 16, 0x0b00, false, damagePalette);    // $67, OAM $00
        WriteLinkFrame(output, source, 16, (int)Facing.Up * 16, 0x0a40, false, damagePalette);     // $90, OAM $00
        WriteLinkFrame(output, source, 16, (int)Facing.Right * 16, 0x0b40, true, damagePalette);  // $91, OAM $01
        WriteSymmetricLinkCell(output, source, 16, (int)Facing.Down * 16, 0x0ac0, damagePalette); // $92, OAM $04
        WriteLinkFrame(output, source, 16, (int)Facing.Left * 16, 0x0b40, false, damagePalette);  // $93, OAM $00

        return ImageTexture.CreateFromImage(output);
    }

    private Texture2D BuildAttackLinkTexture(bool damagePalette)
    {
        Image source = OracleGraphicsCache.LoadImage(
            "res://assets/oracle/gfx/spr_link.png");
        Image output = Image.CreateEmpty(48, 64, false, Image.Format.Rgba8);
        for (int facing = 0; facing < 4; facing++)
        for (int phase = 0; phase < 3; phase++)
        {
            LinkGraphicRecord record =
                _linkItems.Graphic("attack", 0, phase, facing);
            WriteLinkFrame(
                output, source, phase * 16, facing * 16,
                record.ByteOffset, record.MirrorX, damagePalette);
        }
        return ImageTexture.CreateFromImage(output);
    }

    private (Texture2D[,] LinkTextures, Texture2D[] WeaponTextures,
        Texture2D[,] Poses, Texture2D[,] DamagePoses, Vector2[,] Offsets)
        BuildSeedShooterPoseTextures()
    {
        SeedShooterRecord record = SeedShooterRecord.Load();
        Image linkSource = OracleGraphicsCache.LoadTwoBitSpriteSheet(
            "res://assets/oracle/gfx/spr_link.2bpp", 0x22e0);
        Image weaponSource = OracleGraphicsCache.LoadImage(
            $"res://assets/oracle/gfx/{record.WeaponSprite}.png");
        const int variantCount = 3;
        int angleCount = record.WeaponOam.Length;
        var linkTextures = new Texture2D[variantCount, angleCount];
        var weaponTextures = new Texture2D[record.WeaponOam.Length];
        var poses = new Texture2D[variantCount, angleCount];
        var damagePoses = new Texture2D[variantCount, angleCount];
        var offsets = new Vector2[variantCount, angleCount];
        for (int angle = ObjectAngle.Up; angle < angleCount; angle++)
        {
            (Texture2D weapon, Vector2 weaponOffset) =
                NpcCharacter.BuildPositionedOamTexture(
                    weaponSource, record.WeaponOam[angle], tileBase: 0,
                    basePalette: record.WeaponPalette,
                    paletteOverride: null,
                    sourceGrayscaleInverted:
                        record.WeaponSourceGrayscaleInverted);
            weaponTextures[angle] = weapon;
            for (int variant = 0; variant < variantCount; variant++)
            {
                LinkGraphicRecord linkGraphic =
                    _linkItems.Graphic("shooter", variant, 0, angle);
                (Texture2D link, Vector2 linkOffset) =
                    NpcCharacter.BuildPositionedOamTexture(
                        linkSource, linkGraphic.Oam,
                        tileBase: 0, basePalette: 0,
                        paletteOverride: null,
                        sourceGrayscaleInverted: true,
                        sourceOffset: linkGraphic.ByteOffset);
                (Texture2D damageLink, Vector2 damageLinkOffset) =
                    NpcCharacter.BuildPositionedOamTexture(
                        linkSource, linkGraphic.Oam,
                        tileBase: 0, basePalette: 5,
                        paletteOverride: null,
                        sourceGrayscaleInverted: true,
                        sourceOffset: linkGraphic.ByteOffset);
                if (damageLinkOffset != linkOffset)
                {
                    throw new InvalidOperationException(
                        $"ITEM_SHOOTER Link palette changed variant {variant}, " +
                        $"angle {angle} OAM bounds.");
                }
                linkTextures[variant, angle] = link;
                (poses[variant, angle], offsets[variant, angle]) =
                    ComposeSeedShooterPose(
                        link, linkOffset, weapon, weaponOffset);
                (Texture2D damagePose, Vector2 damageOffset) =
                    ComposeSeedShooterPose(
                        damageLink, damageLinkOffset, weapon, weaponOffset);
                damagePoses[variant, angle] = damagePose;
                if (damageOffset != offsets[variant, angle])
                {
                    throw new InvalidOperationException(
                        $"ITEM_SHOOTER damage palette changed variant " +
                        $"{variant}, angle {angle} bounds.");
                }
            }
        }
        return (linkTextures, weaponTextures, poses, damagePoses, offsets);
    }

    private static (Texture2D Texture, Vector2 Offset) ComposeSeedShooterPose(
        Texture2D link,
        Vector2 linkOffset,
        Texture2D weapon,
        Vector2 weaponOffset)
    {
        int minX = Mathf.FloorToInt(Mathf.Min(linkOffset.X, weaponOffset.X));
        int minY = Mathf.FloorToInt(Mathf.Min(linkOffset.Y, weaponOffset.Y));
        int maxX = Mathf.CeilToInt(Mathf.Max(
            linkOffset.X + link.GetWidth(), weaponOffset.X + weapon.GetWidth()));
        int maxY = Mathf.CeilToInt(Mathf.Max(
            linkOffset.Y + link.GetHeight(), weaponOffset.Y + weapon.GetHeight()));
        Image output = Image.CreateEmpty(
            maxX - minX, maxY - minY, false, Image.Format.Rgba8);
        output.Fill(Colors.Transparent);

        // queueDrawEverything enqueues priority-$01 items before Link, so the
        // shooter receives lower (winning) hardware OAM indices. Blend Link
        // first and the shooter second to preserve that per-pixel priority.
        BlendSeedShooterLayer(output, link.GetImage(), linkOffset, minX, minY);
        BlendSeedShooterLayer(output, weapon.GetImage(), weaponOffset, minX, minY);
        return (ImageTexture.CreateFromImage(output), new Vector2(minX, minY));
    }

    private static void BlendSeedShooterLayer(
        Image output,
        Image layer,
        Vector2 offset,
        int minX,
        int minY)
    {
        output.BlendRect(
            layer,
            new Rect2I(0, 0, layer.GetWidth(), layer.GetHeight()),
            new Vector2I(
                Mathf.RoundToInt(offset.X) - minX,
                Mathf.RoundToInt(offset.Y) - minY));
    }

    private Texture2D BuildUnderwaterAttackLinkTexture(bool damagePalette)
    {
        Image source = OracleGraphicsCache.LoadImage(
            "res://assets/oracle/gfx/spr_link.png");
        Image output = Image.CreateEmpty(48, 64, false, Image.Format.Rgba8);
        for (int facing = 0; facing < 4; facing++)
        for (int phase = 0; phase < 3; phase++)
        {
            LinkGraphicRecord record = _linkItems.Graphic(
                "underwater-attack", 0, phase, facing);
            WriteLinkFrame(
                output, source, phase * 16, facing * 16,
                record.ByteOffset, record.MirrorX, damagePalette);
        }
        return ImageTexture.CreateFromImage(output);
    }

    private Texture2D BuildShovelLinkTexture(bool damagePalette)
    {
        Image source = OracleGraphicsCache.LoadImage(
            "res://assets/oracle/gfx/spr_link.png");
        Image output = Image.CreateEmpty(32, 64, false, Image.Format.Rgba8);

        // LINK_ANIM_MODE_DIG_2 ($1a) selects $f8-$ff. The first and second
        // columns are the $f8-$fb and $fc-$ff phases respectively.
        for (int facing = 0; facing < 4; facing++)
        for (int phase = 0; phase < 2; phase++)
        {
            LinkGraphicRecord record =
                _linkItems.Graphic("shovel", 0, phase, facing);
            WriteLinkFrame(
                output, source, phase * 16, facing * 16,
                record.ByteOffset, record.MirrorX, damagePalette);
        }
        return ImageTexture.CreateFromImage(output);
    }

    private Texture2D BuildSwordTexture(bool chargedPalette)
    {
        Image source = OracleGraphicsCache.LoadImage(
            "res://assets/oracle/gfx/spr_swords.png");
        Image output = Image.CreateEmpty(8 * 32, 32, false, Image.Format.Rgba8);

        for (int animation = 0; animation < 8; animation++)
        foreach (SwordPart part in _linkItems.SwordOam(animation))
        {
            int sourceX = (part.Tile / 2) * 8;
            int destinationX = animation * 32 + part.X + 8;
            int destinationY = part.Y;
            for (int y = 0; y < 16; y++)
            for (int x = 0; x < 8; x++)
            {
                int readX = sourceX + (part.FlipX ? 7 - x : x);
                int readY = part.FlipY ? 15 - y : y;
                Color pixel = RecolorSwordPixel(source.GetPixel(readX, readY), chargedPalette);
                if (pixel.A > 0.0f)
                    output.SetPixel(destinationX + x, destinationY + y, pixel);
            }
        }
        return ImageTexture.CreateFromImage(output);
    }

    private static Texture2D BuildDrownTexture(bool damagePalette)
    {
        Image source = OracleGraphicsCache.LoadImage(
            "res://assets/oracle/gfx/spr_link.png");
        Image output = Image.CreateEmpty(32, 64, false, Image.Format.Rgba8);

        // LINK_ANIM_MODE_DROWN ($0a) uses directional graphics $d4-$d7 for
        // six updates. Their OAM records $10-$12 place both 8x16 cells at
        // y=$0c. The final sixteen updates use graphics $0b with OAM $12.
        WriteLinkFrame(output, source, 0, (int)Facing.Up * 16, 0x0e00, false, damagePalette);    // $d4, OAM $10
        WriteLinkFrame(output, source, 0, (int)Facing.Right * 16, 0x0ec0, true, damagePalette); // $d5, OAM $11
        WriteSymmetricLinkCell(output, source, 0, (int)Facing.Down * 16, 0x0e80, damagePalette); // $d6, OAM $12
        WriteLinkFrame(output, source, 0, (int)Facing.Left * 16, 0x0ec0, false, damagePalette); // $d7, OAM $10

        for (int facing = 0; facing < 4; facing++)
            WriteSymmetricLinkCell(
                output, source, 16, facing * 16, 0x0f40, damagePalette); // $0b, OAM $12

        return ImageTexture.CreateFromImage(output);
    }

    private Texture2D BuildTopDownSwimTexture(bool damagePalette)
    {
        Image source = OracleGraphicsCache.LoadImage(
            "res://assets/oracle/gfx/spr_link.png");
        Image output = Image.CreateEmpty(32, 64, false, Image.Format.Rgba8);

        for (int frame = 0; frame < 2; frame++)
        for (int direction = ObjectDirection.Up; direction < 4; direction++)
        {
            TopDownSwimmingFrame record =
                _topDownSwimmingData.Frame(frame, direction);
            if (record.OamIndex == 0x12)
            {
                WriteSymmetricLinkCell(
                    output,
                    source,
                    frame * 16,
                    direction * 16,
                    record.SourceOffset,
                    damagePalette);
            }
            else
            {
                WriteLinkFrame(
                    output,
                    source,
                    frame * 16,
                    direction * 16,
                    record.SourceOffset,
                    mirroredOam: record.OamIndex == 0x11,
                    damagePalette);
            }
        }
        return ImageTexture.CreateFromImage(output);
    }

    private Texture2D BuildTopDownDiveTexture(bool damagePalette)
    {
        Image source = OracleGraphicsCache.LoadImage(
            "res://assets/oracle/gfx/spr_link.png");
        Image output = Image.CreateEmpty(32, 16, false, Image.Format.Rgba8);

        for (int frame = 0; frame < 2; frame++)
        {
            TopDownDivingFrame record =
                _topDownSwimmingData.DiveFrame(frame);
            WriteSymmetricLinkCell(
                output,
                source,
                frame * 16,
                0,
                record.SourceOffset,
                damagePalette);
        }
        return ImageTexture.CreateFromImage(output);
    }

    private Texture2D BuildSideScrollSwimTexture(
        bool mermaidSuit,
        bool damagePalette)
    {
        Image source = OracleGraphicsCache.LoadImage(
            "res://assets/oracle/gfx/spr_link.png");
        Image output = Image.CreateEmpty(32, 64, false, Image.Format.Rgba8);

        for (int frame = 0; frame < 2; frame++)
        for (int direction = ObjectDirection.Up; direction < 4; direction++)
        {
            SideScrollSwimmingFrame record =
                _sideScrollPlayerData.SwimmingFrame(
                    mermaidSuit, frame, direction);
            WriteLinkFrame(
                output,
                source,
                frame * 16,
                direction * 16,
                record.SourceOffset,
                mirroredOam: record.OamIndex == 0x01,
                damagePalette);
        }
        return ImageTexture.CreateFromImage(output);
    }

    private static Texture2D BuildFallInHoleTexture(bool damagePalette)
    {
        Image source = OracleGraphicsCache.LoadImage(
            "res://assets/oracle/gfx/spr_link.png");
        Image output = Image.CreateEmpty(48, 16, false, Image.Format.Rgba8);

        // LINK_ANIM_MODE_FALLINHOLE (mode $0d) uses frames $08, $09,
        // and $0a. In Ages' specialObjectAnimationData.s these resolve to:
        //   $08: OAM $00, spr_link+$0100, 4 tiles, duration $10
        //   $09: OAM $06, spr_link+$0140, 2 tiles, duration $0a
        //   $0a: OAM $06, spr_link+$0160, 2 tiles, duration $0a
        WriteLinkFrame(output, source, 0, 0, 0x0100, false, damagePalette);
        WriteCenteredSingleLinkCell(
            output, source, 16, 0, 0x0140, damagePalette);
        WriteCenteredSingleLinkCell(
            output, source, 32, 0, 0x0160, damagePalette);

        return ImageTexture.CreateFromImage(output);
    }

    private static Texture2D BuildLedgeJumpTexture(bool damagePalette)
    {
        Image source = OracleGraphicsCache.LoadImage(
            "res://assets/oracle/gfx/spr_link.png");
        Image output = Image.CreateEmpty(64, 64, false, Image.Format.Rgba8);

        // LINK_ANIM_MODE_JUMP ($18) is animationData19f78:
        // 9 updates of $e4-$e7, 9 of $e8-$eb, 6 of $ec-$ef, then
        // terminal frame $80-$83. The entries retain their source OAM flips.
        WriteSymmetricLinkCell(
            output, source, 0, 0, 0x0c00, damagePalette);
        WriteLinkFrame(
            output, source, 0, 16, 0x0c60, true, damagePalette);
        WriteSymmetricLinkCell(
            output, source, 0, 32, 0x0c40, damagePalette, flipY: true);
        WriteLinkFrame(
            output, source, 0, 48, 0x0c60, false, damagePalette);

        WriteSymmetricLinkCell(
            output, source, 16, 0, 0x0c20, damagePalette);
        WriteLinkFrame(
            output, source, 16, 16, 0x0ca0, true, damagePalette);
        WriteSymmetricLinkCell(
            output, source, 16, 32, 0x0c00, damagePalette, flipY: true);
        WriteLinkFrame(
            output, source, 16, 48, 0x0ca0, false, damagePalette);

        WriteSymmetricLinkCell(
            output, source, 32, 0, 0x0c40, damagePalette);
        WriteLinkFrameWithFlips(
            output, source, 32, 16, 0x0c60,
            mirrorX: false, flipY: true, damagePalette: damagePalette);
        WriteSymmetricLinkCell(
            output, source, 32, 32, 0x0c20, damagePalette, flipY: true);
        WriteLinkFrameWithFlips(
            output, source, 32, 48, 0x0c60,
            mirrorX: true, flipY: true, damagePalette: damagePalette);

        WriteLinkFrame(
            output, source, 48, 0, 0x0000, true, damagePalette);
        WriteLinkFrame(
            output, source, 48, 16, 0x00c0, true, damagePalette);
        WriteLinkFrame(
            output, source, 48, 32, 0x0200, true, damagePalette);
        WriteLinkFrame(
            output, source, 48, 48, 0x00c0, false, damagePalette);

        return ImageTexture.CreateFromImage(output);
    }

    private static Texture2D BuildDeathTexture()
    {
        Image source = OracleGraphicsCache.LoadImage(
            "res://assets/oracle/gfx/spr_link.png");
        Image output = Image.CreateEmpty(80, 16, false, Image.Format.Rgba8);

        // LINK_ANIM_MODE_SPIN ($01) uses graphics $02,$01,$00,$03 and
        // LINK_ANIM_MODE_COLLAPSED ($02) uses frame $04.
        WriteLinkFrame(output, source, 0, 0, 0x0000, false, false); // $00
        WriteLinkFrame(output, source, 16, 0, 0x0080, true, false); // $01
        WriteLinkFrame(output, source, 32, 0, 0x0200, false, false); // $02
        WriteLinkFrame(output, source, 48, 0, 0x0080, false, false); // $03
        WriteSymmetricLinkCell(output, source, 64, 0, 0x03e0, false); // $04

        return ImageTexture.CreateFromImage(output);
    }

    private static void WriteWalkFrame(
        Image output,
        Image source,
        Facing facing,
        int frame,
        int byteOffset,
        bool mirroredOam,
        bool damagePalette)
    {
        // spr_link.png is interleaved as 8x16 cells (32 bytes each). OAM $00
        // draws cells 0/1 normally; OAM $01 swaps them and flips both on X.
        WriteLinkFrame(
            output, source, frame * 16, (int)facing * 16,
            byteOffset, mirroredOam, damagePalette);
    }

    private static void WriteLinkFrame(
        Image output,
        Image source,
        int destinationX,
        int destinationY,
        int byteOffset,
        bool mirroredOam,
        bool damagePalette)
    {
        WriteLinkFrameWithFlips(
            output,
            source,
            destinationX,
            destinationY,
            byteOffset,
            mirroredOam,
            flipY: false,
            damagePalette: damagePalette);
    }

    private static void WriteLinkFrameWithFlips(
        Image output,
        Image source,
        int destinationX,
        int destinationY,
        int byteOffset,
        bool mirrorX,
        bool flipY,
        bool damagePalette)
    {
        int firstCell = byteOffset / 32;

        for (int destinationPart = 0; destinationPart < 2; destinationPart++)
        {
            int sourcePart = mirrorX ? 1 - destinationPart : destinationPart;
            int cell = firstCell + sourcePart;
            int cellX = (cell % 16) * 8;
            int cellY = (cell / 16) * 16;

            for (int y = 0; y < 16; y++)
            for (int x = 0; x < 8; x++)
            {
                int sourceX = cellX + (mirrorX ? 7 - x : x);
                int sourceY = cellY + (flipY ? 15 - y : y);
                Color sourceColor = source.GetPixel(sourceX, sourceY);
                output.SetPixel(
                    destinationX + destinationPart * 8 + x,
                    destinationY + y,
                    RecolorLinkPixel(sourceColor, damagePalette));
            }
        }
    }

    private static void WriteCenteredSingleLinkCell(
        Image output,
        Image source,
        int destinationX,
        int destinationY,
        int byteOffset,
        bool damagePalette)
    {
        int cell = byteOffset / 32;
        int cellX = (cell % 16) * 8;
        int cellY = (cell / 16) * 16;

        for (int y = 0; y < 16; y++)
        for (int x = 0; x < 8; x++)
        {
            Color sourceColor = source.GetPixel(cellX + x, cellY + y);
            output.SetPixel(
                destinationX + 4 + x,
                destinationY + y,
                RecolorLinkPixel(sourceColor, damagePalette));
        }
    }

    private static void WriteSymmetricLinkCell(
        Image output,
        Image source,
        int destinationX,
        int destinationY,
        int byteOffset,
        bool damagePalette,
        bool flipY = false)
    {
        int cell = byteOffset / 32;
        int cellX = (cell % 16) * 8;
        int cellY = (cell / 16) * 16;

        for (int destinationPart = 0; destinationPart < 2; destinationPart++)
        for (int y = 0; y < 16; y++)
        for (int x = 0; x < 8; x++)
        {
            int sourceX = cellX + (destinationPart == 0 ? x : 7 - x);
            int sourceY = cellY + (flipY ? 15 - y : y);
            Color sourceColor = source.GetPixel(sourceX, sourceY);
            output.SetPixel(
                destinationX + destinationPart * 8 + x,
                destinationY + y,
                RecolorLinkPixel(sourceColor, damagePalette));
        }
    }

    internal static Color RecolorLinkPixel(
        Color source,
        bool damagePalette = false)
    {
        float value = source.R;
        if (damagePalette)
        {
            // updateLinkInvincibilityCounter replaces Link's OAM palette 0
            // with standardSpritePaletteData palette 5 while frame-counter
            // bit 2 is clear.
            return value < 0.1f ? Colors.Transparent
                : value < 0.5f ? GbcColor(0x1f, 0x16, 0x06)
                : value < 0.9f ? GbcColor(0x1b, 0x00, 0x00)
                : Colors.Black;
        }
        return value < 0.1f ? Colors.Transparent
            // specialObjectSetOamVariables gives Link OAM flags $08, selecting
            // standardSpritePaletteData palette 0. Color 0 is transparent.
            : value < 0.5f ? Colors.Black
            : value < 0.9f ? GbcColor(0x02, 0x15, 0x08)
            : GbcColor(0x1f, 0x1a, 0x11);
    }

    private static Color GbcColor(int red, int green, int blue) =>
        new(red / 31.0f, green / 31.0f, blue / 31.0f);

    private static Color RecolorSwordPixel(Color source, bool chargedPalette)
    {
        float value = source.R;
        if (chargedPalette)
        {
            return value < 0.1f ? Colors.Transparent
                : value < 0.5f ? GbcColor(0x1f, 0x16, 0x06)
                : value < 0.9f ? GbcColor(0x1b, 0x00, 0x00)
                : Colors.Black;
        }
        return value < 0.1f ? Colors.Transparent
            : value < 0.5f ? Colors.Black
            : value < 0.9f ? Color.Color8(16, 173, 66)
            : Color.Color8(255, 214, 140);
    }

}
