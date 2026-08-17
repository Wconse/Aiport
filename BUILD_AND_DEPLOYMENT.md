# Build and deployment notes

## Current authoritative deployment

- Build `0.0.37-dev`, protocol `1`.
- Runtime `AIPort.dll`: 97,792 bytes; SHA-256 `78bd9bb6c58602168a8391a758a4c18e9c6393f5de38d9317e57356d383fa148`.
- Bootstrap: 10,240 bytes; SHA-256 `8357d5c56766dabba2777d472b87b1919a1925242b6d1f8a00bf99fd653934cf`.
- Deployment rollback: `backups\m0-20260814-073639`; source rollback: `backups\source-20260814-072255-pre-0.0.37`.
- Client/server runtime and bootstrap hashes match.
- Groq is enabled through the server process environment with explicit config `E:\BCOOP\aiport-server.json`; model `llama-3.1-8b-instant`.
- Regular `CharacterObject` targets are supported in the townsfolk/villager, prison-guard, castle-guard and selected alley root menus. Hero dialogue remains root-only at `hero_main_options`.
- Every target receives a deterministic immutable narrative profile derived from target id, culture and occupation. Hero traits remain authoritative; the profile does not invent a biography and requires no persistence file.
- Up to five relevant authoritative campaign-log facts are added from a bounded scan of 96 recent entries: meetings, player battles, captivity/release, war/peace and settlement ownership changes.
- New 0.0.37 suite passed 21/21; 0.0.36 prompt suite passed 17/17; explicit-routing, provider and config suites remain green.
- Disposable server: wrapper PID `27484`, engine PID `29144`, log `artifacts\runtime-m0\logs\coop-server-20260814-073640.log`; `CampaignReady` and engine `SERVING` confirmed at `07:37:26`, with no fatal/unhandled error.
- Gameplay actions, diplomacy mutations, synthetic dynamic events and writable persistence remain disabled.
## Historical deployment snapshot

- Build `0.0.28-dev`, protocol `1`.
- Runtime 75,264 bytes, SHA-256 `b5ff59c1f1769367ee7b71e1699c57b3ed4eaf25368ea4bba7fab91baee37d02`; bootstrap SHA-256 `8357d5c56766dabba2777d472b87b1919a1925242b6d1f8a00bf99fd653934cf`.
- Deployment rollback `backups\m0-20260814-051256`; source rollback `backups\source-20260814-051222-pre-0.0.28`.
- Fixes 0.0.27 player-to-player return-token auto-chaining with a deterministic NPC bridge and keeps the main AI entry visible during pending/retry state.
- Server wrapper `27128`, engine `33984`, log `artifacts\runtime-m0\logs\coop-server-20260814-051306.log`; CampaignReady/SERVING, backend disabled/no key.


## Historical deployment snapshot

- Build `0.0.27-dev`, protocol `1`.
- Runtime 74,752 bytes, SHA-256 `19c29e93349ee278b533feee891f1f4779ccddbd2dd41fdf90345db3c9c94662`; bootstrap SHA-256 `8357d5c56766dabba2777d472b87b1919a1925242b6d1f8a00bf99fd653934cf`.
- Deployment rollback `backups\m0-20260814-050315`; source rollback `backups\source-20260814-050239-pre-0.0.27`.
- 0.0.26 bundled suite passed all 15 runtime checks. 0.0.27 adds `Сказать ещё...` and `Вернуться к обычному разговору.` post-response choices.
- Disposable server wrapper `25012`, engine `4248`, log `artifacts\runtime-m0\logs\coop-server-20260814-050322.log`; CampaignReady/SERVING. Backend disabled, no key.


## Historical deployment snapshot

- Build `0.0.24-dev`, protocol `1`.
- Runtime: 73,216 bytes; SHA-256 `222a3bcdff72fcd22726c68877715b3b46ea2920af426ede68c2ccccd5790464`.
- Bootstrap: 10,240 bytes; SHA-256 `8357d5c56766dabba2777d472b87b1919a1925242b6d1f8a00bf99fd653934cf`.
- Deployment rollback: `backups\m0-20260814-035153`; source rollback: `backups\source-20260814-034949-pre-0.0.24`.
- Disposable server: wrapper `35328`, engine `19640`, log `artifacts\runtime-m0\logs\coop-server-20260814-035201.log`; CampaignReady/SERVING at `03:52:44`.
- Client/server runtime hashes match. Backend explicitly disabled and no key present. Actions, diplomacy, dynamic events and persistence remain disabled.



