#nullable enable
using System;
using System.Globalization;

namespace N_m3u8DL_RE_GUI.Core;

/// <summary>One verified transfer sample from the UVD curl transports.
/// Bytes and speed are measurements, while Total may be a marked estimate.</summary>
public readonly record struct UvdTransferSample(double Percent,long Bytes,long Total,
    long SpeedBytesPerSecond,long EtaSeconds,bool EstimatedTotal)
{
    public const string Prefix="[UVD_METRICS] ";
    public static bool TryParse(string? line,out UvdTransferSample result)
    {
        result=default;
        if(line is null || !line.StartsWith(Prefix,StringComparison.Ordinal))return false;
        var fields=line[Prefix.Length..].Trim().Split('|');
        if(fields.Length!=6 ||
           !double.TryParse(fields[0],NumberStyles.Float,CultureInfo.InvariantCulture,out var percent) ||
           !long.TryParse(fields[1],out var bytes) ||
           !long.TryParse(fields[2],out var total) ||
           !long.TryParse(fields[3],out var speed) ||
           !long.TryParse(fields[4],out var eta) ||
           fields[5] is not ("0" or "1") ||
           !double.IsFinite(percent) || percent<0 || percent>100 ||
           bytes<0 || total<=0 || speed<0 || eta< -1)return false;
        result=new(percent,bytes,total,speed,eta,fields[5]=="1");
        return true;
    }
}
