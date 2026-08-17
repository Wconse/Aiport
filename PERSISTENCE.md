# Persistence plan

AIInfluence uses Bannerlord `SyncData` plus external JSON under campaign-specific folders. At least 32 direct `File.WriteAllText` sites were found.

## Server-only layout

```text
E:\BCOOP\data\AIInfluence\campaigns\<UniqueGameId>\
  manifest.json
  aiinfluence_campaign_diplomacy.json
  npcs\
  snapshots\
  logs\
```

## Save barrier

1. Stop accepting state-changing requests.
2. Await/cancel active AI requests.
3. Flush `SaveQueueManager` equivalent.
4. Write temporary files.
5. Atomic rename.
6. Write manifest with save ID, campaign ID/day, schema version and hashes.
7. Release the Coop save operation.

On load, external state must match the loaded Coop save generation. Mismatch means restore a matching snapshot or start read-only; never silently mix generations.

## 0.0.38 narrative memory scope

Bounded dialogue memory now survives separate conversations and peer reconnects inside the running server process. It is keyed by authoritative player hero plus canonical target-instance ID. It is intentionally not serialized yet: server restart/save-load persistence remains blocked on the save-generation barrier described above.
