using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateFountainFairies()
    {
        var random = new OracleRandom();
        using var fixture = RoomEntityValidationFixture.Attach(this, "FountainFairies",
            new() { Inventory = _inventory, SaveData = _saveData, Random = random });
        RoomEntityManager manager = fixture.Manager;
        var sounds = new List<int>();
        var texts = new List<int>();
        bool textOpen = false;
        int displayedHealth = 0;
        manager.SoundRequested += sounds.Add;
        manager.RoomEntityDialogueRequested += (id, text, position) =>
        {
            FailIf(string.IsNullOrWhiteSpace(text), $"Great Fairy TX_{id:x4} is empty.");
            texts.Add(id);
            textOpen = true;
        };
        manager.TextActiveSource = () => textOpen;
        manager.DisplayedHealthSource = () => displayedHealth;
        void Tick(int count = 1)
        {
            for (int i = 0; i < count; i++) manager.Update(1.0 / 60.0, _player);
        }
        FountainFairyRoomEntity Load(int group, int room)
        {
            textOpen = false;
            _player.WarpTo(new Vector2(0x18, 0x68), recordSafe: false);
            manager.LoadRoom(group, _world.LoadRoom(group, room));
            return manager.Entities<FountainFairyRoomEntity>().Single();
        }
        void Appear(FountainFairyRoomEntity fairy)
        {
            for (int i = 0; i < 60 && fairy.State < 3; i++) Tick();
            FailIf(fairy.State != 3 || !fairy.Visible,
                "ENEMY_GREAT_FAIRY $38 did not appear after INTERAC_PUFF $05:$02.");
        }

        foreach ((int group, int room, int y) in new[]
        {
            (2, 0x6e, 0x30), (2, 0xdf, 0x38), (2, 0xf8, 0x38),
            (3, 0x3f, 0x38), (3, 0xf6, 0x38)
        })
        {
            FountainFairyRoomEntity placed = Load(group, room);
            FailIf(placed.Position != new Vector2(0x50, y) || placed.Visible ||
                manager.Entities<EnemyCharacter>().Count != 0,
                $"Room {group}:{room:x2} lost noncombat ENEMY_GREAT_FAIRY $38:$00 placement.");
            Appear(placed);
        }

        _inventory.RefillHealth();
        FountainFairyRoomEntity fairy = Load(2, 0xdf);
        sounds.Clear();
        Tick();
        FailIf(fairy.State != 1 || sounds.Count != 0, "$38 state 0 must only initialize Z/state.");
        Tick();
        PuzzlePuffEffect puff = manager.Entities<PuzzlePuffEffect>().Single();
        FailIf(fairy.State != 2 || puff.Position != new Vector2(0x50, 0x28) ||
            puff.ElapsedUpdates != 1 || !sounds.SequenceEqual(new[] { 0x0f, 0x98 }),
            "$38 state 1 lost fountain music, puff position, sound, or same-pass interaction update.");
        Appear(fairy);
        foreach (Vector2 delta in new[] { new Vector2(-25, 16), new Vector2(25, 16),
            new Vector2(0, 15), new Vector2(0, 49) })
        {
            _player.WarpTo(fairy.Position + delta, recordSafe: false);
            Tick();
            FailIf(fairy.State != 3, "$38 proximity accepted an outside $31/$21 boundary.");
        }
        _player.WarpTo(fairy.Position + new Vector2(-24, 16), recordSafe: false);
        Tick();
        FailIf(fairy.State != 8 || texts.Last() != 0x4105 ||
            !_player.CutsceneControlled || !manager.PlayerMenusDisabled,
            "$38 full-health inclusive boundary must show TX_4105 and lock Link/menu.");
        int height = fairy.Height;
        Tick(60);
        FailIf(fairy.State != 8 || fairy.Height != height,
            "$38 ordinary enemy updates must freeze during dialogue.");
        textOpen = false;
        Tick(29);
        FailIf(fairy.State != 8 || !_player.CutsceneControlled, "$38 full-health wait ended before update 30.");
        Tick();
        FailIf(fairy.State != 9 || _player.CutsceneControlled ||
            manager.PlayerMenusDisabled || !sounds.Contains(0x91) ||
            manager.Entities<FountainFairyHeartRoomEntity>().Count != 0,
            "$38 update 30 must release input and begin its 60-update disappearance without hearts.");
        Tick(58);
        FailIf(fairy.Finished, "$38 disappeared before the final counter update.");
        Tick();
        FailIf(!fairy.Finished || manager.Entities<FountainFairyRoomEntity>().Count != 0,
            "$38 disappearance did not include the state-8 fallthrough decrement.");

        // Re-entry creates a fresh source object; healing never writes a room flag.
        _inventory.ApplyDamage(4);
        fairy = Load(2, 0xdf);
        Appear(fairy);
        _player.WarpTo(fairy.Position + new Vector2(24, 48), recordSafe: false);
        Tick();
        FailIf(fairy.State != 4 || texts.Last() != 0x4100,
            "$38 missing-health inclusive boundary must select TX_4100/state 4.");
        Tick(30);
        FailIf(fairy.HeartCount != 0, "$38 spawned hearts while text was open.");
        textOpen = false;
        int healthBefore = _player.HealthQuarters;
        for (int update = 1; update <= 108; update++)
        {
            Tick();
            int expected = Math.Min(update / 12, 8);
            FailIf(fairy.HeartCount != expected || _player.HealthQuarters != healthBefore,
                $"$38 healing update {update}: expected {expected} hearts without refill.");
            if (update == 12)
            {
                FountainFairyHeartRoomEntity heart = manager.Entities<FountainFairyHeartRoomEntity>().Single();
                FailIf(heart.Angle != 0 || heart.Position != _player.Position + new Vector2(0, -32),
                    "PART_GREAT_FAIRY_HEART $30 did not update at angle $00 in its spawn pass.");
            }
            if (update == 15)
            {
                FountainFairyHeartRoomEntity heart = manager.Entities<FountainFairyHeartRoomEntity>().Single();
                FailIf(heart.Angle != 31 || heart.Position != _player.Position + new Vector2(-7, -32),
                    "PART $30 lost signed fixed-point truncation at angle $1f.");
            }
        }
        FailIf(fairy.State != 6, "$38 ninth 12-update boundary must enter the 30-update healing wait.");
        Tick(29);
        FailIf(_player.HealthQuarters != healthBefore, "$38 healed one update early.");
        Tick();
        FailIf(fairy.State != 7 || _player.HealthQuarters != _player.MaxHealthQuarters,
            "$38 did not refill health at the state-6 zero update.");
        Tick(192);
        FailIf(fairy.HeartCount != 8 || fairy.State != 7,
            "PART $30 must wait for displayed hearts, even with full live health.");
        displayedHealth = _player.MaxHealthQuarters;
        for (int update = 0; update < 96 && fairy.HeartCount != 0; update++) Tick();
        FailIf(fairy.HeartCount != 0 || fairy.State != 7,
            "$38 must observe final part deletion on the following enemy pass.");
        Tick();
        FailIf(fairy.State != 8, "$38 did not enter its final hold after the last heart.");
        Tick(28);
        FailIf(fairy.State != 8, "$38 healing final hold ended early.");
        Tick();
        FailIf(fairy.State != 9 || _player.CutsceneControlled,
            "$38 healing final hold lost its same-update first decrement/input release.");

        fairy = Load(2, 0xdf);
        Appear(fairy);
        _player.WarpTo(fairy.Position + new Vector2(0, 16), recordSafe: false);
        Tick();
        manager.Clear();
        FailIf(_player.CutsceneControlled || manager.PlayerMenusDisabled,
            "$38 cancellation left Link or menus locked.");

        manager.LoadRoom(2, _world.LoadRoom(2, 0xde));
        manager.BeginScreenTransition(2, _world.LoadRoom(2, 0xdf), new Vector2(160, 0));
        fairy = manager.Entities<FountainFairyRoomEntity>().Single();
        textOpen = false;
        Tick(60);
        FailIf(fairy.State != 1 || fairy.Visible || manager.Entities<PuzzlePuffEffect>().Count != 0,
            "Room 2:df destination fairy advanced beyond state 0 during scrolling.");
        manager.FinishScreenTransition();
        Tick();
        FailIf(fairy.State != 2, "Room 2:df fairy did not resume after scrolling.");

        string SpawnPassTrace(bool batched)
        {
            _inventory.RefillHealth();
            _inventory.ApplyDamage(4);
            displayedHealth = 0;
            FountainFairyRoomEntity actor = Load(2, 0xdf);
            // Room loads deliberately preserve the global frame phase. Both
            // experiments must start from the same phase for sound comparison.
            manager.RestoreDebugStateAfterRoomParse(
                manager.CaptureDebugState() with { FrameCounter = 0 });
            Appear(actor);
            _player.WarpTo(actor.Position + new Vector2(0, 16), recordSafe: false);
            Tick();
            textOpen = false;
            sounds.Clear();
            long calls = random.Calls;
            if (batched) manager.Update(108.0 / 60.0, _player);
            else Tick(108);
            FailIf(random.Calls != calls, "$38 fountain/heart updates consumed extra global RNG.");
            return $"{actor.State}:{actor.Height}:{actor.HeartCount}:" +
                string.Join(',', sounds) + ":" +
                string.Join(';', manager.Entities<FountainFairyHeartRoomEntity>()
                    .Select(heart => $"{heart.Angle}:{heart.Position}"));
        }
        string split = SpawnPassTrace(false);
        string batched = SpawnPassTrace(true);
        FailIf(split != batched, $"$38 split/batched updates changed heart slots, orbit phases, or sounds: {split} / {batched}.");
        GD.Print("Validated all five ENEMY_GREAT_FAIRY $38 fountains, TX_4100/TX_4105, exact healing/departure counters, PART $30 orbit and displayed-health gate, input cleanup, re-entry, scrolling, RNG neutrality, and split/batched updates.");
    }
}
