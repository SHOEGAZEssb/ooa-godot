using Godot;
using System;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public partial class ValidationRoot
{
    private void ValidateSmogPlacement()
    {
        var allRecords = new CrownDungeonDatabase().GetRoomRecords(4,0xbf);
        FailIf(allRecords.Count != 3 || allRecords[0].Kind != DungeonObjectKind.SmogController ||
            allRecords[0].Order != 0 || allRecords[0].Id != 0x33 || allRecords[0].SubId != 0xff ||
            allRecords[0].Position != new Vector2(120,88) || allRecords[0].Predicate != DungeonObjectCondition.Always,
            "$4:$bf must import its unconditional INTERAC$33:$ff at source order$00, position$58,$78.");
        FailIf(allRecords[1].Kind != DungeonObjectKind.BossReward || allRecords[1].Order != 1 ||
            allRecords[1].Id != 0x20 || allRecords[1].SubId != 1 || allRecords[1].Position != new Vector2(120,88) ||
            allRecords[1].Predicate != DungeonObjectCondition.Always,
            "$4:$bf must retain the unconditional $20:$01 reward at order$01; its script owns flag$80/item gating.");
        var records = allRecords.Where(record => record.Kind == DungeonObjectKind.SmogSentinel).ToList();
        FailIf(records.Count != 1 || records[0].Kind != DungeonObjectKind.SmogSentinel || records[0].Order != 4 ||
            records[0].Id != 0x7c || records[0].SubId != 5 || records[0].Position != new Vector2(120,88) ||
            records[0].Predicate != DungeonObjectCondition.Flag80Clear,
            "$4:$bf BeforeEvent must retain ENEMY$7c:$05 at main order$04, position$58,$78, gated by room flag$80.");
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var input = (ApplicationInputBuffer)typeof(GameRoot).GetField("_applicationInput",flags)!.GetValue(this)!;
        var scheduler = (ApplicationFixedUpdateScheduler)typeof(GameRoot).GetField("_applicationUpdates",flags)!.GetValue(this)!;
        var update = (Action)typeof(GameRoot).GetMethod("AdvanceApplicationUpdate",flags)!.CreateDelegate(typeof(Action),this);
        foreach (bool batch in new[] { false,true })
        {
            _saveData.SetRoomFlag(4,0xbf,0x80,false);
            LoadValidationRoom(4,0xbf);
            void Step(int count)
            {
                input.CaptureForValidation([],[],Vector2.Zero);
                if (batch) scheduler.Advance(count / 60.0,update);
                else for (int i = 0; i < count; i++) scheduler.Advance(1.0 / 60.0,update);
            }
            var sentinel = _entities.Entities<SmogCharacter>().Single();
            Step(1);
            FailIf(sentinel.State != 8 || sentinel.SubId != 5 || sentinel.Visible || sentinel.CollisionEnabled ||
                sentinel.Position != new Vector2(120,88) || sentinel.Speed != 1 || _entities.RoomEnemyCount != 1,
                "Ordinary room entry must initialize one hidden/noncolliding Smog sentinel and retain the native enemy count.");
            Step(8);
            FailIf(sentinel.IsDead || sentinel.State != 8 || _entities.RoomEnemyCount != 1,
                "Smog sentinel must remain counted and inert after initialization.");
            _saveData.SetRoomFlag(4,0xbf,0x80,true);
            LoadValidationRoom(4,0xbf);
            FailIf(_entities.Entities<SmogCharacter>().Count != 0 || _entities.RoomEnemyCount != 0,
                "Completed $4:$bf must suppress the BeforeEvent sentinel.");
        }
        _saveData.SetRoomFlag(4,0xbf,0x80,false);
        LoadValidationRoom(0,0x60);
        GD.Print("Validated Crown boss-room sentinel source placement, live initialization/count retention and completed-room suppression.");
    }
}
