
---

### `Makefile`
```make
# Makefile for logic-analyzer
# Minimal build/run helpers for .NET 9 console app.
# Usage:
#   make build
#   make run IN=data/nand_raw_mixed_2k.csv
#   make demo
#   make clean

SHELL := /bin/bash

DOTNET  ?= dotnet
PROJECT ?= logic-analyzer.csproj
CONFIG  ?= Release

DATADIR ?= data
OUTDIR  ?= out

# Default input (can be overridden: make run IN=path/to/input.csv)
IN ?= $(DATADIR)/nand_raw_mixed_2k.csv

# Derive output file names based on input file name
BASE := $(basename $(notdir $(IN)))
OUT_COMPLETE := $(OUTDIR)/$(BASE).complete_cmd_addr.csv
OUT_REJECT   := $(OUTDIR)/$(BASE).cmd_addr_reject_log.csv
OUT_STATS    := $(OUTDIR)/$(BASE).ac_params_stats.csv
OUT_DETECT   := $(OUTDIR)/$(BASE).ac_params_detect_log.csv

# Demo input prefers the *_with_violations.csv if present, else falls back to IN
DEMO_IN := $(if $(wildcard $(DATADIR)/nand_raw_mixed_2k_with_violations.csv),$(DATADIR)/nand_raw_mixed_2k_with_violations.csv,$(IN))

.PHONY: help build run demo clean ensure-out

help:
	@echo "Targets:"
	@echo "  make build                 - Build the project ($(CONFIG))"
	@echo "  make run IN=<input.csv>    - Run analyzer on a CSV input (default: $(IN))"
	@echo "  make demo                  - Run with mixed-with-violations CSV if available"
	@echo "  make clean                 - Clean bin/obj and out"

build:
	$(DOTNET) build -c $(CONFIG)

run: ensure-out
	$(DOTNET) build -c $(CONFIG)
	$(DOTNET) run --project $(PROJECT) -- $(IN) $(OUT_COMPLETE) $(OUT_REJECT) $(OUT_STATS) $(OUT_DETECT)
	@echo ""
	@echo "Outputs:"
	@echo "  -> $(OUT_COMPLETE)"
	@echo "  -> $(OUT_REJECT)"
	@echo "  -> $(OUT_STATS)"
	@echo "  -> $(OUT_DETECT)"

demo:
	$(MAKE) run IN=$(DEMO_IN)

ensure-out:
	@mkdir -p $(OUTDIR)

clean:
	@rm -rf bin obj $(OUTDIR)
