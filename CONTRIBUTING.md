\## Contributor Rules



\### Project posture



Babel Deck is being rebuilt as a sequence of vertical slices around one core product chain:



`source media -> timed transcript -> translated/adapted dialogue -> spoken dubbed output -> in-context preview and refinement`



Contributors should protect that center of gravity.

This repo should not drift into a shell-first, framework-first, or abstraction-first project.



\### Before you start



Read:

\- `PLAN.md` for milestone order and gates

\- `AGENTS.md` for repo behavior rules

\- any milestone-specific docs relevant to the area you are touching



If your intended work does not clearly support the current milestone, it is probably out of scope.



\### Scope discipline



Contributors are expected to work one milestone at a time.



Do not:

\- start downstream features early

\- add optional enhancements before the current milestone is proven

\- refactor broadly just because a cleaner architecture is possible

\- add speculative extension points for future providers, runtimes, or workflows



A narrower real feature is preferred over a broader partial one.



\### Truthful behavior only



Do not merge code that gives a false impression of readiness.



That includes:

\- fake buttons or surfaces that look functional but are not

\- silent fallback paths

\- “coming soon” behavior disguised as completed implementation

\- runtime or local-model claims that have not been verified



If something is incomplete, it should be obviously incomplete.



\### Verification requirements



Before opening or merging a PR, contributors should verify changes at the appropriate level.



Minimum expectation:

\- `dotnet build`

\- `dotnet test`



If the change affects behavior that can be manually exercised, also run the relevant smoke path for the current milestone and note what was verified.



A change is not done because it compiles.

It is done when the milestone gate it touches is actually demonstrated.



\### Smoke notes



For milestone work, include a short smoke note in the PR description or related notes.



A good smoke note says:

\- what was tested

\- on what kind of input

\- what exact gate was verified

\- anything still missing or fragile



Example:

“Loaded sample media, generated transcript, persisted artifacts, restarted app, and confirmed transcript reopened correctly. This verifies the ingest/transcript persistence gate.”



\### Refactors



Refactors are allowed only when they do at least one of these:

\- unblock the current milestone

\- reduce real complexity in code being actively changed

\- remove a proven source of instability



Refactors are not justified by:

\- aesthetic preference

\- architectural purity

\- future-proofing alone

\- desire to align the whole repo to a new pattern mid-milestone



\### UI work



Do not prioritize shell polish over core workflow progress.



UI additions should generally serve one of these purposes:

\- make the current milestone usable

\- expose truthful state

\- improve debugging or inspection

\- support transcript/translation/TTS/preview flow directly



Avoid prestige UI work before the product loop is real.



\### New abstractions



Before adding a new service, coordinator, factory, interface, or subsystem, ask:

\- is this needed right now for the current milestone?

\- does it model a real current behavior?

\- is there a smaller honest version that works?



If the abstraction mainly serves imagined future needs, do not add it yet.



\### Historical preservation



Do not delete prior working code or experiments without preserving them somewhere recoverable.

If you are replacing something, archive the old path first.



The repo has already lost useful working history before. Do not repeat that.



\### PR expectations



A good PR is:

\- tightly scoped

\- aligned to one milestone or one blocker

\- honest about what it does and does not complete

\- accompanied by build/test results

\- accompanied by smoke results when relevant



A bad PR:

\- mixes milestone work with opportunistic polish

\- introduces large unrelated refactors

\- adds fake scaffolding

\- claims completion without verifying behavior



\### When you find a blocker



If you hit a real blocker:

\- document it clearly

\- fix only what is needed to remove the blocker

\- avoid expanding into adjacent cleanup unless it is necessary



Known infrastructure risks may deserve early attention, but they should not become an excuse for sideways expansion.



\### Definition of good contribution



A good contribution moves the repo closer to this real user outcome:



load media -> get transcript -> get translated/adapted dialogue -> generate spoken output -> preview/refine in context -> save and resume later



If your change does not strengthen that path, it needs a strong reason to exist.

