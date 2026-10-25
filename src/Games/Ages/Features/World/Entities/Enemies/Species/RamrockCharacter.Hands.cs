using Godot;
using System;

namespace Oracles;

internal sealed partial class RamrockCharacter
{
    private int _handHit, _handRetiring, _shieldOffset;
    private RamrockCharacter Parent() => _world.ResolveEnemy(ParentSlot) ??
        throw new NotSupportedException($"ramrockArms.s: ENEMY$05:${Subid:x2} references deleted/reused enemy slot${ParentSlot:x2}.");
    private Vector2 RelativePosition()
    {
        var parent = Parent(); return new((byte)((int)parent.Position.X + ((Subid & 1) == 0 ? 14 : -14)),
            (byte)((int)parent.Position.Y + 8));
    }
    private void InitializeHand()
    {
        if (Subid < 2)
        {
            CopyHigh(new(_data.HandX(Subid),16)); ZFixed = -7 << 8; Angle = 16; Counter1 = 8;
            SetAnimation(Subid); Speed = 0x3c;
        }
        else if (Subid < 4)
        {
            SetAnimation(Subid + 2); CopyHigh(RelativePosition()); ZFixed = -127 << 8;
            // setRelativePosition leaves A equal to the copied X high byte.
            Speed = (int)Position.X;
        }
        else if (Subid < 6)
        {
            CopyHigh(new(_data.HandX(Subid),0x4e)); Angle = _data.HandAngle(Subid);
            ZFixed = -127 << 8; _shieldOffset = 4; SetAnimation(Subid + 2);
            // enemySetAnimation restores the active bank in A ($10).
            Speed = 0x10;
        }
        else throw new NotSupportedException($"ramrockArms.s: subid${Subid:x2}.");
        Show(0xc0);
    }
    private bool Near(Vector2 target, int margin) =>
        ((int)Position.Y - (int)target.Y + margin & 255) < margin * 2 + 1 &&
        ((int)Position.Y - (int)target.Y + margin & 255) != 0 &&
        ((int)Position.X - (int)target.X + margin & 255) < margin * 2 + 1 &&
        ((int)Position.X - (int)target.X + margin & 255) != 0;
    private void ReturnHand()
    { Substate = 4; Speed = 0x3c; Angle = OracleObjectMovement.Shared.RelativeAngle(Position, RelativePosition()); }
    private bool TouchingWall() => EnemyAdjacentWallResolver.Shared.Probe(Position, Angle,
        point => point.X < 0 || point.Y < 0 || point.X >= _world.Room.Width || point.Y >= _world.Room.Height ||
            _world.Room.IsSolidForEnemyMovement(point, false)).Bitset != 0;
    private void UpdateHand(Vector2 link)
    {
        if (Subid < 2) UpdateFist(link);
        else if (Subid < 4) UpdateBombHand();
        else if (Subid < 6) UpdateShieldHand();
        else throw new NotSupportedException($"ramrockArms.s: live subid${Subid:x2}.");
    }
    private void UpdateFist(Vector2 link)
    {
        var parent = Parent();
        if (parent.Subid == 4 && _handRetiring == 0) { _handRetiring = 1; Substate = 6; Counter1 = 60; }
        switch (Substate)
        {
            case 0:
                AdvanceAnimation(); Move(); if (!Dec1()) return;
                Counter1 = 8; Angle = (Angle + (Subid == 0 ? -1 : Subid)) & 31;
                if ((Angle & 15) != 8) return;
                Substate++; parent.Subid++; SetAnimation(Subid + 2); return;
            case 1:
                AdvanceAnimation(); CopyHigh(RelativePosition()); if (parent.Subid == 3) ReturnHand(); return;
            case 2:
                AdvanceAnimation(); CopyHigh(RelativePosition()); if (!Dec2()) return;
                if (Math.Abs((int)link.X - (int)Position.X) > 4 && Math.Abs((int)link.Y - (int)Position.Y) > 4) return;
                if (OracleObjectMovement.Shared.RelativeAngle(Position, link) != 16) return;
                Substate++; Angle = 16; Counter1 = 6; _handHit = 0; Speed = 0x28;
                SetAnimation(Subid); _world.Sound(SoundId.SndBigSword); return;
            case 3:
                Move();
                if (_collisionSignal == ItemCollisionType.Link) { ReturnHand(); return; }
                if (_collisionSignal is >= ItemCollisionType.L1Sword and <= ItemCollisionType.L3Sword)
                { Substate = 5; Speed = 0x50; Angle ^= 16; return; }
                if (TouchingWall()) { ReturnHand(); return; }
                if (Dec1()) { Counter1 = 6; Nudge(link); } return;
            case 4:
                Move(); Angle = OracleObjectMovement.Shared.RelativeAngle(Position, RelativePosition());
                if (!Near(RelativePosition(), 2)) return;
                _world.Sound(SoundId.SndBombLand); Substate = 2; Counter2 = 60; SetAnimation(Subid + 2); return;
            case 5:
                AdvanceAnimation();
                if (_handHit == 0 && RoomEntityManager.ObjectCollisionXYOverlaps(CollisionBounds, parent.CollisionBounds))
                { _handHit = 1; parent.ApplyPhaseHit(); }
                if (!TouchingWall()) { Move(); return; }
                if (AnimationParameter != 0) ReturnHand(); return;
            case 6:
                if (parent.Subid != Subid + 4 || !Dec1()) return;
                _world.Puff(Position, ZFixed >> 8); parent.Subid++; Finish(); return;
            default: throw new NotSupportedException($"ramrockArms.s: fist substate${Substate:x2}.");
        }
    }
    private void UpdateBombHand()
    {
        var parent = Parent();
        switch (Substate)
        {
            case 0:
                if (!UpdateZ(0x10)) return;
                parent.Subid = 7; _world.Sound(SoundId.SndScentSeed); Substate++; return;
            case 1:
                if (parent.Subid != 8) return;
                if ((Subid & 1) != 0) { Finish(); return; }
                Show(NativeVisible & 0x7f); Substate++; return;
            case 2:
                CopyHigh(new(parent.Position.X, (byte)((int)parent.Position.Y + 8))); _collision = false;
                if (parent.Subid == 10)
                {
                    Subid = 1; CopyHigh(RelativePosition()); _world.Puff(Position, ZFixed >> 8);
                    Subid = 0; CopyHigh(RelativePosition()); _world.Puff(Position, ZFixed >> 8);
                    Subid = 2; Finish(); return;
                }
                if (parent.Subid != 9) return; _collision = true;
                // findItemWithID reads the first native item slot only.
                var bombs = _world.Bombs(); if (bombs.Count == 0) return;
                var bomb = bombs[0];
                if (!Near(bomb.Position.Floor(), 6)) return;
                int z = bomb.ZFixed >> 8; if (z != 0 && (z & 255) < 0xfc) return;
                if (!bomb.ConsumeByBoss()) return; parent.ApplyPhaseHit(); return;
            default: throw new NotSupportedException($"ramrockArms.s: bomb hand substate${Substate:x2}.");
        }
    }
    private void UpdateShieldHand()
    {
        var parent = Parent();
        switch (Substate)
        {
            case 0:
                if (!UpdateZ(0x10)) return;
                SetNativeCollisionRadii(6,6); _speedZ = -0x80; Speed = 0x28; Counter2 = 62; Substate++; return;
            case 1:
                if ((ZFixed >> 8) != -7) { UpdateZ(0); return; }
                if (!Dec2()) { Move(); return; }
                Substate++; if ((Subid & 1) != 0) { parent.Subid = 12; _world.Palette(0x84); } return;
            case 2:
                if (parent.Substate != 1)
                {
                    if (_collisionSignal >= 0 || parent.Subid == 13)
                    { parent.Subid = 13; parent.ShieldSignal = 16; Substate++; return; }
                    if (parent.Subid == 16) { _world.Puff(Position, ZFixed >> 8); Finish(); return; }
                }
                if (_shieldOffset != 12) _shieldOffset++;
                CopyShieldX(parent, _shieldOffset); return;
            case 3:
                if (_collisionSignal >= 0) parent.ShieldSignal = 16;
                int offset = _shieldOffset - 2;
                if (offset >= 4) { _shieldOffset = offset; CopyShieldX(parent, offset); return; }
                if (parent.Subid != 13) Substate = 2;
                CopyShieldX(parent, 4); return;
            default: throw new NotSupportedException($"ramrockArms.s: shield hand substate${Substate:x2}.");
        }
    }
    private void CopyShieldX(RamrockCharacter parent, int offset) =>
        CopyHigh(new((byte)((int)parent.Position.X + ((Subid & 1) != 0 ? offset : -offset)), Position.Y));
}
