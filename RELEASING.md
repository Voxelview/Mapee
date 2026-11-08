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
