using System;
using System.Linq;

namespace oracleofages;

internal sealed class MakuConfettiDatabase
{
    internal int[] Delays {get;}
    internal int[][] Pieces {get;}
    internal int YOffset {get;}
    internal int SparkleInitial {get;}
    internal int SparkleRepeat {get;}
    internal int SoundCounter {get;}
    internal int Sound {get;}
    internal int YLimit {get;}
    internal int XLimit {get;}
    internal int DeleteY {get;}
    internal DungeonInteractionVisual Visual {get;}
    internal InteractionSparkleVisual Sparkle {get;}
    internal MakuConfettiDatabase()
    {
        var row=GeneratedTable.Load("res://assets/oracle/effects/maku_confetti.tsv",new("INTERAC $62 present confetti",
            GeneratedTableKeySemantics.Ordered,["delays","positions","y-offset","sparkle-initial","sparkle-repeat","sound-counter","sound","y-limit","x-limit","delete-y","source"],headerRequired:true)).SingleRow();
        Delays=Array.ConvertAll(row.SplitRequired(0,','),int.Parse);
        Pieces=row.SplitRequired(1,',').Select(s=>Array.ConvertAll(s.Split(':'),int.Parse)).ToArray();
        if(Pieces.Length!=5||Pieces.Any(p=>p.Length!=4)||Delays.Length!=6)throw row.Invalid(1,"five native Y/X/acceleration rows and six delays");
        YOffset=row.UnsignedDecimal(2);SparkleInitial=row.UnsignedDecimal(3);SparkleRepeat=row.UnsignedDecimal(4);
        SoundCounter=row.UnsignedDecimal(5);Sound=row.HexByte(6);YLimit=row.UnsignedDecimal(7);XLimit=row.UnsignedDecimal(8);DeleteY=row.UnsignedDecimal(9);
        var visuals=GeneratedTable.Load("res://assets/oracle/cutscenes/remote_maku_first_essence_visuals.tsv",new("Shared Maku visuals",
            GeneratedTableKeySemantics.Unique,["key","sprite","tile-base","palette","animation"],["key"],headerRequired:true)).Rows;
        var first=visuals.Single(r=>r.RequiredString(0)=="confetti-left");
        var second=visuals.Single(r=>r.RequiredString(0)=="confetti-right");
        // This shared sheet uses the default inverted source shades; reversing
        // them makes the flower's transparent surround an opaque rectangle.
        Visual=new("maku-confetti",[first.RequiredString(1)],first.UnsignedDecimal(2),first.UnsignedDecimal(3),true,[first.RequiredString(4),second.RequiredString(4)]);
        var sparkle=visuals.Single(r=>r.RequiredString(0)=="sparkle");
        Sparkle=new(sparkle.RequiredString(1),sparkle.UnsignedDecimal(2),sparkle.UnsignedDecimal(3),sparkle.RequiredString(4));
    }
}
