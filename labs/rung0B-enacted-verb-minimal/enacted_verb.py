"""A minimal realization of the admitted act, written without the Puppeteer engine.

Purpose. Paper 0B claims that the admitted act of a subject is a first-class value:

  (a) an exercise of a verb becomes an act of a subject P only at P's frontier, and is one act
      whether it achieves or fails;
  (b) the act is kept as a value that P's own later computation can take as an operand, and what
      that computation derives returns to P's history only through the same frontier.

This module shows that the claim does not depend on the engine that motivated it. It deliberately
leaves out what belongs to that engine's realization: there is no re-execution (an act keeps its
verb's name, its captured arguments and its occasion, never a program), no persistence, no
concurrency and no notation.

The base is three things and nothing else:

  * Subject.enact(verb, args, occasion) -> Act | None
        The frontier. A partial operation: an unknown verb or a guard that refuses leaves nothing
        and consumes no identifier; an admitted exercise reserves an identifier, runs the body
        against the subject's state with no rollback, and appends exactly one Act whether the body
        achieves or fails. The verdict is kept apart from the act, keyed by its identifier.
  * Subject.acts -> tuple[Act, ...]
        The history, read-only. There is no other way to add to it than enact.
  * a clock passed in as a plain value ("now"), which only says when to look.

Two operations are written over that base only:

  * pending(acts, now) and rests_on(acts, act)
        relate prior acts: the commitments a subject made to itself that are still open, computed
        from the history alone, and the chain of acts an act rests on, followed through occasion
        links.
  * attend(subject, now)
        constitutes new acts from prior ones: for every open commitment that is due, it proposes the
        committed exercise at the subject's own frontier, naming the act that armed it as its
        occasion. It holds no write access: the subject admits or refuses.

Desisting is an ordinary act of the primitive verb "desist", whose occasion is the commitment it
drops; the commitment and the decision to drop it both stay in the history.
"""

from __future__ import annotations

import copy
import json
from dataclasses import dataclass
from types import MappingProxyType
from typing import Any, Callable, Dict, Iterable, List, Mapping, Optional, Tuple

DESIST = "desist"

ACHIEVED = "achieved"
FAILED = "failed"


def _require(value: Any, name: str) -> Any:
    if value is None:
        raise TypeError(f"{name} must not be None")
    return value


def _freeze(mapping: Mapping[str, Any]) -> Mapping[str, Any]:
    return MappingProxyType(copy.deepcopy(dict(mapping)))


@dataclass(frozen=True)
class Arming:
    """A commitment a subject makes to itself inside one of its acts."""

    verb: str
    args: Mapping[str, Any]
    due: int
    every: int
    times: int

    def due_at(self, activation: int) -> int:
        return self.due + self.every * activation


@dataclass(frozen=True)
class Act:
    """One admitted exercise of a verb by a subject. Its outcome is not part of it."""

    id: int
    subject: str
    verb: str
    args: Mapping[str, Any]
    occasion: Optional[int]
    arming: Optional[Arming]


@dataclass(frozen=True)
class Look:
    """What a guard may read: a copy of the state, the history, and the occasion being claimed.
    Nothing it does reaches the subject."""

    state: Mapping[str, Any]
    acts: Tuple[Act, ...]
    occasion: Optional[int]


Guard = Callable[[Look, Mapping[str, Any]], bool]


class Context:
    """What a body receives besides the state: the primitives of the subject's language."""

    def __init__(self) -> None:
        self._arming: Optional[Arming] = None

    def commit_to_self(self, verb: str, args: Mapping[str, Any], due: int, every: int = 0,
                       times: int = 1) -> None:
        _require(verb, "verb")
        _require(args, "args")
        if self._arming is not None:
            raise ValueError("an act arms at most one commitment")
        if times < 1:
            raise ValueError("times must be at least 1")
        if times > 1 and every < 1:
            raise ValueError("a repeated commitment needs a positive period")
        self._arming = Arming(verb, _freeze(args), due, every, times)


Body = Callable[[Dict[str, Any], Mapping[str, Any], Context], None]


@dataclass(frozen=True)
class Verb:
    """A verb the subject's author composed: a body and, optionally, a guard over the subject's own state."""

    name: str
    body: Body
    guard: Optional[Guard] = None


def _desist_guard(look: Look, args: Mapping[str, Any]) -> bool:
    # A subject can only drop a commitment that is still open, and must name it.
    return look.occasion is not None and look.occasion in {c.armed_by for c in pending(look.acts)}


def _desist_body(state: Dict[str, Any], args: Mapping[str, Any], ctx: Context) -> None:
    # The act is the whole effect: the history now holds the decision to drop the commitment.
    return None


