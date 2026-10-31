using Godot;
using System;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private static void CheckSharedBurningEnemy(FrontendValidationRoot root, bool batched)
    {
        var rooms = root._gameplaySession!.Rooms;
        var entities = root._seasonsOpening?.Entities ?? root._entities;
        var player = root._scene.Player;
        var app = new ApplicationValidationFixture(root);
        OracleGame game = rooms.SaveData.Game;
        bool ages = game == OracleGame.Ages;
        var rom = ValidationRom.LoadCleanUs(game);
        int Word(int at) => rom.Span[at] | rom.Span[at + 1] << 8;
        int bank = (ages ? 0x16 : 0x15) * 0x4000;
        int pointers = bank + (ages ? 0x3668 : 0x318f);
        int oamPointers = bank + (ages ? 0x371e : 0x3237);
        int animation = bank + (Word(bank + (Word(pointers + 0x12 * 2) & 0x3fff)) & 0x3fff);
        int oam = bank + (Word(oamPointers + 0x12 * 2) & 0x3fff);
        int oamBank = (ages ? 0x14 : 0x13) * 0x4000;
        var row = GeneratedTable.Load(rooms.AssetRoot + "effects/burning_enemy.tsv",
            new("native burning effect",GeneratedTableKeySemantics.Unique,
                ["sprite","tile-base","palette","frames","gravity","animation","source"],["source"],headerRequired:true)).SingleRow();
        var definition = OracleGraphicsCache.GetAnimationDefinition(row.RequiredString(5));
        FailIf(row.RequiredString(0) != "spr_common_sprites" || row.UnsignedDecimal(1) != 8 || row.UnsignedDecimal(2) != 2 ||
            row.UnsignedDecimal(3) != 59 || row.UnsignedDecimal(4) != 16 || definition.Frames.Length != 6 || definition.LoopStart != 3,
            $"{game}: PART_BURNING_ENEMY $12 native construction/loop contract differs.");
        for (int index = 0; index < 6; index++)
        {
            int at = animation + index * 3;
            int cells = oamBank + (Word(oam + rom.Span[at + 1]) & 0x3fff);
            string expected = string.Join(';',Enumerable.Range(0,rom.Span[cells]).Select(cell =>
                string.Join(',',Enumerable.Range(0,4).Select(value => rom.Span[cells + 1 + cell * 4 + value]))));
            FailIf(definition.Frames[index].Duration != rom.Span[at] || definition.Frames[index].Parameter != rom.Span[at + 2] ||
                definition.Frames[index].EncodedOam != expected,
                $"{game}: PART$12 frame ${index:x2} must use its clean-ROM OAM/duration/parameter.");
        }
        foreach (bool replaceTarget in new[] { false,true })
        foreach (int freezeKind in new[] { 0,1,2 })
        {
            entities.Clear(); player.WarpTo(new(40,80)); player.Inventory.EquipA(0); player.Inventory.EquipB(0);
            var target = new BurningEnemyValidationTarget();
            var native = new BraceletRom(rooms.CurrentRoom,player.Position,0,0xff,game:game);
            native.LoadSave(rooms.SaveData);
            native[0xd180] = 1; native[0xd181] = (byte)target.BurnTargetId; native[0xd1a9] = (byte)target.BurnHealth;
            native.Word(0xd18a,(int)target.BurnPosition.Y * 256); native.Word(0xd18c,(int)target.BurnPosition.X * 256);
            native.Word(0xd18e,target.BurnZFixed);
            native[0xd0c0] = 1; native[0xd0c1] = 0x12; native.Word(0xd0d6,0xd180);
            var part = entities.Spawn<BurningEnemyPart>(new BurningEnemySpawn(target));
            FailIf(!part.InitializationPending || part.Visible || part.Counter != 0 || target.BurnHealth != 5 ||
                target.BurnZFixed != -0x12b || target.ReleaseCalls != 0,
                $"{game}: PART$12 construction must not execute native counter/target initialization.");
            int updates = 0;
            void Step(int count)
            {
                app.Step(count,Vector2.Zero,batched:batched);
                for (int update = 0; update < count; update++) native.Update(item:0,updateParts:true);
                updates += count;
                bool alive = native[0xd0c0] != 0;
                FailIf(part.Finished == alive || target.BurnHealth != native[0xd1a9] ||
                    target.BurnZFixed != unchecked((short)native.Word(0xd18e)),
                    $"{game}: PART$12 target health/Z/deletion differs at update {updates}, replacement={replaceTarget}: " +
                    $"runtime={part.Finished}/{target.BurnHealth}/{target.BurnZFixed}, native={alive}/{native[0xd1a9]}/{unchecked((short)native.Word(0xd18e))}, state/counter={native[0xd0c4]}/{native[0xd0c6]}.");
                if (alive)
                {
                    int frame = ((native.Word(0xd0e2) - (animation & 0x3fff) - 0x4000) / 3) - 1;
                    FailIf(part.Counter != native[0xd0c6] || part.AnimationFrame != frame ||
                        part.Position != new Vector2(native[0xd0cd],native[0xd0cb]),
                        $"{game}: PART$12 native counter/animation/position differs at update {updates}: runtime={part.Counter}/{part.AnimationFrame}, native={native[0xd0c6]}/{frame}.");
                }
            }
            byte previousScroll = entities.RuntimeState.ReadField("wScrollMode");
            if (freezeKind == 0) { entities.RuntimeState.SetField("wDisabledObjects",8); native[0xcc8a] = 8; }
            else if (freezeKind == 1) { entities.RuntimeState.SetField("wScrollMode",8); native[0xcd00] = 8; }
            else { root._scene.Dialogue.ShowMessage("Burning effect gate.",player.Position.Y); native[0xcba0] = 1; }
            Step(1); Step(4);
            entities.RuntimeState.SetField("wDisabledObjects",0); native[0xcc8a] = 0;
            entities.RuntimeState.SetField("wScrollMode",previousScroll); native[0xcd00] = 0;
            if (freezeKind == 2) { root._scene.Dialogue.Close(); native[0xcba0] = 0; }
            Step(1);
            if (replaceTarget) { target.BurnTargetId++; native[0xd181]++; Step(1); }
            while (!part.Finished && updates < 70)
                Step(batched ? Math.Min(4,Math.Max(1,part.Counter - 1)) : 1);
            FailIf(!part.Finished || target.ReleaseCalls != (replaceTarget ? 0 : 1),
                $"{game}: PART$12 must restore/release on completion and delete without restoring a replaced target.");
            Step(4);
        }
        entities.Clear();
    }
}
