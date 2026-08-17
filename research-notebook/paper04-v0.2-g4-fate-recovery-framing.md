# Paper 4 → v0.2 notes: align G4 framing with the in-process fate map (crash-window recovery)

Source: reader review of v3 (the same reviewer who *praised* G4 as a strength). The reviewer now
flags that `topologies.md §4` — and `saga.md` for a different subsystem — say the delivery fate
record is in-process and empty after a process restart, which is exactly the failure G4 claims to
recover from. Paper 4 is **published**, so this is a v0.2 edit note, not an edit to the live text.

Verified against: `04-cross-actor-continuity.md` (Claim 9 `:83`, §5.4 `:480`, §8.5 G4 `:699`),
`labs/lab04-tell/Program.cs` (`G4_TellFateRecovery` `:458`, `RecoverWithFate` `:445`,
`StageCrashWindowTell` `:433`), and the runtime:
`Choreography/Transport/Brokered/BrokerTellTransport.cs:26-31,41-42,142-147`,
`Choreography/Transport/Brokered/IMessageBroker.cs:26-30`,
`Puppeteer/Tell/InMemoryTransport.cs:100-116`,
`Puppeteer/EventSourcing/ActorHandler.cs:729-772` (`RecoverPendingTells`).

---

## Verdict

**The reviewer is technically correct — and this time it is NOT stale** (contrast the multi-event
replay flag, which was already fixed). But the paper is **honest, not false**: the flank is
*framing/emphasis*, not a wrong claim. The escape clause the reviewer wants is already in the text in
three places. The fix is to **promote the process-crash case from a subordinate escape clause to a
first-class outcome**, and to scope the word "recovery."

Ground truth from the code:

- `BrokerTellTransport.fates` is a plain in-process `ConcurrentDictionary` (`:41-42`). Its own class
  comment (`:26-31`) states it: *"after a process restart the map is empty and GetFateAsync honestly
  answers InFlight."* The reviewer's citation is exact.
- `RecoverPendingTells` (`ActorHandler.cs:729-772`) does nothing but cite `transport.GetFateAsync`
  per pending tell: `Delivered`→journal ack, `Failed`→journal verdict, `InFlight`/throw→leave
  pending. So after a real process restart a fresh `BrokerTellTransport` answers `InFlight` for
  **every** pending tell → all stay pending. No definite verdict is recovered at rehydration.
- The durable fix is a genuine feature, not a patch: the broker seam `IMessageBroker.Subscribe`
  delivers only *"from the subscription point onward"* (`IMessageBroker.cs:26-30`) — no
  replay-from-offset — so reconstructing fate from the durable ack topic needs an interface
  extension. The code comment already calls this *"a later iteration."*
- **What G4 actually proves:** the lab stages the transport's testimony with
  `InMemoryTransport.SetFate(...)`, whose comment says it *"Models the durable record a real
  transport keeps"* (`InMemoryTransport.cs:110`). The whole lab runs in one process; "recovery" =
  a fresh actor over the same in-memory store + a transport *told what to testify*. So G4 validates
  the **sender-side machinery given a transport that can still testify** — effectively the
  *logical-rehydration / surviving-authority* case, plus one genuine `InFlight` case. It does not
  exercise a broker fate record surviving a process crash.

## Two nuances that are load-bearing for the defense (don't over-correct)

1. **G4's crash is in the *dispatch window*** — the envelope was journaled but never handed to the
   transport. Those tells would be `InFlight` *even with a durable fate store* (the transport never
   saw them). Definite-verdict recovery (`Delivered`/`Failed`) only applies to tells that *were*
   dispatched and then lost the ack/failure round-trip — and only if the transport's fate record
   survives. So the honest split is: dispatched-then-lost + surviving transport → verdict;
   everything else → honest pending.
2. **Stranded pending tells are not orphaned forever in a replicated deployment.** On a red-black
   takeover, `ActorHandler.ReissuePendingTells` re-dispatches the retained pending envelopes
   (last-turn finding; `ActorHandler.cs:3635/3645`). A lone-actor cold restart is the case with no
   re-issuer — there they stay honestly pending until a testifying transport (or a future durable
   fate store) settles them.

