# Target architecture

## Authority

The BCOOP campaign server is the only authority for prompts, NPC memory, AI backend calls, diplomacy, dynamic events, action validation and external AIInfluence state.

Clients own presentation only: dialogue input/output, pending/error/cancel state, notifications and optional TTS/images.

## Components

### AIPort protocol

Shared protobuf DTOs. They contain primitive IDs and strings, never TaleWorlds object references or secrets.

### Server core

- `PlayerContextResolver`: `NetPeer/controller id -> Coop player hero/party`.
- `ConversationCoordinator`: per-conversation ordering, idempotency and cancellation.
- `PromptService`: adapted AIInfluence prompt generation with explicit player context.
- `BackendService`: OpenRouter/DeepSeek/Player2/Ollama/KoboldCpp behind one settings interface.
- `ResponsePipeline`: strict schema, validation and safe text extraction.
- `ActionPolicy`: deny by default and explicit per-action limits.
- `ActionExecutor`: game-thread execution only.
- `ServerPersistence`: save barriers, atomic files and snapshots.

### Client shell

- Detect current dialogue partner.
- Send requests and correlate responses.
- Never execute authoritative consequences.
- Never receive API keys.

## Loading seam found in Coop

`SerializableTypeMapper` scans all loaded AppDomain assemblies for `[ProtoContract]` and creates a stable ID from the full type name.

`InterfaceCollector.GetInterfaces<IHandler>("Coop.Core.Client")` and the server equivalent scan all AppDomain types whose namespace starts with the requested prefix. Therefore handlers in this addon can be auto-discovered if:

1. `AIPort.dll` is loaded before Coop builds its Autofac container;
2. client handlers use a `Coop.Core.Client...` namespace;
3. server handlers use a `Coop.Core.Server...` namespace;
4. dependencies resolve from the active Coop bin directory.

This is source-confirmed but not runtime-confirmed.

## Hard boundary

The original `AIInfluence.SubModule` is not reused on the server. It unconditionally enables UIExtender and initializes client, mission and browser systems. Logic is ported behind explicit server/client bootstraps instead.
## Provider configuration boundary

The dedicated server can select an absolute JSON configuration with the server-owned `AIPORT_CONFIG_PATH` environment variable. Provider credentials are never read from JSON: authorization remains fixed to `AIPORT_API_KEY`. The resolved endpoint must be HTTPS (or HTTP loopback for disposable tests), redirects are disabled, and completion length is bounded before sending the request.
## Narrative prompt boundary

The runtime prompt may reuse narrative concepts researched from the original AIInfluence defaults, but it does not load or call the original mod. Only server-resolved Hero/CharacterObject facts, a deterministic immutable profile, a bounded read-only campaign-log summary and bounded volatile conversation history enter the request. Hero traits and relations remain authoritative. Regular-character profiles are stable across restarts because they are derived from target id, culture and occupation; they are not AI-written biographies and create no persistence file. JSON action schemas, emitted internal thoughts, gameplay commands, writable persistence and mutation subsystems remain outside the port boundary.

## Future AIInfluence feature architecture

The planning-only master design for diplomacy, typed actions, persistence, dynamic events, diseases, memory, quests, initiative, party tasks, romance, combat and client presentation is [`FUTURE_AI_FEATURES_PLAN.md`](FUTURE_AI_FEATURES_PLAN.md). It preserves the server-authority boundary above and does not enable any of those systems.
