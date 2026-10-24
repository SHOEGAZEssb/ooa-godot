using Godot;

namespace oracleofages;

internal sealed partial class TwinrovaCharacter
{
    private bool Rise()
    {
        int z = (ZFixed >> 8) & 255;
        if (z >= 0xfe) { HighZ(z - 1); return false; }
        Flags |= 0x18; return true;
    }
    private void Merge(RoomEntityFrame frame)
    {
        switch (Substate)
        {
            case 0:
                Substate = 1; Flags &= ~3; Direction = 0; _speedZ = -0x3e0;
                if (Subid == 0) Text(0x2f0a); return;
            case 1:
                int z = ZFixed;
                OracleObjectMath.UpdateSpeedZ(ref z, ref _speedZ, 8); ZFixed = z;
                int cameraY = World.CameraY(Position);
                int y = (int)Position.Y;
                int screenY = y >= cameraY ? y - cameraY : y;
                int highZ = (ZFixed >> 8) & 255;
                if (highZ >= 128 && ((highZ + screenY) & 255) < 0xf0) { AdvanceAnimation(); return; }
                Substate = 2; Flags = (Flags | 2) & ~128; Counter = 60; Visible = false; return;
            case 2:
                if (Subid != 0 || (Twin.Flags & 2) == 0) return;
                Twin.Substate++; Substate++; return;
            case 3:
                if (Dec() != 0) return;
                Counter = 48; Substate = 4; HighZ(0xa0); Angle = Subid == 0 ? 8 : 24;
                Flags |= 128; Visible = true; ZIndex = ObjectDrawPriority.FromVisible(0xc2);
                World.Entities.OnSoundRequested(SoundId.SndWind); goto case 4;
            case 4:
                var offset = MovementCircleArcOffset(Counter, Angle);
                HighY(0x58 + offset.Y); HighX(0x78 + offset.X); MovingAnimation((Angle + 8) & 31);
                HighZ(((ZFixed >> 8) + 1) & 255);
                if (((ZFixed >> 8) & 1) == 0)
                {
                    Angle = (Angle + 1) & 31;
                    if (Dec() == 0) Substate = 5;
                }
                AdvanceAnimation(); return;
            case 5:
                if (Subid != 0) { DeleteNative(); return; }
                if (frame.Player.IsDying) return;
                World.Entities.RuntimeState.SetWramByte(WramAddress.wDisabledObjects, 1);
                World.Entities.RuntimeState.SetWramByte(0xcbca, 1); // wDisableLinkCollisionsAndMenu.
                Substate = 6; _palette = 0; RestartAnimation(12); World.Entities.OnSoundRequested(SoundId.SndTransform);
                World.Fade(false, 2); return;
            case 6:
                if (World.Entities.PaletteFadeActiveSource()) return;
                Substate = 7; Flags &= ~1;
                World.Entities.RuntimeState.SetWramByte(WramAddress.wLoadedTreeGfxIndex, 1); return;
            case 7:
                World.SpawnMerged(this); return;
            default: throw Unsupported();
        }
    }
    internal void DeleteNative()
    {
        // enemyDelete clears the slot without decNumEnemies. In a lower-slot
        // role swap the disappearing subid1 can be the counted placement.
        byte count = (byte)World.Entities.RoomEnemyCount;
        Finish(); World.Entities.SetNativeRoomEnemyCount(count);
    }
}
