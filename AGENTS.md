\# AGENTS.md



\## Mission



Build Babel Deck as a disciplined sequence of vertical slices centered on the real product chain:



`source media -> timed transcript -> translated/adapted dialogue -> spoken dubbed output -> in-context preview and refinement`



The repo is not allowed to drift back into a shell-first or architecture-first project.



The current phase is defined by `PLAN.md`. If this file conflicts with a proposed change, the plan wins.



\---



\## What Matters Most



The product is succeeding when a user can:



1\. Load source media

2\. Generate a timed transcript

3\. Produce translated/adapted dialogue

4\. Generate spoken dubbed output

5\. Preview and refine that output in context

6\. Reopen the session and continue without losing work



Everything else is secondary until that loop is real.



\---



\## Non-Negotiable Rules



\### 1. Work one milestone at a time

Do not start downstream scope before the current milestone is complete.



A milestone is not complete because code exists.

It is complete when:

\- the build passes

\- relevant tests pass

\- a manual smoke note exists

\- the milestone gate in `PLAN.md` is actually satisfied



\### 2. Do not widen scope

Do not add optional features, nice-to-have polish, alternate providers, runtime matrices, UI prestige work, or speculative extensibility unless the current milestone explicitly requires them.



\### 3. Do not fake readiness

Never leave behind code that implies a feature is working when it is not.



Use explicit placeholders, disabled states, or honest errors.

Do not:

\- silently fall back

\- pretend a local path is active

\- claim a runtime is ready without verification

\- scaffold UI that reads as implemented when it is not



\### 4. Preserve the product center of mass

Do not let the repo quietly revert into a generic media player project.



Playback exists to support dubbing workflow and in-context inspection.

Transcription, translation/adaptation, and TTS are the main product chain.



\### 5. Retire known technical risks early, but do not let them become the product

If a recurring infrastructure failure point blocks the plan, it is valid to tackle it early.

That does not justify expanding unrelated scope around it.



Example:

headless media transport stability may be addressed early as risk retirement.

That does not justify rebuilding the whole playback shell before the dub loop is real.



\### 6. Prefer ugly truth over elegant incompleteness

A working narrow slice is better than a cleaner design that proves less of the product.

Do not trade working behavior for prettier abstractions unless the current milestone demands it.



\### 7. Do not delete working history

Never remove old working code, branches, or experiments without preserving them somewhere recoverable.

Archive instead of erasing.



\### 8. Keep missing work visible

If something is not implemented, make that obvious in code and UI.

Use names and comments that tell the truth.



\### 9. Avoid premature architecture

Do not introduce:

\- provider matrices

\- execution-target routing systems

\- setup hubs

\- plugin architectures

\- backend factories for hypothetical future paths

\- generalized workflow engines



unless the current milestone requires them to deliver a real gate.



\### 10. Keep one owner for session/workflow state

Do not scatter product state across views, random services, and helper classes.

A clear coordinator or equivalent owner should drive workflow progression.



\---



\## Allowed Work



Allowed work is work that directly helps the current milestone pass its gate.



Examples:

\- implementing the missing core behavior for the current slice

\- fixing build/test failures

\- adding tests needed to prove the milestone

\- adding narrowly scoped models/types needed by the slice

\- improving logging/diagnostics that unblock debugging

\- simplifying existing code when it reduces friction without widening scope



\---



\## Forbidden Work Unless Explicitly Required



Do not do these on your own initiative:



\- broad refactors outside the current milestone

\- replacing major subsystems because a new stack seems cleaner

\- adding multiple model/provider choices early

\- building large settings surfaces before the workflow exists

\- polishing visual design before the core loop is usable

\- introducing elaborate abstractions “for later”

\- building fake facades that mimic unfinished features

\- migrating stable code just to make the architecture look purer

\- changing naming, structure, or patterns repo-wide without direct milestone need



\---



\## How To Make Changes



\### When touching code

\- keep changes narrow

\- preserve existing working behavior

\- prefer direct fixes over framework theater

\- leave comments only where they clarify non-obvious behavior or constraints

\- do not move code across layers unless the current milestone truly needs it



\### When adding a new type or service

Ask:

\- does this directly serve the current milestone?

\- is it the smallest honest shape that works?

\- does it model something real in the product, or just future possibility?



If it mostly serves future possibility, do not add it yet.



\### When blocked

If the current plan is blocked by a real technical issue:

\- fix the blocker

\- document why it blocked the milestone

\- do not use the blocker as an excuse to expand the project sideways



\---



\## Verification Expectations



Before calling work complete:

\- run the build

\- run relevant tests

\- add or update tests when the change is testable

\- perform the milestone’s manual smoke path

\- record a short smoke note naming the exact gate that was verified



Do not claim completion based on static inspection alone if the milestone is behavior-driven.



\---



\## Truthfulness Requirements



When reporting status:

\- state what is actually working

\- state what is partial

\- state what is still missing

\- name any shortcuts or temporary seams



Do not describe aspirational structure as shipped behavior.



Bad:

\- “local runtime support is in place” when only the settings shell exists



Good:

\- “local runtime UI exists, but no verified local inference path has been implemented yet”



\---



\## Preferred Biases



When there is a tradeoff, generally prefer:



\- working slice over broad foundation

\- narrow real behavior over broad fake readiness

\- direct implementation over speculative abstraction

\- persistent artifacts over recomputation

\- recoverability over elegance

\- clear failure states over silent fallback



\---



\## If Unsure



If a proposed change feels smart but not necessary for the current milestone, do not do it.



If a change improves architecture but delays the main product loop, do not do it.



If a change makes the shell nicer but does not strengthen transcript -> translation/adaptation -> TTS -> preview, it is probably not the priority.



When in doubt, serve the milestone gate and protect the product center of mass.

