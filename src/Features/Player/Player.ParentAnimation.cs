using Godot;
using System;

namespace Oracles;

public partial class Player
{
    private void DrawPublishedParentBody()
    {
        if (_publishedParentSprite is { } sprite)
        {
            sprite.Renderer.DrawRelativeFrame(this, sprite.Frame, 0);
            return;
        }
        var body = PublishedParentBody ?? throw new InvalidOperationException("Parent body drawing requires its completed graphics publication.");
        if (body.Region is { } region)
            DrawTextureRectRegion(body.Texture, new Rect2(body.Offset, region.Size), region);
        else DrawTexture(body.Texture, body.Offset);
    }

    private (Texture2D Texture, Rect2? Region, Vector2 Offset)? SelectPublishedParentBody()
    {
        Rect2 region = new(0, (int)_facing * 16, 16, 16);
        Vector2 offset = NormalSpriteOrigin;
        switch (_publishedParentId)
        {
            case TreasureId.Bracelet:
            case TreasureId.Bombs:
            {
                int graphic = _inventory.RuntimeState.ReadWramByte(0xd231);
                // LIFT_4's final $5c frame is the first carried WALK cell.
                if (graphic == 0x5c)
                    return (DamagePaletteActive ? Sprites.DamageCarriedObjectTexture : Sprites.CarriedObjectTexture, region, offset);
                for (int pose = 0; pose < 3; pose++)
                    if (_linkItems.Graphic("bracelet", pose, 0, 0).GraphicsIndex == graphic)
                        return ((DamagePaletteActive ? Sprites.DamageBraceletActionTextures : Sprites.BraceletActionTextures)[pose, (int)_facing],
                            null, new Vector2(-16, -16));
                throw new NotSupportedException($"func_4553 ITEM${_publishedParentId:x2}: unsupported lift/throw graphic${graphic:x2}.");
            }
            case TreasureId.Harp when IsUsingHarp:
            case TreasureId.Flute when IsUsingHarp:
                _publishedParentSprite = (_harpRenderer ?? throw new InvalidOperationException("An active instrument requires its sprite renderer."),
                    (_harpFrames ?? throw new InvalidOperationException("An active instrument requires imported frames."))[_harpFrame]);
                return null;
            case TreasureId.Shooter when IsUsingSeedShooter:
            {
                int angle = CheckedSeedShooterAngle(_world.SeedShooterAngle);
                int variant = _world.Underwater ? 2 : _minecartRideControlled ? 1 : 0;
                return (DamagePaletteActive ? Sprites.DamageSeedShooterPoseTextures[variant, angle] : Sprites.SeedShooterPoseTextures[variant, angle],
                    null, Sprites.SeedShooterPoseOffsets[variant, angle]);
            }
            case TreasureId.BiggoronSword when IsUsingBiggoron:
            {
                var body = _world.BiggoronLinkBody(this, DamagePaletteActive) ??
                    throw new NotSupportedException("func_4553 ITEM$0c: selected Biggoron body has no bound presenter.");
                return (body.Texture, null, body.Offset);
            }
            case TreasureId.CaneOfSomaria when IsUsingSomaria:
            {
                int mode = _world.SomariaAnimationMode, frame = _world.SomariaAnimationFrame;
                int phase = mode == 0x26 ? frame : frame < 3 ? frame : 1;
                Texture2D texture = mode == 0x26 ? DamagePaletteActive ? Sprites.DamageMinecartAttackTexture : Sprites.MinecartAttackTexture :
                    mode == 0x2d ? DamagePaletteActive ? Sprites.DamageUnderwaterAttackTexture : Sprites.UnderwaterAttackTexture :
                    DamagePaletteActive ? Sprites.DamageAttackTexture : Sprites.AttackTexture;
                offset += (mode != 0x26 && frame == 2 ? _linkItems.AttackPoseOffset((int)_facing) : Vector2.Zero) + Vector2.Down * EnemyContactZ;
                region.Position += new Vector2(phase * 16, 0);
                return (texture, region, offset);
            }
            case TreasureId.Punch when IsUsingPunch:
                offset += _expertPunch && _punchFrame is >= 3 and < 11 ? _linkItems.AttackPoseOffset((int)_facing) : Vector2.Zero;
                region.Position += new Vector2(16, 0);
                return (DamagePaletteActive ? Sprites.DamageAttackTexture : Sprites.AttackTexture, region, offset);
            case TreasureId.Shovel when IsUsingShovel:
                region.Position += new Vector2(_shovelFrame < _linkItems.Constants.ShovelSecondPoseFrame ? 0 : 16, 0);
                return (DamagePaletteActive ? Sprites.DamageShovelLinkTexture : Sprites.ShovelLinkTexture, region, offset);
            case TreasureId.Sword when _forcedSwordParentInitializing:
                return (DamagePaletteActive ? Sprites.DamageAttackTexture : Sprites.AttackTexture, region, offset);
            case TreasureId.Sword when IsAttacking:
            {
                int phase = GetSwordPosePhase();
                if (_minecartRideControlled && _swordState == SwordActionState.Swing)
                {
                    region.Position += new Vector2(phase * 16, 0);
                    return (DamagePaletteActive ? Sprites.DamageMinecartAttackTexture : Sprites.MinecartAttackTexture, region, offset + SwordDrawOffset);
                }
                int column = _swordState == SwordActionState.Spin || phase == 3 ? 1 : phase;
                region.Position = new(column * 16, (int)GetSwordPoseFacing() * 16);
                return (SwordUsesUnderwaterAnimation ? DamagePaletteActive ? Sprites.DamageUnderwaterAttackTexture : Sprites.UnderwaterAttackTexture :
                    DamagePaletteActive ? Sprites.DamageAttackTexture : Sprites.AttackTexture, region, AttackSpriteOrigin + SwordDrawOffset);
            }
            case TreasureId.Boomerang when IsUsingBoomerang:
            case TreasureId.Bombchus when IsUsingBombchu:
            {
                int graphic = _publishedParentId == TreasureId.Boomerang ? _world.BoomerangParentGraphic : _world.BombchuParentGraphic;
                bool mounted = graphic is 0xcc or 0x58;
                region.Position += new Vector2((graphic == 0x58 ? 3 : 1) * 16, 0);
                return (mounted ? DamagePaletteActive ? Sprites.DamageMinecartAttackTexture : Sprites.MinecartAttackTexture :
                    DamagePaletteActive ? Sprites.DamageAttackTexture : Sprites.AttackTexture, region, offset + Vector2.Down * ObjectZHigh);
            }
            case TreasureId.SeedSatchel when IsUsingSeedSatchel:
            case TreasureId.SwitchHook when IsUsingSwitchHook:
            {
                bool hook = _publishedParentId == TreasureId.SwitchHook;
                region.Position += new Vector2(16, 0);
                return (hook && _world.Underwater ? DamagePaletteActive ? Sprites.DamageUnderwaterAttackTexture : Sprites.UnderwaterAttackTexture :
                    DamagePaletteActive ? Sprites.DamageAttackTexture : Sprites.AttackTexture,
                    region, offset + (hook ? Vector2.Down * (_topDownAirZFixed >> 8) : Vector2.Zero));
            }
            default:
                throw new NotSupportedException($"func_4553 parent ${_publishedParentPage:x2}, ITEM${_publishedParentId:x2}: selected animation has no bound body presenter.");
        }
    }
}
