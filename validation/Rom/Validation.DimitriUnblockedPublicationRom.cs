using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateDimitriUnblockedPublicationRom() => CompareCompanionUnblockedPublicationRom(0x0c);
    private void ValidateMooshCliffPublicationRom() => CompareCompanionUnblockedPublicationRom(0x0d);

    private void CompareCompanionUnblockedPublicationRom(int id)
    {
        foreach (bool cliff in id == 0x0d ? new[] { true } : new[] { false, true })
        foreach (int direction in cliff ? new[] { 2 } : new[] { 0, 1, 2, 3 })
        foreach (bool batched in new[] { false, true })
        {
            var (actor, rom) = PrepareMountedCompanionRom(id, direction);
            Vector2 input = cliff ? Vector2.Down : Vector2.Zero;
            if (cliff) SetCompanionRomTile(rom, 4, 4, 0xd4, 3);
            byte[] observed = new byte[4], expected = new byte[4];
            int probeVisits = 0, motionVisits = 0, frozenVisits = 0, update = 0;
            var probe = new PartMovementScratchProbe(() =>
            {
                probeVisits++;
                for (int i = 0; i < 4; i++) observed[i] = _runtimeState.ReadWramByte(0xcec0 + i);
            });
            _entities.RegisterEnemySlot(probe, 15); _entities.AddEntity(probe);
            void Seed()
            {
                for (int i = 0; i < 4; i++)
                { byte value = (byte)(0xa5 + i); _runtimeState.SetWramByte(0xcec0 + i, value); rom[0xcec0 + i] = value; }
            }
            void Step(int count, bool attack = false, bool text = false)
            {
                int frame = 0;
                StepGameplayUpdates(count, input,
                    attack ? ["attack"] : [], attack ? ["attack"] : [], batched, () =>
                    {
                        int state = rom[0xd104], substate = rom[0xd105], counter = rom[0xd106];
                        bool selected = state == 8 || state == 7;
                        bool moving = !text && (state == 8 && substate < 2 || state == 7 && counter == 0);
                        rom.Update(CompanionMovement.AngleForInput(input), attack && frame == 0 ? (byte)1 : (byte)0,
                            attack ? (byte)1 : (byte)0, afterLink: () =>
                            {
                                for (int i = 0; i < 4; i++) expected[i] = rom[0xcec0 + i];
                            });
                        update++; frame++;
                        CompareCompanionMotion(actor, rom, $"Companion${id:x2} publication cliff={cliff}/dir={direction}/visit={update}");
                        // Observe only the selected unblocked source states;
                        // ground collision movement is a separate contract.
                        if (selected && !text)
                            FailIf(probeVisits != 1 || !observed.SequenceEqual(expected),
                                $"Companion${id:x2} state${state:x2}:${substate:x2} visit={update}: pre-PART movement publication differs from native: runtime=[{string.Join(',', observed)}], native=[{string.Join(',', expected)}].");
                        if (moving) motionVisits++;
                        if (selected && text) frozenVisits++;
                        probeVisits = 0;
                        Seed();
                    });
            }
            Seed(); Step(cliff ? 4 : 1, attack: !cliff);
            Step(3);
            _dialogue.ShowGameplayMessage("Companion movement hold.", 100); rom[0xcba0] = 1;
            Step(3, text: true); _dialogue.Close(); rom[0xcba0] = 0;
            if (id == 0x0d) input = Vector2.Zero;
            Step(70);
            bool unfinished = actor is DimitriCompanionRoomEntity { Phase: DimitriPhase.Eating or DimitriPhase.CliffJump } ||
                actor is MooshCompanionRoomEntity { Phase: MooshCompanionPhase.CliffJump };
            FailIf(motionVisits < 4 || frozenVisits != 3 || unfinished,
                $"Companion${id:x2} fixture must complete its selected source path after freeze: cliff={cliff}, movements={motionVisits}, holds={frozenVisits}.");
            _entities.Clear();
        }
    }
}
