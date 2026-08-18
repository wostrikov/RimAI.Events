# Events interior inventory — Phase 7.5.11 Wave A

Measured against `RimAI.Events` at Wave A inventory time.
Production scope: `Source/**/*.cs` excluding `obj`/`bin`.
**No Waves B–D applied yet.**

Authoritative starting HEADs (post-7.5.10):

| Repo | HEAD |
| --- | --- |
| Core | `f61836d` |
| Communication | `863fa3e` |
| Personas | `9d4a0a8` |
| Events | `ff2109c` |
| integration | `382692e` |

---

## Product shape (critical)

`RimAI.Events` is **not** a general RimWorld event bus (death / damage / social / construction
emitters). It is the **RimTalk Event+** ongoing-situation injector:

```text
active quests / map conditions / threat letters / site parts
  → filter (category / def / instance / context pawns)
  → format text block
  → Communication prompt context (Talk decorate + Advanced Mode variables)
```

Representative “event families” for this module are therefore:

| Family | Source of truth | Normalized model |
| --- | --- | --- |
| Quest | `Find.QuestManager` + `QuestLinkUtil` | `OngoingEventSnapshot` (`Kind=Quest`) |
| MapCondition | `map.gameConditionManager` | `OngoingEventSnapshot` |
| Threat | Letter archive (recent red threat) | `OngoingEventSnapshot` (`IsThreat`) |
| SitePart | non-home `Site` map parts | `OngoingEventSnapshot` |

There are **no** Harmony hooks for pawn death, combat damage, mood, inventory, or storyteller
incident fire-and-forget AI. Do not invent those paths in later waves.

---

## Starting metrics

| Metric | Value |
| --- | ---: |
| Production files | 28 |
| Production LOC (line count) | 3756 |
| `.Instance` (mod facade) | 1 (`EventsMod.Instance`) |
| `EventsComposition.Current` | 1 (static singleton decl) |
| `Lazy<T>` service graphs | 0 |
| Direct Verse `Log.*` | **0** |
| `RimAiLog.*` call sites | 30 |
| Catch-all (`Exception` / `System.Exception`) | 16 |
| Marked `TEMPORARY_EXPLICIT_EXCEPTION` | **1** (`EventsCommunicationIntegration.TryRegister`) |
| Marked `ALLOWED_TOP_LEVEL_BOUNDARY` | 11 |
| Unmarked catch-all | 4 (`BlacklistMigrationHelper`, `EnhancedPromptDetector` startup, `PromptService_OngoingEventsPatch`, `QuestCacheComponent`) |
| Direct `File.*` | **0** |
| `AiRequestArbiter` | **0** (Events does not enqueue AI work) |
| Live `[HarmonyPatch]` | **2** |
| Oversized types (501+) | **0** (largest file `QuestLinkUtil.cs` ≈463 LOC) |

### Largest files (LOC)

| LOC | File |
| ---: | --- |
| 463 | `QuestLinkUtil.cs` |
| 401 | `OngoingEventsUtil.cs` |
| 399 | `EventFilterUIEventQueries.cs` |
| 272 | `EventFilterUIOptimizationPanel.cs` |
| 227 | `ContextPawnMatcher.cs` |
| 220 | `EventFilterUIInstanceSection.cs` |
| 210 | `EventFilterUITypeSection.cs` |
| 208 | `EventFilterSettings.cs` |

Architecture validator (workspace, Wave A time): global `status=PASS`; Events contributes
**0** oversized TEMPORARY rows. Events-specific TEMPORARY catch debt that is **labeled** = 1.
Unmarked catches are Wave B/D debt candidates (must not increase; prefer mark/narrow).

---

## Responsibility map

