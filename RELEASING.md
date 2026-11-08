# Releasing Mapee

## Release a new version

1. Merge everything that should ship into `master`.
2. On GitHub, open **Actions → Release → Run workflow**, enter the version (for example `1.4.0`), and click **Run workflow**.

That's the whole process. The run takes about 10 minutes and has three jobs:

| Job | What it does |
| --- | --- |
| **build** | Checks the version: valid format, newer than the latest release, not already tagged, run from `master`. Then builds the installer with that version baked in and writes the update files. |
| **github-release** | Creates the tag and the release "Mapee 1.4.0" with the `.msi` and `.zip`. The release notes are generated from merged pull requests; edit them on the release page afterwards if you like. |
| **go-live** | Uploads the two update files to the Google Cloud Storage bucket. From this moment, installed copies of Mapee offer the update on their next start, and mapee.net shows the new download. |

Users don't see anything until **go-live** succeeds. If a job fails, fix the cause and click **Re-run failed jobs**: **github-release** and **go-live** are safe to repeat. Don't use "Re-run all jobs", because **build** refuses to run once the tag exists.

If a release turns out to be broken, release a fixed version (for example `1.4.1`). Users who haven't updated yet go straight to it.

To **rebuild a release under the same version** instead, delete it first (the workflow refuses an existing tag), then run Release again with the same number:

```bash
gh release delete 1.4.0 --repo Voxelview/Mapee --cleanup-tag --yes
```

Users already on that version aren't offered the rebuilt one (the updater only offers newer versions). Installing the rebuilt installer over the old one replaces it, thanks to `AllowSameVersionUpgrades` in `Package.wxs`.

## Publishing data

Block colors, biomes, styles and the other files in `Resources/` can reach users **without an app release**, as a *data pack*.

1. Merge the data change into `master`. The **Build** workflow loads `Resources/` with the app on every pull request, so broken JSON fails the PR.
2. Open **Actions → Publish data → Run workflow** and keep `channel: live`.

When Mapee starts, it checks for a newer pack. If there is one, it downloads it (a small progress window shows) and uses it straight away, with no restart. The title bar shows the data in use, for example `v1.5.0 · data r57`.

**Version numbers.** Publishing data doesn't change the app version. Each pack gets a *data revision* (`r57`): the number of commits that changed `Resources/` since revisions were reset (counted by `scripts/get-data-revision.ps1`, starting at r1). An app release bundles the data of its commit with the same numbering, so a pack is only downloaded if it's newer than what the app already has.

**Minecraft version.** `Resources/manifest.json` also says which Minecraft version the data covers (`"minecraftVersion": "26.3"`). Update it by hand when you add data for a new Minecraft version; packs, `latest.json` and the download log carry it, and the data check fails without it.

**Trying a pack first.** Run Publish data with `channel: test`. Only apps started with the environment variable `MAPEE_DATA_CHANNEL=test` download it. That's also how to test with a local build, which otherwise never downloads data:

```powershell
$env:MAPEE_DATA_CHANNEL = 'test'; .\Build\Debug\Mapee.exe
```

Ticking `validate only` builds and checks the pack without publishing anything, from any branch.

### What can ship as data, and what needs an app release

Each pack has a **data format** number (`Resources/manifest.json`), and each app build reads exactly one format (`DataFormat.Supported` in `Mapper.Gui/Shared/DataPacks/DataFormat.cs`). Apps only download packs of their format, from `data/format-<N>/` in the bucket.

**Safe as data** (same format):
- Block colors, grouping, step, elevation and depth opacity, including entries for **new block ids**
- Property variants using the existing expression operators
- Biome colors and new biomes
- Block and biome renames, including future Minecraft data versions
- Style settings, and whole new styles (style folders are discovered automatically)
- Colormap PNGs (keep them at 96 DPI)

**Needs an app release and a format bump:**
- New JSON keys or value types, new enum values (`BlockType`, `StepType`, `ScanType`, …), new operators or asset categories
- Changing a file's root type, a schema, or the folder layout
- Anything that also needs code, such as a new chunk or region format