## Current candidate

- Updated: 2026-08-14 03:53 +05:00.
- 0.0.23 runtime regression passed: compatible handshake, one native + one free-form + one native exit turn, memory 1 -> 2 -> 3, display-only results and clean conversation end.
- Built 0.0.24-dev (protocol 1): runtime 73,216 bytes, SHA-256 `222a3bcdff72fcd22726c68877715b3b46ea2920af426ede68c2ccccd5790464`; bootstrap unchanged at SHA-256 `8357d5c56766dabba2777d472b87b1919a1925242b6d1f8a00bf99fd653934cf`.
- Security delta: fixed `AIPORT_API_KEY` credential source; only HTTPS or loopback HTTP endpoints; no endpoint user-info/fragments.
- Source rollback: `backups\source-20260814-034949-pre-0.0.24`. Deployment pending.

Updated: 2026-08-14 00:46 +05:00

## Current authoritative build and deployment

- Build: `0.0.23-dev`; protocol: `1`.
- Runtime: 72,704 bytes; SHA-256 `d13198f0ee4c0e3840076e44b70ec6cbd800053478c6fb63672690fae6109316`.
- Bootstrap: 10,240 bytes; SHA-256 `8357d5c56766dabba2777d472b87b1919a1925242b6d1f8a00bf99fd653934cf`.
- Source rollback: `backups\source-20260814-004147-pre-0.0.23`.
- Deployment rollback: `backups\m0-20260814-004339`.
- Client and server runtime/bootstrap hashes match.
- Disposable runtime: wrapper PID `24340`, engine PID `30292`, log `coop-server-20260814-004353.log`, CampaignReady/SERVING.
- First deploy attempt at `00:42:49` was safely rolled back when launcher PID `25404` held the client bootstrap DLL; the authorized launcher was closed and the guarded retry succeeded.
- Backend explicitly disabled; no API key is present.

## Target

- `netstandard2.0`
- Bannerlord 1.4.7 assemblies
- Coop/Common 0.1.1 assemblies
- protobuf-net and Serilog from the Coop distribution

## Build environment

No standalone .NET SDK is installed. Visual Studio 2026 provides Roslyn CSC 5.7 at `E:\VIS STUD\MSBuild\Current\Bin\Roslyn\csc.exe`. The SDK-style project cannot resolve `Microsoft.NET.Sdk`, so `tools\build.py` compiles directly against the Visual Studio netstandard reference facade and game/Coop assemblies.

## Current build

- Build: `0.0.21-dev`
- Protocol: `1`
- Bootstrap: `10,240` bytes
- Bootstrap SHA-256: `a2e0d24e5a5c503270ce3406dcc63ae8a71da1b9251ebe1925f44af5932b270d`
- Runtime `AIPort.dll`: `69,632` bytes
- Runtime SHA-256: `f00588885595dfcf80707ffc97b24170245427440b614a281f78a8fea345b56f`

## 0.0.14 deployment

- Build: `0.0.21-dev`
- Protocol: `1`
- Runtime `AIPort.dll`: `69,632` bytes
- Runtime SHA-256: `f00588885595dfcf80707ffc97b24170245427440b614a281f78a8fea345b56f`
- Bootstrap unchanged: `8,704` bytes, SHA-256 `a2e0d24e5a5c503270ce3406dcc63ae8a71da1b9251ebe1925f44af5932b270d`
- Staging and deployed hashes verified for both client and server.
- Rollback backup: `backups/m0-20260813-212758`.
- Disposable server PID `16116`, engine PID `26484`, log `coop-server-20260813-212816.log`.
- Server loaded 0.0.14 and reached CampaignReady/SERVING at `21:29:00`.

## M0 staging

`tools\stage_m0.py` creates a non-live package under `artifacts\staging`:

