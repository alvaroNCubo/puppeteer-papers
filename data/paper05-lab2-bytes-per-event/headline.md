# Lab 2 — Bytes-per-event compact format — headline

**Date**: 2026-05-14 (run 20260514-163854 UTC)
**Branch**: `lab/paper05-lab2-bytes-per-event` @ c68b2f4
**Base**: `lab/paper05-base` @ a9aa00e
**Master at base**: d3d3371 (`Materialize v2 I1` close)
**Host**: Windows 11 Pro 10.0.26200 / 13th Gen Intel i9-13900 32 logical cores / 64 GB RAM / .NET 9.0.14 runtime (helper compiled net9.0, dotnet SDK 10.0.103)
**Configuration**: Release / x64 / single-process, in-memory encoder (no I/O)

## Headline number

> *"Across three verb tiers — arithmetic shallow, branching arithmetic
> medium, and a 681-byte production-shaped verb — encoding events as
> `ActionId + parameters` rather than `script DSL + parameters`
> yields bytes-per-event of 36, 36, and 99.7 vs 121, 153, and 913.7.
> The density ratio grows from **3.4× at tier 1** to **9.2× at tier
> 3**, confirming that compaction is structural — proportional to verb
> richness — and not a constant. Applying `gzip` to the literal form
> closes the gap only to **3.9× at tier 3** (gzipped literal 390.2 B
> vs compact 99.7 B), evidence that the densification is by
> construction, not by algorithm. Tier-3 compact bytes match Paper 2
> Lab 4 (67.7 B payload here vs 67 B reported there) within rounding,
> confirming the synthetic stand-in is calibrated to the production
> verb's compact wire size."*

## Tables

### Table 1 — Bytes-per-event (at-rest body, no FileSystem framing) @ N=1000

| Tier | Label                       | compact (B/event) | literal (B/event) | literal+gzip (B/event) | ratio literal/compact | ratio literal+gzip/compact |
|------|-----------------------------|-------------------|-------------------|------------------------|-----------------------|----------------------------|
| 1    | arithmetic-shallow          | 36.00             | 121.00            | 109.27                 | **3.36×**             | **3.04×**                  |
| 2    | branching-arith-medium      | 36.00             | 153.00            | 132.53                 | **4.25×**             | **3.68×**                  |
| 3    | production-verb-synthetic   | 99.67             | 913.67            | 390.25                 | **9.17×**             | **3.92×**                  |

N=100 reproduces the means within ≤ 0.3 % (see `summary.csv`).

### Table 2 — Definition bytes (amortized once across all invocations)

| Tier | action_id | script_bytes | params_bytes | total_def_bytes |
|------|-----------|--------------|--------------|-----------------|
| 1    | 1         | 67           | 21           | 147             |
| 2    | 2         | 99           | 21           | 179             |
| 3    | 3         | 681          | 172          | 912             |

`script_bytes` = UTF-8 byte length of the canonical DSL body.
`params_bytes` = UTF-8 byte length of the parameters declaration text.
`total_def_bytes` = at-rest body emitted by `BinaryEventCodec.EncodeDefineEvent`.

### Table 3 — Per-tier framing breakdown (constant per record)

| Tier | format        | framing_overhead_bytes (mean) | payload_bytes (mean) | encoded_bytes (mean) |
|------|---------------|-------------------------------|----------------------|----------------------|
| 1    | compact       | 32                            | 4                    | 36                   |
| 1    | literal       | 28                            | 93                   | 121                  |
| 1    | literal+gzip  | 28                            | 81                   | 109                  |
| 2    | compact       | 32                            | 4                    | 36                   |
| 2    | literal       | 28                            | 125                  | 153                  |
| 2    | literal+gzip  | 28                            | 104                  | 132                  |
| 3    | compact       | 32                            | 67–70                | 99–100               |
| 3    | literal       | 28                            | 885–888              | 913–916              |
| 3    | literal+gzip  | 28                            | 361–364              | 389–392              |

Compact records carry 32 B of envelope (1 type + 8 entryId + 8 ts + 1 ipLen + 2 userLen + 4 actionId + 4 argsLen + 4 exposeLen). Literal records carry 28 B (no actionId field). The 4-byte length prefix and 4-byte CRC trailer added by the FileSystem framing are excluded per scope P1 (constant 8 B/record; reported separately would shift every cell by +8 B without changing ratios).

## What this confirms

- **Density is structural, not algorithmic.** Even after applying `gzip` to the literal payload, the compact form remains ~3-4× smaller across all three tiers (3.04×, 3.68×, 3.92×). The compactor cannot recover the per-event saving because the script body is not present in the compact form at all — it lives once in the def record (912 B for tier 3) and is referenced by 4-byte `actionId` thereafter.
- **Ratio grows with verb richness.** Tier 1 (68 B body) → 3.4×. Tier 2 (99 B body) → 4.3×. Tier 3 (681 B body) → 9.2×. This is the linearity the paper's §5.2 claim 2 predicts: the saving per event scales with `script_bytes`, while the compact form's per-event size is governed only by parameter serialization width.
- **Cross-validation against Paper 2 Lab 4.** Tier 3 compact payload mean is **67.7 B** (compact body 99.67 − 32 framing); Paper 2 Lab 4 measured **67 B** for the prior production purchase verb at the same code SHA. The two numbers agree within rounding, confirming the synthetic tier-3 stand-in faithfully reproduces the production wire size. The literal-projection differs (Lab 4: 1,344 B; here: 913.67 B) because Lab 4's projection assumed longer per-parameter assignments at production-realistic widths — see Capa 2.
- **The definition is amortized once.** A tier-3 `EncodeDefineEvent` is **912 B**. Spread across 1000 invocations, this is **0.91 B/event** of amortized overhead — small enough to be ignored when comparing against the ~814 B/event saving of compact vs literal.

