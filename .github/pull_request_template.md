## Summary

<!-- What does this PR do? One or two sentences. -->

## Type of change

- [ ] Bug fix
- [ ] New feature / DSP / effect
- [ ] Refactor / cleanup
- [ ] Docs / assets

## Checklist

- [ ] Builds cleanly (`dotnet build Drift.sln -c Release`)
- [ ] No allocations or LINQ added to per-sample or per-callback hot paths
- [ ] Patch serialization round-trips correctly if `SynthPatch` was changed
- [ ] `CHANGELOG.md` updated if user-visible behaviour changed
- [ ] README / docs updated if needed