- client: patched Coop descriptor plus Bootstrap and runtime AIPort DLLs;
- server: patched DedicatedServer.Windows descriptor plus the same DLLs;
- `manifest.json`: source and staged hashes.

The strategy adds AIPort as a second submodule inside existing descriptors. It does not add an active module ID and therefore avoids client/server module-list mismatch.

## Deployment and rollback guards

- `tools\deploy_m0.py --apply` refuses active game/server processes.
- It regenerates staging, backs up every replaced file under `backups\m0-<timestamp>`, deploys and verifies hashes.
- A partial-copy failure automatically restores entries already changed.
- `tools\rollback_m0.py --apply` restores the latest recorded deployment.
- `tools\stop_m0_server.py --apply` stops the wrapper and leftover dedicated-server `dotnet.exe` processes.
- `tools\run_m0_server.py --apply` launches only the disposable `artifacts\runtime-m0` / `aiport-m0` test world.

## Current deployed test

- Deployment applied: `0.0.14-dev`.
- Rollback backup: `backups\m0-20260813-212758`.
- Disposable server log: `artifacts\runtime-m0\logs\coop-server-20260813-212816.log`.
- Server wrapper PID: `16116`; engine PID: `26484`.
- Launcher PID for pending 0.0.14 proof: `17060`.
- 0.0.13 handshake RequestId: `8fb02151c7ec40a0a38cd2eb8ae26467`.
- 0.0.13 retry proof ConversationId: `23865aec886f429382d6d0877ada088a`.
- Server memory proof: `MemoryTurns=1`, then `MemoryTurns=2`.
- Disconnect/reconnect proof: peer 0 memory cleared, peer 1 memory reset, next NPC conversation restarted at `MemoryTurns=1` and reached `MemoryTurns=2`.
- Rate-limit retry proof: RequestId `c45abbccf41a43df8fdb12e5fa37bd29` returned `RetryAfterMs=33450`; retry RequestId `9f438fa80b0842aa870b1f816ad444a0` was accepted with `MemoryTurns=2`.
- 0.0.13 implementation and successful retry path are runtime-proven; cancellation-before-timer proof remains.
- Current client RGL log: `C:\ProgramData\Mount and Blade II Bannerlord\logs\rgl_log_25620.txt`.
- Live `saveauto1` was reverified unchanged: 80,655 bytes, SHA-256 `0d9d79dca420f086b68336df6cb32569d044ba08d50acfab097772db7f8320d5`.
- Backend disabled: `enabled=False`, `keyPresent=False`.
- Live `saveauto1` was not used or modified.

## Pinned Coop binaries

Do not replace:

- `Coop.Core.dll`: `52fa0c26b918cc9d8e77f7a0b1284551b08eecaa1bce63ad2fad415f6d13d406`
- `GameInterface.dll`: `c737b7cb3b7296a8191e8e9f02657126f71df502baec556b8dfdc12288ff26d9`
- `Common.dll`: `650087882c3885e603d6c3eee7fa6606421e902f89e1778354d55dcd01c0faa4`
- `Coop.Steam.dll`: `eef511c50b1c932264eddf5f6b6da576250f399900c9ca677ec2caa105ac013b`

## Operational rules

- Preserve server console output and client RGL/Coop logs for every proof.
- Never deploy `AIInfluence.analysis-clean.dll`.
- Never store `AIPORT_API_KEY`; provide it only as an environment variable for a controlled backend test.
- Keep actions, diplomacy, dynamic events and persistence disabled through the narrative milestone.

## 0.0.15 staged candidate

- Build: `0.0.21-dev`; protocol: `1`.
- Runtime `AIPort.dll`: 52,224 bytes; SHA-256 `6ecbefc64d2a1aedd97bb750bf0f1479586181ab646707255e8bb8c36f138575`.
- Bootstrap: 8,704 bytes; SHA-256 `a2e0d24e5a5c503270ce3406dcc63ae8a71da1b9251ebe1925f44af5932b270d`.
- Staged client/server hashes match the build.
- Source rollback: `backups/source-20260813-215942-pre-0.0.15`.
- Deployed with rollback backup `backups/m0-20260813-221843`.
- Disposable server wrapper PID `30344`, engine PID `30464`, log `coop-server-20260813-221853.log`; runtime build 0.0.15 reached CampaignReady/SERVING at `22:19:37`.
- Do not launch the game; the user launches/connects for runtime proof.

