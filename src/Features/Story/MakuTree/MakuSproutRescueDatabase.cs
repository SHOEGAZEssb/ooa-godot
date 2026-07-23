using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>Imported room $1:$38 rescue controller and its four script lanes.</summary>
internal sealed class MakuSproutRescueDatabase
{
    private const string Root = "res://assets/oracle/cutscenes/";

    public MakuSproutRescueDatabaseEventRecord Record { get; }
    public IReadOnlyDictionary<string, MakuSproutRescueDatabaseActorRecord> Actors { get; }
    public IReadOnlyList<CutsceneCommand> Sprout { get; }
    public IReadOnlyList<CutsceneCommand> Controller { get; }
    public IReadOnlyList<CutsceneCommand> MoblinLeft { get; }
    public IReadOnlyList<CutsceneCommand> MoblinRight { get; }
    public string FearfulSproutAnimation { get; }

    public MakuSproutRescueDatabase()
    {
        GeneratedTableRow row = GeneratedTable.Load(
            Root + "maku_sprout_rescue_event.tsv",
            new GeneratedTableSchema(
                "Maku Sprout rescue event",
                GeneratedTableKeySemantics.Ordered,
                [
                    "group", "room", "sprout-id", "sprout-subid", "controller-y", "controller-x",
                    "moblin-id", "moblin-y", "left-x", "right-x", "initial-gate-position",
                    "clear-tile", "gate-left", "gate-inner-left", "gate-inner-right", "gate-right",
                    "room-flag", "advice-flag", "saved-flag", "state-min", "state-max",
                    "map-text-low", "trigger-radius-y", "trigger-radius-x", "jump-speed-z",
                    "jump-gravity", "jump-sound", "gate-counter", "shake-counter",
                    "final-text-position", "post-text-id", "post-text-base64"
                ],
                headerRequired: true)).SingleRow();
        Record = new MakuSproutRescueDatabaseEventRecord(
            row.Decimal(0, 0, 7), row.HexByte(1), row.HexByte(2), row.HexByte(3),
            row.HexByte(4), row.HexByte(5), row.HexByte(6), row.HexByte(7),
            row.HexByte(8), row.HexByte(9), row.HexByte(10), row.HexByte(11),
            row.HexByte(12), row.HexByte(13), row.HexByte(14), row.HexByte(15),
            row.HexByte(16), row.HexByte(17), row.HexByte(18), row.HexByte(19),
            row.HexByte(20), row.HexByte(21), row.HexByte(22), row.HexByte(23),
            row.Decimal(24), row.HexByte(25), row.HexByte(26),
            row.UnsignedDecimal(27), row.HexByte(28), row.UnsignedDecimal(29),
            row.HexWord(30), row.Base64Utf8(31));
        Actors = LoadActors();
        Sprout = CutsceneCommandCatalog.Load(Root + "maku_sprout_rescue_sprout.tsv");
        Controller = CutsceneCommandCatalog.Load(Root + "maku_sprout_rescue_controller.tsv");
        MoblinLeft = CutsceneCommandCatalog.Load(Root + "maku_sprout_rescue_moblin_left.tsv");
        MoblinRight = CutsceneCommandCatalog.Load(Root + "maku_sprout_rescue_moblin_right.tsv");
        FearfulSproutAnimation = Sprout[1] is CutsceneSetAnimationCommand
            { Actor: "Sprout", Animation: 0x02, EncodedAnimation: var animation } &&
            !string.IsNullOrEmpty(animation)
                ? animation
                : throw new InvalidOperationException(
                    "makuSprout_subid01Script did not begin with fearful animation $02.");
        Validate();
    }

    private static IReadOnlyDictionary<string, MakuSproutRescueDatabaseActorRecord> LoadActors()
    {
        var result = new Dictionary<string, MakuSproutRescueDatabaseActorRecord>(StringComparer.Ordinal);
        GeneratedTable table = GeneratedTable.Load(
            Root + "maku_sprout_rescue_actors.tsv",
            new GeneratedTableSchema(
                "Maku Sprout rescue actors",
                GeneratedTableKeySemantics.Unique,
                [
                    "actor", "id", "subid", "y", "x", "sprite", "tile-base", "palette",
                    "up-animation", "right-animation", "down-animation", "left-animation"
                ],
                ["actor"],
                headerRequired: true));
        foreach (GeneratedTableRow row in table.Rows)
        {
            string actor = row.RequiredString(0);
            result.Add(actor, new MakuSproutRescueDatabaseActorRecord(
                actor, row.HexByte(1), row.HexByte(2), row.HexByte(3), row.HexByte(4),
                row.RequiredString(5), row.UnsignedDecimal(6), row.UnsignedDecimal(7),
                row.RequiredString(8), row.RequiredString(9), row.RequiredString(10),
                row.RequiredString(11)));
        }
        return result;
    }

    private void Validate()
    {
        if (Record is not
            { Group: 1, Room: 0x38, SproutId: 0x88, SproutSubId: 0,
              ControllerY: 0x40, ControllerX: 0x50, MoblinId: 0x96,
              MoblinY: 0x30, LeftX: 0x68, RightX: 0x38,
              InitialGatePosition: 0x52, ClearTile: 0xf9,
              GateLeft: 0x73, GateInnerLeft: 0x74,
              GateInnerRight: 0x75, GateRight: 0x76,
              RoomFlag: 0x80, AdviceFlag: 0x3f, SavedFlag: 0x12,
              StateMin: 1, StateMax: 2, MapTextLow: 0xd6,
              TriggerRadiusY: 4, TriggerRadiusX: 0x50,
              JumpSpeedZ: -512, JumpGravity: 0x30, JumpSound: 0x53,
              GateCounter: 30, ShakeCounter: 6, FinalTextPosition: 2,
              PostTextId: 0x05d5 } ||
            Actors.Count != 3 ||
            Sprout.Count != 19 || Controller.Count != 71 ||
            MoblinLeft.Count != 21 || MoblinRight.Count != 22 ||
            Sprout[0] is not CutsceneNativeYieldCommand { Handler: "SpawnController" } ||
            FearfulSproutAnimation == Actors["Sprout"].DownAnimation ||
            Controller[56] is not CutsceneNativeYieldCommand { Handler: "SpawnGateOpening" } ||
            Controller[59] is not CutsceneSetGlobalFlagCommand { Flag: 0x3f } ||
            Controller[62] is not CutsceneSetGlobalFlagCommand { Flag: 0x12 } ||
            MoblinLeft[18] is not CutsceneNativeCommand { Handler: "SpawnMaskedMoblinLeft" } ||
            MoblinRight[19] is not CutsceneNativeCommand { Handler: "SpawnMaskedMoblinRight" })
        {
            throw new InvalidOperationException(
                "Room 1:38 rescue data diverges from its sprout/controller/Moblin scripts.");
        }
    }
}
