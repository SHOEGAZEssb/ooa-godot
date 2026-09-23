using Godot;
using System;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public partial class ValidationRoot
{
    private void ValidateCrownPatternChests()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var input = (ApplicationInputBuffer)typeof(GameRoot).GetField("_applicationInput",flags)!.GetValue(this)!;
        var scheduler = (ApplicationFixedUpdateScheduler)typeof(GameRoot).GetField("_applicationUpdates",flags)!.GetValue(this)!;
        var update = (Action)typeof(GameRoot).GetMethod("AdvanceApplicationUpdate",flags)!.CreateDelegate(typeof(Action),this);
        var data = new DungeonChestPatternDatabase();
        static Vector2 Point(int packed) => new((packed & 15) * 16 + 8,(packed >> 4) * 16 + 8);
        foreach (var test in new[] {
            (Room:0x9b, Sub:0x13, Chest:0x54, Positions:new[] { 0x47,0x48,0x49,0x57,0x59,0x67,0x68,0x69 },
                Tiles:new[] { 0x2c,0x2d,0x2e,0x2c,0x2d,0x2e,0x2c,0x2e }),
            (Room:0x9e, Sub:0x14, Chest:0x47, Positions:new[] { 0x45,0x49 },Tiles:new[] { 0x2a,0x2a }),
            (Room:0xa5, Sub:0x15, Chest:0x53, Positions:new[] { 0x54,0x62,0x33,0x52,0x44,0x73 },
                Tiles:new[] { 0x2c,0x2c,0x2d,0x2d,0x2e,0x2e }) })
        foreach (bool batch in new[] { false,true })
        {
            void Step()
            {
                input.CaptureForValidation([],[],Vector2.Zero);
                scheduler.Advance(1.0 / 60.0,update);
            }
            _saveData.SetRoomFlag(4,test.Room,0x20,false);
            LoadValidationRoom(4,test.Room); _player.BeginCutsceneControl();
            var cells = data.Cells(test.Sub);
            FailIf(cells.Count != test.Positions.Length,"Crown pattern lost source cell count.");
            for (int i = 0; i < cells.Count; i++)
            {
                int min = test.Sub == 0x13 ? 0x2c : test.Tiles[i], max = test.Sub == 0x13 ? 0x2e : test.Tiles[i];
                FailIf(cells[i].Position != test.Positions[i] || cells[i].MinimumTile != min || cells[i].MaximumTile != max,
                    $"INTERAC $21:${test.Sub:x2} cell{i} must retain source order and tile condition.");
                _currentRoom.SetPositionTileAndCollision(Point(test.Positions[i]),(byte)test.Tiles[i],null,0);
            }
            FailIf(!data.Matches(test.Sub,_currentRoom),"Source Crown solution must satisfy its imported predicate.");
            for (int i = 0; i < cells.Count; i++)
            {
                foreach (int invalid in new[] { cells[i].MinimumTile - 1,cells[i].MaximumTile + 1 })
                {
                    _currentRoom.SetPositionTileAndCollision(Point(test.Positions[i]),(byte)invalid,null,0);
                    FailIf(data.Matches(test.Sub,_currentRoom),$"Crown pattern must reject wrong tile ${invalid:x2} at ${test.Positions[i]:x2}.");
                }
                _currentRoom.SetPositionTileAndCollision(Point(test.Positions[i]),(byte)test.Tiles[i],null,0);
            }
            var actor = _entities.Entities<DungeonPuzzleChestRoomEntity>().Single();
            FailIf(_entities.InteractionSlot(actor) != 2,"Crown pattern reward must retain source interaction order0.");
            int last = test.Positions.Length - 1;
            _currentRoom.SetPositionTileAndCollision(Point(test.Positions[last]),0xa3,null,0);
            Step();
            FailIf(actor.Finished,"Incomplete Crown pattern must not spawn a chest.");
            _sound.ClearPlayRequestAudit();
            _currentRoom.SetPositionTileAndCollision(Point(test.Positions[last]),(byte)test.Tiles[last],null,0);
            if (batch)
            {
                input.CaptureForValidation([],[],Vector2.Zero);
                scheduler.Advance(2.0 / 60.0,update);
            }
            else { Step(); Step(); }
            FailIf(!actor.Finished || _currentRoom.GetMetatile(Point(test.Chest)) != 0xf1 ||
                _sound.PlayRequestsFor(OracleSoundEngine.SndSolvePuzzle) != 1,
                "Completed Crown pattern must immediately spawn its chest and play the solve cue only once.");
            _saveData.SetRoomFlag(4,test.Room,0x20,true);
            LoadValidationRoom(4,test.Room); Step();
            FailIf(_entities.Entities<DungeonPuzzleChestRoomEntity>().Count != 0,
                "Collected Crown pattern reward must retire without checking the pattern.");
            _saveData.SetRoomFlag(4,test.Room,0x20,false);
        }
        LoadValidationRoom(0,0x60);
        GD.Print("Validated three Crown pattern chest conditions, every required tile, native order, immediate reward and item-flag retirement.");
    }
}