## 2026-08-13 - 0.0.15 UI proof, stuck-flow defect, and 0.0.16 fix

- Runtime 0.0.15 proved the intended transport and identity path with hero `lord_5_13`, ConversationId `79ee654712bf4e8ea4a60e10c673ced4`.
- Actual selected player lines were captured and sent: turn 1 `Меня зовут Руган, господин. Могу ли я узнать ваше имя?`; turn 2 `Я хочу кое-что обсудить.`
- Server accepted both requests and logged `MemoryTurns=1` then `MemoryTurns=2`; both stub results appeared in the active dialogue UI.
- Defect: after the second result the dialogue became stuck. 0.0.15 changed `ConversationManager._currentSentenceText` and called the VM `Refresh()`, corrupting the native conversation flow while replacing the displayed line.
- 0.0.16 removes both state mutations. The response is now a presentation-only assignment to the active mission/map conversation VM `DialogText`; Bannerlord retains its native sentence, options, continuation and exit state.
- Source rollback: `backups\source-20260813-222657-pre-0.0.16`.
- Build/deployed DLL: 51,712 bytes, SHA-256 `d8becd713ecd998d268f3b8605c1bc909451ddd3c6926e82ad5eab89ccff228f`; protocol remains 1.
- Deployment rollback: `backups\m0-20260813-222805`.
- User-authorized client/launcher PID `23188` was closed; only the disposable server was stopped/restarted.
- Server wrapper PID `31044`, engine PID `31592`, log `coop-server-20260813-222816.log`; 0.0.16 reached CampaignReady/SERVING at `22:28:59`.
- Backend/actions/diplomacy/dynamic events/persistence remain disabled.

## 2026-08-13 - 0.0.16 runtime pass and 0.0.17 free-form input deployment

- User confirmed the 0.0.16 display-only override no longer traps the native dialogue; continuation and exit work. This closes the 0.0.15 stuck-flow defect.
- Added a native `Сказать своими словами…` player option at the standard `hero_main_options` token for active one-to-one hero conversations.
- Selecting it opens the standard Bannerlord `TextInquiryData` input with Send/Cancel actions, non-empty validation and the existing 4000-character protocol limit.
- A static headless-safe bridge connects the campaign dialogue registration to the Coop client handler only when the client handler is active and no request is pending.
- The custom option's own sentence ID is filtered from `ConsequenceRunned`, so only the entered free-form text is sent; the option label is never sent as a duplicate turn.
- Added a native waiting NPC line (`Я слушаю…`) that returns to `hero_main_options`. The authoritative result still changes only the displayed `DialogText`, preserving Bannerlord's internal sentence graph.
- Existing ordinary selected-option capture remains enabled as a secondary path.
- Source rollback: `backups\source-20260813-223814-pre-0.0.17`.
- Build `0.0.17-dev`, protocol 1: `AIPort.dll` 55,296 bytes, SHA-256 `3625c916edde82c4c7caa8f4d68c0777efff9df08d054f54455a05d63af9873c`; bootstrap unchanged.
- Source invariants passed: standard hero token, standard text inquiry, custom-line duplicate filter, 4000-character guard, display-only response, no internal manager mutation and no synthetic Hello probes.
- Closed user-authorized launcher PID `28244`, stopped only the disposable server, and deployed identical client/server DLLs.
- Deployment rollback: `backups\m0-20260813-223917`.
- Server wrapper PID `28624`, engine PID `30168`, log `coop-server-20260813-223937.log`; runtime loaded 0.0.17 and reached CampaignReady/SERVING at `22:40:21`.
- Backend remains disabled; actions, diplomacy, dynamic events and persistence remain disabled.
- Runtime proof pending: option visibility, textbox submission, exact free-form server turn, in-dialogue stub response, return to native options and normal exit.

## 2026-08-13 - 0.0.17 option-registration failure and 0.0.18 bootstrap lifecycle fix

