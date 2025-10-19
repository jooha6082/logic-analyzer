# Logic-Waveform Analyzer for NAND (mini, reproducible)

Small, trace‑driven analyzer for NAND logic‑analyzer CSV (event‑based). It decodes
**commands/addresses**, measures **AC parameters** (`tCMDH`, `tADDH`), and exports compact CSV reports.

## Background
- A real customer user case showed misbehavior; they requested analysis with probe captures.
- Internal tool (C#/Python, 2024–present) decodes and flags AC‑timing/order issues, visualizes sequences,
  and exports min/max/avg/std timing stats. It detects PIR misuse and timing hazards, enabling fast reproduction and
  **>90% reduction** in customer debug turnaround.
- Technical paper (internal review): *A Method to Measure NAND Flash Operation Timing Parameter Using Logic Analyzer Signal Waveform*
  (Product Engineering Tech Conference).
- This repo is a **public, minimal** reproduction (no confidential data).

## What it produces
1. **Complete Commands** — one row only if a command is followed by **six valid addresses**.  
2. **Filtered (Rejects)** — command/address entries dropped, with reasons.  
3. **AC Parameter Stats** — stats of **all pin‑detected** `tCMDH`/`tADDH` (F2), no time‑based filtering.  
4. **AC Detect Log** — **all pin‑detected** command/address windows (F2), stream order (no sorting).

## Input (event‑based CSV)
Header (fixed):
```
Time(ns), IO, nCE0, ALE, CLE, nWE, nRE, RnB, nWP, DQS
```
Guidelines: add a row only when pins change. Typical: `nCE0`=0 from 2nd event, `nRE/nWP/DQS`=1.
- Command window (per WE‑high): **`RnB=0 & CLE=1`** across the whole WE‑high.
- Address window (per WE‑high): **`RnB=0 & ALE=1`** across the whole WE‑high.
- Ambiguous (CLE=1 & ALE=1 simultaneously) → reject.

AC definitions (pin‑based only):
- `tCMDH`: WE rising → first time **CLE** goes 0.  
- `tADDH`: WE rising → first time **ALE** goes 0.  
- Thresholds (acceptance only): `tCMDH ≥ 20 ns`, `tADDH ≥ 30 ns` (values below are still recorded in stats/log).

## Quick Start
Requirements: **.NET SDK 9.x**
```bash
dotnet build -c Release
IN="data/nand_raw_mixed_2k.csv"
OUTDIR="out"
BASE="$(basename "${IN%.*}")"

mkdir -p "$OUTDIR"

dotnet run --project logic-analyzer.csproj -- \
  "$IN" \
  "$OUTDIR/$BASE.complete_cmd_addr.csv" \
  "$OUTDIR/$BASE.cmd_addr_reject_log.csv" \
  "$OUTDIR/$BASE.ac_params_stats.csv" \
  "$OUTDIR/$BASE.ac_params_detect_log.csv"
```
Outputs (to `out/`):
```
<input>.complete_cmd_addr.csv
<input>.cmd_addr_reject_log.csv
<input>.ac_params_stats.csv
<input>.ac_params_detect_log.csv
```

## Sample Results (illustrative)

Small, hand‑crafted examples that mirror the actual CSV shapes.

### 1) Complete Commands (`*.complete_cmd_addr.csv`)

| Time | CMD     | ADD1 | ADD2 | ADD3 | ADD4 | ADD5 | ADD6 |
|-----:|---------|:----:|:----:|:----:|:----:|:----:|:----:|
| 120  | Program |  A0  |  A1  |  A2  |  A3  |  A4  |  A5  |
| 640  | Erase   |  10  |  20  |  30  |  40  |  50  |  60  |

### 2) Filtered (Rejects) (`*.cmd_addr_reject_log.csv`)

| Time | Kind     | Reason              | Code | TAC(ns) | ParentCmdTime | ParentCmdName |
|-----:|----------|---------------------|:----:|:-------:|--------------:|:--------------|
| 100  | Command  | tCMDHShort          |  20  |  15.00  |               |               |
| 205  | Address  | tADDHShort          |  A2  |  25.00  |           180 | Program       |
| 310  | Address  | AddressGatingFailed |  B1  |         |           280 | Program       |
| 410  | Command  | AmbiguousCLE_ALE    |  10  |         |               |               |

### 3) AC Parameter Stats (`*.ac_params_stats.csv`)

| Parameter |  AVG  |  MIN  |  MAX  |  STDV |
|:---------:|:-----:|:-----:|:-----:|:-----:|
|  tCMDH    | 28.75 | 15.00 | 45.00 |  7.65 |
|  tADDH    | 34.10 | 25.00 | 50.00 |  6.12 |

*Stats are computed from **all pin‑detected windows** (rounded to F2). Values **below minimum** are included. Ambiguous/gating‑fail windows are not measured and thus not included.*

### 4) AC Detect Log (`*.ac_params_detect_log.csv`)

| Time |  Stage  |  Name   | Code | TAC(ns) | ParentCmdTime | ParentCmdName |
|-----:|:-------:|:-------:|:----:|:-------:|--------------:|:--------------|
| 100  | Command | Program |  20  |  15.00  |               |               |
| 180  | Command | Program |  20  |  30.00  |               |               |
| 185  | Address | Address |  A0  |  25.00  |           180 | Program       |
| 210  | Address | Address |  A1  |  35.00  |           180 | Program       |
| 235  | Address | Address |  A2  |  25.00  |           180 | Program       |
| 260  | Address | Address |  A3  |  30.00  |           180 | Program       |

## Notes
- Command naming: `0x30→Erase`, `0x20→Program`, `0x10→Read`, `0x00→Reset`, others→`Unknown`.
- No sorting: files preserve input stream order for forensic review.
- Educational/demo scope; do not include customer‑identifying data.

## License
Provided for research, teaching, and demonstration.
