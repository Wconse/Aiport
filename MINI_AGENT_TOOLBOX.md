# Player2 mini-agent toolbox

## Purpose

An optional local orchestration toolbox is available at:

`E:\BCOOP\Новая папка\player2_toolbox\АНАЛИЗ`

Use it as a **secondary research and review layer**, not as a source of truth. It can parallelize narrow code analysis, compare implementation approaches and review selected changes. The primary agent remains responsible for source inspection, implementation, builds, tests and runtime proof.

## Mandatory boundaries

1. Do not read or summarize historical `reports`, `crew_output`, `outputs`, `results` or similarly named generated-output folders unless the user explicitly asks. Old outputs are not trusted project context.
2. Do not read, print, copy or document `accounts.txt`, token files, passwords or access tokens. Scripts may load credentials internally when executed; credentials must never be placed in prompts, console excerpts, project docs or commits.
3. Never auto-apply mini-agent output to `aiport`, `Coop`, `AIInfluence` or `E:\BCOOP`.
4. Verify every API/type/member claim against actual source or ILSpy-decompiled game assemblies.
5. Verify every proposed change with the normal AIPort build, cumulative tests and the appropriate disposable runtime gate.
6. Keep native diplomacy mutation disabled unless the user separately and explicitly authorizes it.
7. Prefer a narrow temporary context directory over passing an entire game/mod tree.

## Tool roles

### `p2_agents.py` — parallel research and review

Best for mapping a small set of related files, finding likely call paths, comparing decompiled TaleWorlds behavior with AIPort/Coop code, and independently reviewing files changed in the current milestone.

It shards files between agents and gives the reviewer summaries rather than guaranteed full cross-file context. Therefore its conclusions are hypotheses until independently verified.

Example with a narrow staging folder:

```powershell
python "E:\BCOOP\Новая папка\player2_toolbox\АНАЛИЗ\p2_agents.py" `
  --dir "E:\TEMP\aiport-agent-context\map-notifications" `
  --agents 3 `
  --model gpt_5_6_luna `
  --lang ru `
  --ext .cs .md `
  --focus "Find the exact Bannerlord map-notification registration and click path. Separate verified members from hypotheses. Review the proposed AIPort integration without inventing APIs." `
  --out "E:\TEMP\aiport-agent-runs"
```

Read only the new run created for the current task. Do not treat its report as proof.

### `p2_crew.py` — architecture comparison or adversarial review

Best for comparing implementation strategies, producing acceptance criteria and finding missing edge cases in an already-written design or patch.

Limitations:

- the workflow is strongest for Python;
- C# validation is mainly textual and does not replace compilation;
- workers may invent TaleWorlds/Coop APIs;
- generated files must not be copied automatically into production source.

Example for analysis/review only:

```powershell
python "E:\BCOOP\Новая папка\player2_toolbox\АНАЛИЗ\p2_crew.py" `
  --task "Review the supplied AIPort map-notification design. Compare registration options, click handling, accept/reject networking, deduplication and reconnect behavior. Do not generate replacement production code; return verified risks and acceptance checks." `
  --workers 3 `
  --teams 2 `
  --iterations 2 `
  --model gpt_5_6_luna `
  --lang ru `
  --context-dir "E:\TEMP\aiport-agent-context\map-notifications"
```

### `dll_extract.py` — metadata inventory fallback

This extracts .NET metadata but not method bodies. For Bannerlord investigation, prefer `ilspycmd` full decompilation. Use `dll_extract.py` only for quick assembly/type inventories or when ILSpy is unavailable.

## Recommended workflow

1. Define one precise question and the evidence needed.
2. Inspect current AIPort/Coop source and identify exact game assemblies.
3. Use `ilspycmd` to decompile only relevant game types/classes.
4. Create a clean task-specific folder under `E:\TEMP\aiport-agent-context\<topic>` containing only selected source/decompiled files.
5. Run `p2_agents.py` for research, or `p2_crew.py` for architecture comparison/review.
6. Independently verify each useful finding against source/decompiled code.
7. Implement changes manually in `aiport` only.
8. Build, run cumulative tests, deploy through the guarded rollback workflow and gather runtime evidence.
9. Record only verified conclusions in milestone/status documentation.

## When not to use it

Do not invoke mini-agents for simple one-file edits, facts already proven by source/runtime logs, handling secrets, direct deployment/save manipulation, native war/peace commit decisions, or tasks where local search/ILSpy is faster and more reliable.

## Current notification research seed

The currently verified native path includes:

- `CampaignInformationManager.NewMapNoticeAdded(InformationData)`;
- `MBInformationManager.AddNotice` / `OnAddMapNotice`;
- `MapNotificationVM.RegisterMapNotificationType(Type data, Type item)`;
- `KingdomDecisionMapNotification` / `KingdomVoteNotificationItemVM`;
- `PeaceOfferMapNotification` / `PeaceOfferNotificationItemVM`.

Future sessions should verify the exact registration surface available from the active map view, then implement a custom AIPort `InformationData` plus notification item VM that opens a non-dialogue accept/reject inquiry and sends an authoritative server request. Do not reuse vanilla peace-offer callbacks blindly: they execute vanilla campaign logic rather than AIPort recipient-consent logic.
