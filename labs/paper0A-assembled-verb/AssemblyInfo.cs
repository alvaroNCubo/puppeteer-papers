// The suite runs its labs one at a time, declared rather than inherited.
//
// MSTest does not parallelize by default, so this attribute changes no behaviour. It is
// here because one lab depends on that default and would be wrong without it, quietly:
// WhatTheRecordCostsLab times journal writes, cold reconstructions and a direct loop that
// completes in about two milliseconds. A lab running beside it - the two Orleans labs each
// start an in-process silo, and several others write journals to disk - competes for the
// same cores and disk, and the price table would then report the contention rather than the
// record's price. Saying so in the assembly is better than relying on a default that a
// runner's configuration could change.
[assembly: DoNotParallelize]
