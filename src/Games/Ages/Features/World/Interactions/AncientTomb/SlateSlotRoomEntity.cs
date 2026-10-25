using Godot;
using System;
using System.Collections.Generic;

namespace Oracles;

// slateSlot.s and scriptHelper.s:slateSlot_placeSlate. Placement, inventory,
// torch writes and input release execute together through the native script.
internal sealed partial class SlateSlotRoomEntity(DungeonObjectRecord record,
    AncientTombDatabase data, OracleSaveData save, InventoryState inventory, OracleRuntimeState runtime,
    Action<byte, byte> tile, Action<int> sound, Action showText)
    : Node2D, IRoomEntity, IFixedRoomEntity, IRoomEntityLifetime, IPlayerInteractable,
    IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze
{
    private int _genericStep;
    private bool _collisionsInitialized;
    internal int State { get; private set; }
    internal int PushCounter { get; private set; }
    public bool Finished { get; private set; }
    public Node2D Node => this;
    public bool UpdatesDuringDialogue => State == 0;
    public bool UpdatesDuringRoomEntityFreeze => State == 0;
    public void SetTransitionDrawOffset(Vector2 offset) { }
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        Position = record.Position; Visible = false;
        if (Finished) return;
        if (State == 0)
        {
            if (save.HasRoomFlag(record.Group, record.Room, (byte)(1 << record.SubId))) Finished = true;
            else State = 1;
            return;
        }
        if (State == 1)
        {
            var player = frame.Player;
            bool touching = _collisionsInitialized && !player.IsDying &&
                RoomEntityManager.ObjectCollisionZOverlaps(player.EnemyContactZ, 0, 7) &&
                RoomEntityManager.ObjectCollisionXYOverlaps(new Rect2(record.Position - new Vector2(6, 6), new(12, 12)),
                    new Rect2(player.EnemyContactPosition - new Vector2(6, 6), new(12, 12)));
            var delta = player.Position.Floor() - record.Position;
            if (!touching) PushCounter = 10;
            if (!player.IsAttemptingCenteredObjectPush || !(Math.Abs(delta.X) <= 4 || Math.Abs(delta.Y) <= 4))
                PushCounter = 10;
            PushCounter = (PushCounter - 1) & 255;
            if (PushCounter != 0)
            {
                // rungenericnpc yields; initcollisions runs next dispatch.
                if (_genericStep++ != 0) _collisionsInitialized = true;
                return;
            }
            if (inventory.Slates == 0) { showText(); PushCounter = 10; return; }
            if (!player.NativePuzzleResetVulnerable) { PushCounter = 10; return; }
            runtime.SetWramByte(AgesWramAddress.wDisabledObjects, 0x81);
            runtime.SetWramByte(AgesWramAddress.wMenuDisabled, 0x81);
            State = 2;
        }
        sound(SoundId.SndDoorClose);
        tile((byte)(((int)record.Position.Y & 0xf0) | ((int)record.Position.X >> 4)), (byte)(0xd4 + record.SubId));
        save.SetRoomFlag(record.Group, record.Room, (byte)(1 << record.SubId));
        if (!inventory.TryConsumeSlate()) throw new InvalidOperationException("slateSlot_placeSlate lost its reserved slate.");
        var pair = data.Torches(record.SubId);
        tile(pair.First, 9); tile(pair.Second, 9);
        // asm15 returns carry set: enableinput and scriptend execute
        // in this same interaction dispatch.
        runtime.SetWramByte(AgesWramAddress.wDisabledObjects, 0);
        runtime.SetWramByte(AgesWramAddress.wMenuDisabled, 0);
        Finished = true;
    }
    public bool TryInteract(Player player)
    {
        if (Finished || State != 1 || !_collisionsInitialized || !player.NativeInteractionCollisionsEnabled) return false;
        Vector2 delta = record.Position - player.Position.Floor();
        if (Math.Abs(delta.X) > 14 || Math.Abs(delta.Y) > 14 || delta.Dot(player.FacingVector) <= 0) return false;
        showText(); return true;
    }
}
