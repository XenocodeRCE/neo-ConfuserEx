# Neo-ConfuserEx

Neo-ConfuserEx is an open-source .NET assembly obfuscator descended from
[ConfuserEx](https://yck1509.github.io/ConfuserEx/). This fork modernizes the
toolchain and assembly-resolution path for current .NET applications.

## Modernized toolchain

- All maintained projects use SDK-style project files and build with the .NET 8 SDK.
- Assembly rewriting uses the supported
  [dnlib NuGet package](https://www.nuget.org/packages/dnlib) instead of a vendored,
  modified dnlib submodule.
- The CLI uses
  [System.CommandLine](https://www.nuget.org/packages/System.CommandLine).
- The WPF GUI uses the current
  [Ookii.Dialogs.Wpf](https://www.nuget.org/packages/Ookii.Dialogs.Wpf) package
  and an in-repository `ICommand` implementation; the old MvvmLight and binary
  DLL dependencies are gone.
- Build and test automation uses `dotnet`, PowerShell, and GitHub Actions. No
  checked-in 7-Zip executable or legacy NuGet restore targets are required.

The obfuscator itself runs on .NET 8. Input assemblies do not need to target
.NET 8: the resolver examines each module's core-library reference and
automatically discovers matching installed shared frameworks, reference packs,
and NuGet reference packs.

## Compatibility status

The automated corpus currently builds, protects, disassembles, and executes
four applications on both `net5.0` and `net8.0`:

- nested and variant generic interfaces/classes, generic methods, and managed
  function pointers;
- control flow and recursion;
- strings and numeric constants;
- exception handlers and custom exception types.

Both the minimal rename profile and the normal profile are tested. The
protected programs must preserve their behavior and print `RESULT:PASS`.
.NET 5 is end-of-support and is present here strictly as a requested
compatibility target; its protected outputs execute in Microsoft's archived
.NET 5 runtime container.

Compatibility is protection-specific. In particular, the legacy JIT
anti-tamper mode depends on .NET Framework JIT internals and is explicitly
rejected for modern .NET inputs. Use the normal anti-tamper mode for those
targets. Other Win32-oriented legacy protection modes retain their platform
constraints.

## Build

Build the cross-platform engine and CLI:

```bash
dotnet build Confuser2.mono.sln --configuration Release
```

Build the full solution, including the WPF GUI, on Windows:

```powershell
dotnet build Confuser2.sln --configuration Release
```

Create a CLI release archive:

```powershell
./Build/build.ps1
```

## Run the compatibility corpus

Docker is needed only to execute the .NET 5 output:

```bash
tests/ModernNet/run-modern-compat.sh
```

The test writes inspectable output to `artifacts/modern-compat/`:

```text
artifacts/modern-compat/
  GenericResolutionSample/
    rename-only/
      net8.0/
        GenericResolutionSample.protected.dll
        original.il
        protected.il
        original.sha256
        protected.sha256
        obfuscator.log
        runtime.log
```

The artifact directory is intentionally ignored by Git. CI uploads the complete
directory for each run, including original/protected ILSpy disassemblies so
renamed types and members can be reviewed directly.

## Usage

Protect a project file:

```bash
dotnet Confuser.CLI.dll --no-pause path/to/project.crproj
```

Or protect modules using a project file as a rule template:

```bash
dotnet Confuser.CLI.dll --no-pause --out protected \
  path/to/Application.dll path/to/template.crproj
```

Run `dotnet Confuser.CLI.dll --help` for all options. The project format is
documented in [docs/ProjectFormat.md](docs/ProjectFormat.md).

## Features

- Symbol renaming, including WPF/BAML analysis
- Debugger/profiler and memory-dump protections
- Anti-tamper modes
- Control-flow obfuscation
- Constant and resource encryption
- Reference proxies
- Type scrambling
- Dependency embedding and output compression
- Extensible plugin API

## License and credits

See [LICENSE](LICENSE). Neo-ConfuserEx builds on the original work by
[yck1509](https://github.com/yck1509) and the dnlib work by
[0xd4d](https://github.com/0xd4d).
