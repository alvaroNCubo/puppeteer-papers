# Paper 0A assets — the dissection, as replayable history

Assets for *The Assembled Verb* (`0A-assembled-verb.md`). The paper's corpus analysis rests on a
dissection whose evidence is not a report but a **git history**: rules committed before any
removal, each rule applied in two audited commits, hole counts recorded in commit messages, and
a negative control run at the end. That history is what this folder carries.

## Directory structure

```
paperA-assets/
└── dissection/
    ├── eshop-dissection.bundle      full clone of dotnet/eShop @ 9b4f943, branch `dissection`
    ├── grzybek-dissection.bundle    full clone of kgrzybek/modular-monolith-with-ddd @ 91c8ef2,
    │                                branch `dissection`
    └── DISSECTION-RULES.md          the pre-registered rule file, identical in both bundles —
                                     copied out for direct reading
```

The bundles are self-contained: no fork has to stay alive anywhere for the history to remain
auditable.

## How to open a bundle

```
git clone eshop-dissection.bundle eshop-dissection
cd eshop-dissection
git checkout dissection
```

## How to audit it

The rule file promises two things a reader can check without trusting anyone:

```
git log --reverse            # the rules, in the order applied, with each step's counts
                             # recorded in the message of the commit that took it
git blame <surviving file>   # names the rule that spared any surviving line
```

The dissection's base commits are the same commits the lab suite's corpus is built at
(`labs/paper0A-assembled-verb/fetch-corpus.ps1`), so the binaries the paper composes and the
sources the paper dissects are one corpus, pinned once.

Both upstream repositories are MIT-licensed; the bundles carry their license files in-tree.