- Runtime test showed no `Сказать своими словами…` option anywhere. Ordinary selected-option capture still worked, but every turn had `Source=native_option`; no free-form registration log appeared.
- Root cause: the deployed SubModule points to `AIPortBootstrapSubModule`. The bootstrap loaded `AIPort.dll` for Coop discovery but did not forward Bannerlord's `OnGameStart` lifecycle to the runtime `AIPortSubModule`, so its `CampaignGameStarter.AddPlayerLine` registration never executed.
- Added a public idempotent runtime entrypoint `RegisterCampaignDialogs(IGameStarter)`, keyed by the exact `CampaignGameStarter` instance.
- Bootstrap now overrides `OnGameStart`, loads the runtime if needed, and invokes that entrypoint via reflection. Bootstrap build references now include `TaleWorlds.Core`.
- Build advanced to `0.0.18-dev`; protocol remains 1.
- Runtime DLL: 55,296 bytes, SHA-256 `0883f87a710a767fd37b01ac0aa76554ae8a420b90d8f630bd42f28e2ddf7374`.
- Bootstrap DLL: 9,728 bytes, SHA-256 `9c37eb49241e26ea43b2564da71ca00a78056d1fabea5f782bfd6f834a84d85f`.
- Source rollback: `backups\source-20260813-225151-pre-0.0.18`; deployment rollback: `backups\m0-20260813-225215`.
- Closed user-authorized launcher PID `32512`, deployed matching client/server runtime and bootstrap hashes, and restarted only the disposable server.
- Server wrapper PID `32408`, engine PID `31844`, log `coop-server-20260813-225229.log`; CampaignReady/SERVING at `22:53:14`.
- Server runtime proof now contains both required lifecycle markers: `Free-form hero dialogue option registered` and `Bootstrap delegated OnGameStart to runtime dialogue registration`.
- Client-side visibility and textbox submission remain to be verified after the user launches 0.0.18.

## 2026-08-13 - 0.0.18 free-form runtime proof passed

- User confirmed the option appeared, accepted typed input, and allowed a normal dialogue exit.
- Client/server handshake matched build `0.0.18-dev`, protocol 1, Compatible=true (`632af7e5b0f245a8bf089b16da00c3e5`).
- Hero target: `lord_5_13`; ConversationId `fb4c3ce382c04b70b3806cd8611e0684`.
- Native opening turn: RequestId `335706e5b79d4e1981eaf27d3dc4b980`, MemoryTurns=1.
- Free-form submission was captured as `Source=free_form`, 12 characters, Turn=2 — not as the custom option label. RequestId `a7fb4741ba3f4a0eab17a13c3143edcd`.
- Server accepted the free-form request under the same conversation and returned the stub with MemoryTurns=2.
- Client queued/applied the authoritative response as a display-only `DialogText` override.
- A subsequent native exit/option turn stayed in the same conversation (Turn=3, MemoryTurns=3), followed by `conversation ended`; native flow was not trapped.
- This proves option registration, textbox input, exact free-form transport, stable identity/memory, server authority, response rendering and safe exit.
- Backend remains disabled; next milestone: explicit waiting/error/timeout UX, then one controlled real-backend turn.

## 2026-08-13 - 0.0.19 async dialogue UX hardening deployed

