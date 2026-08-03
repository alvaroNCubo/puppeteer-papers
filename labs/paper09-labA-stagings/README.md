# Paper 9 — Lab A: five stagings

The same `Well` runs on a console; in a browser with input and output over a WebSocket; in a browser
over HTTP with server-sent events; and across two StageManager actors joined once in memory and once
over a real Kestrel TLS channel. Five stagings differing in process, transport and wire format.

Headline → §2 (Experiment A) and Appendix A (Lab A). **0 domain edits** — a diff of the domain
directory from the first staging to the last is empty — and the domain's own suite passes unchanged
throughout, which is a regression check and not a coverage claim.

**This lab has no source of its own.** Its five stagings *are* hosts of the example, and the
evidence is the empty diff across them rather than a harness. The hosts are `console/`, `web/`,
`web-rest/`, `sm-duo/` and `sm-duo-tls/` in the example repository. The write-up is in
`data/paper09-labA-stagings/`.
