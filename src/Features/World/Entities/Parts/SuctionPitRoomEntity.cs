using Godot;
using System.Collections.Generic;

namespace Oracles;

// seaEffects.s PART_SEA_EFFECTS $2e. A checked room-initialization
// allocation precedes placed PARTs. It stays allocated even after pits change.
internal sealed partial class SuctionPitRoomEntity : TransitionOffsetNode2D,
    IRoomEntity, IFixedRoomEntity, IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze,
    IScreenTransitionPreloadRoomEntity
{
    private readonly RoomEntityManager _owner;
    private readonly SuctionPitDatabase _data = SuctionPitDatabase.Shared;
    internal int State { get; private set; }
    internal int Counter { get; private set; }
    internal SuctionPitRoomEntity(RoomEntityManager owner)
    {
        _owner = owner;
        Name = "SeaEffects";
        Visible = false;
    }
    public Node2D Node => this;
    void IRoomEntity.SetTransitionDrawOffset(Vector2 offset) => SetTransitionDrawOffset(offset);
    public bool UpdatesDuringDialogue => State == 0;
    public bool UpdatesDuringRoomEntityFreeze => State == 0;
    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    {
        // updateParts admits state zero while scrolling; initialization does
        // not touch Link or the shared transition signal.
        if (State == 0) { State = 1; Counter = 0x20; }
        return ScreenTransitionPresentation.Hidden;
    }
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (State == 0) { State = 1; Counter = 0x20; return; }
        Player player = frame.Player;
        if (!player.NativeNormalStateForInteraction) return;
        _owner.RuntimeState.SetWramByte(AgesWramAddress.wDisableScreenTransitions, 0);
        if (!player.NativeInteractionCollisionsEnabled && !player.TopDownDiving) return;
        OracleRoomData room = _owner.ActiveRoom;
        Vector2 contactPosition = player.EnemyContactPosition;
        int y = OracleObjectPosition.HighByte(contactPosition.Y);
        int x = OracleObjectPosition.HighByte(contactPosition.X);
        int linkY = y, linkX = x;
        // The offsets are cumulative and visited in reverse physical order.
        // Do not replace them with independent probes around the foot tile.
        int probeEnd = player.RaftRideActive ? 10 : 5;
        for (int index = probeEnd - 1; index >= probeEnd - 5; index--)
        {
            y = unchecked((byte)(y + _data.Probes[index].Y));
            x = unchecked((byte)(x + _data.Probes[index].X));
            int packed = (y & 0xf0) | (x >> 4);
            if (y >= room.Height || x >= room.Width) continue;
            byte tile = room.GetPackedStorageMetatile((byte)packed);
            if (!_data.TryGetEffect(room.ActiveCollisions, tile, out int effect)) continue;
            var center = new Vector2((packed & 15) * 16 + 8, (packed >> 4) * 16 + 8);
            if (player.CompanionRideActive || player.RaftRideActive || player.MinecartRideActive)
                throw new System.NotSupportedException(
                    $"seaEffects.s:PART$2e room${room.Group:x1}:${room.Id:x2}, tile${tile:x2}: mounted special-object damage/movement requires its original owner.");
            if (effect == 0)
            {
                player.ApplyPollutionContact(center);
                return;
            }
            // @collision1 ignores dry, unmounted Link on surface whirlpools.
            if (effect == 1 && !player.TopDownSwimming)
                return;
            if (unchecked((byte)(linkY - (int)center.Y + 1)) < 3 &&
                unchecked((byte)(linkX - (int)center.X + 1)) < 3)
            {
                player.RequestSuctionPitFall(center);
                return;
            }
            _owner.RuntimeState.SetWramByte(AgesWramAddress.wDisableScreenTransitions, 0xff);
            int angle = OracleObjectMovement.Shared.RelativeAngle(new(linkX, linkY), center);
            if (Counter != 0) Counter--;
            int speed = _data.Speeds[(Counter & 0x1c) >> 2];
            if (speed >= 0x19) player.StopSuctionPitMovementSpeed();
            player.AdvanceInteractionVelocity(speed, angle);
            return;
        }
        if ((room.TilesetFlags & 0x01) != 0 &&
            _data.TryGetCurrent(room.ActiveCollisions, room.GetPackedStorageMetatile(
                (byte)((linkY & 0xf0) | (linkX >> 4))), out int currentAngle))
        {
            if (player.CompanionRideActive || player.RaftRideActive || player.MinecartRideActive)
                throw new System.NotSupportedException(
                    $"seaEffects.s:PART$2e room${room.Group:x1}:${room.Id:x2}: mounted current displacement requires its original special-object owner.");
            player.AdvanceInteractionVelocity(0x3c, currentAngle);
        }
        Counter = 0x20;
    }
}
