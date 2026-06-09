# Repository Setup

This repository is designed to use a simple public maintenance model:

- one long-lived branch: `main`;
- GitHub Pages deployed by GitHub Actions from `docs/`;
- Windows portable release packages created from version tags;
- no separate Pages branch required.

## Recommended GitHub settings

### Default branch

Set the default branch to `main`:

1. Open **Settings**.
2. Open **Branches**.
3. Confirm that **Default branch** is `main`.


### GitHub About and topics

Use these fields in the repository **About** panel so GitHub search, topic pages, and external previews describe the project consistently.

**Description**

```text
IEC 60870-5-101 Windows master tester for SCADA FAT/SAT, RTU gateway testing, NUC redundancy, commands, SOE audit, and protocol traces.
```

**Website**

```text
https://masarray.github.io/IEC101MasterTester/
```

**Topics**

```text
iec60870-5-101
iec101
iec-101
scada
substation-automation
rtu
gateway
protocol-analyzer
master-tester
serial-communication
fat-testing
sat-testing
commissioning
telecontrol
soe
wpf
dotnet
windows
apache-2-0
```

### GitHub Pages

Use GitHub Actions as the Pages source:

1. Open **Settings**.
2. Open **Pages**.
3. Set **Source** to **GitHub Actions**.
4. Push to `main` with changes under `docs/`.
5. The `Deploy GitHub Pages` workflow publishes the website.

### Actions permissions

For release automation, confirm:

1. Open **Settings**.
2. Open **Actions** > **General**.
3. Allow actions to run.
4. Under **Workflow permissions**, allow GitHub Actions to create and approve releases when required by your organization policy.

## First push from a local folder

```powershell
git init
git branch -M main
git add .
git commit -m "Prepare public release"
git remote add origin https://github.com/masarray/IEC101MasterTester.git
git push -u origin main
```

## Keep only `main` in the remote repository

List remote branches:

```powershell
git ls-remote --heads origin
```

Delete old remote branches that are no longer needed:

```powershell
git push origin --delete old-branch-name
```

Do not delete `main`.

## Create a portable Windows release

Create and push a semantic version tag:

```powershell
git tag v0.1.0
git push origin v0.1.0
```

The `Release Windows Portable` workflow builds the main application and simulator, creates a portable ZIP, generates a SHA256 checksum, and attaches both files to the GitHub Release.

## Update the website

Edit files under `docs/`, then push to `main`:

```powershell
git add docs .github/workflows/deploy-pages.yml
git commit -m "Update documentation website"
git push origin main
```

The website is deployed without creating any additional branch.

## Pre-release validation

Before publishing a public tag, follow the [Release Checklist](RELEASE_CHECKLIST.md).
