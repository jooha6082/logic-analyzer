// Program.cs
// Entry: reads raw CSV, runs analysis, writes FOUR CSV outputs in the required order.
//  1) complete commands (Time,CMD,ADD1..ADD6)
//  2) command/address rejects (filtered out ones)
//  3) AC parameter stats (tCMDH/tADDH, F2; pin-detected only; no time pass/fail judgement)
//  4) AC detect log (all pin-detected command/address windows, F2)
//
// English only.

using LogicAnalyzer;

class Program
{
    // Usage:
    //   dotnet run -- <input_csv> <out_complete_csv> <out_cmd_addr_reject_csv> <out_ac_params_stats_csv> <out_ac_params_detect_log_csv>
    static int Main(string[] args)
    {
        if (args.Length < 5)
        {
            Console.WriteLine("Usage: dotnet run -- <input_csv> <out_complete_csv> <out_cmd_addr_reject_csv> <out_ac_params_stats_csv> <out_ac_params_detect_log_csv>");
            return 2;
        }

        string inCsv          = args[0];
        string outComplete    = args[1];
        string outReject      = args[2];
        string outAcStats     = args[3];
        string outAcDetectLog = args[4];

        if (!File.Exists(inCsv))
        {
            Console.WriteLine($"Input not found: {inCsv}");
            return 3;
        }

        try
        {
            var samples = Csv.read_samples(inCsv);
            var (complete, rejects, tcmdhAll, taddhAll, detects) = Analyzer.analyze(samples);

            // #1 complete commands
            Csv.write_complete_cmds(outComplete, complete);

            // #2 filtered commands/addresses
            Csv.write_cmd_addr_reject_log(outReject, rejects);

            // #3 AC parameter stats (pin-detected only; include short values)
            var statsCmd = Analyzer.stats(tcmdhAll);
            var statsAdr = Analyzer.stats(taddhAll);
            Csv.write_ac_stats(outAcStats, statsCmd, statsAdr);

            // #4 all pin-detected AC parameter windows
            Csv.write_ac_detect_log(outAcDetectLog, detects);

            Console.WriteLine($"Complete commands : {complete.Count}");
            Console.WriteLine($"Cmd/Addr rejects  : {rejects.Count}");
            Console.WriteLine($"tCMDH samples     : {tcmdhAll.Count}");
            Console.WriteLine($"tADDH samples     : {taddhAll.Count}");
            Console.WriteLine($"AC detects        : {detects.Count}");
            Console.WriteLine("Done.");
            Console.WriteLine($"  -> {outComplete}");
            Console.WriteLine($"  -> {outReject}");
            Console.WriteLine($"  -> {outAcStats}");
            Console.WriteLine($"  -> {outAcDetectLog}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine("ERROR: " + ex.Message);
            return 1;
        }
    }
}
