# Babel Deck

Babel Deck is a dubbing-oriented desktop application being rebuilt as a disciplined sequence of vertical slices.

The core product chain is:

`source media -> timed transcript -> translated/adapted dialogue -> spoken dubbed output -> in-context preview and refinement`

This repo is intentionally being shaped around that chain rather than around a generic media-player identity or speculative architecture work.

## Current posture

The project is in an early rebuild phase.

The main goal right now is to prove the core workflow in the right order, with truthful states and tight scope control. The repo should prefer narrow working slices over broad partial systems.

## Repo guides

Start here before making changes:

- `PLAN.md` — milestone order, product sequencing, and gates
- `docs/architecture.md` — current structural map of the system and major boundaries
- `AGENTS.md` — rules for AI contributors and anti-drift guardrails
- `CONTRIBUTING.md` — contributor workflow, verification expectations, and scope discipline

## Working principle

A feature is not done because it compiles or because a UI surface exists.

A feature is done when the current milestone gate has been actually demonstrated with build, tests, and a short smoke result.

## Current priority

Protect the center of mass:

- transcription must produce trustworthy timed text
- translation/adaptation must produce speakable dialogue
- TTS must turn that dialogue into compelling spoken output
- playback and inspection exist to support preview and refinement

Anything that pulls the repo away from that chain needs a strong reason to exist.
