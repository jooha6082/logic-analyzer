# Crash-Consistent AI Checkpoints (macOS/APFS)

**Mini‑research project for AI Infrastructure Reliability — with emphasis on Storage & Filesystem integrity.**  
This work explores *how AI training checkpoints can remain crash‑consistent, detectable for corruption, and recoverable automatically*.  
It provides a reproducible, small‑scale experiment that mirrors large‑scale reliability problems in data‑intensive AI systems.

---

## 🔧 Quick Start
```bash
git clone <git@github.com:jooha6082/ckpt-integrity.git> ckpt-integrity
cd ckpt-integrity
python3 -m venv .venv && . .venv/bin/activate
pip install -r requirements.txt
make -C repro repro_all        # one-click full experiment
```

---

## 🧩 Structure
| Folder | Role |
|---------|------|
| `src/aiwork` | checkpoint writers (single/group) |
| `src/guard`  | integrity scanner + rollback |
| `tools/` | summarizers, plotting, automation |
| `trace/` | outputs (ckpts, CSVs, logs) |
| `figures/` | generated charts |

---

## 🚀 Key Targets
```bash
make baseline_torch summary_torch      # single-file integrity
make group_fuzz summary_group          # group atomicity under crash
make bench_group summary_bench         # latency p50/p90/p99
make trace_one timeline plot_timeline  # cross-layer timeline
make rollback_latest                   # recovery demo
```

---

## 📊 Artifacts
| CSV | Figure |
|------|--------|
| `bench_summary.csv` | `bench_bars.png` |
| `bench_group.csv` | `bench_group_cdf.png` |
| `group_summary.csv` | `group_bars.png`, `groups_reasons.png` |
| `torch_mode_summary.csv` | `torch_mode_bars.png` |
| `timeline.csv` | `timeline.png` |

---

## 🧠 Summary
- **Problem:** AI training checkpoints can be *torn* by crashes or *silently corrupted* by storage faults.  
- **Method:** compare unsafe vs. atomic write protocols; add SHA‑256 integrity guard and rollback.  
- **Evaluation:** measure latency, robustness, and detection coverage under injected faults.  
- **Result:** atomic_dirsync fully prevents corruption, adding ~40–70 % latency overhead versus unsafe writes.

---

## 🧰 Reproduce
All results regenerate via:
```bash
make repro_all
```
Outputs → `trace/` (CSVs) and `figures/` (plots).
---

---

## Sample Results (illustrative)

Below are **small, hand‑crafted examples** that mirror the actual CSV shapes. They are for orientation only.

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

- Stats are computed from **all pin‑detected windows** (rounded to F2).  
- Values **below minimum** (e.g., `tCMDH=15.00 ns`) are included in stats.  
- Ambiguous/gating‑fail windows are not measured and thus not included.

### 4) AC Detect Log (`*.ac_params_detect_log.csv`)

| Time |  Stage  |  Name   | Code | TAC(ns) | ParentCmdTime | ParentCmdName |
|-----:|:-------:|:-------:|:----:|:-------:|--------------:|:--------------|
| 100  | Command | Program |  20  |  15.00  |               |               |
| 180  | Command | Program |  20  |  30.00  |               |               |
| 185  | Address | Address |  A0  |  25.00  |           180 | Program       |
| 210  | Address | Address |  A1  |  35.00  |           180 | Program       |
| 235  | Address | Address |  A2  |  25.00  |           180 | Program       |
| 260  | Address | Address |  A3  |  30.00  |           180 | Program       |
