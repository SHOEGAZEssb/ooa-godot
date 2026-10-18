using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateScriptEnemyAdmissionRom()
    {
        foreach (bool maku in new[] { true, false })
        foreach (bool batched in new[] { false, true })
        foreach (int free in maku ? new[] { 0, 1, 2 } : new[] { 0, 1, 2, 3 })
        {
            ReinitializeGameplayForValidation();
            NpcCharacter[] actors;
            if (maku)
            {
                _saveData.SetGlobalFlag(GlobalFlag.MakuTreeSaved, false); _saveData.SetMakuTreeState(1);
                _saveData.SetRoomFlag(1, 0x38, 0x80, false); LoadValidationRoom(1, 0x38);
                var rescue = _roomEvents.Get<MakuSproutRescueEvent>();
                actors = new[] { SomariaPrivate<NpcCharacter>(rescue, "_leftMoblin"), SomariaPrivate<NpcCharacter>(rescue, "_rightMoblin") };
                SomariaPrivate<CutsceneCommandRunner>(rescue, "_sproutRunner").Clear();
                SomariaPrivate<CutsceneCommandRunner>(rescue, "_controllerRunner").Clear();
                // Declared terminal script positions retain the actual event
                // lanes/commands and application update, without a long rescue.
                SomariaPrivate<CutsceneCommandRunner>(rescue, "_leftRunner").Start(rescue.Database.MoblinLeft, 18);
                SomariaPrivate<CutsceneCommandRunner>(rescue, "_rightRunner").Start(rescue.Database.MoblinRight, 19);
            }
            else
            {
                var rescue = _roomEvents.Get<MooshRescueEvent>(); var record = rescue.Database.Record;
                _saveData.WriteWramByte(record.EssenceAddress, (byte)(_saveData.ReadWramByte(record.EssenceAddress) | record.EssenceMask));
                _saveData.SetRoomFlag(record.FlagGroup, record.FlagRoom, (byte)record.FlagMask);
                _saveData.WriteWramByte(record.MooshStateAddress, 0); _inventory.LoseTreasure(record.ChevalRopeTreasure);
                LoadValidationRoom(0, 0x6c);
                var bindings = SomariaPrivate<System.Collections.Generic.Dictionary<string, NpcCharacter>>(rescue, "_actors");
                actors = Enumerable.Range(0, 3).Select(index => bindings[$"Ghini{index}"]).ToArray();
                var lanes = SomariaPrivate<CutsceneCommandLaneScheduler>(rescue, "_lanes"); lanes.Clear();
                var commands = new[] { rescue.Database.Ghini0, rescue.Database.Ghini1, rescue.Database.Ghini2 };
                for (int i = 0; i < 3; i++)
                {
                    int first = Enumerable.Range(0, commands[i].Count).Single(index => commands[i][index] is CutsceneNativeCommand native && native.Handler == $"SpawnEnemyGhini{i}");
                    lanes.StartLane($"Ghini{i}", commands[i], firstCommand: first);
                }
            }
            _inventory.SetScriptedEquippedItems(0, 0); _player.BeginCutsceneControl();
            var native = new SomariaRom(_saveData, _random.CaptureState(), _currentRoom, 0, (int)_player.Position.X, (int)_player.Position.Y);
            foreach (int slot in F8Enemies.Values) { native[0xd080 + slot * 256] = 1; native[0xd084 + slot * 256] = 8; }
            int roomCount = _entities.RoomEnemyCount; native[0xcdd1] = (byte)roomCount; F8Fill(native, free);
            for (int i = 0; i < actors.Length; i++)
            {
                int page = 0xd2 + i, at = page * 256 + 0x40;
                native[at] = 1; native[at + 11] = (byte)actors[i].Position.Y; native[at + 13] = (byte)actors[i].Position.X;
                native[0xffae] = 0x40; native[0xffaf] = (byte)page;
                // Execute the actual checked native producer. The script
                // opcode takes its byte-stream pointer from the stack.
                byte[] caller = maku ? [0x16, (byte)page, 0xcd, 0x92, 0x75, 0xc9] :
                    [0x16, (byte)page, 0x21, 0x00, 0xc2, 0xe5, 0xc3, 0xf4, 0x41];
                for (int index = 0; index < caller.Length; index++) native[0xc100 + index] = caller[index];
                native[0xc201] = 0x17; native[0xc202] = 0;
                SomariaPrivate<FrontendRom>(native, "_rom").Call(0xc100, maku ? 0x15 : 0x0c);
            }
            StepGameplayUpdates(1, Vector2.Zero, batched: batched);
            int enemyId = maku ? 0x20 : 0x17;
            var children = _entities.EntityAdapters<IRoomEntity>().Where(actor => maku ? actor.Node is MaskedMoblinCharacter : actor.Node is GhiniCharacter).ToArray();
            int[] sourceSlots = Enumerable.Range(0, 16).Where(slot => native[0xd080 + slot * 256] != 0 && native[0xd081 + slot * 256] == enemyId).ToArray();
            FailIf(children.Length != free || sourceSlots.Length != free || actors.Any(actor => actor.Active) ||
                children.Any(child => !F8Enemies.ContainsKey(child)) || _entities.RoomEnemyCount != native[0xcdd1],
                $"Scripted ENEMY${enemyId:x2} must skip full capacity while retiring its interaction actor: free={free}, runtime={children.Length}, native={sourceSlots.Length}.");
            for (int i = 0; i < children.Length; i++)
                FailIf(F8Enemies[children[i]] != sourceSlots[i] || children[i].Node.Position != new Vector2(native[0xd08d + sourceSlots[i] * 256], native[0xd08b + sourceSlots[i] * 256]),
                    "Script enemy admission must retain the source physical allocation order and copied high coordinates.");
            if (free == 0) _entities.RetireEntity(F8Enemies.First(pair => pair.Key is EnemyAiSlotReservation).Key);
            StepGameplayUpdates(3, Vector2.Zero, batched: batched);
            FailIf(_entities.EntityAdapters<IRoomEntity>().Count(actor => maku ? actor.Node is MaskedMoblinCharacter : actor.Node is GhiniCharacter) != free,
                "Completing the source wait/scriptend must not retry a rejected scripted enemy spawn.");
            _roomEvents.Get<MakuSproutRescueEvent>().Cancel(); _roomEvents.Get<MooshRescueEvent>().Cancel();
            _entities.Clear(); FailIf(F8Enemies.Count != 0, "Rescue cancellation must release every admitted enemy and reservation.");
        }
        ReinitializeGameplayForValidation();
        GD.Print("Compared Maku's two and Moosh's three checked script enemy spawns with original producers, partial capacity and no-retry completion.");
    }
}
