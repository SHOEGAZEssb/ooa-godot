namespace Oracles;

internal sealed class SeasonsSokraScriptHost : SeasonsTalkNpcScriptHost
{
    private readonly SeasonsSokraRoomEntity _owner;
    private readonly SeasonsOpeningSession _session;

    internal SeasonsSokraScriptHost(SeasonsSokraRoomEntity owner, SeasonsOpeningSession session,
        SeasonsTalkNpcDatabase data, IReadOnlyList<CutsceneCommand> commands) : base(owner, session, data, commands)
    {
        _owner = owner;
        _session = session;
    }

    public override void WriteMemory(string binding, int value)
    {
        if (binding == "$d008")
            ScriptPlayer.Face((Vector2I)OracleObjectMath.StrictCardinalVector(value * 8));
        else base.WriteMemory(binding, value);
    }

    public override void RunNativeHandler(string handler)
    {
        string[] fields = handler.Split(' ', 2);
        string[] args = fields.Length == 1 ? [] : fields[1].Split(',', StringSplitOptions.TrimEntries);
        if (fields[0] == "asm15")
        {
            switch (args[0])
            {
                case "createSokraSnore": _owner.SpawnSnore(); return;
                case "scriptHelp.sokra_alert":
                    _owner.SetAnimation(4);
                    _session.Entities.TryCreateInteractionEntity(new ExclamationMarkSpawn(ScriptActor.Position + new Vector2(-4, -16), Duration: 0x40));
                    return;
                case "scriptHelp.villageSokra_checkStageInGame":
                    _session.Memory.SetWramByte(0xcfc0, (byte)((Rooms.SaveData.ReadField("wEssencesObtained") & 2) == 0 ? 0 :
                        Rooms.SaveData.HasGlobalFlag(SeasonsGlobalFlag.SeasonAlwaysSpring) ? 2 : 1));
                    return;
                case "scriptHelp.villageSokra_waitUntilLinkInPosition":
                    _owner.Move(_owner.ReadByte(0x50), _owner.ReadByte(0x49));
                    if (Math.Abs((int)ScriptActor.Position.X - (int)ScriptPlayer.Position.X) + Math.Abs((int)ScriptActor.Position.Y - (int)ScriptPlayer.Position.Y) < 0x10)
                        _owner.WriteByte(0x76, 1);
                    return;
                case "scriptHelp.seasonsFunc_15_5802":
                    if ((int)ScriptActor.Position.Y == (int)ScriptPlayer.Position.Y) _owner.WriteByte(0x76, 1);
                    else _owner.Move(_owner.ReadByte(0x50), _owner.ReadByte(0x49));
                    return;
                case "scriptHelp.seasonsFunc_15_5812":
                    _owner.Move(_owner.ReadByte(0x50), _owner.ReadByte(0x49));
                    if ((int)ScriptActor.Position.Y == Convert.ToInt32(args[1].TrimStart('$'), 16)) _owner.WriteByte(0x76, 0);
                    return;
                case "scriptHelp.suburbsSokra_jumpOffStump": _owner.AdvanceJump(); return;
                case "scriptHelp.seasonsFunc_15_5840":
                    _owner.WriteByte(0x49, (int)ScriptActor.Position.Y < (int)ScriptPlayer.Position.Y ? 0x10 : 0);
                    return;
            }
        }
        base.RunNativeHandler(handler);
    }
}
