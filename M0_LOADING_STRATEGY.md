# M0 loading strategy

## Decision

Load the same `AIPort.dll` as an additional submodule of modules that are already active, instead of introducing a new Bannerlord module ID:

- client: append `AIPort.AIPortSubModule` to `Coop/SubModule.xml`;
- dedicated server: append it to `DedicatedServer.Windows/SubModule.xml`;
- copy the DLL beside the owning module's managed binaries.

The active module list remains unchanged, so Coop module-version validation should still compare the same module IDs and versions.

## Source evidence

- `DedicatedServer.Windows.DedicatedServerSubModule.OnSubModuleLoad()` installs Coop's resolver and delegates to `CoopServerHost.OnSubModuleLoad()`.
- `CoopServerHost.OnSubModuleLoad()` only reads configuration and installs console handling.
- The server Autofac container is built later, when the first-tick startup path reaches `CoopDriver.HostSaveGameAsServer()` and `CoopartiveMultiplayerExperience.StartAsServerCore()`.
- Therefore an AIPort submodule listed after `DedicatedServer.Windows` loads before the server handler scan.
- On the client, `Coop.CoopMod.NoHarmonyLoad()` creates `CoopartiveMultiplayerExperience`, but the client Autofac container is built only in `StartAsClientCore()` during join.
- Therefore an AIPort submodule listed after `Coop.CoopMod` also loads before the client handler scan and avoids early dependency-binding risk.
- Both `ClientModule` and `ServerModule` scan loaded AppDomain assemblies for `IHandler` implementations under their namespace prefixes.

## Why not a standalone AIPort module for M0

The BCOOP wrapper exposes no documented extra-module list. A standalone client module would also appear in Coop's module validation while the server wrapper may not activate it. Reusing active modules avoids both unknowns without changing a hash-pinned Coop assembly.

## Safety boundaries

- No hash-pinned Coop DLL is changed.
- Staging writes only under `Modules/aiport/artifacts/staging`.
- Live deployment requires `--apply` and creates rollback data under `Modules/aiport/backups`.
- Deployment and rollback refuse to run while Bannerlord or the dedicated server process is active.
- The first runtime test must use a disposable campaign.

## Remaining uncertainty

The wrapper may enforce integrity outside the four published Coop DLL hashes, or the engine may reject the added server submodule. The disposable M0 runtime test is required to close this risk.
