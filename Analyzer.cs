// Analyzer.cs
// AC Parameter detection with strict pin gating. No sorting; preserves stream order.
//
// Outputs are consumed by Program.cs as tuples:
//   (1) complete commands (6 valid addresses)
//   (2) command/address rejects (filtered out ones)
//   (3) tCMDH list (ALL pin-detected command holds, F2-rounded; includes < min)
//   (4) tADDH list (ALL pin-detected address holds, F2-rounded; includes < min)
//   (5) AC detect log (ALL pin-detected windows: command/address), F2-rounded
//
// Rules:
//   • Command window: RnB==0 AND CLE==1 across the WE-high window.
//   • Address window: RnB==0 AND ALE==1 across the WE-high window.
//   • Ambiguous (CLE==1 && ALE==1 while WE-high) → reject (not measurable as a separated window).
//   • tCMDH/tADDH are always measured and recorded (stats + detect log) once the pin window is valid,
//     then thresholds are applied only to acceptance of the complete command / address chain.
//
// Thresholds:
//
namespace LogicAnalyzer;

public static class Analyzer
{
    public const double MIN_TCMDH_NS = 20.0;
    public const double MIN_TADDH_NS = 30.0;

    public static (List<ValidCmdRow> complete,
                   List<CmdAddrReject> rejects,
                   List<double> tcmdhAll,   // ALL pin-detected command holds (F2)
                   List<double> taddhAll,   // ALL pin-detected address holds (F2)
                   List<AcDetectLog> detects)
        analyze(IReadOnlyList<Sample> s)
    {
        var completeRows = new List<ValidCmdRow>(512);
        var rejectLog    = new List<CmdAddrReject>(4096);
        var tcmdhAll     = new List<double>(4096);
        var taddhAll     = new List<double>(16384);
        var detectLog    = new List<AcDetectLog>(8192);

        for (int i = 1; i < s.Count; i++)
        {
            // nWE rising: 0 -> 1
            if (s[i - 1].nWE == 0 && s[i].nWE == 1)
            {
                int rise = i;
                int fall = find_next_fall(s, rise);
                int end  = (fall >= 0) ? fall : s.Count;

                bool cle_all = window_all(s, rise, end, k => s[k].CLE == 1);
                bool ale_all = window_all(s, rise, end, k => s[k].ALE == 1);
                bool rnb_all = window_all(s, rise, end, k => s[k].RnB == 0);

                // ambiguous window → reject as Command (we cannot separate pins)
                if (rnb_all && cle_all && ale_all)
                {
                    rejectLog.Add(new CmdAddrReject {
                        Time = s[rise].TimeNs, Kind = "Command",
                        Reason = "AmbiguousCLE_ALE", Code = s[rise].IO.ToUpperInvariant(),
                        TAC_ns = double.NaN, ParentCmdTime = 0, ParentCmdName = ""
                    });
                    continue;
                }

                bool is_cmd = rnb_all && cle_all;

                // Command gating fail (diagnostic reject)
                if (!is_cmd && cle_all && !rnb_all)
                {
                    rejectLog.Add(new CmdAddrReject {
                        Time = s[rise].TimeNs, Kind = "Command",
                        Reason = "CommandGating_RnBHigh", Code = s[rise].IO.ToUpperInvariant(),
                        TAC_ns = double.NaN, ParentCmdTime = 0, ParentCmdName = ""
                    });
                }

                if (is_cmd)
                {
                    // Measure tCMDH for stats/detect (always)
                    double tcmdh = measure_hold_ns(s, rise, k => s[k].CLE == 1);
                    double tcmdh_r = round2(tcmdh);

                    tcmdhAll.Add(tcmdh_r); // <- ALWAYS included in stats
                    string cmdHex  = s[rise].IO.ToUpperInvariant();
                    string cmdName = classify_cmd_name(cmdHex);
                    int cmdTime    = s[rise].TimeNs;

                    // Detect log (ALWAYS)
                    detectLog.Add(new AcDetectLog {
                        Time = cmdTime, Stage = "Command", Name = cmdName, Code = cmdHex,
                        TAC_ns = tcmdh_r, ParentCmdTime = 0, ParentCmdName = ""
                    });

                    // Threshold only affects acceptance
                    if (tcmdh_r < MIN_TCMDH_NS)
                    {
                        rejectLog.Add(new CmdAddrReject {
                            Time = cmdTime, Kind = "Command",
                            Reason = "tCMDHShort", Code = cmdHex,
                            TAC_ns = tcmdh_r, ParentCmdTime = 0, ParentCmdName = ""
                        });
                        continue;
                    }

                    // Try to collect exactly 6 addresses
                    var (ok, addrs, addrDetects, cmdRejectIfAny) =
                        collect_6_addresses(s,
                                            (fall >= 0) ? fall + 1 : rise + 1,
                                            cmdTime, cmdName,
                                            taddhAll, rejectLog);

                    // Append address detects (already pin-detected, in-order)
                    detectLog.AddRange(addrDetects);

                    if (!ok)
                    {
                        if (cmdRejectIfAny is not null)
                            rejectLog.Add(cmdRejectIfAny!);
                        continue;
                    }

                    // Accept complete command (6 addresses all valid by gating and thresholds)
                    completeRows.Add(new ValidCmdRow(cmdTime, cmdName, addrs));
                }
            }
        }

        return (completeRows, rejectLog, tcmdhAll, taddhAll, detectLog);
    }

