using System;
using System.Collections.Generic;
using System.Numerics;

namespace oracleofages;

internal sealed class DungeonSignalScriptDatabase
{
    private readonly Dictionary<(int Dungeon,int SubId),DungeonSignalScriptProfile> _profiles = new();
    internal DungeonSignalScriptDatabase()
    {
        Load("res://assets/oracle/objects/dungeon_signal_scripts.tsv");
        Load("res://assets/oracle/objects/heros_cave_signal_scripts.tsv");
    }
    private void Load(string path)
    {
        var table=GeneratedTable.Load(path,
            new GeneratedTableSchema("Dungeon signal scripts",GeneratedTableKeySemantics.Unique,
                ["dungeon","subid","kind","test-mask","output-mask","position","half-steps","angle","wait","source"],
                ["dungeon","subid"],headerRequired:true));
        foreach (var row in table.Rows)
        {
            var kind=row.RequiredString(2) switch {
                "bridge" => DungeonSignalScriptKind.Bridge,
                "trigger-bridge" => DungeonSignalScriptKind.TriggerBridge,
                "conjunction" => DungeonSignalScriptKind.Conjunction,
                _ => throw row.Invalid(2,"bridge, trigger-bridge or conjunction") };
            var profile=new DungeonSignalScriptProfile(kind,row.HexByte(3),row.HexByte(4),
                row.HexByte(5),row.HexByte(6),row.HexByte(7),row.UnsignedDecimal(8),row.RequiredString(9));
            if (profile.TestMask == 0 || !BitOperations.IsPow2((uint)profile.OutputMask) ||
                kind is DungeonSignalScriptKind.Bridge or DungeonSignalScriptKind.TriggerBridge && (profile.HalfSteps == 0 || profile.Angle > 3 || profile.Wait != 0) ||
                kind == DungeonSignalScriptKind.Conjunction && (profile.Position != 0 || profile.HalfSteps != 0 ||
                    profile.Angle != 0 || profile.Wait is < 1 or > 255 || (profile.TestMask&profile.OutputMask) != 0))
                throw row.Invalid(2,"a supported bridge or conjunction profile");
            int dungeon=row.HexByte(0);
            if (dungeon >= 16) throw row.Invalid(0,"a dungeon index$00..$0f");
            _profiles.Add((dungeon,row.HexByte(1)),profile);
        }
    }
    internal DungeonSignalScriptProfile Profile(int dungeon,DungeonObjectRecord record) =>
        _profiles.TryGetValue((dungeon,record.SubId),out var profile) ? profile :
        throw new InvalidOperationException($"{record.Source}: no signal script for dungeon${dungeon:x2}, INTERAC${record.Id:x2}:${record.SubId:x2}.");
}

internal enum DungeonSignalScriptKind { Bridge, TriggerBridge, Conjunction }
internal readonly record struct DungeonSignalScriptProfile(DungeonSignalScriptKind Kind,int TestMask,int OutputMask,
    int Position,int HalfSteps,int Angle,int Wait,string Source);