- Added a 100-second client request watchdog. If no matching result/error arrives, the pending request is released, a best-effort cancel is sent, and a safe retry message is queued in the active dialogue.
- Non-retryable and unsupported retryable errors now always clear pending state and surface a localized dialogue message; `backend_timeout`, `backend_failed`, rate limits and player-resolution failures have explicit text.
- Automatic rate-limit/player-resolution retry now keeps the free-form option unavailable for the entire scheduled retry window. Native dialogue submissions are also ignored while a request or retry is pending.
- Retry waiting and retry exhaustion are visible in the dialogue. Repeated native options can no longer replace/reschedule the queued retry payload.
- The native free-form waiting line now says `Секунду… Я обдумаю ответ.` while preserving normal continuation and exit.
- Exiting a dialogue with a pending request sends `AIConversationCancel`. The server tracks active backend request IDs and suppresses late result/error delivery and memory writes after cancellation. The synchronous HTTP call is not forcibly aborted yet.
- Server now classifies `WebExceptionStatus.Timeout` as `backend_timeout`; other backend failures remain safe `backend_failed` events.
- Source rollback: `backups\source-20260813-231533-pre-0.0.19`.
- Build `0.0.19-dev`, protocol 1. Runtime `AIPort.dll`: 61,440 bytes, SHA-256 `30a87c3ce12c9c8c9e7ba4e09275a75f3d0001aff022ff8f0ed68b8a76ecb111`. Bootstrap remains 9,728 bytes, SHA-256 `9c37eb49241e26ea43b2564da71ca00a78056d1fabea5f782bfd6f834a84d85f`.
- Deployment rollback: `backups\m0-20260813-231606`; matching client/server hashes verified.
- Closed user-authorized launcher PID `32904`, restarted only the disposable M0 server. Wrapper PID `18008`, engine PID `14560`, log `coop-server-20260813-231608.log`.
- Runtime loaded 0.0.19, registered the free-form option and reached CampaignReady/SERVING at `23:16:53`. Backend remains disabled and no API key is present.
- Runtime proof pending: normal stub free-form response with the new waiting copy, normal exit, and (later) controlled error/timeout behavior before one real-backend turn.

## 2026-08-13 - paused-dialogue diagnosis, 0.0.20 realtime UI pump, and 0.0.21 backend safety gate

### 0.0.19 runtime evidence

- User completed several free-form conversations successfully. Stub results, stable conversation IDs, memory growth, safe native exit, retry waiting, bounded rate-limit retry and retry cancellation all appeared in the logs.
- A paused dialogue exposed a real presentation defect: the server result arrived immediately but stayed queued until `CampaignEvents.TickEvent` advanced. Example: RequestId `6f2c450edc37400d9f227da4085ba1d9` was queued at `23:26:39`, while paused campaign time prevented the display override.
- Root cause: UI delivery was pumped only from campaign time, although network delivery and the conversation screen continue while campaign time is paused.

### 0.0.20 implementation and deterministic tests

- Added `AIPortRuntimeLifecycleBridge`. The bootstrap now overrides `MBSubModuleBase.OnApplicationTick(float)` and binds a typed `Action<float>` delegate once at runtime load; there is no per-frame reflection.
- The client applies queued dialogue results from real-time application ticks. Campaign tick remains responsible only for periodic target scanning, so paused campaign time no longer gates response rendering.
- Preserved the proven display-only rule: no `_currentSentenceText` mutation and no conversation-VM `Refresh()`.
- Upgraded the OpenAI-compatible backend path: complete JSON string escaping/parsing (including Unicode escapes), UTF-8 request/response handling, read/write timeout, 8,000-character NPC output bound, and an abortable `HttpWebRequest` handle.
- Server cancellation now aborts an active HTTP request and still suppresses late result/error delivery and memory writes.
- Campaign/Hero facts are snapshotted before the worker thread. The prompt now includes authoritative names, clan, kingdom, culture, home, occupation, age, gold and NPC-to-player relation, quotes untrusted dialogue/history, requires the same response language and keeps actions disabled.
- Deterministic local fake-backend tests passed: valid request JSON, control-character escaping, Russian/Unicode response parsing, quote/backslash/newline handling, 8,000-character clamp and `WebExceptionStatus.RequestCanceled` after abort.
- Lifecycle bridge reflection test passed: attach/tick/detach count `2`, sum `4`, bootstrap override present, build/protocol/bounds correct.
- Intermediate 0.0.20 deployment backup: `backups\m0-20260813-234006`.

### 0.0.21 explicit backend activation safety

