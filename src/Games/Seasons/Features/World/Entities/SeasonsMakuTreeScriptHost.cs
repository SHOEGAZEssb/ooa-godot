namespace Oracles;

internal sealed class SeasonsMakuTreeScriptHost : NpcInteractionCommandHost
{
    private readonly SeasonsMakuTreeRoomEntity _owner;
    private readonly SeasonsOpeningSession _session;
    private readonly SeasonsTalkNpcDatabase _data;
    private bool _movementLock;
    internal SeasonsMakuTreeScriptHost(SeasonsMakuTreeRoomEntity owner,SeasonsOpeningSession session,
        SeasonsTalkNpcDatabase data,string program):base(owner,session.Rooms,session.Entities,session.Dialogue,data.Program(program))
    {_owner=owner;_session=session;_data=data;}
    public override void PlaySound(int sound)=>_session.Sound.PlaySound(sound);
    public override void OrRoomFlag(int flag)=>_session.Save.SetRoomFlag(_session.Rooms.ActiveGroup,_session.RoomId,(byte)flag);
    public override bool MemoryEquals(string binding,int value)=>ReadMemory(binding)==value;
    public override int ReadMemory(string binding)=>binding switch
    {
        "cfc0bit7"=>(_session.Memory.ReadWramByte(0xcfc0)>>7)&1,
        "$cfc0"=>_session.Memory.ReadWramByte(0xcfc0),
        "wcc95"=>_session.Memory.ReadField(binding),
        "Interaction.state"=>_owner.ReadByte(0x44),
        "$61" or "Interaction.animParameter"=>_owner.Npc.CurrentAnimationParameter,
        string flag when flag.StartsWith("flag:",StringComparison.Ordinal)=>_session.Save.HasGlobalFlag(Convert.ToInt32(flag[5..],16))?1:0,
        _=>throw UnsupportedCommand($"read {binding}")
    };
    public override void WriteMemory(string binding,int value)
    {
        if(binding!="wcc95")throw UnsupportedCommand($"write {binding}");
        _session.Memory.SetField(binding,(byte)value);_movementLock=value==0x80;
    }
    public override void RunNativeHandler(string handler)
    {
        if(handler=="incstate"){_owner.WriteByte(0x44,_owner.ReadByte(0x44)+1);return;}
        if(handler=="gate-sword-grant")
        {
            if(_session.Entities.TryCreateGroundTreasure(new(_session.Rooms.ActiveGroup,_session.RoomId,0,
                (int)ScriptPlayer.Position.Y,(int)ScriptPlayer.Position.X,"TREASURE_OBJECT_SWORD_03",
                "scripts.s:makuTreeScript_gateHit giveitem ITEM_SWORD,$03")
                {GrabMode=TreasureGrabMode.SpinSlash,RoomFlagTiming=GroundTreasureRoomFlagTiming.Never,
                    ExpectedTreasureId=TreasureId.Sword,ExpectedSubId=3,ExpectedObjectParameter=1}))
                _owner.WriteByte(0x46,3);
            return;
        }
        string[] pieces=handler.Split(' ',2);
        string[] args=pieces.Length==1?[]:pieces[1].Split(',',StringSplitOptions.TrimEntries);
        if(pieces[0]!="asm15")throw UnsupportedCommand(handler);
        int argument=args.Length>1?Convert.ToInt32(args[1].StartsWith("<TX_",StringComparison.Ordinal)?args[1][4..]:args[1].TrimStart('$'),16)&255:0;
        switch(args[0])
        {
            case "scriptHelp.makuTree_checkGateHit":
                if(!ScriptPlayer.IsAttacking || _session.Memory.ReadField("wcc63")!=0 || ScriptPlayer.IsDying)return;
                Vector2 link=ScriptPlayer.Position,gate=ScriptActor.Position;
                if(unchecked((byte)(ScriptPlayer.ObjectZHigh+7))<14 &&
                    unchecked((byte)((int)link.Y-(int)gate.Y+_owner.ReadByte(0x66)+6))<(_owner.ReadByte(0x66)+6)*2 &&
                    unchecked((byte)((int)link.X-(int)gate.X+_owner.ReadByte(0x67)+6))<(_owner.ReadByte(0x67)+6)*2)
                    _session.Memory.SetWramByte(0xcfc0,1);
                return;
            case "scriptHelp.makuTree_storeIntoVar37SpawnBubbleIf0":_owner.SetMakuAnimation(argument);return;
            case "scriptHelp.makuTree_setMakuMapText":_session.Save.SetMakuMapTextPresent(argument);return;
            case "scriptHelp.makuTree_showTextAndSetMapText":_session.Save.SetMakuMapTextPresent(argument);goto case "scriptHelp.makuTree_showText";
            case "scriptHelp.makuTree_showText":ShowText(0x1700|argument,_data.Text(0x1700|argument));return;
            case "scriptHelp.makuTree_dropGnarledKey":_owner.SpawnKey(true);return;
        }
        throw UnsupportedCommand(handler);
    }
    protected override void ResetHostState()
    {
        if(_movementLock && (_session.Memory.ReadField("wcc95")&0x80)!=0)_session.Memory.SetField("wcc95",0);
        _movementLock=false;base.ResetHostState();
    }
}