class Subject:
    """A subject: a name, a repertoire of verbs, a state, and a history only its frontier writes."""

    def __init__(self, name: str, verbs: Iterable[Verb], state: Optional[Mapping[str, Any]] = None) -> None:
        _require(name, "name")
        _require(verbs, "verbs")
        self.name = name
        self._verbs: Dict[str, Verb] = {}
        for verb in verbs:
            _require(verb, "verb")
            if verb.name == DESIST:
                raise ValueError(f"'{DESIST}' is a primitive of the language and cannot be redefined")
            self._verbs[verb.name] = verb
        self._verbs[DESIST] = Verb(DESIST, _desist_body, _desist_guard)
        self.state: Dict[str, Any] = dict(state) if state is not None else {}
        self._history: List[Act] = []
        self._verdicts: Dict[int, str] = {}
        self._next_id = 1

    @property
    def acts(self) -> Tuple[Act, ...]:
        return tuple(self._history)

    def verdict(self, act_id: int) -> str:
        return self._verdicts[act_id]

    def enact(self, verb_name: str, args: Mapping[str, Any], occasion: Optional[int] = None) -> Optional[Act]:
        """The frontier. Returns the act, or None when the exercise was not admitted."""
        _require(verb_name, "verb_name")
        _require(args, "args")
        verb = self._verbs.get(verb_name)
        if verb is None:
            return None
        if occasion is not None and not any(a.id == occasion for a in self._history):
            raise ValueError("an occasion must name an earlier act of the same subject")
        if verb.guard is not None:
            look = Look(MappingProxyType(copy.deepcopy(self.state)), self.acts, occasion)
            if not verb.guard(look, MappingProxyType(dict(args))):
                return None

        act_id = self._next_id
        self._next_id += 1
        captured = _freeze(args)
        ctx = Context()
        verdict = ACHIEVED
        try:
            verb.body(self.state, captured, ctx)
        except Exception as failure:  # the act stays an act; its failure is a verdict about it
            verdict = f"{FAILED}: {type(failure).__name__}: {failure}"
        act = Act(act_id, self.name, verb_name, captured, occasion, ctx._arming)
        self._history.append(act)
        self._verdicts[act_id] = verdict
        return act


@dataclass(frozen=True)
class Commitment:
    armed_by: int
    arming: Arming
    activations: int

    def next_due(self) -> int:
        return self.arming.due_at(self.activations)


def pending(acts: Tuple[Act, ...]) -> List[Commitment]:
    """The commitments still open, computed from the history alone. Nothing is executed."""
    _require(acts, "acts")
    open_commitments: List[Commitment] = []
    for armed in acts:
        if armed.arming is None:
            continue
        activations = sum(1 for a in acts if a.occasion == armed.id and a.verb == armed.arming.verb)
        desisted = any(a.occasion == armed.id and a.verb == DESIST for a in acts)
        if desisted or activations >= armed.arming.times:
            continue
        open_commitments.append(Commitment(armed.id, armed.arming, activations))
    return open_commitments


def rests_on(acts: Tuple[Act, ...], act: Act) -> List[Act]:
    """The chain an act rests on: the act, the act that occasioned it, and so on to its origin."""
    _require(acts, "acts")
    _require(act, "act")
    by_id = {a.id: a for a in acts}
    chain = [act]
    while chain[-1].occasion is not None:
        chain.append(by_id[chain[-1].occasion])
    return chain


def attend(subject: Subject, now: int) -> List[Tuple[int, Optional[Act]]]:
    """Propose every due commitment at the subject's frontier. Returns (armed_by, act or None)."""
    _require(subject, "subject")
    _require(now, "now")
    proposals: List[Tuple[int, Optional[Act]]] = []
    for commitment in pending(subject.acts):
        if commitment.next_due() <= now:
            act = subject.enact(commitment.arming.verb, commitment.arming.args, occasion=commitment.armed_by)
            proposals.append((commitment.armed_by, act))
    return proposals


def dump_acts(acts: Tuple[Act, ...]) -> str:
    """The history as plain JSON, so that it can be read without the subject that wrote it."""
    def encode(a: Act) -> Dict[str, Any]:
        arming = None
        if a.arming is not None:
            arming = {"verb": a.arming.verb, "args": dict(a.arming.args), "due": a.arming.due,
                      "every": a.arming.every, "times": a.arming.times}
        return {"id": a.id, "subject": a.subject, "verb": a.verb, "args": dict(a.args),
                "occasion": a.occasion, "arming": arming}
    return json.dumps([encode(a) for a in _require(acts, "acts")])


def load_acts(text: str) -> Tuple[Act, ...]:
    def decode(d: Dict[str, Any]) -> Act:
        arming = None
        if d["arming"] is not None:
            m = d["arming"]
            arming = Arming(m["verb"], _freeze(m["args"]), m["due"], m["every"], m["times"])
        return Act(d["id"], d["subject"], d["verb"], _freeze(d["args"]), d["occasion"], arming)
    return tuple(decode(d) for d in json.loads(_require(text, "text")))


def provenance_closed(acts: Tuple[Act, ...], subject_name: str) -> bool:
    """L1 as a check over a history: every act is the subject's, identifiers are strictly increasing,
    and every occasion names an earlier act of the same history."""
    _require(acts, "acts")
    _require(subject_name, "subject_name")
    seen = set()
    last = 0
    for a in acts:
        if a.subject != subject_name or a.id <= last:
            return False
        if a.occasion is not None and a.occasion not in seen:
            return False
        seen.add(a.id)
        last = a.id
    return True
