"""Paper 0B, Q4: the admitted act realized outside the engine.

Each test names the property it measures. The subjects, P and Q, hold a running total and a gate;
the verbs are neutral. Run with:  python -m unittest -v
"""

import dataclasses
import unittest

from enacted_verb import (DESIST, FAILED, ACHIEVED, Subject, Verb, attend, dump_acts, load_acts,
                          pending, provenance_closed, rests_on)


def _advance(state, args, ctx):
    state["total"] += args["step"]


def _advance_then_fail(state, args, ctx):
    state["total"] += args["step"]
    raise RuntimeError("the body failed after advancing")


def _open_gate(state, args, ctx):
    state["open"] = True


def _arm(state, args, ctx):
    ctx.commit_to_self(args["verb"], {"step": args["step"]}, due=args["due"],
                       every=args.get("every", 0), times=args["times"])


def _witness(state, args, ctx):
    # Testifying changes nothing in the witness's domain: the act is the testimony.
    return None


def _gate_is_open(look, args):
    return look.state["open"]


def _guard_that_writes(look, args):
    look.state["total"] = 1000
    return True


def new_subject(name="P"):
    verbs = [
        Verb("advance", _advance),
        Verb("advance_then_fail", _advance_then_fail),
        Verb("guarded_advance", _advance, _gate_is_open),
        Verb("open_gate", _open_gate),
        Verb("arm", _arm),
        Verb("witness", _witness),
        Verb("advance_if_guard_may_write", _advance, _guard_that_writes),
    ]
    return Subject(name, verbs, {"total": 0, "open": False})


class FrontierCounts(unittest.TestCase):

    def test_a_refused_exercise_leaves_no_act_and_consumes_no_identifier(self):
        p = new_subject()
        self.assertIsNone(p.enact("guarded_advance", {"step": 1}))
        self.assertEqual((), p.acts)
        act = p.enact("advance", {"step": 1})
        self.assertEqual(1, act.id, "the refused exercise reserved no identifier")

    def test_an_unknown_verb_leaves_nothing(self):
        p = new_subject()
        self.assertIsNone(p.enact("no_such_verb", {}))
        self.assertEqual((), p.acts)

    def test_an_achieved_exercise_is_one_act(self):
        p = new_subject()
        act = p.enact("advance", {"step": 2})
        self.assertEqual(1, len(p.acts))
        self.assertEqual(ACHIEVED, p.verdict(act.id))
        self.assertEqual(2, p.state["total"])

    def test_a_failed_exercise_is_one_act_and_keeps_its_partial_effect(self):
        p = new_subject()
        act = p.enact("advance_then_fail", {"step": 5})
        self.assertEqual((act,), p.acts, "admitted and failed: one act, recorded whole")
        self.assertTrue(p.verdict(act.id).startswith(FAILED))
        self.assertEqual(5, p.state["total"], "nothing is rolled back")
        self.assertFalse(hasattr(act, "verdict"), "the outcome is not part of the act")

    def test_a_guard_cannot_change_the_subject(self):
        p = new_subject()
        with self.assertRaises(TypeError):
            p.enact("advance_if_guard_may_write", {"step": 1})
        self.assertEqual(0, p.state["total"])
        self.assertEqual((), p.acts, "a guard that fails is no admission: zero acts")

    def test_arguments_must_be_given(self):
        p = new_subject()
        with self.assertRaises(TypeError):
            p.enact(None, {})
        with self.assertRaises(TypeError):
            p.enact("advance", None)


class TheHistoryIsClosed(unittest.TestCase):

    def test_the_history_is_read_only_and_its_acts_are_values(self):
        p = new_subject()
        act = p.enact("advance", {"step": 1})
        self.assertIsInstance(p.acts, tuple)
        with self.assertRaises(AttributeError):
            p.acts.append(act)
        with self.assertRaises(dataclasses.FrozenInstanceError):
            act.verb = "something_else"
        with self.assertRaises(TypeError):
            act.args["step"] = 99

    def test_an_occasion_must_name_an_earlier_act_of_the_same_subject(self):
        p = new_subject()
        with self.assertRaises(ValueError):
            p.enact("advance", {"step": 1}, occasion=7)

    def test_provenance_is_closed_in_each_history_and_a_copied_entry_breaks_it(self):
        p, q = new_subject("P"), new_subject("Q")
        p.enact("advance", {"step": 1})
        q_act = q.enact("advance", {"step": 1})
        self.assertTrue(provenance_closed(p.acts, "P"))
        self.assertTrue(provenance_closed(q.acts, "Q"))
        self.assertFalse(provenance_closed(p.acts + (q_act,), "P"),
                         "an entry of Q placed in P's history is not an act of P")

    def test_testimony_is_an_act_of_the_witness_and_never_of_the_observed(self):
        p, q = new_subject("P"), new_subject("Q")
        observed = q.enact("advance", {"step": 3})
        testimony = p.enact("witness", {"saw": {"subject": observed.subject, "act": observed.id,
                                                "verb": observed.verb, "args": dict(observed.args)}})
        self.assertEqual("P", testimony.subject)
        self.assertEqual((observed,), q.acts, "Q's history is untouched by what P testified")
        self.assertEqual("Q", testimony.args["saw"]["subject"], "the observed act travels as a value")
        self.assertTrue(provenance_closed(p.acts, "P"))


