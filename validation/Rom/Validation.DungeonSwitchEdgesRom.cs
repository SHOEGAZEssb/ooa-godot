using Godot;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

public partial class ValidationRoot
{
    private void CompareDungeonSwitchEdgesRom()
    {
        ReinitializeGameplayForValidation(); LoadValidationRoom(4,0x89); _entities.Clear();
        _runtimeState.SetWramByte(OracleRuntimeState.SwitchStateAddress,0);
        var data = new DungeonMechanicDatabase();
        var source = data.GetRoomRecords(4,0x89).Single(row => row.Id == 5);
        var rom = new SomariaRom(_saveData,_random.CaptureState(),_currentRoom,0,120,120);
        var native = SomariaPrivate<FrontendRom>(rom,"_rom");
        var seed = new SeedSatchelDatabase().Ember;
        var spawns = new List<RoomEntitySpawn>();
        // Isolate the source collision predicate at declared initialized
        // PART/ITEM state. Real item contact, delayed toggle, rail update and
        // reachable repetition use the full gameplay comparison below.
        foreach (int collision in new[] { 0x04,0x0d,0x19,0x1b })
        foreach (bool horizontal in new[] { false,true })
        foreach (int distance in new[] { -11,-10,9,10 })
        foreach (float fraction in new[] { 0f,0.5f })
        {
            var part = new DungeonSwitchRoomEntity(source,_currentRoom,data,_runtimeState,
                () => 0,() => { },_ => { },_rooms.TrySetTile,_saveData);
            try
            {
                part.UpdateFrame(new RoomEntityFrame(_player,0,false),spawns);
                Vector2 other = part.Position+(horizontal ? new Vector2(distance+fraction,0) : new Vector2(0,distance+fraction));
                Rect2 hitbox = new(other-new Vector2(6,6),new Vector2(12,12));
                for (int offset = 0; offset < 64; offset++) rom[0xd0c0+offset] = rom[0xd600+offset] = 0;
                rom[0xd0c0] = 1; rom[0xd0c1] = 5; rom[0xd0c2] = 4; rom[0xd0c4] = 1;
                rom[0xd0cb] = (byte)part.Position.Y; rom[0xd0cd] = (byte)part.Position.X;
                rom[0xd0cf] = 0xfa; rom[0xd0e4] = 0x85; rom[0xd0e5] = 0x83;
                rom[0xd0e6] = rom[0xd0e7] = 4;
                rom[0xd0e9] = 0x40; rom[0xd0f0] = 0x62;
                rom[0xd600] = 1; rom[0xd601] = collision == 0x1b ? (byte)0x20 : (byte)1;
                rom.Word(0xd60a,(int)(other.Y*256)); rom.Word(0xd60c,(int)(other.X*256));
                rom[0xd60f] = 0xfa; rom[0xd624] = (byte)(0x80|collision);
                rom[0xd626] = rom[0xd627] = 6; rom[0xd628] = 0xff;
                native.Call(0x41d1,7); // checkEnemyAndPartCollisions, original item mask/XYZ/effect dispatch.
                if (collision == 0x04) part.ApplySwordHit(hitbox,other,1,EnemyKnockbackStrength.Normal,spawns);
                else if (collision == 0x1b) part.ApplySeedCollision(hitbox,other,seed,collision,spawns);
                else part.ApplyItemCollision(collision == 0x19 ? RoomEntityItemCollision.SwordBeam : RoomEntityItemCollision.ThrownObject,
                    hitbox,other,1,spawns);
                bool expected = distance >= -10 && distance < 10;
                bool contact = (rom[0xd0ea]&0x80) != 0;
                FailIf(contact != expected || SomariaPrivate<bool>(part,"_pendingHit") != contact ||
                    part.HitLockout != -unchecked((sbyte)rom[0xd0eb]) || spawns.Count != 0,
                    $"PART$05 collision${collision:x2}, {(horizontal ? "X" : "Y")} offset{distance}+{fraction}: native byte edges require contact={expected}, runtime={SomariaPrivate<bool>(part,"_pendingHit")}, ROM={contact}.");
            }
            finally { part.Free(); }
        }
    }
}
