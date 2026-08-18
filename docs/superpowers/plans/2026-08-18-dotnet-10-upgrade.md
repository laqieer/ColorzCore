# .NET 10 Upgrade Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Upgrade ColorzCore's modern build and release pipeline to .NET 10 and publish a verified `v2026.08.18` release without removing .NET Framework 4.8 compatibility.

**Architecture:** Retarget the existing modern library directly to `net10.0` while retaining the separate `net48` executable over the same source tree. Isolate each project's intermediate outputs, make the Python test runner return a meaningful process status, and let the existing tag-driven GitHub Actions workflow build and attach the three release archives.

**Tech Stack:** .NET SDK 10, C# 9 shared source, .NET Framework 4.8, Python 3 `unittest`, GitHub Actions, GitHub CLI.

## Global Constraints

- `ColorzCore.csproj` must target exactly `net10.0`.
- `ColorzCore.Framework.csproj` must remain at `net48`.
- Both projects must retain `<LangVersion>9</LangVersion>`.
- Release archives must contain sibling `net10` and `net48` directories.
- The release tag must be exactly `v2026.08.18`.
- A failed build, test, package, or upload must stop the release.
- All commits must include `Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>`.

## File Map

- Create `global.json`: select a stable .NET 10 SDK feature band with .NET 10-only roll-forward.
- Create `ColorzCore/Directory.Build.props`: give each sibling project its own `obj` tree.
- Modify `ColorzCore/ColorzCore.csproj`: retarget the modern library.
- Create `Tests/test_test_runner.py`: lock in success/failure aggregation behavior.
- Modify `Tests/ea_test.py`: return whether every behavioral case passed.
- Modify `Tests/run_tests.py`: convert the aggregate result into process exit code 0 or 1.
- Modify `.github/workflows/ci.yml`: install .NET 10, package `net10`, and run tests on Windows.
- Modify `README.md`: document the SDK requirement, build command, and archive layout.

---

### Task 1: Retarget and Stabilize the Build

**Files:**
- Create: `global.json`
- Create: `ColorzCore/Directory.Build.props`
- Modify: `ColorzCore/ColorzCore.csproj:3`

**Interfaces:**
- Consumes: the existing two SDK-style projects in `ColorzCore/ColorzCore.sln`
- Produces: a `net10.0` modern library and independent `obj/ColorzCore/` and `obj/ColorzCore.Framework/` restore trees

- [ ] **Step 1: Reproduce the shared-assets failure**

Run:

```powershell
dotnet restore ColorzCore\ColorzCore.sln --force
dotnet build ColorzCore\ColorzCore.sln -c Release --no-restore
```

Expected: the second command fails with `NETSDK1005` because `ColorzCore\obj\project.assets.json` contains only one project's target.

- [ ] **Step 2: Select .NET 10 at the repository root**

Create `global.json`:

```json
{
  "sdk": {
    "version": "10.0.300",
    "rollForward": "latestFeature",
    "allowPrerelease": false
  }
}
```

- [ ] **Step 3: Isolate intermediate output by project**

Create `ColorzCore/Directory.Build.props`:

```xml
<Project>
  <PropertyGroup>
    <BaseIntermediateOutputPath>obj/$(MSBuildProjectName)/</BaseIntermediateOutputPath>
  </PropertyGroup>
</Project>
```

- [ ] **Step 4: Retarget the modern library**

Change `ColorzCore/ColorzCore.csproj`:

```xml
<TargetFramework>net10.0</TargetFramework>
```

Leave `OutputType`, nullable settings, C# 9, and output paths unchanged.

- [ ] **Step 5: Verify SDK selection and solution build**

Run:

```powershell
dotnet --version
dotnet restore ColorzCore\ColorzCore.sln --force
dotnet build ColorzCore\ColorzCore.sln -c Release --no-restore
```

Expected: `dotnet --version` reports `10.0.3xx`; both `net10.0` and `net48` projects build with zero errors.

- [ ] **Step 6: Commit the build upgrade**

```powershell
git add global.json ColorzCore\Directory.Build.props ColorzCore\ColorzCore.csproj
git commit -m "Upgrade ColorzCore to .NET 10" -m "Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

### Task 2: Make Test Failures Observable

**Files:**
- Create: `Tests/test_test_runner.py`
- Modify: `Tests/ea_test.py:57-73`
- Modify: `Tests/run_tests.py:19-36`

**Interfaces:**
- Produces: `ea_test.run_tests(config, test_cases) -> bool`
- Produces: `run_tests.main(args) -> int`, returning 0 only when every case passes

- [ ] **Step 1: Add failing unit tests for aggregate status**

Create `Tests/test_test_runner.py`:

```python
import sys
import unittest
from pathlib import Path
from unittest.mock import Mock, patch

sys.path.insert(0, str(Path(__file__).parent))

from ea_test import run_tests as run_test_cases
from run_tests import main


