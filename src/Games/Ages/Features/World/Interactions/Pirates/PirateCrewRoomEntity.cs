using Godot;
using System.Collections.Generic;

namespace Oracles;

/// <summary>pirate.s $c4:$00-$03, generic conversation followed by the native jump.</summary>
internal sealed class PirateCrewRoomEntity(NpcCharacter actor, RoomEntityManager owner)
    : NpcCharacterRoomEntityAdapter(actor, actor.SetTransitionDrawOffset),
      IFixedRoomEntity, IRoomBlocker, ITalkTarget
{
    // pirate.s @state1/@state2 share genericCutscene.state ($cfc0).
    internal const byte JumpSignal = 1;
    internal const byte LandedSignal = 2;
    private const int JumpSpeed = -0x200;
    private const int Gravity = 0x28;
    private int _state, _z, _speedZ;
    private bool _buttonPending;
    internal int State => _state;
    internal bool QueueButton()
    {
        if (_state != 1) return false;
        _buttonPending = true;
        return true;
    }
    public bool BlocksLink(Vector2 center) => _state == 1 && Entity.BlocksLinkCenter(center);
    public NpcCharacter? FindTalkTarget(Player player) => _state == 1 && Entity.CanTalkTo(player) ? Entity : null;
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (_state == 0)
        {
            Entity.SetFixedDrawPriority(ObjectDrawPriority.BehindLinkZIndex, withTerrainEffects: true); // objectSetVisiblec2
            _state = 1;
            return;
        }
        byte signal = owner.RuntimeState.ReadWramByte(AgesWramAddress.wTmpcfc0);
        if (_state == 1)
        {
            if ((signal & JumpSignal) != 0)
            {
                Entity.SetScriptAnimation(PirateShipDatabase.Shared.CrewJump);
                _speedZ = JumpSpeed; _state = 2;
                return;
            }
            if (_buttonPending && !owner.IsScriptTextActive() && !frame.Player.IsDying)
            {
                _buttonPending = false;
                owner.OnRoomEntityDialogueRequested(Entity.TextId, Entity.Message, frame.Player.Position);
            }
            // npcFaceLinkAndAnimate replaces state 0's objectSetVisiblec2
            // priority with objectSetPriorityRelativeToLink_withTerrainEffects.
            Entity.ClearFixedDrawPriority();
            Entity.FaceLinkAndAnimateOneUpdate(frame.Player, preventPassing: true);
            return;
        }
        bool landed = OracleObjectMath.UpdateSpeedZ(ref _z, ref _speedZ, Gravity);
        Entity.SetScriptDrawOffset(new(0, _z >> 8));
        if (!landed) return;
        owner.RuntimeState.SetWramByte(AgesWramAddress.wTmpcfc0, (byte)(signal | LandedSignal));
        Entity.AdvanceAnimationUpdates(1);
    }
}
