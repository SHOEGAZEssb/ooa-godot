using System;

namespace oracleofages;

/// <summary>
/// INTERAC_CHEVAL $6a:$00 and cheval_subid00Script in room $2:$0f.
/// </summary>
internal sealed class ChevalEvent :
    InteractiveInfiniteScriptHost<ChevalCharacter>,
    IRoomEntryEvent,
    IUpdatesDuringDialogueRoomEvent,
    ICutsceneCommandHost
{
    private const string ActorName = "Cheval";
    private readonly ChevalEventDatabase _database = new();
    private readonly ChevalEventRecord _record;

    internal ChevalEvent(RoomEventContext context) : base(context, ActorName)
    {
        _record = _database.Record;
    }

    internal ChevalEventDatabase Database => _database;
    internal ChevalCharacter? Actor => ScriptActor;

    public bool Matches(int group, OracleRoomData room) =>
        group == _record.Group && room.Id == _record.Room;

    public void Start(OracleRoomData room)
    {
        if (room.Id != _record.Room || Context.Rooms.ActiveGroup != _record.Group)
        {
            throw new InvalidOperationException(
                $"INTERAC_CHEVAL cannot initialize in " +
                $"{Context.Rooms.ActiveGroup:x}:{room.Id:x2}.");
        }

        NpcCharacter npc = Context.RequireNpc(
            _record.Group,
            _record.Room,
            _record.InteractionId,
            _record.SubId,
            "INTERAC_CHEVAL");
        ChevalCharacter cheval = npc as ChevalCharacter ??
            throw new InvalidOperationException(
                "Room 2:0f instantiated INTERAC_CHEVAL without its native actor.");
        StartInfiniteScript(
            cheval,
            _database.Commands,
            _record.InitialScriptUpdates);
    }

    public override void UpdateFrame()
    {
        AdvanceInfiniteScript();
        ScriptActor?.AdvanceCheval(Context.Player);
    }

    public void UpdateDuringDialogueFrame() =>
        ScriptActor?.AdvanceCheval(Context.Player);

    public override bool MemoryEquals(string binding, int value)
    {
        if (binding != "HasChevalRope" || value != 1)
        {
            throw new InvalidOperationException(
                $"cheval_subid00Script cannot compare '{binding}' with ${value:x2}.");
        }
        return Context.Inventory.HasTreasure(_record.ChevalRopeTreasure);
    }

    public override void ShowText(int textId, string message)
    {
        if (textId is not (0x270c or 0x270d) ||
            message != _database.DialogueText(textId))
        {
            throw new InvalidOperationException(
                $"cheval_subid00Script requested invalid TX_{textId:x4} payload.");
        }
        Context.ShowDialogue(message);
    }

    public override void SetActorCollisionRadii(
        string actor,
        int radiusY,
        int radiusX)
    {
        // initcollisions first installs the common $06/$06 radii; the next
        // source command replaces only this actor's Y radius with $0c.
        if (radiusY == NpcCharacter.CollisionRadius &&
            radiusX == NpcCharacter.CollisionRadius)
        {
            RequireScriptActor(actor).SetCollisionRadii(radiusY, radiusX);
            return;
        }
        if (radiusY != _record.CollisionRadiusY ||
            radiusX != _record.CollisionRadiusX)
        {
            throw new InvalidOperationException(
                $"cheval_subid00Script initialized unexpected collision radii " +
                $"${radiusY:x2}/${radiusX:x2}.");
        }
        RequireScriptActor(actor).SetCollisionRadii(radiusY, radiusX);
    }

    public override void SetGlobalFlag(int flag)
    {
        if (flag != _record.TalkedGlobalFlag)
        {
            throw new InvalidOperationException(
                $"cheval_subid00Script cannot set global flag ${flag:x2}.");
        }
        base.SetGlobalFlag(flag);
    }
}
