using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

// INTERAC $dc:$0f initializes only during warp loading ($02), below Y=$78.
internal sealed partial class RoomEntryTextRoomEntity(DungeonObjectRecord record, OracleRuntimeState runtime,
    Action<Player> showText) : Node2D, IRoomEntity, IFixedRoomEntity, IRoomEntityLifetime,
    IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze, IScreenTransitionPreloadRoomEntity
{
    private bool _initialized;
    public Node2D Node => this;
    public bool Finished { get; private set; }
    public bool UpdatesDuringDialogue => !_initialized;
    public bool UpdatesDuringRoomEntityFreeze => !_initialized;
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) => Dispatch(frame.Player);
    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns) =>
        throw new InvalidOperationException($"{record.Source}: entry-text initialization requires Link.");
    public ScreenTransitionPresentation PrepareForScreenTransition(Player? player, ICollection<RoomEntitySpawn> spawns)
    {
        if (player is null) throw new InvalidOperationException($"{record.Source}: entry-text initialization requires Link.");
        if (!_initialized) Dispatch(player);
        return ScreenTransitionPresentation.Hidden;
    }
    public void SetTransitionDrawOffset(Vector2 offset) { }
    private void Dispatch(Player player)
    {
        if (Finished) return;
        if (!_initialized)
        {
            _initialized = true;
            Finished = (runtime.ReadWramByte(WramAddress.wScrollMode) & 2) == 0 ||
                (unchecked((byte)(int)player.Position.Y)) < 0x78;
            return;
        }
        if (player.IsDying || !RoomEntityManager.ObjectCollisionZOverlaps(player.ObjectZHigh, 0, 7) ||
            !RoomEntityManager.ObjectCollisionXYOverlaps(new(record.Position - Vector2.One * 8, Vector2.One * 16),
                new(player.Position - Vector2.One * NpcCharacter.LinkCollisionRadius, Vector2.One * (NpcCharacter.LinkCollisionRadius * 2)))) return;
        showText(player); Finished = true;
    }
}
