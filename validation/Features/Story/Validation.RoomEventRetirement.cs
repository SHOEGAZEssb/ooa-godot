using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateRoomEventRetirement()
    {
        var failures = new List<string>();
        int cases = 0;
        foreach (bool batched in new[] { false, true })
        foreach (bool roomLoad in new[] { false, true })
        foreach (int updates in new[] { 3, 180 })
        {
            Check<HarpOfAgesEvent>(3, 0xae);
            Check<ImpaIntroEvent>(0, 0x6a);
            Check<NayruIntroEvent>(0, 0x39, () =>
            {
                _saveData.SetGlobalFlag(GlobalFlag.PregameIntroDone);
                _saveData.SetGlobalFlag(GlobalFlag.IntroDone, false);
            });
            Check<PoeEvent>(0, 0x7c);
            Check<TokkeyEvent>(3, 0x8f);
            Check<TokayTheftEvent>(1, 0xaa);
            Check<GraveyardGhostKidsEvent>(0, 0x7b);
            Check<EnterPastEvent>(1, 0x39);
            Check<RaftwreckEvent>(1, 0xa8);
            Check<RalphAfterRaftonEvent>(1, 0x97, () => _saveData.SetGlobalFlag(0x15));
            Check<PreBlackTowerEvent>(1, 0x75, () => _inventory.GiveTreasure(TreasureId.MakuSeed, 0));
            Check<PostD3RemoteMakuEvent>(0, 0xba, () => _inventory.GiveTreasure(TreasureId.Essence, 2));
            Check<TokayCookEvent>(2, 0x3f);

            void Check<T>(int group, int room, Action? arrange = null) where T : class, IRoomEvent
            {
                cases++;
                ReinitializeGameplayForValidation();
                arrange?.Invoke();
                LoadValidationRoom(group, room);
                IRoomEvent owner = _roomEvents.Get<T>();
                try
                {
                    FailIf(!owner.HasState, $"{typeof(T).Name} did not start in ${group:x}:${room:x2}.");
                    StepGameplayUpdates(updates, Vector2.Zero, batched: batched);
                    // Room replacement retires nodes before event cancellation.
                    // Force the engine deletion boundary that synchronous tests
                    // otherwise omit, so retained hosts see disposed wrappers.
                    _entities.Clear();
                    DrainRetiredValidationNodes();
                    if (roomLoad) LoadValidationRoom(0, 0x66);
                    else owner.Cancel();
                    owner.Cancel();
                    FailIf(owner.HasState || owner.BlocksGameplay || owner.MenusDisabled || _player.CutsceneControlled,
                        $"{typeof(T).Name} retained state/control after cancellation.");

                    // A cancelled owner must also leave the next real tile warp
                    // usable. Approach $0:$66's house at tile $54 from below.
                    if (!roomLoad) LoadValidationRoom(0, 0x66);
                    _player.WarpTo(new(0x48, 0x68));
                    StepGameplayUpdates(24, Vector2.Up, batched: batched);
                    StepGameplayUpdates(160, Vector2.Zero, batched: batched);
                    FailIf(_activeGroup != 2 || _currentRoom.Id != 0xe7 || _transitions.IsTransitioning ||
                        _roomEvents.MenusDisabled || _entities.RuntimeState.ReadWramByte(WramAddress.wDisabledObjects) != 0,
                        $"{typeof(T).Name} cancellation prevented $0:$66 -> $2:$e7 house arrival.");
                    Vector2 arrival = _player.Position;
                    StepGameplayUpdates(8, Vector2.Up, batched: batched);
                    FailIf(_player.Position == arrival,
                        $"{typeof(T).Name} cancellation left Link immobile in $2:$e7.");
                }
                catch (Exception exception)
                {
                    failures.Add($"{typeof(T).Name} ${group:x}:${room:x2} updates={updates}, batched={batched}, roomLoad={roomLoad}: {exception}");
                }
            }
        }
        FailIf(failures.Count != 0, string.Join("\n", failures));
        FailIf(cases != 104, $"Room-event retirement matrix expected 104 cases, got {cases}.");
        GD.Print("Validated 104 room-event retirement cases across 13 owners: direct/room-load cancellation, repeated cleanup, individual/batched updates and subsequent house warps.");
    }
}