To bump the format, change `"format"` in `Resources/manifest.json` and `DataFormat.Supported` together, in the same pull request as the code (`--check-data` fails if they differ). Then release the app. Data published afterwards goes to `data/format-<N+1>/`; older apps never read it and keep their last pack until they update.

### If something goes wrong
- **On the user's PC:** packs live in `%LocalAppData%\Mapee\Data`. If a downloaded pack fails to load, Mapee uses its bundled data instead and never uses that revision again. Startup never fails because of a pack. Details are in `%LocalAppData%\Mapee\Logs\data-packs.log`.
- **A bad pack is live:** fix the data and publish again; the new revision replaces it.
- **Check data locally:** `./scripts/check-data.ps1` loads `Resources/` with the app. `./scripts/build-data-pack.ps1 -Revision 1` builds a pack into `artifacts/data/` without publishing it.

## Test builds

The **Build** workflow runs on every push to `master` and every pull request. It builds the app and the installer, so a broken build fails on the pull request instead of during a release.

Each run has one downloadable artifact, `Mapee-portable-0.0.N`: the app folder. Unzip it anywhere and run `Mapee.exe`; no install needed. Test builds are versioned `0.0.N` and never check for updates.

The MSI isn't uploaded. It's numbered `0.0.N`, so Windows treats it as a downgrade and won't install it over a released Mapee. To test installer changes, build locally with a real version (see below).

## Build locally

```powershell
./scripts/build-installer.ps1 -Version 1.4.0
./scripts/write-release-manifests.ps1 -Version 1.4.0   # optional: the update files
```

The output goes to `artifacts/`. You need the .NET 10 SDK; WiX is downloaded from NuGet automatically.

## How it fits together

- **Version.** The version you type becomes the git tag and is passed to the build as `-p:Version`, which sets the exe's version and the MSI's version. `Mapper.Gui.csproj` stays at `0.0.0`, so local builds never check for updates. Don't add `AssemblyVersion` or `FileVersion` to the csproj: they would override the injected version, and the updater reads `AssemblyVersion`.
- **The app's publish settings** are self-contained (users don't need .NET), single-file, ReadyToRun, win-x64. They live in `scripts/build-installer.ps1`.
- **The installer** is `Mapee.Installer/` (WiX v5). It installs everything in the publish folder, so new resource files need no installer changes.
  - The `UpgradeCode` in `Package.wxs` must never change; it's how Windows finds the old install to replace.
  - The package is per-user but asks for elevation. This matches the old Visual Studio installer, so upgrading from 1.3.0 and earlier replaces the old install instead of adding a second one. Don't change `Scope`.
  - WiX is pinned to v5 because it's MS-RL licensed. WiX v6 and later ship under the OSMF EULA.
- **Update files** in the bucket `mapee_update_bucket_goldentadpole`:
  - `self_win64/MapeeVersionInfo-self-win64.xml` is read by the app's updater (AutoUpdater.NET). It points at the MSI and includes its SHA256, which the updater verifies.
  - `website/installer-info.xml` is read by the download page on mapee.net.
  - `data/format-<N>/latest.json` and `mapee-data-r<revision>.zip` are the current data pack (see "Publishing data"). `data-test/` holds the test channel.
- **Where data is loaded from.** `Mapper.Gui/Shared/DataPacks/DataPacks.cs` picks the bundled `Resources` folder next to `Mapee.exe` or a newer downloaded pack, and loads it through `ProgramDomain`. Paths no longer depend on the working directory.

## One-time setup

### 1. Let the Release workflow write to the bucket

The workflow signs in to Google Cloud with Workload Identity Federation: GitHub proves which repository and branch is running, so no key is stored anywhere. Run this in Google Cloud Shell, or anywhere `gcloud` is signed in as a project owner:

