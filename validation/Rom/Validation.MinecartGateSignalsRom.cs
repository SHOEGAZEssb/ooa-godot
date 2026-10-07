using Godot;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void CompareMinecartGateSignalsRom()
    {
        int fixture = 0;
        foreach (int room in new[] { 0x2f, 0x3b, 0x72, 0x78 })
        foreach (int pause in new[] { 0, 1, 2 })
        foreach (bool batched in RomHostSchedules(fixture++))
        {
            ReinitializeGameplayForValidation(); LoadValidationRoom(4, room); _entities.Clear();
            _inventory.EquipA(0); _inventory.EquipB(0);
            var records = (room < 0x70 ? new WingDungeonDatabase().GetRoomRecords(4, room) :
                new SkullDungeonDatabase().GetRoomRecords(4, room))
                .Where(row => row.Id == 0x1b || row.Id == 0x21 && row.SubId is 7 or 8).OrderBy(row => row.Order).ToArray();
            var gateRecord = records.Single(row => row.Id == 0x1b);
            var sensorRecord = records.Single(row => row.Id == 0x21);
            Vector2 safe = Enumerable.Range(1, _currentRoom.HeightInTiles - 2).SelectMany(y =>
                Enumerable.Range(1, _currentRoom.WidthInTiles - 2).Select(x => new Vector2(x * 16 + 8, y * 16 + 8)))
                .First(point => point.DistanceTo(gateRecord.Position) > 48 && !_collision.Collides(point) &&
                    _currentRoom.GetTerrainInfo(point + Vector2.Down * 5).Hazard == HazardType.None);
            _player.WarpTo(safe); _player.Face(Vector2I.Up);
            var seed = _random.CaptureState();
            var rom = new SomariaRom(_saveData, seed, _currentRoom, 0, (int)safe.X, (int)safe.Y);
            rom.InitializeLinkGameplay(); rom.InitializeLinkWalkingAnimation();
            var data = new DungeonInteractionDatabase();
            var puzzle = new ColoredCubePuzzleState(_runtimeState, data.Constant("red-pushable-block"));
            var visuals = new DungeonInteractionVisualDatabase();
            MinecartGateRoomEntity gate = null!; int address = 0;
            int mask = 1 << (gateRecord.SubId & 7);
            int packed = ((gateRecord.Y & 0xf0) | (gateRecord.X >> 4)) - ((gateRecord.SubId >> 4) == 0 ? 1 : 0);
            Vector2 gatePoint = new((packed & 15) * 16 + 8, (packed >> 4) * 16 + 8);
            ulong background = OracleGraphicsCache.PixelHash(_currentRoom.BuildMimickedMetatileTexture(gatePoint).GetImage());
            foreach (var record in records)
            {
                IRoomEntity actor = record.Id == 0x1b ? gate = new MinecartGateRoomEntity(record, () => _entities.ActiveRoom,
                    _runtimeState, visuals.Visual("minecart-gate"), _entities.OnSoundRequested, _entities.OnRoomTileChanged, () => 0) :
                    new DungeonStateController(record, () => _entities.ActiveRoom, data, puzzle, _runtimeState, _entities.SetTrigger);
                _entities.AddEntity(actor);
                int slot = (0xd0 + _entities.InteractionSlot(actor.Node)) * 256 + 0x40;
                rom[slot] = 1; rom[slot + 1] = (byte)record.Id; rom[slot + 2] = (byte)record.SubId;
                rom[slot + 0xb] = (byte)record.Y; rom[slot + 0xd] = (byte)record.X;
                if (record.Id == 0x1b) address = slot;
            }
            FailIf(gate.Visible || SomariaPrivate<bool>(gate, "_initialized"), "Gate allocation must leave native state0 pending without changing collisions.");
            var sounds = _sound.AttachPlayRequestAudit();
            var disabled = _entities.InitializedObjectsDisabledSource;
            void Input(bool blue)
            {
                if (sensorRecord.SubId == 7)
                    _currentRoom.SetPositionTileAndCollision(new((sensorRecord.Y & 15) * 16 + 8, (sensorRecord.Y >> 4) * 16 + 8), blue ? (byte)0xaf : (byte)0xad, null, 0);
                else { puzzle.CubeColor = blue ? 0x82 : 0x80; rom[0xccad] = (byte)puzzle.CubeColor; }
                rom.CopyRoom(_currentRoom);
            }
            void Pause(bool active)
            {
                if (pause == 1) { if (active) _dialogue.ShowMessage("Declared gate pause.", 100); else _dialogue.Close(); rom[0xcba0] = active ? (byte)1 : (byte)0; }
                if (pause == 2) { _entities.InitializedObjectsDisabledSource = active ? () => true : disabled; rom[0xcc8a] = active ? (byte)2 : (byte)0; }
            }
            int update = 0;
            void Step(int count = 1) => StepSomariaMotionRom(rom, count, batched, afterUpdate: () =>
            {
                CompareMinecartGateStateRom(gate, rom, address, $"room${room:x2}, pause={pause}, update{++update}");
                FailIf(_runtimeState.ReadWramByte(OracleRuntimeState.SwitchStateAddress) != rom[0xcdd3] ||
                    puzzle.CubeColor != rom[0xccad] || !sounds.Requests.Where(cue => cue != SoundId.SndText).SequenceEqual(rom.Sounds) ||
                    OracleGraphicsCache.PixelHash(_currentRoom.BuildMimickedMetatileTexture(gatePoint).GetImage()) != background,
                    "Gate signals, ordered cues and original background rendering must agree with native raw layout/collision writes.");
                var random = _random.CaptureState();
                FailIf(random.Rng1 != rom[0xff94] || random.Rng2 != rom[0xff95] || random.Calls - seed.Calls != rom.RandomCalls,
                    "Gate dispatch must preserve shared RNG.");
            });
            try
            {
                _runtimeState.SetWramByte(OracleRuntimeState.SwitchStateAddress, (byte)(0x80 | mask)); rom[0xcdd3] = (byte)(0x80 | mask);
                Input(false); Pause(true); Step();
                bool sensorFirst = sensorRecord.Order < gateRecord.Order;
                FailIf(gate.Open != sensorFirst || gate.Animating || gate.CurrentAnimationIndex != ((gateRecord.SubId >> 4) | (sensorFirst ? 0 : 1)),
                    "Initial gate state must sample the ordered sensor publication, using the opposite static animation.");
                Step(3); Pause(false); Step(18);
                FailIf(!gate.Open || gate.Animating, "The later sensor must open its previously initialized gate on an eligible subsequent pass.");
                Input(true); Step();
                if (!sensorFirst) Step();
                FailIf(gate.Open || !gate.Animating || gate.CurrentAnimationFrame != 0,
                    "Blue publication must close gate collisions on the first consuming pass and start frame zero.");
                Input(false); Pause(true); Step(3); Pause(false); Step(pause == 0 ? 12 : 15);
                FailIf(gate.Open || !gate.Animating || gate.CurrentAnimationFrame != 1,
                    "A closing gate must ignore reversed switch input until native 8/8 animation completion.");
                Step(); FailIf(gate.Open || gate.Animating || gate.CurrentAnimationFrame != 2,
                    "The sixteenth animation update completes closing without consuming the deferred reverse input.");
                Step(); FailIf(!gate.Open || !gate.Animating, "The following pass must begin the deferred opening once.");
                Step(16); Step(3); Input(true); Step(18);
                FailIf(gate.Open || gate.Animating, "The repeated signal must complete another physical closing transition.");
            }
            finally { _dialogue.Close(); _entities.InitializedObjectsDisabledSource = disabled; }
        }
    }

    private void CompareMinecartGateStateRom(MinecartGateRoomEntity gate, SomariaRom rom, int address, string context)
    {
        var animation = (EnemyAnimationPlayer)typeof(DungeonInteractionVisualEntity).GetField("_animation", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(gate)!;
        FailIf(!gate.Visible || gate.Open != ((rom[address + 0x30] & 1) != 0) || gate.Animating != (rom[address + 4] == 3) ||
            (!gate.Animating && rom[address + 4] != (gate.Open ? 2 : 1)) ||
            gate.ZIndex != ObjectDrawPriority.FromVisible(rom[address + 0x1a]) ||
            animation.CurrentParameter != rom[address + 0x21] || SomariaPrivate<int>(animation, "_frameCounter") != rom[address + 0x20],
            $"Gate$1b {context}: open={gate.Open}, animating={gate.Animating}, animation={gate.CurrentAnimationIndex}/{gate.CurrentAnimationFrame}, counter={SomariaPrivate<int>(animation, "_frameCounter")}/{rom[address + 0x20]}, parameter={animation.CurrentParameter}/{rom[address + 0x21]}, priority={gate.ZIndex}/{ObjectDrawPriority.FromVisible(rom[address + 0x1a])}, native state={rom[address + 4]}, var30=${rom[address + 0x30]:x2}.");
    }
}
