using Godot;
using System;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public partial class ValidationRoot
{
    private void ValidateCrownPatternHint()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var input = (ApplicationInputBuffer)typeof(GameRoot).GetField("_applicationInput",flags)!.GetValue(this)!;
        var scheduler = (ApplicationFixedUpdateScheduler)typeof(GameRoot).GetField("_applicationUpdates",flags)!.GetValue(this)!;
        var update = (Action)typeof(GameRoot).GetMethod("AdvanceApplicationUpdate",flags)!.CreateDelegate(typeof(Action),this);
        var setTrigger = (Action<int,bool>)typeof(RoomEntityManager).GetMethod("SetTrigger",flags)!.CreateDelegate(typeof(Action<int,bool>),_entities);
        int[] positions = [0x5c,0x6a,0x3b,0x5a,0x4c,0x7b];
        int[] colors = [0xad,0xad,0xae,0xae,0xaf,0xaf];
        var data = new CrownDungeonDatabase().PatternHint;
        for (int i = 0; i < 6; i++)
            FailIf(data[i].Position != positions[i] || data[i].ShowTile != colors[i] || data[i].RestoreTile != 0xa0,
                "INTERAC $21:$16 must retain source write order, colors and standard floor $a0.");
        foreach (bool batch in new[] { false,true })
        {
            void Step(int count = 1,Vector2 move = default)
            {
                input.CaptureForValidation([],[],move);
                if (batch) scheduler.Advance(count / 60.0,update);
                else for (int i = 0; i < count; i++) scheduler.Advance(1.0 / 60.0,update);
            }
            void CheckTiles(bool shown)
            {
                for (int i = 0; i < 6; i++)
                    FailIf(_currentRoom.GetMetatile(new((positions[i] & 15) * 16 + 8,(positions[i] >> 4) * 16 + 8)) != (shown ? colors[i] : 0xa0),
                        $"Crown hint tile ${positions[i]:x2} must reflect its source {(shown ? "shown" : "restored")} value.");
            }
            LoadValidationRoom(4,0xa5); _player.WarpTo(new(184,120));
            FailIf(_currentRoom.IsSolid(_player.Position),"Crown button approach must start on open floor.");
            var hint = _entities.Entities<DungeonPatternHintRoomEntity>().Single();
            FailIf(_entities.InteractionSlot(hint) != 3 || hint.Showing,"Crown hint must occupy source order1 and start inactive.");
            Step();
            for (int repeat = 0; repeat < 2; repeat++)
            {
                for (int i = 0; !hint.Showing && i < 40; i++)
                {
                    Step(move:Vector2.Up);
                    FailIf(_currentRoom.IsSolid(_player.Position),"Crown button approach entered solid geometry.");
                }
                FailIf(!hint.Showing || (_entities.ActiveTriggers & 1) == 0 || _rooms.PendingTileGraphics != 3,
                    "Button tile plus six hint writes must leave three entries after the four-entry graphics drain.");
                CheckTiles(true);
                Step(32); CheckTiles(true);
                FailIf(_rooms.PendingTileGraphics != 0,"Held hint must not repeatedly enqueue tiles.");
                for (int i = 0; hint.Showing && i < 24; i++) Step(move:Vector2.Down);
                FailIf(hint.Showing || (_entities.ActiveTriggers & 1) != 0 || _rooms.PendingTileGraphics != 3,
                    "Button release plus six restored hint cells must enqueue seven writes in the same update.");
                CheckTiles(false); Step(32);
            }
            setTrigger(3,true); Step(); CheckTiles(true);
            var text = _entities.TextActiveSource;
            try
            {
                _entities.TextActiveSource = () => true;
                setTrigger(3,false); Step(4);
                FailIf(!hint.Showing,"Text must freeze the initialized state1 hint even after its trigger clears.");
            }
            finally { _entities.TextActiveSource = text; }
            Step(); CheckTiles(false);
            setTrigger(3,true); Step();
            LoadValidationRoom(0,0x60); Step(4);
            LoadValidationRoom(4,0xa5); Step();
            FailIf(_entities.Entities<DungeonPatternHintRoomEntity>().Single().Showing || _entities.ActiveTriggers != 0,
                "Reload must cancel the transient hint and shared triggers.");
            CheckTiles(false);
        }
        LoadValidationRoom(0,0x60);
        GD.Print("Validated Crown hint real button approach, hold/release/repeat, ordered tile queue, nonzero trigger gate, text freeze and reload cancellation.");
    }
}
