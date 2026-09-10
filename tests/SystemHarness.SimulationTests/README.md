# SystemHarness.SimulationTests

These scenarios drive the harness against **real applications on a real desktop** — they start
Notepad, move and resize its window, drag with the mouse, type into it, and read the result back.
They are the only place where the Windows-facing surface is exercised end to end rather than
against a double.

## They do not run in CI, on purpose

CI runs `SystemHarness.Tests.exe -trait "Category=CI"` and never invokes this project. Every
scenario here carries `[Trait("Category", "Integration")]`, so it is excluded by the ecosystem's
standard filter as well.

That exclusion used to be implicit — the workflow simply did not mention this project, and nothing
said why. This file is the statement that was missing: **the exclusion is deliberate, not an
oversight**, and re-adding these to CI without an interactive session would make the job red on
every run.

## What they require

An **interactive desktop session on the same window station as the test process**. A process
started from a non-interactive context launches and responds, but gets no enumerable top-level
window — and every scenario here begins by finding that window.

The failure signature when the requirement is not met is unmistakable and says nothing about the
harness:

```
Assert.NotEmpty() Failure: Collection was empty      # the window was never found
Assert.Contains() Failure: Sub-string not found      # nothing was typed, so nothing read back
```

If you see those, check the session before reading the code. A one-line probe settles it:

```powershell
$p = Start-Process notepad -PassThru; Start-Sleep 2; $p.Refresh(); $p.MainWindowHandle; $p.Kill()
```

`0` means this session cannot see windows it creates, and these tests cannot pass here regardless
of the state of the code.

## Running them

From an interactive desktop session (a normal terminal on a logged-in machine):

```bash
dotnet test --project tests/SystemHarness.SimulationTests --configuration Release
```

Expect them to take minutes, not seconds — each scenario waits on a real application.
