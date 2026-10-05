# Events interior inventory — Phase 7.5.11

Measured against `RimAI.Events`. Production scope: `Source/**/*.cs` excluding `obj`/`bin`.

| Wave | Status |
| --- | --- |
| A | inventory + structural characterization |
| pre-B | Core contracts consumed; formatter in Core; scribe labels; measured Harmony guard |
| B | composition Stop unwinds Talk/API; rename to `OngoingEventsPromptContributor` |
| C | structural normalize/filter/dispatch ownership; behavioral isolation tests; **no prompt-set change** |
| D | catch markers; docs; guards; stage close |

---

## Product shape

`RimAI.Events` is an **ongoing-situation prompt injector** (quests / map conditions /
threat letters / site parts), not a push event bus.

```text
EventsComposition
  → Harmony (Map.FinalizeInit, Quest.End) thin host adapters
  → OngoingEventsPromptContributor / EventsCommunicationIntegration
       → OngoingEventsUtil (collect + host adapt)
            → EventFilterPolicy (Core)
            → OngoingEventNormalizer (Core)
            → QuestRuntimeCacheStore via QuestCacheComponent (Core)
       → ContextEventIncludePolicy (Core) on Talk context filter
       → OngoingEventsFormatter → OngoingEventsPromptFormatter (Core)
```

Wave C discriminator: `Formatter_injection_output_shape_is_frozen` / Wave C
formatter discriminator tests were **not** rewritten for behavior — only structural
ownership moved. `EventsInteriorDefaults.WaveCChangesPromptEventSet = false`.

---

## Lifecycle

- Start: PatchAll (process lifetime) + Talk Register + Prompt API TryRegister
- Stop: Unregister Talk + Prompt API; **no** Harmony Unpatch
- Quest.End → `QuestCacheComponent.InvalidateQuest` → `QuestRuntimeCacheStore.InvalidateQuest`

## Rename

`PromptService_OngoingEventsPatch` → `OngoingEventsPromptContributor` (done in Wave B;
guard refuses legacy filename).

## Debt notes

- Dual surfaces (Talk append + Advanced vars) can duplicate content when both enabled
- `EventsComposition.Current` ambient facade remains
- Compression setting retained; compression body path still commented out
- Donor mod id `rimtalkeventplus` retained for Prompt API registration
- Communication `tests/` still has no real test methods

## Since 7.5.11

The inventory above is the 7.5.11 shape. On 2026-10-05 the donor's (Event+) later
work was reviewed and reimplemented, which changed the product shape in three ways:

- **Threats are tracked, not read from letters.** `Source/Threats/ThreatTrackerComponent`
  follows every hostile lord from the incident that made it until the lord is
  removed, and describes it from what the lord is doing (Core `ThreatPhasePolicy`,
  `ThreatPromptPolicy`; registry fields `force` and `phase`). The newest threat
  letter is used only when no tracked group owns it and it still points at a live
  threat. The danger-rating gate and the 7500-tick letter window are gone.
- **Harmony targets**: `Map.FinalizeInit`, `Quest.End`, plus
  `Quest.Notify_SignalReceived` / `AddPart` / `RemovePart` (quest cache
  invalidation) and `IncidentWorker.TryExecute`, `LordMaker.MakeNewLord`,
  `LordManager.AddLord` / `RemoveLord`, `LetterStack.ReceiveLetter`,
  `Pawn.Notify_BecameVisible` (threat capture). `validate_events_interior_sources.py`
  measures the set.
- **Type filters are per category** (quests, map conditions, site parts); threats
  are shown or hidden only as a category. The schema-1 single list is migrated
  once at startup (`EventFilterSettings.TryMigrateLegacyTypeFilters`).
