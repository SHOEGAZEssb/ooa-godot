using Godot;
using System;
using System.Globalization;

namespace Oracles;

internal sealed class SeasonsOpeningScriptHost(SeasonsOpeningSession session, SeasonsOpeningActor owner) : CutsceneCommandHost
{
    public override bool DialogueOpen => session.Dialogue.IsOpen;
    public override bool IsLinkedGame => false;
    public override int FrameCounter => session.FrameCounter;
    public override bool HasActorBinding(CutsceneActorId actor) => actor.Value == "Self";
    public override void SetInputEnabled(bool enabled) => NativeScriptInputControl.SetEnabled(session.Memory, session.Link, enabled);
    public override void SetDisabledObjects(int value) => session.DisabledObjects = (byte)value;
    public override bool MemoryEquals(string binding, int value) => ReadMemory(binding) == value;
    public override int ReadMemory(string binding) => binding switch
    {
        "LinkZ" => session.Link.ObjectZHigh,
        "IsLinkedGame" => 0,
        _ when binding.StartsWith("flag:", StringComparison.Ordinal) => session.Save.HasGlobalFlag(Convert.ToInt32(binding[5..], 16)) ? 1 : 0,
        _ when binding.StartsWith("object:", StringComparison.Ordinal) => owner.Bytes[Convert.ToInt32(binding[7..], 16)],
        _ => session.Memory.ReadWramByte(Convert.ToInt32(binding, 16))
    };
    public override bool RoomFlagSet(int flag) => session.RoomFlag(flag);
    public override bool TryConsumeActorButton(CutsceneActorId actor)
    {
        if (!owner.ButtonPressed) return false;
        owner.ButtonPressed = false; return true;
    }
    public override void ShowText(int textId, string message) => session.ShowText(textId);
    public override void ShowText(int textId, string message, int? textboxPosition) => session.ShowText(textId, textboxPosition);
    public override void ShowLoadedText() => session.ShowText(owner.TextId);
    public override void SetActorTextId(string actor, int textId, string message) => owner.TextId = textId;
    public override void SetActorAnimation(string actor, int animation, string encodedAnimation) => owner.SetAnimation(animation);
    public override void SetActorMovementAnimation(string actor, int angle, string encodedAnimation) => owner.SetAnimation(angle / 8);
    public override void SetActorCollisionRadii(string actor, int radiusY, int radiusX)
    {
        owner.RadiusY = radiusY; owner.RadiusX = radiusX; owner.Node.SetCollisionRadii(radiusY, radiusX);
    }
    public override void SetActorButtonSensitive(string actor) => owner.Node.SetScriptButtonSensitive(true);
    public override void InitializeActorCollisionRadii(string actor) => SetActorCollisionRadii(actor, 6, 6);
    public override void MoveActorAtSpeed(string actor, int speed, int angle) => owner.Move(speed, angle);
    public override void SetActorCoordinates(string actor, int y, int x) => owner.Node.Position = new(x, y);
    public override void SetActorZ(string actor, int zFixed) => owner.SetZ(zFixed);
    public override void WriteObjectByte(string actor, int address, int value) => owner.Bytes[address] = (byte)value;
    public override Vector2 GetActorPosition(CutsceneActorId actor) => owner.Node.Position;
    public override void WriteMemory(string binding, int value) => session.Memory.SetWramByte(Convert.ToInt32(binding, 16), (byte)value);
    public override void PlaySound(int sound) => session.Sound.PlaySound(sound);
    public override void SetMusic(int music) => session.Sound.PlaySound(music);
    public override void SetGlobalFlag(int flag) => session.Save.SetGlobalFlag(flag);
    public override void OrRoomFlag(int flag) => session.OrRoomFlag(flag);
    public override void ScriptEnded() { }

    public override void RunNativeHandler(string handler)
    {
        string[] parts = handler.Split(' ', 2);
        string[] operands = parts.Length == 1 ? [] : parts[1].Split(", ", StringSplitOptions.TrimEntries);
        switch (parts[0])
        {
            case "ormemory":
                int address = Hex(operands[0]);
                session.Memory.SetWramByte(address, (byte)(session.Memory.ReadWramByte(address) | Hex(operands[1]))); return;
            case "cplinkx":
                owner.Bytes[Hex(operands[0])] = owner.Node.Position.X >= session.Link.Position.X ? (byte)0 : (byte)1; return;
            case "setanimationfromobjectbyte": owner.SetAnimation(owner.Bytes[Hex(operands[0])]); return;
            case "setzspeed": owner.SpeedZ = -Hex(operands[0].TrimStart('-')); return;
            case "asm15":
                string helper = operands[0].Replace("scriptHelp.", "", StringComparison.Ordinal);
                switch (helper)
                {
                    case "dinDancingEvent_setTextAdd_0a_ifLinked":
                        owner.TextId = Convert.ToInt32(operands[1].Replace("<TX_", "", StringComparison.Ordinal), 16); return;
                    case "dinDancing_spinLink":
                        int direction = session.Link.FacingVector == Vector2I.Up ? 0 : session.Link.FacingVector == Vector2I.Right ? 1 : session.Link.FacingVector == Vector2I.Down ? 2 : 3;
                        owner.SetAnimation((direction ^ 2) + 9); return;
                    case "din_createExclamationMark":
                        session.Entities.TryCreateExclamation(owner.Node.Position + new Vector2(0, -13), Hex(operands[1])); return;
                    case "piratian_waitUntilJumpDone":
                        if (OracleObjectMath.UpdateSpeedZ(ref owner.ZFixed, ref owner.SpeedZ, 0x30)) owner.Bytes[0x7d] = 1;
                        owner.SetZ(owner.ZFixed); return;
                }
                break;
        }
        throw UnsupportedCommand($"run Seasons opening helper '{handler}'");
    }
    private static int Hex(string value) => int.Parse(value.TrimStart('$'), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
}
