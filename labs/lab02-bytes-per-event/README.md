# Lab — Bytes-per-event (Paper 5, L2)

Producer code for the **L2** measurement of [Paper 5 — *The Journal as Substrate*](../../05-substrate-operations.md), §5.2 (*The compact wire*).

> **Note on naming.** This folder is named `lab02-bytes-per-event/` for historical reasons; the paper, the dataset, and the source branch all use the unambiguous identifier `paper05-lab2-bytes-per-event`. The two refer to the same lab.

## What it measures

The codec on the substrate's wire surface. Two encodings are exercised against three tiers of verb richness:

- **Compact form** — `ActionId` + parameters (the V2 wire form: a 4-byte reference to a named verb plus its arguments).
- **Literal form** — script DSL body + `name := value;` parameter assignments (the V1 self-contained Script entry).

For each tier, the literal form is also encoded with an opportunistic `gzip` pass to test whether the structural saving of the compact form can be recovered by an after-the-fact compressor. It cannot: gzip closes the gap from 9.2× to 3.9× at the production-shaped tier.

The three tiers — `arithmetic-shallow`, `branching-arith-medium`, `production-verb-synthetic` (681 B body) — are designed so the tier-3 compact payload calibrates to Paper 2 Lab 4's measured production verb (67 B compact ≈ 67.7 B here).

## Claim it supports

Paper 5 §5.2 (E2 part 1):

> *"On the substrate's wire surface, the bytes-per-event ratio between compact and literal encodings grows from 3.4× at a trivial arithmetic verb to 9.2× at a 681-byte production-shaped verb. Applying `gzip` to the literal form closes the gap only to 3.9× at tier 3 — the structural saving cannot be recovered by an opportunistic compressor."*

Headline numbers and full methodology live in the dataset's [`headline.md`](../../data/paper05-lab2-bytes-per-event/headline.md).

## How to run

This is a .NET 9 console application that links against the Puppeteer runtime's `BinaryEventCodec`. It requires a sibling clone of the Puppeteer source tree (currently in a private repository; a public-mirror URL will be added when the codebase is released).

```bash
# Expected layout:
#   <parent>/puppeteer-papers/labs/lab02-bytes-per-event/   ← this folder
#   <parent>/Puppeteer-Pacifico-lab-p05l2/                  ← sibling Puppeteer checkout
#                                                            on lab/paper05-lab2-bytes-per-event @ c68b2f4

dotnet run --project Lab02BytesPerEvent.csproj -c Release
# or, with an explicit repo root:
dotnet run --project Lab02BytesPerEvent.csproj -c Release -- <path-to-Puppeteer-checkout>
```

Outputs are written to `<Puppeteer-checkout>/UnitTestPuppeteer/PaperLabs/paper5/lab2-bytes-per-event/results/run-<UTC>-<sha>/`:

- `samples.csv` — one row per `(tier × N × iteration × format × compression)`, 9 900 rows total.
- `summary.csv` — grouped aggregates: sum, mean, p50, p95 bytes per cell.
- `definitions.csv` — per-tier definition record sizes (amortized once across invocations).

## Determinism and scope

- No JIT warm-up, no disk I/O, no live actor. The encoder is exercised directly; bytes produced here are exactly what the runtime would emit on its wire surface (`DiaryStorageFileSystem.WriteInvocationEntry` and `WriteScriptEntry` both call `BinaryEventCodec.EncodeXxxEvent`).
- Each run is reproducible to the byte at the same Git SHA.
- The tier-3 script body and parameter declarations are a synthetic DSL-shaped stand-in calibrated to the byte length of Paper 2 Lab 4's production verb (681 B). No production identifiers appear in the source or in any output.

## Honest limits

The tier-3 literal projection is conservative. At production-realistic parameter widths the literal/compact ratio approaches 20× (the figure Paper 2 Lab 4 reports against a real ticket-purchase verb); the 9.2× headline here is a lower bound. The lab does not run an actor end-to-end — only the codec on the wire surface. See [`headline.md`](../../data/paper05-lab2-bytes-per-event/headline.md) §*What this does not confirm* for the full Capa 2 disclosure.
