using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// INTERAC_GREAT_FAIRY $d5:$00. The Temple Secret fairy is marked solid and
/// hidden during room setup, appears after the source puff/wait sequence, then
/// uses the original eight-step signed Z-height cycle.
/// </summary>
internal sealed class GreatFairyRoomEntity
    : NpcCharacterRoomEntityAdapter,
        IFixedRoomEntity,
        IAlwaysUpdateDuringScreenTransitionRoomEntity,
        IUpdatesDuringDialogueRoomEntity,
        IRoomBlocker,
        ITalkTarget,
        IOrdinaryNpcEntity
{
    private const int AppearanceWait = 32;
    private const int InitialZ = -16;

    private static readonly int[] ZDeltas =
        [-1, -2, -1, 0, 1, 2, 1, 0];

    private readonly Action<int> _soundRequested;
    private GreatFairyAppearanceState _appearanceState;
    private int _appearanceCounter;
    private int _z = InitialZ;

    public NpcCharacter Npc => Entity;

    internal GreatFairyRoomEntity(
        NpcCharacter npc,
        Action<int> soundRequested)
        : base(RequireGreatFairy(npc), npc.SetTransitionDrawOffset)
    {
        _soundRequested = soundRequested;
        Entity.SetScriptDrawOffset(new Vector2(0, InitialZ));
        Entity.SetScriptVisible(false);
    }

    public void UpdateFrame(
        RoomEntityFrame frame,
        ICollection<RoomEntitySpawn> spawns)
    {
        if (!Entity.Active)
            return;

        if (_appearanceState == GreatFairyAppearanceState.WaitingToSpawn)
        {
            _appearanceState = GreatFairyAppearanceState.Appearing;
            _appearanceCounter = AppearanceWait;
            _soundRequested(OracleSoundEngine.SndKillEnemy);
            spawns.Add(new PuzzlePuffSpawn(
                Entity.Position, OracleSoundEngine.SndPoof));
            return;
        }

        if (_appearanceState == GreatFairyAppearanceState.Appearing)
        {
            _appearanceCounter--;
            if (_appearanceCounter != 0)
                return;

            _soundRequested(OracleSoundEngine.MusFairyFountain);
            Entity.SetScriptVisible(true);
            _appearanceState = GreatFairyAppearanceState.Ready;
        }

        Entity.UpdateNpc(1.0 / 60.0, frame.Player.Position);
        if ((frame.Counter & 0x07) != 0)
            return;

        int index = (frame.Counter & 0x38) >> 3;
        _z += ZDeltas[index];
        Entity.SetScriptDrawOffset(new Vector2(0, _z));
    }

    public void UpdateDuringScreenTransition()
    {
        // interactionSetAlwaysUpdateBit retains the object in the update list,
        // but returnIfScrollMode01Unset exits before its script and animation
        // while wScrollMode is the scrolling value.
    }

    public bool BlocksLink(Vector2 linkCenter) =>
        Entity.BlocksLinkCenter(linkCenter);

    public NpcCharacter? FindTalkTarget(Player player) =>
        _appearanceState == GreatFairyAppearanceState.Ready &&
        Entity.CanTalkTo(player)
            ? Entity
            : null;

    private static NpcCharacter RequireGreatFairy(NpcCharacter npc)
    {
        if (npc.Record is not
            {
                Group: 0,
                Room: 0x83,
                Id: 0xd5,
                SubId: 0x00,
                Var03: 0x00,
                Implementation:
                    NpcImplementationClassification.SpecializedNative
            })
        {
            throw new InvalidOperationException(
                $"NPC {npc.Record.Group:x1}:{npc.Record.Room:x2} " +
                $"${npc.Record.Id:x2}:${npc.Record.SubId:x2} " +
                "cannot use the Temple Secret Great Fairy adapter.");
        }
        return npc;
    }
}

internal enum GreatFairyAppearanceState
{
    WaitingToSpawn,
    Appearing,
    Ready
}
