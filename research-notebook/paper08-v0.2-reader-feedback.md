# Paper 8 — reader feedback for v0.2

**Status.** Paper 8 v0.1 is **published**: Zenodo DOI `10.5281/zenodo.21499637`
(https://zenodo.org/records/21499637), repo `main` @ `bb880a0`, runtime anchor
`0bf947b`. v0.1 is frozen. These edits are **repo-only v0.2** — apply when we
open a v0.2 pass; they do not touch the frozen Zenodo v0.1. Reader feedback
received 2026-07-22; analysis below verified against the v0.1 text (disciplined
calibration, not blind concession).

**Open decision (versioning).** Front-matter is `version: 0.1-draft`. Unresolved
whether v0.2 bumps it to `0.2-draft` or stays `0.1-draft` (series convention:
repo evolves, front-matter often stays 0.1-draft; a v0.2 marker only lands on a
new-version Zenodo deposit). Decide at v0.2 time.

---

## APPROVED for v0.2 (Alvaro: "aplica b y d")

### (b) Define the *assembler* early — the real gap
"assembler" is glossed in the TL;DR but first named in the body only at §4
(~L118), with no conceptual definition; a reader may read it as *assembler*
(assembly-language translator) or *orchestrator*. **Fix:** add a definitional
paragraph in §2, right after "…the actor does not speak alone." (the "This is the
paper's claim in one line…" paragraph). Turnkey text:

> Call this other authority the *assembler* — a name worth pinning down, because
> it is neither a compiler nor an orchestrator: it translates no program and
> sequences no work. The assembler is the authority that knows the running
> environment — the deployment the program is assembled into, and the
> destinations that environment makes available: which stores, topics, caches, or
> screens are on hand, and how output reaches them. Where the actor knows *what*
> becomes observable, the assembler knows *where* observation can occur — and it
> knows this not from inside the program but from how the program is wired to the
> world.

(No §4 change needed afterward; "the assembler, which pronounces the destination"
at §4 then flows from an already-named term.)

### (d) §6 close — scope capstone, NO forward references
Reader wanted a final scope-summary + "opening to future papers (topology,
algebra)." **The forward-reference half is REJECTED** — v0.1 deliberately removed
three forward references to the unwritten next paper (§5×2, §8); reintroducing a
"future papers" opening reverses that decision. Apply only the scope-summary
half. §6 already ends (L216) on "output as the first and cleanest axis"; add a
synthesizing capstone paragraph after it, before "## 7. Related Work". Turnkey
text:

> Set together, these limits fix the result's shape rather than merely qualify
> it. Within a trust domain, the three authorities dissolve absent-information
> *by design* — the withheld destination, the withheld account — and there no one
> at either end is left to assert beyond warrant. They do not reach
> absent-information *by disconnection*, the verification a crossed trust boundary
> demands, or the transport that must carry an account to many; each is a real
> problem the division of authorities was never meant to solve. The claim is
> bounded, not hedged: it holds cleanly on the one axis it argues, and stops where
> the knowledge itself stops.

---

## Calibrated / NOT requested (push-back — likely skip)

### (c) "Mention Paper 6 earlier" — mostly already done
Reader said Paper 6 isn't mentioned until §3. **Inaccurate:** it's in the
*Dependencies* note (before §1) — "echoing … the one Paper 6 applied to
infrastructure" — and §3 already makes the exact connection asked for ("Here the
object is a decision, one level in, rather than a component"). Not a gap. Optional
micro-touch only: foreground "same construct, one level in" one clause earlier in
the Dependencies note. Alvaro did not request it.

### (e) "Accountant metaphor too long" — overstated
Reader: it occupies "almost all of §1 and part of §2." **Measured:** 2 of 6 §1
paragraphs (the invoice/MySQL-vs-PostgreSQL example + "the accountant sharpens the
point"); §2 only references it. The metaphor is effective (reader agrees). No
large cut warranted; at most a modest condensation of those 2 paragraphs. Alvaro
did not request it. See [[prose-find-replace-needs-discourse-context]] /
[[paper-review-v02-register-overclaiming]] (calibrate, don't over-cut).

---

## Apply-time checklist (v0.2)
1. Insert (b) in §2 and (d) capstone at §6 end.
2. Decide front-matter versioning.
3. Rebuild `.tex` + `.pdf` (pandoc 3.9 + tectonic 0.16 + header-includes.tex — see
   [[papers-pdf-pipeline]]) and the 8-paper monograph.
4. Commit + push to `origin/main` (repo v0.2; Zenodo v0.1 stays frozen unless a
   new-version deposit is made).
