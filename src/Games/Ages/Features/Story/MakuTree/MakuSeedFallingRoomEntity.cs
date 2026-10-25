using Godot;
using System;
using System.Collections.Generic;

namespace Oracles;

// INTERAC_MAKU_SEED $a6: fixed $0080 descent, then cfc0=$01.
internal sealed class MakuSeedFallingRoomEntity(RoomEventContext context, MakuSeedDatabase data, Action<IRoomEntity> spawn)
    : IRoomEntity, IFixedRoomEntity, IRoomEntityLifetime, IUpdatesDuringDialogueRoomEntity
{
    private readonly NpcCharacter _actor = data.CreateActor(0xa6, 0, Vector2.Zero);
    private bool _initialized;
    private int _birthFrame;
    internal int ZFixed { get; private set; }
    public Node2D Node => _actor;
    public bool Finished { get; private set; }
    public bool UpdatesDuringDialogue => !_initialized;

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (!_initialized)
        {
            _initialized = true; _birthFrame = frame.Counter;
            _actor.SetStatePosition(context.Player.Position.Floor());
            _actor.SetScriptPaletteOverrides(new Dictionary<int, Color[]> { [7] = data.SeedPalette });
            _actor.SetFixedDrawPriority(ObjectDrawPriority.BehindLinkZIndex);
            ZFixed = -0x7500;
            if (context.Entities.InteractionSlotAvailable)
                spawn(new MakuSeedSparkleRoomEntity(context, data, this));
        }
        if (((ZFixed >> 8) & 0xff) >= 0xf3)
        {
            context.Entities.RuntimeState.SetWramByte(0xcfc0, 1);
            Finished = true; _actor.SetActive(false); return;
        }
        ZFixed += 0x80;
        _actor.SetScriptDrawOffset(new(0, ZFixed >> 8));
        if (((frame.Counter - _birthFrame) & 0x3f) == 0)
            context.Sound.PlaySound(SoundId.SndMagicPowder);
    }
    public void SetTransitionDrawOffset(Vector2 offset) => _actor.SetTransitionDrawOffset(offset);
}