    public static (double avg, double min, double max, double std) stats(IEnumerable<double> roundedValues)
    {
        var arr = roundedValues.ToArray();
        if (arr.Length == 0) return (double.NaN, double.NaN, double.NaN, double.NaN);

        double avg = arr.Average();
        double min = arr.Min();
        double max = arr.Max();

        double var = 0.0;
        foreach (var v in arr)
        {
            double d = v - avg;
            var += d * d;
        }
        var /= arr.Length; // population variance (ddof=0)
        double std = Math.Sqrt(var);

        return (round2(avg), round2(min), round2(max), round2(std));
    }

    // --- internals ---

    private static string classify_cmd_name(string hex) =>
        hex switch { "30" => "Erase", "20" => "Program", "10" => "Read", "00" => "Reset", _ => "Unknown" };

    private static int find_next_fall(IReadOnlyList<Sample> s, int from_idx)
    {
        for (int i = from_idx + 1; i < s.Count; i++)
            if (s[i - 1].nWE == 1 && s[i].nWE == 0) return i;
        return -1;
    }

    private static bool window_all(IReadOnlyList<Sample> s, int start, int end, Func<int, bool> pred)
    {
        for (int i = start; i < end; i++)
            if (!pred(i)) return false;
        return true;
    }

    // Measure from WE rising until the first sample where the pin drops to 0
    private static double measure_hold_ns(IReadOnlyList<Sample> s, int rise_idx, Func<int, bool> pin_is_high)
    {
        int t0 = s[rise_idx].TimeNs;
        for (int i = rise_idx; i < s.Count; i++)
        {
            if (!pin_is_high(i))
            {
                int dt = s[i].TimeNs - t0;
                return (double)dt;
            }
        }
        return (double)(s[^1].TimeNs - t0);
    }

    private static (bool ok,
                    List<string> addrs,
                    List<AcDetectLog> addrDetects,
                    CmdAddrReject? cmdRejectIfAny)
        collect_6_addresses(IReadOnlyList<Sample> s,
                            int start_idx,
                            int parentCmdTime,
                            string parentCmdName,
                            List<double> taddhAllSink,
                            List<CmdAddrReject> rejectSink)
    {
        var addrs   = new List<string>(6);
        var detects = new List<AcDetectLog>(8);

        int i = Math.Max(1, start_idx);

        while (addrs.Count < 6 && i < s.Count)
        {
            // seek next WE rising
            int rise = -1;
            for (; i < s.Count; i++)
            {
                if (s[i - 1].nWE == 0 && s[i].nWE == 1) { rise = i; break; }
            }
            if (rise < 0) break;

            int fall = find_next_fall(s, rise);
            int end  = (fall >= 0) ? fall : s.Count;

            bool cle_all = window_all(s, rise, end, k => s[k].CLE == 1);
            bool ale_all = window_all(s, rise, end, k => s[k].ALE == 1);
            bool rnb_all = window_all(s, rise, end, k => s[k].RnB == 0);

            // Next command begins → command-level reject
            if (rnb_all && cle_all)
            {
                return (false, addrs, detects,
                    new CmdAddrReject {
                        Time = parentCmdTime, Kind = "Command",
                        Reason = "NextCommandBefore6Addr", Code = "",
                        TAC_ns = double.NaN, ParentCmdTime = 0, ParentCmdName = parentCmdName
                    });
            }

            // Ambiguous in address window → address-level reject
            if (rnb_all && cle_all && ale_all)
            {
                rejectSink.Add(new CmdAddrReject {
                    Time = s[rise].TimeNs, Kind = "Address",
                    Reason = "AmbiguousCLE_ALE", Code = s[rise].IO.ToUpperInvariant(),
                    TAC_ns = double.NaN, ParentCmdTime = parentCmdTime, ParentCmdName = parentCmdName
                });
                return (false, addrs, detects, null);
            }

            // Address gating must hold (RnB=0 and ALE=1)
            if (!(rnb_all && ale_all))
            {
                rejectSink.Add(new CmdAddrReject {
                    Time = s[rise].TimeNs, Kind = "Address",
                    Reason = "AddressGatingFailed", Code = s[rise].IO.ToUpperInvariant(),
                    TAC_ns = double.NaN, ParentCmdTime = parentCmdTime, ParentCmdName = parentCmdName
                });
                return (false, addrs, detects, null);
            }

            // Measure tADDH for stats/detect (always)
            double hold = measure_hold_ns(s, rise, k => s[k].ALE == 1);
            double hold_r = round2(hold);
            taddhAllSink.Add(hold_r); // <- ALWAYS included in stats

            string addrHex = s[rise].IO.ToUpperInvariant();

            // Detect log (ALWAYS)
            detects.Add(new AcDetectLog {
                Time = s[rise].TimeNs, Stage = "Address", Name = "Address", Code = addrHex,
                TAC_ns = hold_r, ParentCmdTime = parentCmdTime, ParentCmdName = parentCmdName
            });

            // Threshold only affects acceptance of this address within the chain
            if (hold_r < MIN_TADDH_NS)
            {
                rejectSink.Add(new CmdAddrReject {
                    Time = s[rise].TimeNs, Kind = "Address",
                    Reason = "tADDHShort", Code = addrHex,
                    TAC_ns = hold_r, ParentCmdTime = parentCmdTime, ParentCmdName = parentCmdName
                });
                return (false, addrs, detects, null);
            }

            // accept one address
            addrs.Add(addrHex);

            // jump to end of window
            i = (fall >= 0) ? (fall + 1) : (rise + 1);
        }

        if (addrs.Count != 6)
        {
            return (false, addrs, detects,
                new CmdAddrReject {
                    Time = parentCmdTime, Kind = "Command",
                    Reason = "NonAddressWindow", Code = "",
                    TAC_ns = double.NaN, ParentCmdTime = 0, ParentCmdName = parentCmdName
                });
        }

        return (true, addrs, detects, null);
    }

    private static double round2(double v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);
}
