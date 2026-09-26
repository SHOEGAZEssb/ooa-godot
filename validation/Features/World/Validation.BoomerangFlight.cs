using Godot;
using System.Linq;

namespace oracleofages;

public partial class ValidationRoot
{
    private void ValidateBoomerangFlight()
    {
        var data = BoomerangDatabase.Shared;
        FailIf(data.Speed != 0x41 || data.OutwardUpdates != 40 || data.CatchUpdates != 4 ||
            data.NearRadius != 10 || data.CatchRadius != 2 || data.Sound != 0x78 || data.Collision != 0x97 ||
            data.Radius != new Vector2I(6, 6) || data.Damage != 2 || data.OamFlags != 0x0d || data.SourceInverted,
            "ITEM$06 must preserve the clean-US L1 speed/timers/radii/damage/graphics attributes.");
        var animation = OracleGraphicsCache.GetAnimationDefinition(data.Animation);
        FailIf(animation.Frames.Length != 4 || animation.LoopStart != 0 ||
            animation.Frames.Any(f => f.Duration != 2) ||
            !animation.Frames.Select(f => f.Parameter).SequenceEqual(new[] { 0, 0, 0, 1 }),
            "Boomerang rotation requires four two-update frames; only its fourth frame signals sound.");
        var parents = GeneratedTable.Load("res://assets/oracle/metadata/boomerang_parent_animations.tsv",
            new GeneratedTableSchema("boomerang parent fixture", GeneratedTableKeySemantics.Unique,
                ["mode", "frame", "duration", "graphic", "parameter", "source"], ["mode", "frame"], headerRequired: true));
        FailIf(parents.Rows.Count != 4, "Boomerang parent modes$21/$25 each require an eight-update pose and terminal frame.");
        int[] graphics = [0xb0, 0xb0, 0xcc, 0x58];
        for (int i = 0; i < 4; i++)
            FailIf(parents.Rows[i].HexByte(0) != (i < 2 ? 0x21 : 0x25) || parents.Rows[i].Decimal(1) != i % 2 ||
                parents.Rows[i].Decimal(2) != (i % 2 == 0 ? 8 : 127) || parents.Rows[i].HexByte(3) != graphics[i] ||
                parents.Rows[i].HexByte(4) != (i % 2 == 0 ? 6 : 0x86),
                "Boomerang parent source frames changed.");

        void Room()
        {
            LoadValidationRoom(4, 0xa8);
            _entities.Clear();
            _player.WarpTo(new(40, 80));
            for (int y = 8; y < 176; y += 16)
            for (int x = 8; x < 240; x += 16)
                _currentRoom.SetPositionTileAndCollision(new(x, y), 0xa0, 0, 0);
        }
        foreach (bool batched in new[] { false, true })
        {
            Room();
            void Step(int count = 1) => StepGameplayUpdates(count, Vector2.Zero, batched: batched);
            for (int repeat = 0; repeat < 2; repeat++)
            {
                _player.WarpTo(new(40, 80));
                var item = _entities.Spawn<BoomerangItem>(new BoomerangSpawn(new(56.25f, 80.5f), ObjectAngle.Right));
                var adapter = _entities.EntityAdapters<BoomerangRoomEntity>().Single();
                FailIf(_entities.DynamicItemSlotOf(adapter) != 0xd7 || item.Visible || item.CollisionEnabled,
                    "An uninitialized boomerang reserves native dynamic slot$d7 without showing or colliding.");
                _sound.ClearPlayRequestAudit();
                Step();
                FailIf(item.State != 1 || item.Counter != 40 || item.PrecisePosition != new Vector2(56.25f, 80.5f) ||
                    !item.Visible || !item.CollisionEnabled || item.Damage != 2,
                    "Boomerang state0 must initialize without moving or consuming its outward counter.");
                // Independently executed clean-US ITEM$06 at07:569d: these
                // are update numbers AFTER initialization, with Link(40,80).
                Step(39);
                FailIf(item.State != 1 || item.Counter != 1 || item.PrecisePosition != new Vector2(119.625f, 80.5f) ||
                    _sound.PlayRequestsFor(SoundId.SndBoomerang) != 5,
                    "39 outward updates must retain state1/counter1 and signal sound on updates7/15/23/31/39.");
                Step();
                FailIf(item.State != 2 || item.Counter != 0 || item.Angle != 24 || item.PrecisePosition.X != 118,
                    "Update40 must turn and move back immediately on the zero counter.");
                Step(42);
                FailIf(item.State != 2 || item.PrecisePosition.X != 49.75f,
                    "Update82 must still use the steering return state.");
                Step();
                FailIf(item.State != 3 || item.PrecisePosition.X != 48.125f,
                    "Update83 enters direct return inside the source ten-pixel window and still moves.");
                Step(4);
                FailIf(item.State != 3 || item.PrecisePosition.X != 41.625f, "Update87 remains visible before the catch check.");
                Step();
                FailIf(item.State != 4 || item.Counter != 4 || item.Visible || item.CollisionEnabled || item.Finished,
                    "Update88 catches without moving and retains the item slot for four updates.");
                _player.WarpTo(new(100, 100));
                Step(3);
                FailIf(item.Counter != 1 || item.Finished || item.PrecisePosition != new Vector2(100.625f, 100.5f) ||
                    _entities.DynamicItemSlotOf(adapter) != 0xd7,
                    "Catch delay must copy Link's coordinate high bytes, retaining item fractions and its allocation.");
                Step();
                FailIf(!item.Finished || _entities.Entities<BoomerangItem>().Count != 0,
                    "Update92 deletes the item and permits reuse on the next throw.");
            }
            foreach (bool full in new[] { false, true })
            {
                Room();
                _currentRoom.SetPositionTileAndCollision(new(104, 88), 0x2e, 0x0f, 0);
                if (full)
                    for (int i = 0; i < 14; i++) _entities.Spawn<PuzzlePuffEffect>(new PuzzlePuffSpawn(new(200, 120), SoundId.MusNone));
                var item = _entities.Spawn<BoomerangItem>(new BoomerangSpawn(new(95.5f, 80.5f), ObjectAngle.Right, -5));
                _sound.ClearPlayRequestAudit();
                Step(2);
                FailIf(item.State != 1 || item.PrecisePosition.X != 97.125f || _entities.Entities<ClinkEffect>().Count != 0,
                    "Outward tile collision samples before movement, allowing this update to enter the wall.");
                Step();
                var clinks = _entities.Entities<ClinkEffect>();
                FailIf(item.State != 2 || item.Angle != 24 || item.PrecisePosition.X != 95.5f ||
                    clinks.Count != (full ? 0 : 1) || _sound.PlayRequestsFor(SoundId.SndClink) != (full ? 0 : 1),
                    "Wall contact must reverse immediately; checked clink allocation controls sound without blocking return.");
                if (!full) FailIf(clinks[0].Position != new Vector2(97, 80) || clinks[0].ZHigh != -5 ||
                    clinks[0].ElapsedFrames != 1 || clinks[0].Flickers,
                    "Wall clink must copy high XYZ and initialize ordinary subid$00 in the later interaction pass.");
                _currentRoom.SetPositionTileAndCollision(new(88, 88), 0x2e, 0x0f, 0);
                Step(2);
                FailIf(item.State != 2 || item.PrecisePosition.X != 92.25f || _entities.Entities<ClinkEffect>().Count != (full ? 0 : 1),
                    "Returning boomerangs must cross terrain without repeated wall clinks.");
            }
            foreach (bool dialogue in new[] { true, false })
            {
                Room();
                var text = _entities.TextActiveSource;
                var freeze = _entities.NonInteractionObjectsDisabledSource;
                try
                {
                    if (dialogue) _entities.TextActiveSource = () => true;
                    else _entities.NonInteractionObjectsDisabledSource = () => true;
                    var item = _entities.Spawn<BoomerangItem>(new BoomerangSpawn(new(80.25f, 80.5f), ObjectAngle.Right));
                    Step(4);
                    FailIf(item.State != 1 || item.Counter != 40 || item.PrecisePosition.X != 80.25f,
                        "Restricted item dispatch must initialize state0 once, then freeze flight.");
                    item.QueueCollision();
                    Step(2);
                    FailIf(item.State != 1, "Pending contact must remain deferred while initialized items are frozen.");
                    _entities.TextActiveSource = text;
                    _entities.NonInteractionObjectsDisabledSource = freeze;
                    Step();
                    FailIf(item.State != 2 || item.Angle != 24 || item.Counter != 40 || item.PrecisePosition.X != 78.625f,
                        "Resuming a contact hit must return before terrain/counter processing.");
                    _entities.ClearPhysicalPlayerItems();
                    FailIf(_entities.Entities<BoomerangItem>().Count != 0 || !_entities.DynamicItemSlotAvailable,
                        "Native physical-item clearing must remove the boomerang and release its slot.");
                }
                finally { _entities.TextActiveSource = text; _entities.NonInteractionObjectsDisabledSource = freeze; }
            }
        }
        LoadValidationRoom(0, 0x60);
    }
}