```text
EventsMod (handshake + Settings + settings UI)
    ↓ RimAiHandshake.TryActivate
EventsComposition.Start
    ↓ Harmony PatchAll (2 patches)
    ↓ PromptService_OngoingEventsPatch.Register  → TalkLifecycle.PromptDecorated
    ↓ EventsCommunicationIntegration.TryRegister → RimTalkPromptAPI variables
    ↓ RimAIModuleRegistry.Register("events")
EventsComposition.Stop  → IsStarted=false only (no Unpatch, no unregister)

Host / Verse:
  QuestCacheComponent (GameComponent) — reflection + quest-map/pawn caches
  BlacklistMigrationStartup — one-shot XML→settings migration

Dispatch (pull, not push):
  Talk decorate callback OR Advanced Mode variable provider
    → OngoingEventsUtil.GetOngoingEventsNow / TryAdd*
    → EventFilterSettings + optional ContextPawnMatcher
    → OngoingEventsFormatter
```

### Composition / lifecycle

| Item | Owner | Notes |
| --- | --- | --- |
| Handshake | `EventsMod` ctor | optional module; calls `EventsComposition.Current.Start` |
| Harmony | `EventsComposition.Start` | `new Harmony("ustas.rimai.events").PatchAll()` — process lifetime; **Stop does not Unpatch** |
| Talk integration | `PromptService_OngoingEventsPatch` | static `_registered` gate; **no Unregister on Stop** |
| Advanced Mode API | `EventsCommunicationIntegration` | static `_apiAvailable`; **no Unregister on Stop** |
| Module registry | `EventsComposition.Start` | register only |
| Settings | `EventsMod.Settings` / `EventFilterSettings` | ModSettings scribe |

**Reload risk:** Start is idempotent via `IsStarted` / `_registered` / `_apiAvailable`, but Stop
does not clear those gates. A true Stop→Start cycle after process-level flags are set will
**no-op** re-registration. Harmony patches remain installed (intentional 7.5.8-style host policy).

### Event sources (actual)

| Source | Class | Frequency | Role |
| --- | --- | --- | --- |
| Harmony `Map.FinalizeInit` | `Map_FinalizeInit_OngoingEventsDump_Patch` | map init | prewarm `QuestCacheComponent`; DevMode dump |
| Harmony `Quest.End` | `Quest_End_ClearCache_Patch` | quest end | invalidate quest caches |
| Pull: quests | `OngoingEventsUtil.TryAddOngoingQuestsForMap` | per talk / variable | normalize + filter |
| Pull: conditions | `TryAddActiveGameConditionsForMap` | per talk / variable | normalize + filter |
| Pull: threats | `TryAddMostRecentThreatLetter` | when `isInDanger` | at most one recent letter; timeout 7500 ticks |
| Pull: site parts | `TryAddSitePartEvents` | non-home maps | location description |
| UI queries | `EventFilterUIEventQueries` | settings UI | list filterable types/instances |
| Migration | `BlacklistMigrationHelper` | once | XML blacklist → settings |

### Normalization

- DTO for prompt: `OngoingEventSnapshot` (`SourceDefName`, `QuestId`, `Kind`, `Label`, `Body`,
  `QuestDescription`, `IsThreat`).
- DTO for UI filter: `FilterableEvent` (`rootID`, `displayName`, `instanceName`, `category`,
  `sourceDefName`, `instanceID`).
- Categories: `EventCategory` = `Quest | MapCondition | Threat | SitePart`.
- Construction is concentrated in `OngoingEventsUtil` (+ UI queries). Harmony patches do **not**
  build prompt payloads (thin).

### Filtering / policy

Central policy lives in:

1. `EventFilterSettings` — category toggles (incl. Enhanced Prompt lock), def blacklist,
   per-colony instance blacklist, `AppendToContext`, `EnableContextFiltering`.
2. `OngoingEventsUtil.IsEventFiltered` — applies settings during collection.
3. `ContextPawnMatcher.FilterEventsByContext` — optional talk-time quest pawn overlap
   (threats / non-quests always kept).

No separate cooldown ledger beyond threat letter age (`ThreatLetterTimeoutTicks = 7500`).
No once-per-tick event bus dedup (not applicable).

### Dispatch paths (two pull paths, one util)

| Path | Trigger | Entry |
| --- | --- | --- |
| A — Talk context append | `TalkLifecycle.PromptDecorated` | `PromptService_OngoingEventsPatch.OnPromptDecorated` |
| B — Advanced Mode vars | Scriban/prompt variable resolve | `EventsCommunicationIntegration` → `eventplus_*` |

