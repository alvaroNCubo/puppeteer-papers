# The Census Codebook

The census of the paper's §6 — *every named artifact stays within the vocabulary of a single
subject; none names a trajectory spanning subjects* — is a coding task, and this file is its
codebook: the population rule, the lexicon rule, the two coding rules, the manual codings with
their reasons, and the full coded table. The rules are also implemented, verbatim, in
`EveryNameStaysWithinOneSubjectsVocabularyLab.cs`, which re-derives everything below from the
pinned corpus clones and asserts the counts — so the coder is re-runnable, and whoever runs it
is a further coder.

## Population (rule P)

Two subsystems — the two repertoires the paper's composed verb draws on: the commerce system's
ordering bounded context, and the meetings system's payments module, at the pinned corpus
commits (`9b4f943`, `91c8ef2`). Three layers:

- **P1 — domain events.** Declared in the subsystem's domain layer: the ordering domain's
  events folder; classes deriving the payments domain's own domain-event base.
- **P2 — integration events.** The subsystem's integration vocabulary: events it **declares**,
  plus foreign events it **handles** (a handler class named `<Event>Handler` in its application
  layer). Declared-only would understate the vocabulary; handled-only would double-count.
- **P3 — commands.** Imperative artifacts named `*Command` in the subsystem's application
  layer. Handlers are excluded; generic infrastructure wrappers (generic arity > 0) are
  excluded; queries are another grammatical mood and are out of population.

Reproduced denominator: **27 domain events, 16 integration events, 29 commands = 72 artifacts.**

## Lexicon (rule L)

Per subsystem, derived mechanically from the corpus itself — no name added by hand:

- ordering: directory names under the domain's aggregates folder, `Aggregate` suffix stripped →
  `Buyer`, `Order`.
- payments: classes deriving `AggregateRoot` in the domain →
  `MeetingFee`, `MeetingFeePayment`, `Payer`, `PriceListItem`, `Subscription`,
  `SubscriptionPayment`, `SubscriptionRenewalPayment`.

Matches consume the identifier longest-first at token boundaries, plural `s` included — so
`ExpireSubscriptions` references `Subscription` rather than nothing.

## Coding, rule T1 (mechanical)

Strip the layer suffix (`IntegrationEvent`, `DomainEvent`, `Command`, `Event`); consume
aggregate names as above.

- **T** — *names a trajectory spanning subjects*: the identifier references **two or more
  distinct aggregates** of its own system. No verb, no ordering, no judgment required.
- **S** — *stays within one subject's vocabulary*: references exactly one aggregate.

Rule T1 is generous **within its form** — two subjects' nouns in one name suffice — and blind
outside it, which is why it does not code the residual.

## Coding, rule T2 (manual residual)

A trajectory can be named without naming any constituent — *Checkout*, *Onboarding*,
*CompletePurchase* — and a rule that counts aggregate references cannot see that form. Rule T2
therefore hands to **manual coding** every identifier T1 cannot see:

- identifiers referencing **no aggregate at all**, and
- identifiers bearing a word from the published process lexicon:
  `Checkout`, `Onboarding`, `Fulfillment`, `Purchase`, `Process`, `Flow`, `Journey`,
  `Pipeline`, `Saga`, `Workflow`, `Trajectory`, `Lifecycle` (author-supplied, open — extending
  it is part of the defeat condition).

Every manual coding is published below with its reason, and a residual identifier without a
published coding **fails the run**: silence is not a coding. These rows carry code `S*` in the
table.

| residual artifact | manual code | reason |
|---|---|---|
| `GracePeriodConfirmedIntegrationEvent` | S | confirms one wait-state of the ordering process; no second subject's operation is named |
| `MeetingAttendeeAddedIntegrationEvent` | S | one transition of another module's aggregate, which the subsystem merely bills |
| `NewUserRegisteredIntegrationEvent` | S | one transition of the user-access subject |

## Result and agreement