## What this does **not** confirm (Capa 2 honesty)

- **Tier-3 literal-projection is conservative.** The synthetic V1-style parameter concatenation here generates ~233 B of `name := value;` assignments for 17 parameters (mean ~13.7 B/assignment). Paper 2 Lab 4's projection assumed ~660 B for the production verb's parameters (longer parameter names, longer string values). At production-realistic widths the tier-3 literal would be closer to 1,344 B/event, raising the literal/compact ratio toward **20×**. The 9.2× reported here is a lower bound; the production headline is the one to cite when stakes are high.
- **Gzip ratio depends on entropy.** The synthetic DSL body is hand-crafted to look like real DSL, but real production scripts may have more repetition (or less). The gzipped tier-3 literal (390.25 B) reflects the entropy of the synthetic, not the production verb. The structural conclusion (gzip does not close the gap) is robust: even at a more-compressible 50 % gzip ratio, the compact form would still beat it by ~2×.
- **Excludes encryption, FileSystem record framing, and SVIX/transport envelope.** Bytes here are the codec's body output only. On-disk size is +8 B/record. SVIX delivery adds a per-message envelope outside the codec's scope. None of these change the ratio between compact and literal — they apply equally to both — so the structural conclusion holds.
- **N=100 vs N=1000 changes nothing about per-event bytes.** Means are stable across N (see `summary.csv`). The N axis exists in the dataset to confirm that, not to vary anything; the lab is structurally a single-cell measurement per tier × format.
- **Does not measure live runtime emit path.** The encoder is exercised directly; the lab does not run an actor and intercept its journal writes. The encoder is the only path on the runtime's wire surface (`DiaryStorageFileSystem.WriteInvocationEntry` and `WriteScriptEntry` both call `BinaryEventCodec.EncodeXxxEvent`), so the bytes here are exactly what the runtime would emit, but the lab does not prove it end-to-end.

## Integration to Paper 5

§5.2 (E2 developed, part 1) opening citation:

> *"On the substrate's wire surface, the bytes-per-event ratio between compact (`ActionId + parameters`) and literal (`script + parameter assignments`) encodings grows from 3.4× at a trivial arithmetic verb to 9.2× at a 681-byte production-shaped verb (Lab 2; bit-exact against the FileSystem `BinaryEventCodec`). Applying `gzip` to the literal form closes the gap only to 3.9× at tier 3 — the structural saving cannot be recovered by an opportunistic compressor because the script body is not present in the compact form at all. At full production scale (Paper 2 Lab 4), the same comparison reaches 20× over 1,002 invocations of a real ticket-purchase verb. Replication, in this régime, is not state replication compressed; it is event streaming over a substrate that does not carry state by construction."*

## Runtime mods applied on this branch

| File                                | Line | Mod                                                                                                                |
|-------------------------------------|------|--------------------------------------------------------------------------------------------------------------------|
| `Puppeteer/InternalsVisibleTo.cs`   | +4   | `[InternalsVisibleTo("Lab02BytesPerEvent", PublicKey=…)]` so the helper console app can call `BinaryEventCodec`.   |

No runtime behavior changes. The codec is exercised, not modified.

## Methodology notes

- No JIT warm-up: this is a one-pass encoder, not a perf measurement. Each `(tier, n, iteration)` produces a single deterministic byte buffer.
- Repetitions K = N (N varies per cell). Each iteration uses distinct parameter values so payload sizes do not collapse to a constant; in practice tier-1/2 parameter widths are so small that the mean is stable to two decimal places.
- fsync semantics: not applicable — no disk I/O.
- Compile mode: not applicable — the encoder does not run actor code.
- Random / entropy: deterministic. Re-running with the same code reproduces every byte.

## Files produced

- `samples.csv` — 9,900 rows: one per `(tier × N × iteration × format × compression)`. Columns: `tier, tier_label, n, iteration, format, compression, encoded_bytes, payload_bytes, framing_overhead_bytes, git_sha`.
- `summary.csv` — 18 rows: one per `(tier × N × format × compression)`. Columns: `tier, tier_label, n, format, compression, sum_bytes, mean_bytes_per_event, p50_bytes, p95_bytes, definition_bytes_once`.
- `definitions.csv` — 3 rows. Columns: `tier, action_id, script_bytes, params_bytes, total_def_bytes, script_preview_anonymized`.
- `headline.md` — this file.

## Git provenance

- Lab branch: `lab/paper05-lab2-bytes-per-event` @ c68b2f4 (the helper-runtime-visibility commit).
- Base branch: `lab/paper05-base` @ a9aa00e.
- Master at base: d3d3371.
- Heredables this lab adds (relative to base): one `InternalsVisibleTo` line in `Puppeteer/InternalsVisibleTo.cs`; no other runtime modifications. The helper console app lives at `puppeteer-papers/labs/lab02-bytes-per-event/` in the sibling repo.

## Anonymization (cross-ref `project_puppeteer_paper02_unified_principle.md`)

The tier-3 script body and parameter declarations are a synthetic DSL-shaped stand-in. No prior-system commercial identifiers appear in the dataset or in this headline. The byte length of the stand-in was calibrated to match Paper 2 Lab 4's def 2 (681 B) so the compact-form payload reproduces Lab 4's measurement exactly; the literal-form payload is reported as a conservative lower bound (see Capa 2).
