using System.IO;

namespace Oracles;

// Admission/coverage metadata is a projection of native instructions, not another placement program.
internal static class NativeObjectDeclarations
{
    internal static IEnumerable<RoomObjectRecord> Read(
        IReadOnlyDictionary<int, NativeObjectInstruction> instructions,
        IReadOnlyDictionary<string, int> labels)
    {
        IEnumerable<NativeObjectInstruction> Block(string label)
        {
            if (!labels.TryGetValue(label, out int address)) yield break;
            while (true)
            {
                if (!instructions.TryGetValue(address, out var instruction))
                    throw new InvalidDataException($"{label}: missing native instruction ${address:x4}.");
                if (instruction.Opcode is 0xfd or 0xfe or 0xff) yield break;
                yield return instruction;
                address = instruction.Next;
            }
        }

        for (int group = 0; group < 6; group++)
        for (int room = 0; room < 256; room++)
        {
            int order = 0;
            // Other main-data bosses and their children belong to their scripted construction bindings.
            foreach (var instruction in Block($"group{group:x1}Map{room:x2}ObjectData"))
                if (instruction.Opcode == 7 && instruction.Id is 0x02 or 0x62 ||
                    instruction.Opcode == 8 && instruction.Id == 0x59)
                    yield return Placement(instruction, group, room, order++, 0xff);
            int condition = 0xff;
            foreach (var instruction in Block($"group{group:x1}Map{room:x2}EnemyObjectData"))
            {
                if (instruction.Opcode == 0) condition = instruction.Mask;
                else if (instruction.Opcode is 6 or 7 or 8 or 10 || instruction.Opcode == 9 && instruction.Flags is 1 or 2)
                    yield return Placement(instruction, group, room, order++, condition);
            }
        }
    }

    private static RoomObjectRecord Placement(NativeObjectInstruction instruction, int group, int room, int order, int condition)
    {
        RoomObjectKind kind = instruction.Opcode switch
        {
            6 => RoomObjectKind.RandomEnemy,
            7 => RoomObjectKind.FixedEnemy,
            8 => RoomObjectKind.ReservingPart,
            9 when instruction.Flags == 1 => RoomObjectKind.ParameterEnemy,
            9 when instruction.Flags == 2 => RoomObjectKind.ParameterPart,
            10 => RoomObjectKind.ItemDrop,
            _ => throw new InvalidDataException($"{instruction.Source}: no placement projection for opcode ${instruction.Opcode:x2}.")
        };
        bool random = kind == RoomObjectKind.RandomEnemy;
        bool packed = kind is RoomObjectKind.ReservingPart or RoomObjectKind.ItemDrop;
        return new(group, room, order, kind,
            kind == RoomObjectKind.ItemDrop ? 0x59 : instruction.Id,
            kind == RoomObjectKind.ItemDrop ? instruction.Id : instruction.SubId,
            instruction.Opcode == 9 ? 0 : instruction.Flags,
            random ? instruction.Flags >> 5 & 7 : 1,
            random || packed ? -1 : instruction.Y, random || packed ? -1 : instruction.X,
            random ? -1 : packed ? instruction.Position : (instruction.Y & 0xf0) | instruction.X >> 4, condition)
        {
            Var03 = instruction.Var03,
            NativeAddress = instruction.Address,
            SourceOverride = $"{instruction.Source}: room ${group:x1}:${room:x2} object[{order}] at ${instruction.Address:x4}"
        };
    }
}