Both call `OngoingEventsUtil` + `OngoingEventsFormatter`. Path A mutates
`TalkRequest.Context`. Path B returns formatted strings via `RimTalkPromptAPI`.

**Not present:** AI request enqueue, `AiRequestArbiter`, push event bus, internal work queue.

### Communication integration

- Registration owned by `EventsComposition.Start` → `EventsCommunicationIntegration.TryRegister`.
- Depends on handshake approval + Communication `RimTalkPromptAPI` / `Settings.UseAdvancedPromptMode`
  (for advanced vars; Talk path uses `AppendToContext` regardless).
- Direct Communication types used: `TalkLifecycle`, `TalkRequest`, `RimTalkPromptAPI`,
  `PromptContext`, `PawnSelector` (via `ContextPawnMatcher`).

### Settings / enablement

| Setting | Effect |
| --- | --- |
| `AppendToContext` | master switch for Talk decorate path |
| `showQuests` / `showMapConditions` / `showThreats` / `showSiteParts` | category gates |
| Enhanced Prompt lock | forces quest/condition/threat categories off unless override |
| `disabledEventDefNames` | global type filter |
| `disabledEventInstances` | per-colony instance filter |
| `EnableContextFiltering` | Talk path only — quest pawn overlap |
| `enableEventTextCompression` | stored; compression util currently commented out in formatter |

### Persistence

- `EventFilterSettings` ModSettings (Verse scribe) — filter state + migration flag.
- `QuestCacheComponent` — **runtime** caches only (not scribed history).
- No `ILocalStorage` / `File.*` usage.

### Dedup / once semantics

| Mechanism | Scope |
| --- | --- |
| `IsStarted` / `_registered` / `_apiAvailable` | composition/integration once-per-process |
| Threat letter scan | at most one letter; age window 7500 ticks |
| `QuestCacheComponent.InvalidateQuest` | drop caches when quest ends |
| Settings type/instance disable | user policy, not automatic dedup |

Duplicate Talk decorate vs Advanced Mode variables can both surface the same ongoing set if both
are enabled — that is **product dual-surface**, not accidental double Harmony emit.

---

## Target architecture (for Waves B–D)

Prefer clarifying ownership without inventing a push bus:

```text
EventsComposition
  owns: registration flags unwind, integration bridge lifetime, settings access surface
Host adapters (Harmony) stay thin
OngoingEventsUtil = collect + normalize + settings filter
ContextPawnMatcher = optional talk policy
OngoingEventsFormatter = presentation
EventsCommunicationIntegration = Communication adapter only
```

Wave B priority: make `Stop` clear owned registrations safely without Harmony Unpatch;
eliminate duplicate-startup ambiguity; mark/narrow unmarked catch-alls.

Wave C priority: keep util ownership explicit; avoid scattering filter into UI/Harmony.

Wave D priority: integration cleanup + debt monotonicity + docs/guards.

---

## Characterization coverage (Wave A)

Core contracts + `Stage7511EventsInteriorCharacterizationTests` freeze:

- product role (ongoing injector, not death/combat bus)
- category enum set
- dual dispatch surfaces
- Harmony source count = 2
- composition Start/Stop facts
- filter setting keys
- no AiRequestArbiter
- threat timeout / max events constants

Live Verse map/quest emission remains integration-level; not claimed covered by pure Core tests.

---

## Known warts (do not “fix” without characterization)

1. `Stop` is flag-only; static registration gates prevent re-bind after Stop.
2. Dual surfaces (Talk append + Advanced vars) can duplicate content when both enabled.
3. Four unmarked catch-alls.
4. `EventsComposition.Current` ambient singleton (ALLOWED facade candidate vs ROOT_OWNED).
5. `OngoingEventsFormatter` compression path is commented out while setting remains.
6. Donor naming residue (`rimtalkeventplus`, README “RimTalk Event+”).
7. Communication `tests/` project still has no real test methods — do not count as coverage.