- A backend key alone can no longer activate outbound AI traffic. Three conditions are required together: `enabled: true` in server config, a key in the named process environment variable, and an HTTPS or loopback endpoint.
- Plain HTTP to a non-loopback host and malformed endpoints disable the backend. Timeouts clamp to 5–120 seconds, concurrency to 1–4, and per-player rate to 1–60 requests/minute. Diplomacy and dynamic events remain forced off.
- Added secret-free template `config\server.example.json`; no live config and no key were created.
- Seven deterministic settings scenarios passed: key-only disabled, explicit-without-key disabled, HTTPS enabled, remote HTTP blocked, loopback HTTP allowed, limits clamped, malformed endpoint blocked.
- Source rollback: `backups\source-20260813-234230-pre-0.0.21`. Deployment rollback: `backups\m0-20260813-234340`.
- Current build `0.0.21-dev`, protocol 1. Runtime DLL: 69,632 bytes, SHA-256 `f00588885595dfcf80707ffc97b24170245427440b614a281f78a8fea345b56f`. Bootstrap: 10,240 bytes, SHA-256 `8357d5c56766dabba2777d472b87b1919a1925242b6d1f8a00bf99fd653934cf`.
- Matching client/server hashes verified. Disposable server wrapper PID `32196`, engine PID `29612`, log `coop-server-20260813-234341.log`; CampaignReady/SERVING at `23:44:23`.
- Server startup confirms `applicationTickBridge=true`, dialogue registration, `explicitlyEnabled=False`, `enabled=False`, `keyPresent=False`, and allowed HTTPS default endpoint.
- Client runtime proof still required: submit while campaign time is paused and verify that the result replaces the waiting text immediately without unpausing.
## Temporary normal-Coop toggle

Three double-click launchers are available in the `aiport` project root:

- `AI PORT - OFF FOR NORMAL COOP.bat` removes only the exact AIPort bootstrap blocks from the client/server descriptors and parks both runtime/bootstrap DLL pairs under `artifacts\toggle-aiport\parked`.
- `AI PORT - ON FOR DEVELOPMENT.bat` restores the exact parked DLL snapshot, verifies SHA-256 hashes and reinserts the bootstrap blocks without replacing unrelated descriptor content.
- `AI PORT - STATUS.bat` reports `development-enabled`, `normal-coop` or `mixed-unsafe`.

The switch refuses to run while any process maps one of the six affected files. Close Bannerlord, its launcher and the Coop server first. Both directions are guarded, hash-verified and roll back descriptor/file changes after a failure. The live campaign and save files are never touched. Descriptor round-trip and launcher tests pass.

## Disposable server environment (0.0.38+)

`tools/run_m0_server.py` passes `AIPORT_CONFIG_PATH=E:\BCOOP\aiport-server.json` to the child server. It deliberately does not store or synthesize `AIPORT_API_KEY`; the caller must provide that secret in the process environment. After restart, require both `keyPresent=True` and `enabled=True` in the sanitized settings log before backend testing.

## Groq runtime re-enabled — 0.0.38-dev (2026-08-14 10:00 +05:00)

- The disposable server was restarted with `AIPORT_API_KEY` supplied only in the child process environment; the credential was not written to source, config, scripts, logs, or documentation.
- Current server PID: `7764`; save: `aiport-m0`; live campaign saves remain untouched.
- Sanitized startup proof at `09:59:15`: `configPath="E:\BCOOP\aiport-server.json"`, `backend="Groq"`, `explicitlyEnabled=True`, `enabled=True`, `keyPresent=True`, model `llama-3.1-8b-instant`.
- Dedicated server reached `SERVING` at `09:59:31`. No AIPort fatal/unhandled exception was present in the startup scan.
- The earlier `keyPresent=False` note is retained as historical evidence of the first restart and is superseded by this successful launch.

## 0.0.97 candidate build (not deployed)

- Protocol `2`; capability flags `2097151`.
- Build command: `python tools\build.py`.
- `AIPort.dll`: 341504 bytes, SHA-256 `c52f5f0e67a35da1e826f0eb58311831fb028fa4bfa602d22408338a8357f17f`.
- `AIPort.Bootstrap.dll`: 10240 bytes, SHA-256 `8357d5c56766dabba2777d472b87b1919a1925242b6d1f8a00bf99fd653934cf`.
- Current cumulative tests: 20/20 PASS.
- Source rollback: `backups\source-20260817-052827-pre-0.0.97`.
- Runtime-binary rollback: `backups\runtime-20260817-055314-pre-0.0.97`.
- Staged client/server candidate: `artifacts\stage-0.0.97`.
- Runtime still uses `0.0.95-dev`; do not copy candidate DLLs until a deployment rollback is created and revision `11` save handling is decided.
- Backend credentials are supplied only through the process environment; never place them in repository files or command output.
