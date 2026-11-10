
namespace Oracles;

internal sealed class SeasonsTalkNpcScriptHost : NpcInteractionCommandHost
{
    private readonly NativeNpcRoomEntity _owner;
    private readonly SeasonsOpeningSession _session;
    private readonly SeasonsTalkNpcDatabase _data;
    internal SeasonsTalkNpcScriptHost(NativeNpcRoomEntity owner, SeasonsOpeningSession session,
        SeasonsTalkNpcDatabase data, IReadOnlyList<CutsceneCommand> commands) : base(owner, session.Rooms, session.Entities, session.Dialogue, commands)
    { _owner = owner; _session = session; _data = data; }
    public override bool GateOpen(string gate) => gate == "RupeeDisplayUpdated"
        ? _session.StatusBar.DisplayedRupees == _session.Inventory.Rupees : base.GateOpen(gate);
    public override void OrRoomFlag(int flag) => Rooms.SaveData.SetRoomFlag(Rooms.ActiveGroup, Rooms.CurrentRoom.Id, checked((byte)flag));
    public override bool IsLinkedGame => _session.Memory.ReadField("wIsLinkedGame")!=0;
    public override bool MemoryEquals(string binding, int value) => ReadMemory(binding) == value;
    public override int ReadMemory(string binding)
    {
        if (binding == "wIsLinkedGame") return IsLinkedGame ? 1 : 0;
        if (binding == "wRoomStateModifier") return _session.Memory.ReadField(binding);
        if (binding is "$cfc0" or "$ccea") return _session.Memory.ReadWramByte(Hex(binding));
        if (binding.StartsWith("flag:", StringComparison.Ordinal)) return Rooms.SaveData.HasGlobalFlag(Hex(binding[5..])) ? 1 : 0;
        if (binding.StartsWith("treasure:", StringComparison.Ordinal)) return Rooms.SaveData.HasTreasure(Hex(binding[9..])) ? 1 : 0;
        if (binding.StartsWith("mask:", StringComparison.Ordinal))
        {
            var parts = binding.Split(':');
            if (parts[1] != "wSubrosiaRoomFlags+(<ROOM_SEASONS_160)") throw UnsupportedCommand(binding);
            return (Rooms.SaveData.GetRoomFlags(1, 0x60) & Hex(parts[2])) != 0 ? 1 : 0;
        }
        throw UnsupportedCommand($"read {binding}");
    }
    public override void WriteMemory(string binding,int value)
    {
        if(binding=="wTextIsActive"&&value==1){Dialogue.ResumeNativeText();return;}
        if(binding!="$ccea")throw UnsupportedCommand($"write {binding}");
        _session.Memory.SetWramByte(0xccea,checked((byte)value));
    }
    public override void ShowText(int textId, string message)
        =>ShowNativeText(textId,message,false);
    private void ShowNativeText(int textId,string message,bool nonExitable)
    {
        int? position=_owner.Npc.Record.Id==0xe5?2:null;
        int flags=(position.HasValue?8:0)|(nonExitable?2:0);
        ShowNpcText(textId, message, flags, position);
    }
    public override bool TextOptionEquals(int value)
    {Dialogue.TryTakeChoiceResult(out _);return Dialogue.SelectedChoice==value;}
    public override void SetActorCollisionRadii(string actor,int radiusY,int radiusX)
    { base.SetActorCollisionRadii(actor,radiusY,radiusX);ScriptActor.SetBlocksLink(true); }
    public override void InitializeActorCollisionRadii(string actor) => SetActorCollisionRadii(actor,6,6);
    public override void RunNativeHandler(string handler)
    {
        string[] fields = handler.Split(' ', 2);
        string[] args = fields.Length == 1 ? [] : fields[1].Split(',', StringSplitOptions.TrimEntries);
        switch (fields[0])
        {
            case "nonexitabletext":
                int text=Hex(args[0]);ShowNativeText(text,_owner.Text(text),true);return;
            case "setanimationfromangle": _owner.SetAnimation((ActorAngle(new("Self")) >> 3) & 3); return;
            case "setanimationfromobjectbyte": _owner.SetAnimation(_owner.ReadByte(Hex(args[0]))); return;
            case "cplinkx": _owner.WriteByte(args[0]=="Interaction.direction"?0x48:Hex(args[0]), (int)ScriptPlayer.Position.X > (int)ScriptActor.Position.X ? 1 : 0); return;
            case "addobjectbyte":
                int add=args[1].StartsWith('-')?-Hex(args[1][1..]):Hex(args[1]);
                if(args[0]=="Interaction.textID")_owner.AddTextId(add);
                else {int offset=Hex(args[0]);_owner.WriteByte(offset,_owner.ReadByte(offset)+add);}
                return;
            case "setzspeed" when _owner is SeasonsTalkNpcRoomEntity native:
                native.SetSpeedZ(args[0].StartsWith('-') ? -Hex(args[0][1..]) : Hex(args[0])); return;
            case "stopifroomflag80set": if (RoomFlagSet(0x80)) _owner.Delete(); return;
            case "asm15":
                switch (args[0])
                {
                    case "scriptHelp.oldMan_giveRupees":
                        _session.Inventory.GiveTreasure(TreasureId.Rupees, _data.NativeTable("old-man-rupee-values")[_owner.ReadByte(0x42)]); return;
                    case "scriptHelp.din_animateAndLookAtLink":
                        int direction=((OracleObjectMovement.Shared.RelativeAngle(ScriptActor.Position,ScriptPlayer.Position)+4)>>3)&3;
                        _owner.WriteByte(0x48,direction);_owner.SetAnimation(direction);return;
                    case "scriptHelp.subrosianFunc_58ac":
                    case "scriptHelp.subrosianFunc_58b1":
                        int address = args[0].EndsWith("58ac", StringComparison.Ordinal) ? 0xcfde : 0xcfdc;
                        _session.Memory.SetWramByte(address, (byte)(0xd0 + _session.Entities.NativeInteractionSlot(_owner)));
                        _session.Memory.SetWramByte(address + 1, 0x58); return;
                    case "scriptHelp.faceOppositeDirectionAsLink":
                        _owner.SetAnimation((ScriptPlayer.FacingVector == Vector2I.Up ? 0 : ScriptPlayer.FacingVector == Vector2I.Right ? 1 : ScriptPlayer.FacingVector == Vector2I.Down ? 2 : 3) ^ 2); return;
                    case "scriptHelp.dungeonWiseOldMan_setLinksInvincibilityCounterTo0":
                        ScriptPlayer.ResetEnemyInvincibility(); return;
                    case "scriptHelp.impa_checkIf4thEssenceGotten":
                        _session.Memory.SetWramByte(0xcfc0, (byte)(Rooms.SaveData.HasTreasure(TreasureId.Essence)&&(Rooms.SaveData.ReadField("wEssencesObtained") & 8) != 0 ? 0 : 1)); return;
                }
                break;
        }
        throw UnsupportedCommand(handler);
    }
    private static int Hex(string value) => Convert.ToInt32(value.TrimStart('$'), 16);
}