```bash
PROJECT_ID=your-project-id   # the project that owns the bucket
BUCKET=mapee_update_bucket_goldentadpole
REPO=Voxelview/Mapee
PROJECT_NUMBER=$(gcloud projects describe "$PROJECT_ID" --format='value(projectNumber)')

gcloud services enable iam.googleapis.com iamcredentials.googleapis.com sts.googleapis.com --project="$PROJECT_ID"

# The account the workflow acts as. It can only manage files in this bucket.
gcloud iam service-accounts create mapee-release --project="$PROJECT_ID" --display-name="Mapee release uploads"
gcloud storage buckets add-iam-policy-binding "gs://$BUCKET" \
  --member="serviceAccount:mapee-release@$PROJECT_ID.iam.gserviceaccount.com" \
  --role=roles/storage.objectAdmin

# Trust GitHub Actions runs of Voxelview/Mapee on master, and nothing else.
gcloud iam workload-identity-pools create github --project="$PROJECT_ID" --location=global --display-name="GitHub Actions"
gcloud iam workload-identity-pools providers create-oidc mapee --project="$PROJECT_ID" --location=global \
  --workload-identity-pool=github --display-name="Voxelview/Mapee" \
  --issuer-uri="https://token.actions.githubusercontent.com" \
  --attribute-mapping="google.subject=assertion.sub,attribute.repository=assertion.repository,attribute.ref=assertion.ref" \
  --attribute-condition="assertion.repository == '$REPO' && assertion.ref == 'refs/heads/master'"
gcloud iam service-accounts add-iam-policy-binding "mapee-release@$PROJECT_ID.iam.gserviceaccount.com" \
  --project="$PROJECT_ID" --role=roles/iam.workloadIdentityUser \
  --member="principalSet://iam.googleapis.com/projects/$PROJECT_NUMBER/locations/global/workloadIdentityPools/github/attribute.repository/$REPO"

# The two values the workflow needs:
echo "GCP_WORKLOAD_IDENTITY_PROVIDER = projects/$PROJECT_NUMBER/locations/global/workloadIdentityPools/github/providers/mapee"
echo "GCP_SERVICE_ACCOUNT            = mapee-release@$PROJECT_ID.iam.gserviceaccount.com"
```

Add the two printed values as repository **variables** (not secrets; they aren't sensitive) under **Settings → Secrets and variables → Actions → Variables**.

### 2. Check how the bucket makes files public

```bash
gcloud storage buckets describe gs://mapee_update_bucket_goldentadpole --format="value(uniform_bucket_level_access)"
```

- `True`: nothing to do. New files are public the same way the current one is.
- `False` (fine-grained ACLs): also add a repository variable `GCS_PREDEFINED_ACL` = `publicRead`. Without it, the files the workflow uploads would not be public, and the app and website couldn't read them.

### 3. Let mapee.net read the release info (CORS)

```bash
cat > cors.json <<'EOF'
[{"origin": ["https://mapee.net", "https://www.mapee.net", "http://localhost:4200"], "method": ["GET"], "responseHeader": ["Content-Type"], "maxAgeSeconds": 3600}]
EOF
gcloud storage buckets update gs://mapee_update_bucket_goldentadpole --cors-file=cors.json
```

### 4. Before the first WiX release: test the upgrade once

1.4.0 is the first release built with the new installer. Before releasing it, check that it cleanly replaces 1.3.0:

1. Open Windows Sandbox. To enable it, run `Enable-WindowsOptionalFeature -Online -FeatureName Containers-DisposableClientVM` as admin and restart.
2. In the sandbox, install 1.3.0 from its GitHub release.
3. On your PC, run `./scripts/build-installer.ps1 -Version 1.4.0`, copy `artifacts/MapeeInstaller.1.4.0-self-win64.msi` into the sandbox, and run it.
4. Check that **Settings → Apps** lists a single Mapee 1.4.0, that the Desktop and Start menu shortcuts start it, and that `C:\Program Files\GoldenTadpole\Mapee` holds the new files.
