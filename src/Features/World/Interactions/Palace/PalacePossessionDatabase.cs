using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class PalacePossessionDatabase
{
    private readonly Dictionary<string, byte[]> _tables = new();
    private readonly Dictionary<int, ImportedEnemyDefinition> _enemies = new();
    private readonly Dictionary<int, NpcRecord> _actors = new();
    private readonly Dictionary<int, string> _animations = new();
    private readonly Dictionary<int, string> _texts = new();
    private readonly Dictionary<string, int> _entries = new();
    internal IReadOnlyList<CutsceneCommand> Commands { get; } = CutsceneCommandCatalog.Load("res://assets/oracle/cutscenes/palace_possession_commands.tsv");
    internal IReadOnlyDictionary<int, Color[]> Palettes { get; }
    internal PalacePossessionDatabase()
    {
        foreach (var row in GeneratedTable.Load("res://assets/oracle/objects/palace_possession_tables.tsv",
            new("palace possession tables", GeneratedTableKeySemantics.Unique, ["profile","values","source"],["profile"],headerRequired:true)).Rows)
            _tables.Add(row.RequiredString(0), Array.ConvertAll(row.SplitRequired(1,','), value => Convert.ToByte(value,16)));
        foreach (var (key,count) in new[] {("pillars",12),("pillar-offsets",8),("attack-probabilities",10),("presets",10),
            ("guard-motion",12),("guard-animations",6),("collision-06",32),("collision-43",32),("collision-5b",32),("collision-5c",32),("active-61",32),("active-37",32)})
            if (!_tables.TryGetValue(key,out var bytes) || bytes.Length != count) throw new InvalidOperationException($"ENEMY $61 / INTERAC $6e: missing {key}.");
        foreach (var row in GeneratedTable.Load("res://assets/oracle/objects/palace_possession_enemies.tsv",
            new("palace possession enemies", GeneratedTableKeySemantics.Unique,
                ["id","subid","sprites","tile-base","palette","source-grayscale-inverted","radius-y","radius-x","damage-quarters","health","animations-base64"], ["id","subid"],headerRequired:true)).Rows)
            _enemies.Add(row.HexByte(1),new(row.HexByte(0),row.HexByte(1),row.SplitRequired(2,','),row.Decimal(3),row.Decimal(4),row.Boolean01(5),
                row.Decimal(6),row.Decimal(7),row.Decimal(8),row.Decimal(9),row.EncodedAnimations(10)));
        var projectile=GeneratedTable.Load("res://assets/oracle/objects/palace_veran_projectile.tsv",
            new("Veran projectile",GeneratedTableKeySemantics.Unique,
                ["id","subid","sprites","tile-base","palette","source-grayscale-inverted","radius-y","radius-x","damage-quarters","health","animations-base64"],["id","subid"],headerRequired:true)).Rows[0];
        Projectile=new(projectile.HexByte(0),0,projectile.SplitRequired(2,','),projectile.Decimal(3),projectile.Decimal(4),projectile.Boolean01(5),
            projectile.Decimal(6),projectile.Decimal(7),projectile.Decimal(8),projectile.Decimal(9),projectile.EncodedAnimations(10));
        foreach (var row in GeneratedTable.Load("res://assets/oracle/cutscenes/palace_possession_visuals.tsv",
            new("palace possession actors", GeneratedTableKeySemantics.Unique,["subid","sprite","tile-base","palette","animation","source"],["subid"],headerRequired:true)).Rows)
        {
            int subid = row.Decimal(0); string animation = row.RequiredString(4);
            _actors.Add(subid,new(5,0xce,0x6e,subid,0,0,0,0,row.RequiredString(1),row.Decimal(2),row.Decimal(3),0,false,
                animation,animation,animation,animation,"",NpcImplementationClassification.EventOwned));
        }
        foreach (var row in GeneratedTable.Load("res://assets/oracle/cutscenes/palace_possession_animations.tsv",
            new("palace possession animation",GeneratedTableKeySemantics.Unique,["index","animation","source"],["index"],headerRequired:true)).Rows)
            _animations.Add(row.HexByte(0),row.RequiredString(1));
        foreach (var row in GeneratedTable.Load("res://assets/oracle/cutscenes/palace_possession_entries.tsv",
            new("palace possession scripts",GeneratedTableKeySemantics.Unique,["script","index","source"],["script"],headerRequired:true)).Rows)
            _entries.Add(row.RequiredString(0),row.Decimal(1));
        foreach (var row in GeneratedTable.Load("res://assets/oracle/cutscenes/palace_possession_texts.tsv",
            new("palace possession dialogue",GeneratedTableKeySemantics.Unique,["text-id","message-base64","source"],["text-id"],headerRequired:true)).Rows)
            _texts.Add(row.HexWord(0),row.Base64Utf8(1));
        var palette = OracleGraphicsData.LoadPalette("res://assets/oracle/objects/palace_possession_palette.bin",1,6);
        Palettes = new Dictionary<int,Color[]> { [6]=[palette[6,0],palette[6,1],palette[6,2],palette[6,3]] };
    }
    internal byte[] Table(string name) => _tables[name];
    internal ImportedEnemyDefinition Projectile { get; }
    internal ImportedEnemyDefinition Enemy(int subid) => _enemies[subid];
    internal NpcRecord Actor(int subid, Vector2 position, bool intro = false) => _actors[subid] with
        { Id = intro ? 0x6d : 0x6e, X=(int)position.X, Y=(int)position.Y };
    internal string Animation(int index) => _animations[index];
    internal string Text(int id) => _texts[id];
    internal int Entry(string name) => _entries[name];
    internal int Collision(int mode,int item) => _tables[$"collision-{mode:x2}"][item];
    internal bool ActiveCollision(int item) => _tables["active-61"][item] != 0;
    internal bool ProjectileActiveCollision(int item) => _tables["active-37"][item] != 0;
}
