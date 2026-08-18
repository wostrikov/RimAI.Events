# Events interior inventory — Phase 7.5.11

Measured against `RimAI.Events`. Production scope: `Source/**/*.cs` excluding `obj`/`bin`.

| Wave | Status |
| --- | --- |
| A | inventory + structural characterization |
| pre-B | Core contracts consumed; formatter in Core; scribe labels; measured Harmony guard |
| B | composition Stop unwinds Talk/API; rename to `OngoingEventsPromptContributor` |
| C–D | pending |

Authoritative HEADs at Wave A start (post-7.5.10): Core `f61836d`, Communication `863fa3e`,
Personas `9d4a0a8`, Events `ff2109c`, integration `382692e`.

---

## Product shape (critical)

`RimAI.Events` is **not** a general RimWorld event bus (death / damage / social / construction
emitters). It is the **RimTalk Event+** ongoing-situation injector:

```text
active quests / map conditions / threat letters / site parts
  → filter (category / def / instance / context pawns)
  → OngoingEventsPromptFormatter (Core) via Events StripTags adapter
  → Communication prompt context (Talk decorate + Advanced Mode variables)
```

---

## Starting metrics (Wave A)

| Metric | Value |
| --- | ---: |
| Production files | 28 |
| Production LOC (line count) | 3756 |
| `.Instance` (mod facade) | 1 (`EventsMod.Instance`) |
| `EventsComposition.Current` | 1 |
| `Lazy<T>` | 0 |
| Direct Verse `Log.*` | **0** |
| `RimAiLog.*` | 30 |
| Catch-all | 16 (1 TEMPORARY labeled; 11 ALLOWED; 4 unmarked) |
| Direct `File.*` | **0** |
| `AiRequestArbiter` | **0** |
| Live `[HarmonyPatch]` | **2** (measured by `validate_events_interior_sources.py`) |
| Oversized 501+ | **0** |

---

## Responsibility map (post Wave B)

```text
EventsMod (handshake + Settings + settings UI)
    ↓ RimAiHandshake.TryActivate
EventsComposition.Start
    ↓ Harmony PatchAll (2 patches, process lifetime)
    ↓ OngoingEventsPromptContributor.Register  → TalkLifecycle.PromptDecorated
    ↓ EventsCommunicationIntegration.TryRegister → RimTalkPromptAPI variables
    ↓ RimAIModuleRegistry.Register("events")
EventsComposition.Stop
    ↓ OngoingEventsPromptContributor.Unregister
    ↓ EventsCommunicationIntegration.Unregister
    ↓ IsStarted=false
    (no Harmony Unpatch)

Pull dispatch:
  Talk decorate OR Advanced Mode vars
    → OngoingEventsUtil (consumes EventsInteriorDefaults bounds/kinds)
    → EventFilterSettings (EventScribeLabels)
    → optional ContextPawnMatcher
    → OngoingEventsFormatter → OngoingEventsPromptFormatter
```

### Core contracts consumed by production

| Constant / type | Consumer |
| --- | --- |
| `EventScribeLabels.*` | `EventFilterSettings.ExposeData` |
| `ThreatLetterTimeoutTicks` | `OngoingEventsUtil` |
| `DefaultMaxOngoingEvents` | util / Talk / Advanced Mode / Map dump |
| `QuestSnapshotKind` | util + `ContextPawnMatcher` |
| `AdvancedModeModId` | `EventsCommunicationIntegration` |
| `OngoingEventsPromptFormatter` | `OngoingEventsFormatter` + Talk/Advanced maxChars |

Structural Harmony targets are **measured** by `tools/architecture/guards/validate_events_interior_sources.py`
(not trusted from a lone Core constant).

---

## Characterization

- `Stage7511EventsInteriorCharacterizationTests` — product role, scribe labels, formatter output,
  Stop unwind facts, isolation flag names.
- Live Verse quest/map emission remains host-level (not claimed by pure Core tests).
- Communication `tests/` still has no real test methods.

---

## Known warts remaining for Waves C–D

1. Dual surfaces (Talk append + Advanced vars) can duplicate content when both enabled.
2. Four unmarked catch-alls.
3. `EventsComposition.Current` ambient singleton.
4. Compression setting remains; compression body path still commented out.
5. Donor naming residue (`rimtalkeventplus`).
6. Filter/normalize still concentrated in large static util + UI query helpers (Wave C).