- Rule T1, mechanical: **T: 0 of 72**.
- Rule T2, manual residual: **3 of 72**, each coded **S** with its reason above.
- The author's independent reading also coded every artifact S: **raw agreement 72/72**.
- With both codings placing every artifact in a single category there is no variance for a
  chance-corrected coefficient to correct (Cohen, 1960) — κ is undefined, not withheld. The
  auditable substitutes are this codebook, the re-runnable coder, the manual rows, and the
  defeat condition.

**Defeat condition, cheap to run:** exhibit one artifact in either subsystem whose name refers
to a trajectory spanning operations of more than one subject — in either form. Two subjects'
nouns in one name: rule T1 codes it T mechanically. A single word naming the whole — a
*CompletePurchase*, a *Checkout*: rule T2's manual lane exists to see it, and its lexicon is
yours to extend.

## The coded table

| # | code | system | layer | artifact | aggregates referenced |
|---|---|---|---|---|---|
| 1 | S | ordering | command | `CancelOrderCommand` | Order |
| 2 | S | ordering | command | `CreateOrderCommand` | Order |
| 3 | S | ordering | command | `CreateOrderDraftCommand` | Order |
| 4 | S | ordering | command | `SetAwaitingValidationOrderStatusCommand` | Order |
| 5 | S | ordering | command | `SetPaidOrderStatusCommand` | Order |
| 6 | S | ordering | command | `SetStockConfirmedOrderStatusCommand` | Order |
| 7 | S | ordering | command | `SetStockRejectedOrderStatusCommand` | Order |
| 8 | S | ordering | command | `ShipOrderCommand` | Order |
| 9 | S | ordering | domain event | `BuyerAndPaymentMethodVerifiedDomainEvent` | Buyer |
| 10 | S | ordering | domain event | `OrderCancelledDomainEvent` | Order |
| 11 | S | ordering | domain event | `OrderShippedDomainEvent` | Order |
| 12 | S | ordering | domain event | `OrderStartedDomainEvent` | Order |
| 13 | S | ordering | domain event | `OrderStatusChangedToAwaitingValidationDomainEvent` | Order |
| 14 | S | ordering | domain event | `OrderStatusChangedToPaidDomainEvent` | Order |
| 15 | S | ordering | domain event | `OrderStatusChangedToStockConfirmedDomainEvent` | Order |
| 16 | S* | ordering | integration event | `GracePeriodConfirmedIntegrationEvent` | — |
| 17 | S | ordering | integration event | `OrderPaymentFailedIntegrationEvent` | Order |
| 18 | S | ordering | integration event | `OrderPaymentSucceededIntegrationEvent` | Order |
| 19 | S | ordering | integration event | `OrderStartedIntegrationEvent` | Order |
| 20 | S | ordering | integration event | `OrderStatusChangedToAwaitingValidationIntegrationEvent` | Order |
| 21 | S | ordering | integration event | `OrderStatusChangedToCancelledIntegrationEvent` | Order |
| 22 | S | ordering | integration event | `OrderStatusChangedToPaidIntegrationEvent` | Order |
| 23 | S | ordering | integration event | `OrderStatusChangedToShippedIntegrationEvent` | Order |
| 24 | S | ordering | integration event | `OrderStatusChangedToStockConfirmedIntegrationEvent` | Order |
| 25 | S | ordering | integration event | `OrderStatusChangedToSubmittedIntegrationEvent` | Order |
| 26 | S | ordering | integration event | `OrderStockConfirmedIntegrationEvent` | Order |
| 27 | S | ordering | integration event | `OrderStockRejectedIntegrationEvent` | Order |
| 28 | S | payments | command | `ActivatePriceListItemCommand` | PriceListItem |
| 29 | S | payments | command | `BuySubscriptionCommand` | Subscription |
| 30 | S | payments | command | `BuySubscriptionRenewalCommand` | Subscription |
| 31 | S | payments | command | `ChangePriceListItemAttributesCommand` | PriceListItem |
| 32 | S | payments | command | `CreateMeetingFeeCommand` | MeetingFee |
| 33 | S | payments | command | `CreateMeetingFeePaymentCommand` | MeetingFeePayment |
| 34 | S | payments | command | `CreatePayerCommand` | Payer |
| 35 | S | payments | command | `CreatePriceListItemCommand` | PriceListItem |
| 36 | S | payments | command | `CreateSubscriptionCommand` | Subscription |
| 37 | S | payments | command | `DeactivatePriceListItemCommand` | PriceListItem |
| 38 | S | payments | command | `ExpireSubscriptionCommand` | Subscription |
| 39 | S | payments | command | `ExpireSubscriptionPaymentCommand` | SubscriptionPayment |
| 40 | S | payments | command | `ExpireSubscriptionPaymentsCommand` | SubscriptionPayment |
| 41 | S | payments | command | `ExpireSubscriptionsCommand` | Subscription |
| 42 | S | payments | command | `MarkMeetingFeeAsPaidCommand` | MeetingFee |
| 43 | S | payments | command | `MarkMeetingFeePaymentAsPaidCommand` | MeetingFeePayment |
| 44 | S | payments | command | `MarkSubscriptionPaymentAsPaidCommand` | SubscriptionPayment |
| 45 | S | payments | command | `MarkSubscriptionRenewalPaymentAsPaidCommand` | SubscriptionRenewalPayment |
| 46 | S | payments | command | `RenewSubscriptionCommand` | Subscription |
| 47 | S | payments | command | `SendSubscriptionCreationConfirmationEmailCommand` | Subscription |
| 48 | S | payments | command | `SendSubscriptionRenewalConfirmationEmailCommand` | Subscription |
| 49 | S | payments | domain event | `MeetingFeeCanceledDomainEvent` | MeetingFee |
| 50 | S | payments | domain event | `MeetingFeeCreatedDomainEvent` | MeetingFee |
| 51 | S | payments | domain event | `MeetingFeeExpiredDomainEvent` | MeetingFee |
| 52 | S | payments | domain event | `MeetingFeePaidDomainEvent` | MeetingFee |
| 53 | S | payments | domain event | `MeetingFeePaymentCreatedDomainEvent` | MeetingFeePayment |
| 54 | S | payments | domain event | `MeetingFeePaymentExpiredDomainEvent` | MeetingFeePayment |
| 55 | S | payments | domain event | `MeetingFeePaymentPaidDomainEvent` | MeetingFeePayment |
| 56 | S | payments | domain event | `PayerCreatedDomainEvent` | Payer |
| 57 | S | payments | domain event | `PriceListItemActivatedDomainEvent` | PriceListItem |
| 58 | S | payments | domain event | `PriceListItemAttributesChangedDomainEvent` | PriceListItem |
| 59 | S | payments | domain event | `PriceListItemCreatedDomainEvent` | PriceListItem |
| 60 | S | payments | domain event | `PriceListItemDeactivatedDomainEvent` | PriceListItem |
| 61 | S | payments | domain event | `SubscriptionCreatedDomainEvent` | Subscription |
| 62 | S | payments | domain event | `SubscriptionExpiredDomainEvent` | Subscription |
| 63 | S | payments | domain event | `SubscriptionPaymentCreatedDomainEvent` | SubscriptionPayment |
| 64 | S | payments | domain event | `SubscriptionPaymentExpiredDomainEvent` | SubscriptionPayment |
| 65 | S | payments | domain event | `SubscriptionPaymentPaidDomainEvent` | SubscriptionPayment |
| 66 | S | payments | domain event | `SubscriptionRenewalPaymentCreatedDomainEvent` | SubscriptionRenewalPayment |
| 67 | S | payments | domain event | `SubscriptionRenewalPaymentPaidDomainEvent` | SubscriptionRenewalPayment |
| 68 | S | payments | domain event | `SubscriptionRenewedDomainEvent` | Subscription |
| 69 | S* | payments | integration event | `MeetingAttendeeAddedIntegrationEvent` | — |
| 70 | S | payments | integration event | `MeetingFeePaidIntegrationEvent` | MeetingFee |
| 71 | S* | payments | integration event | `NewUserRegisteredIntegrationEvent` | — |
| 72 | S | payments | integration event | `SubscriptionExpirationDateChangedIntegrationEvent` | Subscription |
