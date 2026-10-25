using Godot;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateVinePushMovementRom()
    {
        foreach (bool batched in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            _saveData.WriteWramByte(0xc8f1, 0x32); LoadValidationRoom(1, 0x61);
            _inventory.SetScriptedEquippedItems(0, 0); _player.WarpTo(new(40, 72)); _player.Face(Vector2I.Up);
            var sprout = _entities.Entities<VineSproutRoomEntity>().Single(); F1Retain(sprout);
            StepGameplayUpdates(1, Vector2.Zero);
            // Explicit already-initialized resting boundary, with source room
            // geometry and the occupied tile's real saved underlying value.
            var native = new SomariaRom(_saveData, _random.CaptureState(), _currentRoom, 0, 40, 72)
                { HostileEnemiesEnabled = true };
            native.InitializeLinkGameplay();
            native[0xd080] = 1; native[0xd081] = 0x62; native[0xd082] = 1;
            native[0xd084] = 1; native[0xd086] = 20; native[0xd090] = 0x1e;
            native[0xd08b] = 56; native[0xd08d] = 40;
            native[0xd0b1] = SomariaPrivate<byte>(sprout, "_underlyingTile");
            native[0xd0b2] = 0x32; native[0xd0b3] = 1;
            int update = 0, births = 0, completions = 0; bool wasMoving = false;
            var sounds = _sound.AttachPlayRequestAudit();
            void Step(int count = 1, bool up = false) => StepGameplayUpdates(count, up ? Vector2.Up : Vector2.Zero,
                up ? ["move_up"] : [], [], batched, () =>
                {
                    native.UpdateGameplay(0, up ? 0x40 : 0, up ? 0 : 0xff, _entities.FrameCounter); update++;
                    bool moving = native[0xd084] == 4;
                    if (moving && !wasMoving) births++;
                    if (!moving && wasMoving) completions++;
                    wasMoving = moving;
                    FailIf(sprout.Moving != moving || (moving ? sprout.MoveCounter : sprout.PushCounter) != native[0xd086] ||
                        sprout.Position != new Vector2(native.Word(0xd08c) / 256f, native.Word(0xd08a) / 256f) ||
                        sprout.PersistedPosition != native[0xc8f1],
                        $"Vine $62:$01 push update={update} batch={batched}: runtime={sprout.Moving}/{sprout.PushCounter}/{sprout.MoveCounter}/{sprout.Position}/${sprout.PersistedPosition:x2}; " +
                        $"native={native[0xd084]:x2}/{native[0xd086]}/({native.Word(0xd08c):x4},{native.Word(0xd08a):x4})/${native[0xc8f1]:x2}; Link={_player.Position}/({native[0xd00d]},{native[0xd00b]}).");
                    FailIf(sounds.Requests.Count(id => id == 0x71) != native.Sounds.Count(id => id == 0x71),
                        "Vine push must request its source SND_MOVEBLOCK at acceptance, not on first motion or completion.");
                });
            Step(15, up: true); Step(1); // Physical approach, then cancel the partial hold.
            FailIf(sprout.PushCounter != 20 || sprout.Moving, "Vine partial push must reset when Link releases direction.");
            {
                int priorBirths = births, priorCompletions = completions;
                for (int wait = 0; births == priorBirths && wait < 50; wait++) Step(1, up: true);
                FailIf(births != priorBirths + 1, $"Actual collision approach must accept the repeated vine push: " +
                    $"Link={_player.Position}/({native[0xd00d]},{native[0xd00b]}), counter={sprout.PushCounter}/{native[0xd086]}, " +
                    $"state={native[0xd004]:x2}, input={native[0xcc29]:x2}, angle={native[0xcc2b]:x2}.");
                Step(3);
                _dialogue.ShowGameplayMessage("Vine movement pause", 100); native[0xcba0] = 1;
                Step(3);
                _dialogue.Close(); native[0xcba0] = 0;
                for (int wait = 0; completions == priorCompletions && wait < 25; wait++) Step();
                FailIf(completions != priorCompletions + 1, "Vine must complete its exact source countdown and persist its new position.");
                Step(2);
            }
            // The next upward destination is solid in this real room. Repeat
            // the action after completion: both handlers must reject it,
            // preserving position/save bytes and avoiding another move cue.
            Step(60, up: true);
            FailIf(births != 1 || completions != 1 || sprout.Moving || sprout.PersistedPosition != 0x22,
                "Repeated vine push against room 1:61's solid destination must preserve the completed position.");
            _entities.Clear();
        }
        GD.Print("Validated native Vine $62:$01 real-geometry approach, hold cancellation, acceptance/no-motion, source movement/countdown, text pause, completion/persistence and repeated push through split/batched application updates.");
    }
}