class RunTestsStatusTests(unittest.TestCase):
    def test_returns_true_when_every_test_passes(self):
        passing_test = Mock(name="passing_test")
        passing_test.name = "passing"
        passing_test.run_test.return_value = True

        self.assertIs(run_test_cases(Mock(), [passing_test]), True)

    def test_returns_false_when_any_test_fails(self):
        passing_test = Mock(name="passing_test")
        passing_test.name = "passing"
        passing_test.run_test.return_value = True
        failing_test = Mock(name="failing_test")
        failing_test.name = "failing"
        failing_test.run_test.return_value = False

        self.assertIs(run_test_cases(Mock(), [passing_test, failing_test]), False)

    @patch("run_tests.run_tests", return_value=True)
    def test_main_returns_zero_when_every_test_passes(self, run_tests_mock):
        self.assertEqual(main(["run_tests.py", "ColorzCore.exe"]), 0)

    @patch("run_tests.run_tests", return_value=False)
    def test_main_returns_one_when_any_test_fails(self, run_tests_mock):
        self.assertEqual(main(["run_tests.py", "ColorzCore.exe"]), 1)


if __name__ == "__main__":
    unittest.main()
```

- [ ] **Step 2: Verify the unit tests fail**

Run:

```powershell
python Tests\test_test_runner.py
```

Expected: all four assertions fail because `run_tests` and `main` currently return `None`.

- [ ] **Step 3: Return the aggregate behavioral result**

Replace `run_tests` in `Tests/ea_test.py` with:

```python
def run_tests(config: EATestConfig, test_cases: list[EATest]) -> bool:
    success_count = 0
    test_count = len(test_cases)

    for i, test_case in enumerate(test_cases):
        success = test_case.run_test(config)

        message = SUCCESS_MESSAGE if success else FAILURE_MESSAGE
        print(f"[{i + 1}/{test_count}] {test_case.name}: {message}")

        if success:
            success_count = success_count + 1

    passed = success_count == test_count
    if passed:
        print(f"{success_count}/{test_count} tests passed {SUCCESS_MESSAGE}")

    else:
        print(f"{success_count}/{test_count} tests passed {FAILURE_MESSAGE}")

    return passed
```

- [ ] **Step 4: Propagate the aggregate result to the process**

Replace `main` in `Tests/run_tests.py` with:

```python
def main(args) -> int:
    import argparse

    arg_parse = argparse.ArgumentParser()

    arg_parse.add_argument("command")
    arg_parse.add_argument("--extra-params")

    args = arg_parse.parse_args(args[1:])

    command: str = args.command
    extra_params: str | None = args.extra_params

    test_cases = ALL_TEST_CASES

    config = Config(command, extra_params)
    return 0 if run_tests(config, test_cases) else 1
```

Keep:

```python
if __name__ == '__main__':
    sys.exit(main(sys.argv))
```

- [ ] **Step 5: Verify unit and behavioral tests**

Run:

```powershell
python Tests\test_test_runner.py
python Tests\run_tests.py ColorzCore\bin\Framework\Release\net48\ColorzCore.exe
```

Expected: four unit tests pass; all 128 behavioral tests pass; both commands exit 0.

- [ ] **Step 6: Commit the test-runner fix**

```powershell
git add Tests\test_test_runner.py Tests\ea_test.py Tests\run_tests.py
git commit -m "Fail builds when ColorzCore tests fail" -m "Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

### Task 3: Upgrade CI and Release Packaging

**Files:**
- Modify: `.github/workflows/ci.yml:25-43`

**Interfaces:**
- Consumes: `run_tests.py` exit status from Task 2
- Produces: `ColorzCore-${runner.os}.zip` with `net10`, `net48`, and `README.md`

- [ ] **Step 1: Update the SDK and package directory**

Change the setup and publish lines:

```yaml
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: 10.0.x

      - name: Build release payload
        shell: pwsh
        run: |
          dotnet publish ColorzCore/ColorzCore.csproj -c Release -o artifacts/package/net10
          dotnet build ColorzCore/ColorzCore.Framework.csproj -c Release
          New-Item -ItemType Directory -Force artifacts/package/net48 | Out-Null
          Copy-Item ColorzCore/bin/Framework/Release/net48/* artifacts/package/net48 -Recurse -Force
          Copy-Item README.md artifacts/package/
```

- [ ] **Step 2: Gate packaging with tests on Windows**

Insert before `Package artifact`:

```yaml
      - name: Run tests
        if: runner.os == 'Windows'
        shell: pwsh
        run: |
          python Tests/test_test_runner.py
          python Tests/run_tests.py artifacts/package/net48/ColorzCore.exe
```

- [ ] **Step 3: Verify version references and workflow diff**

Run:

```powershell
rg -n "net6|6\.0\.x" .github ColorzCore README.md
git diff --check
```

Expected: no obsolete .NET 6 references and no whitespace errors.

- [ ] **Step 4: Commit the workflow update**

