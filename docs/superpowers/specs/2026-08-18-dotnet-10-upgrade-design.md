# .NET 10 Upgrade Design

## Goal

Upgrade ColorzCore's modern build and release pipeline from .NET 6 to .NET 10, preserve the intentional .NET Framework 4.8 compatibility executable, and publish a verified `v2026.08.18` GitHub release.

## Success Criteria

- `ColorzCore.csproj` targets `net10.0`.
- Local and CI builds use a .NET 10 SDK.
- Release packages contain the modern library under `net10` and the compatibility executable under `net48`.
- A solution-wide Release build completes without the two projects overwriting each other's restore assets.
- All 128 Python behavioral tests pass against the Windows compatibility executable.
- The changes are committed and pushed to `master`.
- GitHub Actions builds all three platform archives for tag `v2026.08.18`.
- The GitHub release exists, points to the pushed commit, and exposes the Linux, macOS, and Windows archives.

## Chosen Approach

Retarget the existing modern project directly from `net6.0` to `net10.0` while retaining `ColorzCore.Framework.csproj` at `net48`.

This completes the requested runtime upgrade without either maintaining an unnecessary .NET 6 target or removing the legacy executable relied on by existing Windows users. The shared source remains at C# 9 because the explicit language-version limit is a compatibility constraint shared by both projects, not part of the runtime upgrade.

Rejected alternatives:

- Multi-targeting `net6.0;net10.0` would preserve an obsolete target, expand the release matrix, and weaken the meaning of the requested upgrade.
- Replacing both projects with a single `net10.0` executable would remove the deliberate .NET Framework compatibility surface and change the fork's library output.

## Repository Changes

1. Change `ColorzCore/ColorzCore.csproj` to target `net10.0`.
2. Add a root `global.json` selecting the .NET 10 feature band with safe roll-forward within .NET 10.
3. Add `ColorzCore/Directory.Build.props` so each sibling project uses a distinct intermediate-output directory. This removes the existing `obj/project.assets.json` collision and makes solution restore/build deterministic.
4. Update `.github/workflows/ci.yml` to install .NET 10, publish the modern project into `artifacts/package/net10`, and run the Python behavioral suite on Windows before packaging.
5. Add concise build and artifact-layout documentation to `README.md`.

## Build and Release Flow

The local validation path restores and builds `ColorzCore/ColorzCore.sln` in Release mode. The Windows `net48` executable is then passed to `Tests/run_tests.py`.

On GitHub, the existing operating-system matrix builds the `net10` library and `net48` executable, then creates one archive per runner. A pushed `v2026.08.18` tag triggers the existing release job, which downloads all three archives and attaches them to the corresponding GitHub release.

## Failure Handling

- Restore, build, test, packaging, missing-file, or upload failures remain hard CI failures.
- The tag is pushed only after local validation and the `master` commit push succeed.
- Release completion is accepted only after the tag workflow succeeds and the remote release has all three expected assets.
- If the workflow fails, fix the cause on `master`, move the unpublished tag only if no release was created, and rerun validation before publishing. Never report a release as complete with missing or failed artifacts.

## Validation

1. Confirm `dotnet --version` resolves to .NET 10 through `global.json`.
2. Run a clean solution restore and Release build.
3. Run all Python behavioral tests against the built `net48` executable.
4. Publish the modern project locally and verify the output path targets `net10.0`.
5. Review the final diff and repository status.
6. Push commits, push `v2026.08.18`, and wait for the tag-triggered workflow.
7. Verify the workflow conclusion, release target commit, release URL, and the three archive names.
