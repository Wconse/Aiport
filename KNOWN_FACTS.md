# Known facts

## Versions

- Bannerlord: 1.4.7
- Coop: 0.1.1
- Coop commit: `324d59795711727d40bc1aa3eb56a652ae656306`
- AIInfluence: 6.0.2

## AIInfluence extraction

- Original SHA-256: `472658f15a0ee00b6b05904830fda257593653f74fdf3c8fec1345267a73862d`
- Extracted files: 610 C# files
- Approximate lines: 468,810
- Metadata types: 2,890
- Methods: 17,091
- Main extraction: `E:\AIInfluence_Extracted_20260813`

## Important recovered types

- `AIInfluence.SubModule`
- `AIInfluence.InitializationManager`
- `AIInfluence.AIInfluenceBehavior`
- `AIInfluence.DialogManager`
- `AIInfluence.PromptGenerator`
- `AIInfluence.ModSettings`
- `AIInfluence.API.AIClient`
- `AIInfluence.Diplomacy.DiplomacyManager`
- `AIInfluence.DynamicEvents.DynamicEventsManager`
- `AIInfluence.Behaviors.AIActions.AIActionManager`
- `AIInfluence.SaveQueueManager`
- `AIInfluence.SaveSystem.SaveSnapshotManager`

## Key risks

- UIExtender is enabled unconditionally by original `SubModule`.
- `InitializationManager` mixes server, UI and mission systems.
- MCM settings are accessed directly throughout core logic.
- Prompt/action code assumes single-player globals.
- Persistence is split across Bannerlord save and external JSON.
- Decompiled code contains obfuscation/decompiler artifacts and is not directly build-ready.
