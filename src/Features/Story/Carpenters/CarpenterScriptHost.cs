using Godot;
using System;

namespace oracleofages;

internal sealed class CarpenterScriptHost(CarpenterEvent owner, CarpenterRoomEntity entity)
    : CutsceneCommandHost, ICutsceneCommandHost
{
    public RoomEventContext Context => owner.Context;
    public NpcCharacter Npc => entity.Npc;
    internal CutsceneCommandRunner Runner { get; set; } = null!;
    internal bool ButtonSensitive { get; private set; }
    internal bool ButtonPressed { get; set; }
    private int _z;
    private int _speedZ;
    internal bool Departing { get; private set; }

    public override void WriteObjectByte(string actor, int address, int value)
    {
        if (actor != "Carpenter" || address != 0x44 || value != 2)
            throw UnsupportedCommand($"write carpenter byte ${address:x2} = ${value:x2}");
        Departing = true;
        Npc.SetScriptButtonSensitive(false);
        Runner.Clear(); // Native state $02 supersedes the suspended script.
    }

    internal void UpdateDeparture()
    {
        Npc.AdvanceAnimationUpdates(1);
        Npc.SetStatePosition(Npc.Position + OracleObjectMovement.Shared.Delta(owner.Database.Constant("departure-speed"), 0x18));
        if (!OracleObjectMath.IsInsideOriginalScreenBoundary(Npc.Position))
        {
            Npc.SetActive(false);
            owner.ReturnWorker(Npc.Record.SubId);
            return;
        }
        if (OracleObjectMath.UpdateSpeedZ(ref _z, ref _speedZ, owner.Database.Constant("departure-gravity")))
        {
            _speedZ = owner.Database.Constant("jump-speed");
            Context.Sound.PlaySound(owner.Database.Constant("jump-sound"));
        }
        Npc.SetScriptDrawOffset(new(0, _z >> 8));
    }

    internal void UpdateNative()
    {
        Npc.AnimateAsNpcOneUpdate(Context.Player);
        OracleObjectMath.UpdateSpeedZ(ref _z, ref _speedZ, owner.Database.Constant("gravity"));
        Npc.SetScriptDrawOffset(new(0, _z >> 8));
    }

    public override bool HasActorBinding(CutsceneActorId actor) => actor.Value == "Carpenter";
    public override void SetActorButtonSensitive(string actor)
    {
        ButtonSensitive = true;
        Npc.SetScriptButtonSensitive(true);
    }
    public override bool TryConsumeActorButton(CutsceneActorId actor)
    {
        bool pressed = ButtonPressed;
        ButtonPressed = false;
        if (pressed) Context.Player.ApplyObjectInteractionGrace();
        return pressed;
    }
    public override void SetActorAnimation(string actor, int animation, string encodedAnimation) =>
        Npc.SetScriptAnimation(encodedAnimation);
    public override void SetActorMovementAnimation(string actor, int angle, string encodedAnimation) =>
        Npc.SetScriptAnimation(encodedAnimation);
    public override void MoveActorAtSpeed(string actor, int speed, int angle)
    {
        Vector2 position = Npc.Position;
        Npc.SetStatePosition(OracleObjectMovement.Shared.ApplySpeed(ref position, speed, angle));
    }
    public override void SetInputEnabled(bool enabled) => owner.SetInputEnabled(enabled);
    public override void SetMenuEnabled(bool enabled) => owner.SetMenuEnabled(enabled);
    public override void SetDisabledObjects(int value)
    {
        if (value != 0) throw UnsupportedCommand($"set disabled objects ${value:x2}");
        owner.SetInputEnabled(true);
    }
    public override bool MemoryEquals(string binding, int value) => ReadMemory(binding) == value;
    public override int ReadMemory(string binding)
    {
        if (binding == "SearchState") return owner.SearchState;
        if (binding == "Returned") return entity.Returned ? 1 : 0;
        if (binding.StartsWith("Global:", StringComparison.Ordinal) &&
            int.TryParse(binding.AsSpan(7), out int flag))
            return Context.Rooms.SaveData.HasGlobalFlag(flag) ? 1 : 0;
        throw UnsupportedCommand($"read carpenter memory '{binding}'");
    }
    public override void WriteMemory(string binding, int value)
    {
        if (binding != "SearchState") throw UnsupportedCommand($"write carpenter memory '{binding}'");
        owner.SearchState = value;
    }
    public override bool TextOptionEquals(int value)
    {
        if (!Context.TryTakeDialogueChoice(out int choice))
            throw UnsupportedCommand("read an absent carpenter dialogue choice");
        return choice == value;
    }
    public override void ShowText(int textId, string message)
    {
        if (textId is 0x2302 or 0x2304 or 0x2305) Context.ShowChoiceDialogue(message);
        else Context.ShowDialogue(message);
    }
    public override void RunNativeHandler(string handler)
    {
        if (handler == "Jump") _speedZ = owner.Database.Constant("jump-speed");
        else if (handler == "EnableMenu") owner.SetMenuEnabled(true);
        else if (handler == "FaceLink")
        {
            Vector2 target = Context.Entities.ActiveScentSeedTarget() ?? Context.Player.Position;
            int direction = ((OracleObjectMovement.Shared.RelativeAngle(Npc.Position, target) + 4) & 0x18) >> 3;
            // These four entries are also the movement animations in the imported catalog.
            foreach (var command in owner.Database.Commands)
                if (command is CutsceneMoveCommand move && move.Angle == direction * 8)
                {
                    Npc.SetScriptAnimation(move.EncodedAnimation);
                    return;
                }
            throw UnsupportedCommand($"find carpenter direction ${direction:x2}");
        }
        else if (handler.StartsWith("BuildColumn:", StringComparison.Ordinal) &&
            int.TryParse(handler.AsSpan(12), System.Globalization.NumberStyles.HexNumber, null, out int position))
            owner.BuildColumn(position);
        else throw UnsupportedCommand($"run carpenter helper '{handler}'");
    }
    public override void ScriptEnded()
    {
        Npc.SetScriptButtonSensitive(false);
        if (!Departing) Npc.SetActive(false);
    }
}