```powershell
git add .github\workflows\ci.yml
git commit -m "Build .NET 10 release artifacts" -m "Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

### Task 4: Document and Validate the Upgrade

**Files:**
- Modify: `README.md`

**Interfaces:**
- Documents: local build requirements and release archive layout

- [ ] **Step 1: Add build documentation**

Add after the release links:

````markdown
## Building

Building requires the .NET 10 SDK:

```powershell
dotnet build ColorzCore/ColorzCore.sln -c Release
```

Release archives contain the .NET 10 library in `net10` and the .NET Framework 4.8 compatibility executable in `net48`.
````

- [ ] **Step 2: Run the complete local validation loop**

Run:

```powershell
dotnet --version
dotnet restore ColorzCore\ColorzCore.sln --force
dotnet build ColorzCore\ColorzCore.sln -c Release --no-restore
dotnet publish ColorzCore\ColorzCore.csproj -c Release --no-restore
python Tests\test_test_runner.py
python Tests\run_tests.py ColorzCore\bin\Framework\Release\net48\ColorzCore.exe
git diff --check
```

Expected: .NET 10 is selected, both projects build, publish output is under `bin/Core/Release/net10.0/publish`, four unit tests and all 128 behavioral tests pass, and the diff check is clean.

- [ ] **Step 3: Commit documentation and plan**

```powershell
git add README.md docs\superpowers\plans\2026-08-18-dotnet-10-upgrade.md
git commit -m "Document .NET 10 build requirements" -m "Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

### Task 5: Review, Push, and Publish

**Files:**
- Review all files changed since `origin/master`

**Interfaces:**
- Produces: pushed `master`, tag `v2026.08.18`, successful tag workflow, and a GitHub release with three archives

- [ ] **Step 1: Review the final branch**

Run:

```powershell
git status --short --branch
git --no-pager diff origin/master...HEAD --check
git --no-pager diff origin/master...HEAD --stat
git --no-pager log origin/master..HEAD --oneline
```

Expected: only scoped upgrade files and commits are present.

- [ ] **Step 2: Push the validated commits**

Run:

```powershell
git push origin master
```

Expected: `origin/master` advances to the validated local HEAD.

- [ ] **Step 3: Create and push the release tag**

Run:

```powershell
git tag -a v2026.08.18 -m "ColorzCore v2026.08.18"
git push origin v2026.08.18
```

Expected: the tag push starts the `CI` workflow.

- [ ] **Step 4: Wait for the tag workflow**

Run:

```powershell
$runId = gh run list --repo laqieer/ColorzCore --workflow ci.yml --limit 20 --json databaseId,headBranch,event --jq '.[] | select(.headBranch == "v2026.08.18" and .event == "push") | .databaseId' | Select-Object -First 1
if (-not $runId) {
    throw "No CI run found for v2026.08.18"
}
gh run watch $runId --repo laqieer/ColorzCore --exit-status
```

Expected: all Linux, Windows, macOS, and release jobs conclude `success`.

- [ ] **Step 5: Add release notes**

Run:

```powershell
gh release edit v2026.08.18 --repo laqieer/ColorzCore --title "v2026.08.18" --notes "Upgrade the modern ColorzCore library and release pipeline to .NET 10 while retaining the .NET Framework 4.8 compatibility executable. Release builds now run the complete behavioral test suite before packaging."
```

- [ ] **Step 6: Verify the remote release**

Run:

```powershell
gh release view v2026.08.18 --repo laqieer/ColorzCore --json tagName,targetCommitish,isDraft,isPrerelease,publishedAt,assets,url
git ls-remote origin refs/heads/master refs/tags/v2026.08.18
```

Expected: the release is public and non-prerelease, the tag resolves to the pushed commit, and assets are exactly `ColorzCore-Linux.zip`, `ColorzCore-macOS.zip`, and `ColorzCore-Windows.zip`.

- [ ] **Step 7: Inspect downloaded archive layouts**

Run:

```powershell
$verifyRoot = Join-Path $env:TEMP ("ColorzCore-v2026.08.18-" + [guid]::NewGuid())
New-Item -ItemType Directory -Path $verifyRoot | Out-Null
gh release download v2026.08.18 --repo laqieer/ColorzCore --dir $verifyRoot

Get-ChildItem $verifyRoot -Filter *.zip | ForEach-Object {
    $extractPath = Join-Path $verifyRoot $_.BaseName
    Expand-Archive -LiteralPath $_.FullName -DestinationPath $extractPath

    foreach ($requiredPath in @("README.md", "net10", "net48")) {
        if (-not (Test-Path (Join-Path $extractPath $requiredPath))) {
            throw "$($_.Name) is missing $requiredPath"
        }
    }

    if (Test-Path (Join-Path $extractPath "net6")) {
        throw "$($_.Name) still contains net6"
    }
}

Remove-Item -LiteralPath $verifyRoot -Recurse -Force
```

Expected: all three archives have the documented layout and no `net6` directory.
