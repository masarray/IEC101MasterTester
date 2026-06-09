# Build from Source

## Requirements

- Windows 10 or Windows 11.
- Visual Studio 2022 Community, Professional, Enterprise, or Build Tools.
- .NET desktop development workload.
- .NET Framework 4.8 Developer Pack.

## Package dependencies

The main application is self-contained at source level and uses .NET Framework/WPF assemblies only. There is no project package restore step for the main application.

The simulator project is SDK-style; the build command includes `/restore` so MSBuild can generate its normal build metadata. It does not introduce external application packages.

## Build the main application

```powershell
msbuild IEC101MasterTester.csproj /t:Rebuild /p:Configuration=Debug /p:UseSharedCompilation=false /m
msbuild IEC101MasterTester.csproj /t:Rebuild /p:Configuration=Release /p:UseSharedCompilation=false /m
```

## Build the slave simulator

```powershell
msbuild IecSlaveSimulator\IecSlaveSimulator.csproj /restore /t:Rebuild /p:Configuration=Debug /p:UseSharedCompilation=false /m
msbuild IecSlaveSimulator\IecSlaveSimulator.csproj /restore /t:Rebuild /p:Configuration=Release /p:UseSharedCompilation=false /m
```

## Output folders

Typical outputs:

```text
bin\Release\
IecSlaveSimulator\bin\Release\net48\
```

## Create a local portable folder

```powershell
$package = 'artifacts\IEC101MasterTester-windows-portable'
$sim = Join-Path $package 'tools\IecSlaveSimulator'
New-Item -ItemType Directory -Force -Path $package,$sim | Out-Null
Copy-Item 'bin\Release\*' $package -Recurse -Force
Copy-Item 'IecSlaveSimulator\bin\Release\net48\*' $sim -Recurse -Force
Remove-Item (Join-Path $package '*.pdb') -Force -ErrorAction SilentlyContinue
Remove-Item (Join-Path $sim '*.pdb') -Force -ErrorAction SilentlyContinue
Copy-Item 'README.md','LICENSE','NOTICE','THIRD_PARTY_NOTICES.md' $package -Force
```

## Release workflow

The repository includes a GitHub Actions workflow that builds a portable package when a version tag is pushed:

```powershell
git tag v0.1.0
git push origin v0.1.0
```

The workflow uploads a ZIP package and SHA256 checksum.
