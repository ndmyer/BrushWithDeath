# Handoff

## Latest debug fix
- Symptom: player could sometimes trigger the end-game exit before completing Puzzle D when near it.
- Likely root cause: the outer Puzzle D tempo doors should be local-radio/proximity driven, but the shared `Door_Tempo_Base` prefab was subscribed to the global `TempoService`, allowing global/player tempo changes to satisfy latched receivers from anywhere.
- Fix:
  - Set `listenToGlobalTempo` to `0` on `Assets/Prefabs/Puzzle/Door_Tempo_Base.prefab` so tempo doors default to local `RadioController` broadcasts.
  - Added a `Level-1.unity` prefab override for the middle door (`Door_Tempo_Fast`) setting `listenToGlobalTempo` back to `1`, because the middle of the three tempo doors is intentionally global-tempo driven.

## Verification
- Confirmed prefab now has `listenToGlobalTempo: 0`.
- Confirmed `Level-1.unity` has one `listenToGlobalTempo` override, on `Door_Tempo_Fast` / middle door, set to `1`.
- Previous CLI compile attempt: `dotnet build Assembly-CSharp.csproj --no-restore` was blocked by missing Unity-generated `Temp/obj/Assembly-CSharp/project.assets.json`, so no CLI compile result is available.

## Remaining uncertainty
- Needs in-Unity playtest: before solving Puzzle D, verify only the middle door responds to global tempo and the exit trigger remains inactive until the full intended Puzzle D condition is satisfied.
