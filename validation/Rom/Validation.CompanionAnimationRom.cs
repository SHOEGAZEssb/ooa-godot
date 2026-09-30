using Godot;
using System;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateCompanionAnimationRom()
    {
        int traces = 0;
        foreach (int id in new[] { 0x0b, 0x0c, 0x0d })
        {
            PrepareCompanionFidelityRoom(); _entities.Clear();
            var actor = SpawnFidelityCompanion(id, new(72, 64));
            var animation = CompanionField<EnemyAnimationPlayer>(actor, "_animation");
            var rom = new LinkCollisionRom(); rom[0xd101] = (byte)id;
            int count = CompanionField<Array>(animation, "_animations").Length;
            for (int index = 0; index < count; index++)
            {
                animation.SetAnimation(index);
                rom.Call(0x2b0a, objectPage: 0xd1, accumulator: index);
                for (int tick = 0; tick < 1024; tick++)
                {
                    FailIf(animation.CurrentParameter != rom[0xd121] || CompanionField<int>(animation, "_frameCounter") != rom[0xd120],
                        $"Companion ${id:x2} animation ${index:x2}, update {tick}: native parameter/counter=${rom[0xd121]:x2}/{rom[0xd120]}, runtime=${animation.CurrentParameter:x2}/{CompanionField<int>(animation, "_frameCounter")}.");
                    if (rom[0xd121] == 0xff || id == 0x0d && index >= 0x17 && (rom[0xd121] & 0x80) != 0)
                        break; // Handler consumes terminal pose and leaves this stream.
                    animation.Advance();
                    rom.Call(0x2aef, objectPage: 0xd1); // specialObjectAnimate
                }
                traces++;
            }
        }
        GD.Print($"Validated {traces} companion animation streams through terminal signals or 1024 native updates, including alternate entries, label fallthrough and loop targets.");
    }
}