Net: "the transport, the sole authority on delivery" is a **separation-of-concerns** claim (delivery
belongs to the transport, correlation to the journal) — keep it. The problem is only that the worked
examples lead with testimony (unacked/ack) and demote `InFlight` to a tail clause, so a reader takes
definite-verdict recovery as the normal post-crash outcome. For a true process crash it is the
exception.

---

## Concrete edit targets (v0.2)

**1. §8.5 G4 (`:699`) — promote the process-crash outcome; scope "recovery."**
Keep the current three-verdict narration, but after "*…nothing while the fate is still in flight (the
transport keeps ownership)*" make the crash-scope explicit as a first-class case, not only the later
"cannot testify" aside. Suggested insertion (adapt to voice):

> The scope of *recovery* here depends on what the failure took with it. When the transport's own
> record of delivery survives — an actor instance rehydrated while its process (and transport) lived
> on, or a transport backed by a durable fate store — a dispatched tell whose acknowledgment was lost
> is recovered to a definite verdict. When the failure is a full process restart, the transport's
> in-process fate record is gone (`BrokerTellTransport` keeps it in memory only), so every pending
> tell is answered `InFlight` and left honestly pending — to be settled later by the transport's own
> redelivery for tells that were dispatched, or by a takeover re-issue for tells stranded before
> dispatch. The guarantee G4 makes is therefore about the *honest record*, not a resurrected verdict:
> after a crash the journal never asserts a send that did not land — it either carries the verdict a
> surviving authority can still give, or it carries the tell as pending. It does not manufacture a
> verdict a restarted, amnesiac transport cannot supply.

Then the existing "*a transport that cannot testify … answers InFlight*" sentence reads as the
general rule it already is, rather than an edge case.

**2. §5.4 (`:480`) — one clause on the in-process record.** The sentence already hedges "*bounded by
what that transport can still testify*." Extend minimally: after that clause add "— and a process
restart empties an in-process fate record, so across a cold restart every in-flight tell is bounded
to `InFlight`/pending rather than a verdict." Keeps the paragraph honest without a rewrite.

**3. Claim 9 (`:83`) — retune the verb.** "*recovers each tell's fate*" over-promises. Change to
something like "*records each tell's fate honestly — a definite verdict when the transport can still
testify, otherwise honestly pending*." (The clause already lists "honestly pending"; only the framing
verb "recovers" needs softening.)

**4. Subtitle already helps.** G4's own subtitle is "*the honest record under crash*" — lean on that
framing throughout; avoid letting "Tell-fate **recovery**" imply verdict-resurrection is the default.

**Optional lab strengthening (makes the boundary empirical, not asserted):** add a G4 sub-case that
builds a real `BrokerTellTransport` over an `InProcessBroker`, sends + acks a tell so its `fates`
holds `Delivered`, then **drops and re-creates the transport** (simulating the process restart) and
shows `GetFateAsync` → `InFlight` → the tell stays pending. That converts the honest limit into a
demonstrated property and pre-empts the "a code reader finds `BrokerTellTransport.cs:26-31`" objection
by showing the paper already tests it.

**Roadmap cross-ref:** the real elimination of the limit is a durable, replay-from-offset fate store
(needs an `IMessageBroker` replay seam). If pursued, G4 could later claim verdict recovery across a
process restart unconditionally. Track separately; not required for v0.2.

---

## Checklist for the 0.2 pass

- [ ] §8.5 G4: insert the crash-scope paragraph; demote the "cannot testify" line to general rule.
- [ ] §5.4: add the in-process-record clause to the existing "bounded by what that transport can
      still testify" sentence.
- [ ] Claim 9: soften "recovers each tell's fate" → verdict-or-honestly-pending.
- [ ] (Optional) lab: add the drop-transport G4 sub-case demonstrating `InFlight` after restart.
- [ ] Confirm no other section (abstract, §7.x) leans on "recovery" as verdict-resurrection.
- [ ] Guide already aligned (topologies.md §4, this turn's prior edits) — no guide change needed.
