# Milestone 0.0.54 - stable persistence identity

Date: 2026-08-16

A runtime save/restart test exposed that Coop transient `TransferSave` events could swap the observed save and campaign identifiers. The social receipt was written successfully but into a different generation directory, so restart loaded the canonical generation without that receipt.

## Fix

- The state store now retains the campaign/save identity established by `GameLoaded`.
- Later save events may report transient identifiers for diagnostics but cannot replace the stable generation.
- The executable persistence harness deliberately supplies swapped identifiers and proves reload from the original generation.
- Native relation mutation remains disabled.

Build `0.0.54-dev` passed all cumulative suites and was superseded by deployed `0.0.55-dev`.
