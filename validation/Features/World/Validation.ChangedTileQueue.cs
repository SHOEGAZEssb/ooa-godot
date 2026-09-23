using System.Collections.Generic;
using System.Linq;
using System;
using System.Reflection;

namespace oracleofages;

public partial class ValidationRoot
{
    private void ValidateChangedTileQueue()
    {
        var queue = new ChangedTileQueue();
        var logical = new List<ChangedTileWrite>();
        var graphics = new List<ChangedTileWrite>();
        for (int i = 0; i < 31; i++)
            FailIf(!queue.TryWrite(0x11,(byte)i,logical.Add),"setTile must accept31 entries, including repeated positions.");
        FailIf(queue.TryWrite(0x12,0xff,logical.Add) || logical.Count != 31 || graphics.Count != 0 || queue.Count != 31,
            "The32nd setTile must fail without changing terrain or queue state; graphics must remain deferred.");
        foreach (byte mode in new byte[] { 2,4,8,0x0e })
            queue.UpdateGraphics(mode,graphics.Add);
        FailIf(queue.Count != 31 || graphics.Count != 0,"Scroll-mode bits$0e must suppress the changed-tile drain.");
        queue.UpdateGraphics(1,graphics.Add);
        FailIf(queue.Count != 27 || !graphics.SequenceEqual(logical.Take(4)),
            "Normal scroll mode$01 must drain exactly four ordered writes, retaining intermediate repeated-position values.");
        for (int i = 0; i < 4; i++)
            FailIf(!queue.TryWrite(0x12,(byte)(0x80+i),logical.Add),"Freed ring entries must accept wrapped writes.");
        FailIf(queue.TryWrite(0x13,0xff,logical.Add),"Wrapped queue must retain its31-entry capacity.");
        for (int i = 0; i < 8; i++) queue.UpdateGraphics(0,graphics.Add);
        FailIf(queue.Count != 0 || !graphics.SequenceEqual(logical),"Draining a wrapped queue must retain every accepted write in order.");
        queue.UpdateGraphics(0x10,graphics.Add);
        FailIf(graphics.Count != 35,"An empty queue must not replay stale entries.");

        // INTERAC$33's source cursor retries against real queue saturation.
        for (int i = 0; i < 31; i++) queue.TryWrite(0x22,0xa3,logical.Add);
        var sequence = new SmogTileSequence();
        sequence.BeginGeneration(new SmogControllerDatabase().Phases[0],new(120,88));
        int puffs = 0, accepted = logical.Count;
        void Step(int count)
        {
            for (int i = 0; i < count; i++)
                sequence.Update(_ => 0,(position,tile) => queue.TryWrite((byte)position,(byte)tile,logical.Add),_ => puffs++);
        }
        Step(5);
        FailIf(logical.Count != accepted || puffs != 0 || sequence.Counter != 5,
            "Smog generation must retain its first source pair and reset the five-update delay on queue-full failure.");
        queue.UpdateGraphics(1,graphics.Add);
        Step(4);
        FailIf(logical.Count != accepted,"Queue capacity becoming available must not shorten Smog's retry interval.");
        Step(1);
        FailIf(logical.Count != accepted + 1 || logical[^1] != new ChangedTileWrite(0x11,0x0c) || puffs != 1,
            "Smog's fifth retry update must accept the original tile and emit exactly one puff.");
        foreach (int room in new[] { 0xbf,0xbb })
        {
            LoadValidationRoom(4,room); _entities.Clear();
            var position = new Godot.Vector2(24,24);
            byte[] Snapshot() => Enumerable.Range(0,4).SelectMany(i => new byte[] {
                _currentRoom.GetBackgroundSubtileForValidation(2+i%2,2+i/2),
                _currentRoom.GetBackgroundAttributeForValidation(2+i%2,2+i/2) }).ToArray();
            _currentRoom.SetPositionTileAndCollision(position,0x1d,null,0);
            byte[] block = Snapshot();
            byte blockCollision = _currentRoom.GetTerrainInfo(position).Collision;
            _currentRoom.SetPositionTileAndCollision(position,0xa3,null,0);
            byte[] floor = Snapshot();
            byte floorCollision = _currentRoom.GetTerrainInfo(position).Collision;
            FailIf(block.SequenceEqual(floor) || blockCollision == floorCollision,
                "Crown tile fixture must distinguish block$1d and floor$a3 in both collision and graphics.");
            var liveQueue = new ChangedTileQueue();
            // Five writes to one position: first drain ends on block, while
            // the final floor has already become authoritative terrain.
            foreach (byte tile in new byte[] { 0x1d,0xa3,0xa3,0x1d,0xa3 })
                liveQueue.TryWrite(0x11,tile,write => _currentRoom.SetTileWithoutGraphicsReload(write,0));
            FailIf(!Snapshot().SequenceEqual(floor) || _currentRoom.GetTerrainInfo(position).Collision != floorCollision,
                "Queued logical writes must preserve old graphics and immediately replace collision.");
            liveQueue.UpdateGraphics(1,write => _currentRoom.ApplyQueuedTileGraphics(write,0));
            FailIf(!Snapshot().SequenceEqual(block) || _currentRoom.GetTerrainInfo(position).Collision != floorCollision ||
                _currentRoom.GetTerrainInfo(position).Tile != 0xa3,
                "Fourth queued graphic must display block$1d without reverting the fifth write's floor$a3 terrain.");
            liveQueue.UpdateGraphics(1,write => _currentRoom.ApplyQueuedTileGraphics(write,0));
            FailIf(!Snapshot().SequenceEqual(floor) || liveQueue.Count != 0,
                "Next drain must display the remaining floor write and empty the queue.");
        }
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var input = (ApplicationInputBuffer)typeof(GameRoot).GetField("_applicationInput",flags)!.GetValue(this)!;
        var scheduler = (ApplicationFixedUpdateScheduler)typeof(GameRoot).GetField("_applicationUpdates",flags)!.GetValue(this)!;
        var update = (Action)typeof(GameRoot).GetMethod("AdvanceApplicationUpdate",flags)!.CreateDelegate(typeof(Action),this);
        foreach (bool batch in new[] { false,true })
        {
            LoadValidationRoom(4,0xbf); _entities.Clear();
            _player.WarpTo(new(120,88));
            void GameplayStep(int count)
            {
                input.CaptureForValidation([],[],Godot.Vector2.Zero);
                if (batch) scheduler.Advance(count / 60.0,update);
                else for (int i = 0; i < count; i++) scheduler.Advance(1.0 / 60.0,update);
            }
            for (int i = 0; i < 9; i++)
                FailIf(!_rooms.TrySetTile(0x11,(byte)(i % 2 == 0 ? 0xa3 : 0x1d)),"Session queue must accept the tile write.");
            var preload = _rooms.GetRoom(4,0xbb);
            FailIf(_rooms.PendingTileGraphics != 9,"Preloading room data must not consume or clear the active changed-tile queue.");
            GameplayStep(2);
            FailIf(_rooms.PendingTileGraphics != 1,"Two gameplay updates must drain eight writes, including within one host frame.");
            GameplayStep(1);
            FailIf(_rooms.PendingTileGraphics != 0,"The next gameplay update must drain the ninth write.");
            _rooms.TrySetTile(0x11,0x1d);
            _rooms.SetLoadedRoom(4,preload);
            FailIf(_rooms.PendingTileGraphics != 0,"Committing a preloaded room must discard the previous room's pending writes.");
            _rooms.TrySetTile(0x11,0x1d);
            _rooms.Load(4,0xbf);
            FailIf(_rooms.PendingTileGraphics != 0,"Full room load must clear changed-tile queue indices.");
            _rooms.TrySetTile(0x11,0x1d);
            _rooms.LoadCutsceneRoom(4,0xbb);
            FailIf(_rooms.PendingTileGraphics != 0,"disableLcdAndLoadRoom must also clear changed-tile queue indices.");
        }
        foreach (bool fullQueue in new[] { false,true })
        {
            _saveData.SetRoomFlag(4,0xbf,0x80,false);
            LoadValidationRoom(4,0xbf);
            _player.WarpTo(new(120,136));
            _currentRoom.SetPositionTileAndCollision(new(24,24),0x0c,null,0);
            var cloud = _entities.Spawn<SmogCharacter>(new SmogEnemySpawn(new(120,88),3));
            input.CaptureForValidation([],[],Godot.Vector2.Zero);
            scheduler.Advance(4 / 60.0,update);
            if (fullQueue)
                for (int i = 0; i < 31; i++) _rooms.TrySetTile(0x22,0xa3);
            scheduler.Advance(1 / 60.0,update);
            FailIf(cloud.SubId != 4 || _currentRoom.GetTerrainInfo(new(24,24)).Tile != (fullQueue ? 0x0c : 0xa3) ||
                _rooms.PendingTileGraphics != (fullQueue ? 27 : 0),
                "Large Smog initialization must use the session queue, ignore a full-queue failure, and allow the later four-entry graphics drain.");
        }
        LoadValidationRoom(0,0x60);
        Godot.GD.Print("Validated native changed-tile capacity, wrap/order, immediate terrain, four-entry graphics drain and Smog saturation retry.");
    }
}