class CommitmentsToSelf(unittest.TestCase):

    def test_pending_is_computed_from_the_history_alone(self):
        p = new_subject()
        p.enact("arm", {"verb": "advance", "step": 1, "due": 10, "every": 5, "times": 3})
        attend(p, now=10)
        restored = load_acts(dump_acts(p.acts))
        live = [(c.armed_by, c.activations) for c in pending(p.acts)]
        from_record = [(c.armed_by, c.activations) for c in pending(restored)]
        self.assertEqual([(1, 1)], live)
        self.assertEqual(live, from_record, "no subject and no execution: the record is enough")

    def test_attending_before_the_due_time_writes_nothing(self):
        p = new_subject()
        p.enact("arm", {"verb": "advance", "step": 1, "due": 10, "times": 1})
        self.assertEqual([], attend(p, now=9))
        self.assertEqual(1, len(p.acts))

    def test_attending_constitutes_an_act_that_names_the_act_that_armed_it(self):
        p = new_subject()
        arming = p.enact("arm", {"verb": "advance", "step": 4, "due": 10, "times": 1})
        [(armed_by, activation)] = attend(p, now=10)
        self.assertEqual(arming.id, armed_by)
        self.assertEqual(arming.id, activation.occasion)
        self.assertEqual("P", activation.subject, "the activation is the subject's own act")
        self.assertEqual(4, p.state["total"])
        self.assertEqual([], pending(p.acts), "exhausted: closed with no entry of its own")
        self.assertEqual([activation, arming], rests_on(p.acts, activation))

    def test_a_repeated_commitment_advances_only_when_due(self):
        p = new_subject()
        p.enact("arm", {"verb": "advance", "step": 1, "due": 10, "every": 5, "times": 3})
        self.assertEqual(1, len(attend(p, now=10)))
        self.assertEqual([], attend(p, now=12), "the second activation is due at 15")
        self.assertEqual(15, pending(p.acts)[0].next_due())
        self.assertEqual(1, len(attend(p, now=15)))
        self.assertEqual(1, len(attend(p, now=20)))
        self.assertEqual([], pending(p.acts))
        self.assertEqual(3, p.state["total"])

    def test_a_refused_continuation_leaves_nothing_and_the_commitment_stays_open(self):
        p = new_subject()
        arming = p.enact("arm", {"verb": "guarded_advance", "step": 1, "due": 10, "times": 1})
        [(_, refused)] = attend(p, now=10)
        self.assertIsNone(refused)
        self.assertEqual(1, len(p.acts))
        self.assertEqual([(arming.id, 0)], [(c.armed_by, c.activations) for c in pending(p.acts)])
        p.enact("open_gate", {})
        [(_, admitted)] = attend(p, now=11)
        self.assertEqual(arming.id, admitted.occasion)
        self.assertEqual([], pending(p.acts))

    def test_a_failed_activation_is_an_act_and_counts(self):
        p = new_subject()
        p.enact("arm", {"verb": "advance_then_fail", "step": 2, "due": 10, "times": 1})
        [(_, activation)] = attend(p, now=10)
        self.assertTrue(p.verdict(activation.id).startswith(FAILED))
        self.assertEqual(2, p.state["total"])
        self.assertEqual([], pending(p.acts))

    def test_desisting_is_an_act_that_names_the_commitment_and_keeps_both_in_the_history(self):
        p = new_subject()
        arming = p.enact("arm", {"verb": "advance", "step": 1, "due": 10, "times": 2, "every": 5})
        desisting = p.enact(DESIST, {}, occasion=arming.id)
        self.assertEqual(arming.id, desisting.occasion)
        self.assertEqual([], pending(p.acts))
        self.assertEqual([], attend(p, now=100))
        self.assertEqual([arming, desisting], list(p.acts), "nothing is forgotten: both acts remain")

    def test_desisting_without_naming_an_open_commitment_is_refused(self):
        p = new_subject()
        self.assertIsNone(p.enact(DESIST, {}))
        plain = p.enact("advance", {"step": 1})
        self.assertIsNone(p.enact(DESIST, {}, occasion=plain.id), "that act armed nothing")
        arming = p.enact("arm", {"verb": "advance", "step": 1, "due": 10, "times": 1})
        attend(p, now=10)
        self.assertIsNone(p.enact(DESIST, {}, occasion=arming.id), "already closed")


if __name__ == "__main__":
    unittest.main()
