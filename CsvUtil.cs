// CsvUtil.cs
// CSV IO for samples, outputs, and logs. UTF-8 (no BOM). English only.

using System.Globalization;
using System.Text;

namespace LogicAnalyzer;

public static class Csv
{
    public static List<Sample> read_samples(string path)
    {
        using var sr = new StreamReader(path, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var header = sr.ReadLine();
        if (header is null) throw new InvalidOperationException("Empty CSV.");

        var cols = header.Split(',').Select(s => s.Trim()).ToArray();
        int iTime = Array.IndexOf(cols, "Time(ns)");
        int iIO   = Array.IndexOf(cols, "IO");
        int iCE   = Array.IndexOf(cols, "nCE0");
        int iALE  = Array.IndexOf(cols, "ALE");
        int iCLE  = Array.IndexOf(cols, "CLE");
        int iWE   = Array.IndexOf(cols, "nWE");
        int iRE   = Array.IndexOf(cols, "nRE");
        int iRNB  = Array.IndexOf(cols, "RnB");
        int iWP   = Array.IndexOf(cols, "nWP");
        int iDQS  = Array.IndexOf(cols, "DQS");

        if (new[]{iTime,iIO,iCE,iALE,iCLE,iWE,iRE,iRNB,iWP,iDQS}.Any(x => x < 0))
            throw new InvalidOperationException("CSV header missing required columns.");

        var list = new List<Sample>(1024);
        string? line;
        while ((line = sr.ReadLine()) != null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var p = line.Split(',').Select(x => x.Trim()).ToArray();

            list.Add(new Sample(
                TimeNs: int.Parse(p[iTime], CultureInfo.InvariantCulture),
                IO    : p[iIO],
                nCE0  : int.Parse(p[iCE], CultureInfo.InvariantCulture),
                ALE   : int.Parse(p[iALE], CultureInfo.InvariantCulture),
                CLE   : int.Parse(p[iCLE], CultureInfo.InvariantCulture),
                nWE   : int.Parse(p[iWE], CultureInfo.InvariantCulture),
                nRE   : int.Parse(p[iRE], CultureInfo.InvariantCulture),
                RnB   : int.Parse(p[iRNB], CultureInfo.InvariantCulture),
                nWP   : int.Parse(p[iWP], CultureInfo.InvariantCulture),
                DQS   : int.Parse(p[iDQS], CultureInfo.InvariantCulture)
            ));
        }
        return list;
    }

    public static void write_complete_cmds(string path, IEnumerable<ValidCmdRow> rows)
    {
        using var sw = new StreamWriter(path, false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        sw.WriteLine("Time,CMD,ADD1,ADD2,ADD3,ADD4,ADD5,ADD6");
        foreach (var r in rows)
            sw.WriteLine($"{r.Time},{r.CMD},{r.ADD1},{r.ADD2},{r.ADD3},{r.ADD4},{r.ADD5},{r.ADD6}");
    }

    public static void write_cmd_addr_reject_log(string path, IEnumerable<CmdAddrReject> logs)
    {
        using var sw = new StreamWriter(path, false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        sw.WriteLine("Time,Kind,Reason,Code,TAC(ns),ParentCmdTime,ParentCmdName");
        foreach (var e in logs)
            sw.WriteLine($"{e.Time},{e.Kind},{e.Reason},{e.Code},{(double.IsNaN(e.TAC_ns) ? "" : e.TAC_ns.ToString("F2", CultureInfo.InvariantCulture))},{e.ParentCmdTime},{e.ParentCmdName}");
    }

    public static void write_ac_stats(
        string path,
        (double avg, double min, double max, double std) tcmdh,
        (double avg, double min, double max, double std) taddh)
    {
        using var sw = new StreamWriter(path, false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        sw.WriteLine(",AVG,MIN,MAX,STDV");
        sw.WriteLine($"tCMDH,{fmt2(tcmdh.avg)},{fmt2(tcmdh.min)},{fmt2(tcmdh.max)},{fmt2(tcmdh.std)}");
        sw.WriteLine($"tADDH,{fmt2(taddh.avg)},{fmt2(taddh.min)},{fmt2(taddh.max)},{fmt2(taddh.std)}");

        static string fmt2(double v) => double.IsNaN(v) ? "" : v.ToString("F2", CultureInfo.InvariantCulture);
    }

    public static void write_ac_detect_log(string path, IEnumerable<AcDetectLog> logs)
    {
        using var sw = new StreamWriter(path, false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        sw.WriteLine("Time,Stage,Name,Code,TAC(ns),ParentCmdTime,ParentCmdName");
        foreach (var e in logs)
            sw.WriteLine($"{e.Time},{e.Stage},{e.Name},{e.Code},{e.TAC_ns.ToString("F2", CultureInfo.InvariantCulture)},{e.ParentCmdTime},{e.ParentCmdName}");
    }
}
