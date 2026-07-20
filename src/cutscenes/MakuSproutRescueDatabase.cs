using Godot;
using System;
using System.Collections.Generic;
using System.Text;

namespace oracleofages;

/// <summary>Imported room $1:$38 rescue controller and its four script lanes.</summary>
internal sealed class MakuSproutRescueDatabase
{
    private const string Root = "res://assets/oracle/cutscenes/";

    public EventRecord Record { get; }
    public IReadOnlyDictionary<string, ActorRecord> Actors { get; }
    public IReadOnlyList<CutsceneCommand> Sprout { get; }
    public IReadOnlyList<CutsceneCommand> Controller { get; }
    public IReadOnlyList<CutsceneCommand> MoblinLeft { get; }
    public IReadOnlyList<CutsceneCommand> MoblinRight { get; }
    public string FearfulSproutAnimation { get; }

    public MakuSproutRescueDatabase()
    {
        string[] f = FirstDataRow(Root + "maku_sprout_rescue_event.tsv").Split('\t');
        if (f.Length != 32)
            throw new InvalidOperationException(
                $"Maku Sprout rescue row should have 32 fields, got {f.Length}.");
        Record = new EventRecord(
            int.Parse(f[0]), Hex(f[1]), Hex(f[2]), Hex(f[3]),
            Hex(f[4]), Hex(f[5]), Hex(f[6]), Hex(f[7]), Hex(f[8]), Hex(f[9]),
            Hex(f[10]), Hex(f[11]), Hex(f[12]), Hex(f[13]), Hex(f[14]), Hex(f[15]),
            Hex(f[16]), Hex(f[17]), Hex(f[18]), Hex(f[19]), Hex(f[20]), Hex(f[21]),
            Hex(f[22]), Hex(f[23]), int.Parse(f[24]), Hex(f[25]), Hex(f[26]),
            int.Parse(f[27]), Hex(f[28]), int.Parse(f[29]), Hex(f[30]),
            Encoding.UTF8.GetString(Convert.FromBase64String(f[31])));
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

    private static IReadOnlyDictionary<string, ActorRecord> LoadActors()
    {
        var result = new Dictionary<string, ActorRecord>(StringComparer.Ordinal);
        foreach (string raw in FileAccess.GetFileAsString(
            Root + "maku_sprout_rescue_actors.tsv").Split(
                '\n', StringSplitOptions.RemoveEmptyEntries))
        {
            string line = raw.TrimEnd('\r');
            if (line.StartsWith('#'))
                continue;
            string[] f = line.Split('\t');
            if (f.Length != 12)
                throw new InvalidOperationException($"Malformed Maku rescue actor row: {line}");
            result.Add(f[0], new ActorRecord(
                f[0], Hex(f[1]), Hex(f[2]), Hex(f[3]), Hex(f[4]), f[5],
                int.Parse(f[6]), int.Parse(f[7]), f[8], f[9], f[10], f[11]));
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

    private static string FirstDataRow(string path)
    {
        foreach (string raw in FileAccess.GetFileAsString(path).Split(
            '\n', StringSplitOptions.RemoveEmptyEntries))
        {
            string line = raw.TrimEnd('\r');
            if (!line.StartsWith('#'))
                return line;
        }
        throw new InvalidOperationException($"{path} is empty.");
    }

    private static int Hex(string value) => Convert.ToInt32(value, 16);

    internal readonly record struct ActorRecord(
        string Actor, int Id, int SubId, int Y, int X,
        string SpriteName, int TileBase, int Palette,
        string UpAnimation, string RightAnimation,
        string DownAnimation, string LeftAnimation)
    {
        public NpcDatabase.NpcRecord ToNpcRecord(int group, int room) => new(
            group, room, Id, SubId, Y, X, 0, 0, SpriteName, TileBase, Palette,
            0, false, UpAnimation, RightAnimation, DownAnimation, LeftAnimation,
            string.Empty);
    }

    internal readonly record struct EventRecord(
        int Group, int Room, int SproutId, int SproutSubId,
        int ControllerY, int ControllerX, int MoblinId, int MoblinY,
        int LeftX, int RightX, int InitialGatePosition, int ClearTile,
        int GateLeft, int GateInnerLeft, int GateInnerRight, int GateRight,
        int RoomFlag, int AdviceFlag, int SavedFlag, int StateMin, int StateMax,
        int MapTextLow, int TriggerRadiusY, int TriggerRadiusX,
        int JumpSpeedZ, int JumpGravity, int JumpSound,
        int GateCounter, int ShakeCounter, int FinalTextPosition,
        int PostTextId, string PostText);
}
