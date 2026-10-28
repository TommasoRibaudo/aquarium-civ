# 05 · GitHub as the hub: CI/CD for Unity 6.3 LTS mobile (iOS + Android)

> Researched 2026-10-28 against live sources. Context: solo dev, Windows 11, **no Mac**, Unity 6.3 LTS + URP, Unity Personal license, Claude Code driving the repo through `gh`.
> **Short answer: yes, GitHub can be the hub for building *and* deploying both platforms, with no Mac.** Android builds run on Linux runners. iOS builds export the Xcode project on Linux, then archive, sign and upload to TestFlight on a GitHub-hosted macOS runner.

---

## 1. Building Unity with GameCI on GitHub Actions

### Current versions (checked via `gh api`, 2026-10-28)
| Action | Latest | Published | Notes |
|---|---|---|---|
| [`game-ci/unity-builder`](https://github.com/game-ci/unity-builder/releases) | **v6.0.0** | 2026-09-10 | A thin wrapper around `game-ci/cli`. The release notes say "Inputs, secrets, and project layout are unchanged from `@v5`", so moving up is a drop-in change. v5.0.0 (May 2026) renamed Cloud Runner to "Orchestrator" and moved to Node 24. v5.0.1 and v4.8.2 (2026-09-01) fix shared-memory failures on Unity 6.6+ through the new `dockerShmSize` input, which defaults to `1025m` ([release v6.0.0](https://github.com/game-ci/unity-builder/releases/tag/v6.0.0)). |
| [`game-ci/unity-test-runner`](https://github.com/game-ci/unity-test-runner/releases) | **v4.4.0** (newest). v4.3.2 is flagged "Latest". | 2026-09-09 | Moves to Node 24, also becomes a CLI wrapper, and restores the `UNITY_LICENSE`/`UNITY_LICENSE_FILE` activation methods ([v4.4.0](https://github.com/game-ci/unity-test-runner/releases/tag/v4.4.0)). v5.0.0 is still in beta. |

- **Docs lag behind the code.** game.ci is still labelled "v4 (current)", and most examples use `@v4`, with only a few using `@v6` ([builder docs](https://game.ci/docs/github/builder)). **Recommendation:** pin `unity-builder@v6` and `unity-test-runner@v4`. A pin to `@v4` still works if a v6 regression shows up.
- **Unity 6 / 6.3 support:** confirmed. Docker Hub already carries `unityci/editor:ubuntu-6000.3.27f1-android-3.2.2` and `ubuntu-6000.3.27f1-ios-3.2.2` images, pushed 2026-10-21 ([Docker Hub tags API](https://hub.docker.com/v2/repositories/unityci/editor/tags?page_size=50&name=6000.3)). The editor version is auto-detected from `ProjectSettings/ProjectVersion.txt`. Any `unityVersion` override must be the full string, e.g. `6000.3.27f1` ([builder docs](https://game.ci/docs/github/builder)). **Pin the exact 6.3 patch you use locally** and check that a GameCI image exists for it before you upgrade the editor.
- Also new: Unity's own **Unity CLI** (`unity install/test/build/license`, available through `winget install Unity.CLI` / brew). It was announced in September 2026 and is aimed at CI. Its licensing docs only cover floating/offline flows and don't mention Personal ([Unity blog](https://unity.com/blog/cicd-made-easier-with-unity-cli)). Worth watching, but not a GameCI replacement for a Personal license today.

### Activating a Unity **Personal** license in CI (2026)
- **Personal licenses have no serial number.** `UNITY_SERIAL` is only for Pro/Enterprise, and Unity's docs say command-line activation "only works for Unity Plus and Unity Pro licenses, because Personal licenses don't have serial numbers" ([Unity manual](https://docs.unity3d.com/2021.1/Documentation/Manual/OnlineActivationGuide.html)).
- **What changed:** the old manual flow (`.alf` upload to license.unity3d.com, then download a `.ulf`) is no longer supported for Personal ([dev.to write-up](https://dev.to/ankursheel/workaround-for-unity-personal-license-manual-activation-not-supported-49j2)). The current GameCI method ([activation docs](https://game.ci/docs/github/activation)) works like this:
  1. In Unity Hub, go to Preferences > Licenses > **Add** > "Get a free personal license". GameCI warns that a license can appear in Hub without a `.ulf` file being written, so go through Add explicitly.
  2. Copy the file from **`C:\ProgramData\Unity\Unity_lic.ulf`** on Windows. The docs say licenses aren't tied to the OS, so a file from Windows works on Linux runners.
  3. Add three repo secrets: `UNITY_LICENSE` (the full `.ulf` contents), `UNITY_EMAIL` and `UNITY_PASSWORD`. Use an account **without** a 2FA prompt for login, or a dedicated CI Unity ID.
  4. If the `.ulf` stops working (for example after re-activation in Hub), regenerate it and update the secret.

### Android (APK/AAB + keystore)
- `targetPlatform: Android`, plus `androidExportType: androidAppBundle` for Play uploads (default is `androidPackage` = APK). Signing uses the inputs `androidKeystoreName`, `androidKeystoreBase64`, `androidKeystorePass`, `androidKeyaliasName` and `androidKeyaliasPass`, plus an optional `androidTargetSdkVersion` ([builder docs](https://game.ci/docs/github/builder)).
- Create the keystore in Unity (Player Settings > Keystore Manager) and **keep the `.keystore` out of git**. Base64 it on Windows with `[convert]::ToBase64String([IO.File]::ReadAllBytes("user.keystore"))` ([GameCI Android](https://game.ci/docs/github/deployment/android)).
- Back up the keystore and passwords offline (password manager). With Play App Signing this is the **upload** key, and Google can reset it if it's lost, but avoid that hassle.
- `versioning: Semantic` derives the version from git tags. Use `androidVersionCode` (e.g. `${{ github.run_number }}`) so every upload has a unique versionCode.

### iOS without a Mac
Flow from [GameCI iOS docs](https://game.ci/docs/github/deployment/ios):
1. **Job 1 (`ubuntu-latest`)**: `unity-builder` with `targetPlatform: iOS` exports an **Xcode project**, which is uploaded as an artifact (`build/iOS`).
2. **Job 2 (`macos-*`)**: downloads the artifact, then runs fastlane. **`match`** fetches the certificates and profiles from a private git repo, `gym`/`build_app` archives, and `upload_to_testflight` (= `pilot`) uploads.
3. Secrets used: `APPSTORE_KEY_ID`, `APPSTORE_ISSUER_ID`, `APPSTORE_P8`, `MATCH_REPOSITORY`, `MATCH_PASSWORD`, `MATCH_DEPLOY_KEY`, `GH_PAT`, `APPLE_TEAM_ID`, `IOS_BUNDLE_ID` and `APPLE_DEVELOPER_EMAIL`/`APPLE_CONNECT_EMAIL`.
4. **No Mac is needed** if you generate the certificates through the provided GitHub Actions workflow. A local Mac is only needed for manual `match` management or cleaning an old portal ([GameCI iOS](https://game.ci/docs/github/deployment/ios)).
- **Important:** the GameCI example pins **Xcode 16.4**. Since **2026-04-28**, Apple rejects uploads not built with **Xcode 26 / iOS 26 SDK** ([Apple upcoming requirements](https://developer.apple.com/news/upcoming-requirements/)). Use `runs-on: macos-26` (= `macos-latest`, arm64), and select Xcode 26.x, not 27-preview ([runner-images](https://github.com/actions/runner-images)). macOS 14 images are deprecated. Check that your Unity 6.3 patch officially supports Xcode 26.
- Use an App Store Connect **Team API key** (role App Manager or Admin) so that `match` and `pilot` don't need 2FA. Individual keys can't use the provisioning endpoints ([fastlane ASC API](https://docs.fastlane.tools/app-store-connect-api/)).
- Make the iOS release workflow **manual (`workflow_dispatch`) or tag-triggered**. Apple rate-limits frequent uploads, and macOS minutes are expensive.

### Caching
- **Library folder:** `actions/cache@v4` on `Library`, with key `Library-<platform>-${{ hashFiles('Assets/**','Packages/**','ProjectSettings/**') }}` and restore-keys `Library-<platform>-`. Keep **one cache per target platform** ([builder docs](https://game.ci/docs/github/builder)). Older GameCI docs claim this speeds builds up by ">50%".
- **Limits:** the Actions cache is **10 GB per repo** on Free/Pro, and old entries are evicted ([billing](https://docs.github.com/en/billing/concepts/product-billing/github-actions)). A mobile Library is often 2–6 GB, so two platforms fit, but just barely with big asset packs.
- **Cache Git LFS objects too.** Restore `.git/lfs` keyed on a hash of `git lfs ls-files -l` before `git lfs pull`. Otherwise **every CI checkout re-downloads all LFS files and burns LFS bandwidth** (see §4).
- **Disk space:** Unity docker images are large. If you hit "no space left", free disk space on `ubuntu-latest` (a common pattern is the `jlumbroso/free-disk-space` action).

### Build times and Actions cost
- **Build times** are estimates because no public Unity 6 benchmark exists. Unity 6 clean IL2CPP Android builds run 25–50% slower than 2022.3 ([Unity issue tracker](https://issuetracker.unity.com/issues/9134/building-the-player-takes-25-50-more-time-in-unity-6-compared-to-20223-when-the-il2cpp-scripting-backend-is-selected)). On a 2-core `ubuntu-latest`, expect roughly **25–45 min cold** (first import plus IL2CPP) and **10–20 min warm** for an Android AAB. The iOS export is similar on Linux, plus **10–25 min** of Xcode archive and upload on macOS. Measure with a cold and a warm run.
- **2026 pricing** ([GitHub Actions billing](https://docs.github.com/en/billing/concepts/product-billing/github-actions)): included usage is now a **dollar amount**: **Free $12/month**, **Pro $18/month**. Pricing changed on 2026-01-01, when hosted runners got up to 39% cheaper ([changelog](https://github.blog/changelog/2025-12-16-coming-soon-simpler-pricing-and-a-better-experience-for-github-actions/)). Per-minute rates:
  - Linux 2-core: **$0.006/min**. $12 is about **2,000 Linux min**, and the [pricing page](https://github.com/pricing) still says "2,000 CI/CD minutes/month".
  - Windows: **$0.010/min**.
  - macOS: **$0.062/min**, about **10x Linux**. $12 is only about **190 macOS min**.
  - **Public repos:** standard hosted runners are **free and unlimited**.
  - **Private repos:** usage draws from the included amount. With no payment method on file, jobs **block** when it runs out.
  - **Self-hosted runners:** **free**. The announced $0.002/min self-hosted platform fee was **postponed** in Dec 2025, and no new date had been announced as of Oct 2026 ([changelog](https://github.blog/changelog/2025-12-16-coming-soon-simpler-pricing-and-a-better-experience-for-github-actions/), [community discussion](https://github.com/orgs/community/discussions/182186)).
  - **Artifact storage:** 500 MB on Free and 1 GB on Pro, with overage at $0.25/GB-month. Set `retention-days: 1` on the build artifacts, because an iOS Xcode project artifact can be 1 GB or more.
- **Rough per-build cost (private repo):**
  - Android warm: about 15 min × $0.006 = **~$0.09**.
  - iOS: Linux export ~$0.09, plus macOS ~20 min × $0.062 = **~$1.25–1.50**.
  - **Example month:** 40 Android builds plus 8 iOS releases is about **$15**, so slightly over Free ($12), or within budget on Pro ($18) plus a small overage. Set a spending budget in billing.

---

## 2. Deploying from CI

### Google Play (internal testing)
- **Option A: fastlane `supply` / `upload_to_play_store`** (GameCI's documented path). Use a lane `android internal` with `track: 'internal'`, `release_status: 'completed'` (or `'draft'`). Secrets: `GOOGLE_PLAY_KEY_FILE` (the service-account JSON as plain text) and `ANDROID_PACKAGE_NAME` ([GameCI Android](https://game.ci/docs/github/deployment/android)).
- **Option B: [`r0adkll/upload-google-play@v1`](https://github.com/r0adkll/upload-google-play)**. It needs no Ruby or fastlane. Inputs are `serviceAccountJsonPlainText`, `packageName`, `releaseFiles: build/Android/*.aab`, `tracks: internal` (`track` is deprecated) and `status`. **Simpler: recommended for Android.**
- **Prerequisites for both options:**
  - **Upload the first AAB manually** in Play Console (internal testing). The API returns "Package not found" until the app exists.
  - Until the app has been reviewed once and is no longer a draft, only `status: draft` releases work.
  - Create a Google Cloud service account, enable the Play Developer API, and invite the service account in Play Console > Users & permissions with release permissions.
- **Personal accounts created after 2023-11-13** must run a **closed test with 12+ testers opted in for 14 continuous days** before production unlocks. Internal testing is not affected. Organization accounts (D-U-N-S number) are exempt ([overview](https://www.choicely.com/blog/google-play-12-tester-rule)). Plan the testers early.

### TestFlight
- fastlane: call `app_store_connect_api_key(key_id:, issuer_id:, key_content:)` with the `.p8` from a secret, then `match(type: "appstore", readonly: true)`, `build_app`, and `upload_to_testflight` (alias `pilot`), passing `skip_waiting_for_build_processing: true` to save macOS minutes. API keys avoid 2FA ([fastlane docs](https://docs.fastlane.tools/app-store-connect-api/)).
- The app record must exist in App Store Connect first. `produce` doesn't support API keys, so create the record once in the web UI.
- Internal TestFlight testers (up to 100 team members) need no review. External testers require a Beta App Review.

### Accounts and fees
| Account | Cost | Source |
|---|---|---|
| Google Play Console | **US$25 one-time** (needs ID verification; no prepaid cards) | [Play Console Help](https://support.google.com/googleplay/android-developer/answer/6112435?hl=en) |
| Apple Developer Program | **US$99 per year** (local pricing varies) | [Apple enroll](https://developer.apple.com/programs/enroll/) |
| Unity Personal | Free (revenue cap applies) | [GameCI activation](https://game.ci/docs/github/activation) |
| GitHub Free / Pro | $0 / ~$4 per month | [GitHub pricing](https://github.com/pricing) |

---

## 3. Alternatives

| Option | 2026 pricing | Fit for this project |
|---|---|---|
| **Unity Build Automation (Unity DevOps)** | Free every month: **200 Windows (Micro) min, 100 Mac (Standard) min, 100 Linux min**, 2 concurrent builds, 25 GB storage, 100 GB egress. Overage: Windows Micro **$0.02/min**, Mac Standard **$0.07/min**, extra concurrency $10, storage $0.14/GB-month, egress $0.10/GB ([Unity support](https://support.unity.com/hc/en-us/articles/34748492914964-Understanding-Unity-DevOps-charges), [pricing updates](https://unity.com/en/products/pricing-updates)). | **Strong for iOS:** 100 free Mac minutes is enough for about 3–5 iOS builds per month, with no fastlane or match. You upload a `.p12` and `.mobileprovision`, which you can make on Windows with OpenSSL. It can pull from GitHub. Drawbacks: weaker GitHub/`gh` integration, Android incremental builds reported at 40+ min ([forum](https://discussions.unity.com/t/concerningly-long-build-times/914170)), and it needs a payment method for overage. |
| **Codemagic** | Free tier: 500 macOS M2 min per month. Pay-as-you-go: M2 $0.095/min, Linux/Windows $0.045/min ([pricing](https://codemagic.io/pricing/)). | **Not viable with Unity Personal.** "Building Unity apps in a cloud CI/CD environment requires a Unity Plus or a Pro license" ([Codemagic Unity docs](https://docs.codemagic.io/yaml-quick-start/building-a-unity-app/)), so you'd need a Pro seat. |
| **Local batchmode build** (`Unity.exe -batchmode -quit -projectPath . -executeMethod BuildScript.BuildAndroid -logFile -`) | $0 | Fastest feedback, since the Library is already warm and the machine is already licensed. Android only, because iOS needs Xcode. You can also register the PC as a **self-hosted runner**, which is free, so Actions triggers local builds without any CI license activation or LFS re-downloads. Drawback: the PC must be on. |

**Recommendation: hybrid, about $0–15 per month on top of store fees.**
1. **GitHub private repo + GitHub Actions as the hub.** Claude Code can create and trigger workflows and read logs through `gh`.
2. **Android:** GameCI `unity-builder@v6` on `ubuntu-latest`, plus `r0adkll/upload-google-play` to the internal track. Optionally add a **self-hosted runner on the Windows PC** for day-to-day builds. That costs $0 and avoids LFS bandwidth, while hosted Linux stays as the clean or reproducible fallback.
3. **iOS:** export on `ubuntu-latest`, then fastlane `match` + `pilot` on `macos-26`, triggered manually or by tag. This is about $1.50 per release. **Fallback:** if the match/fastlane setup becomes painful without a Mac, use Unity Build Automation's free 100 Mac minutes per month for iOS only.
4. Skip Codemagic (it needs Unity Pro). No Mac purchase is required. A Mac only becomes useful for on-device debugging with Xcode or Instruments.

---

## 4. Repo setup for Unity on GitHub

- **Unity settings:** Project Settings > Editor: **Version Control = Visible Meta Files** and **Asset Serialization = Force Text**. Commit every `.meta` file.
- **`.gitignore`:** use GitHub's official [`Unity.gitignore`](https://github.com/github/gitignore/blob/main/Unity.gitignore). It ignores `/Library/`, `/Temp/`, `/Obj/`, `/Build(s)/`, `/Logs/`, `/UserSettings/`, `*.csproj`/`*.sln`, `*.apk`/`*.aab`, `*.unitypackage`, Addressables' `/ServerData` and more. Add your own lines for `*.keystore`, `*.p8`, `*.p12`, `*.mobileprovision`, `fastlane/*.json` and `.env`.
- **`.gitattributes`** (minimal starting point):
  ```gitattributes
  * text=auto
  # Unity YAML: text + Smart Merge
  *.unity  merge=unityyamlmerge eol=lf
  *.prefab merge=unityyamlmerge eol=lf
  *.asset  merge=unityyamlmerge eol=lf
  *.mat    merge=unityyamlmerge eol=lf
  *.anim   merge=unityyamlmerge eol=lf
  *.controller merge=unityyamlmerge eol=lf
  *.meta   merge=unityyamlmerge eol=lf
  # LFS: binaries (one pattern per line; repeat for jpg, jpeg, tga, tif, exr, hdr, obj, blend, mp3, aif, mp4, mov, otf, a, so, dll, aar, jar, zip, cubemap)
  *.png filter=lfs diff=lfs merge=lfs -text
  *.psd filter=lfs diff=lfs merge=lfs -text
  *.fbx filter=lfs diff=lfs merge=lfs -text
  *.wav filter=lfs diff=lfs merge=lfs -text
  *.ogg filter=lfs diff=lfs merge=lfs -text
  *.ttf filter=lfs diff=lfs merge=lfs -text
  ```
  `git lfs track "*.ext"` writes these lines for you. `.gitattributes` takes **one pattern per line**.
- **Smart Merge / UnityYAMLMerge** ([Unity 6.3 manual](https://docs.unity.com/en-us/engine/6000.3/manual/get-started/project-configuration/version-control/smart-merge)): the tool lives at `C:\Program Files\Unity\Hub\Editor\<version>\Editor\Data\Tools\UnityYAMLMerge.exe`. The official configuration is a mergetool:
  ```ini
  [merge]
      tool = unityyamlmerge
  [mergetool "unityyamlmerge"]
      trustExitCode = false
      cmd = 'C:/Program Files/Unity/Hub/Editor/<ver>/Editor/Data/Tools/UnityYAMLMerge.exe' merge -p "$BASE" "$REMOTE" "$LOCAL" "$MERGED"
  ```
  For the automatic `merge=unityyamlmerge` attribute above, also add a merge driver. This is a community pattern, not in the manual: `[merge "unityyamlmerge"] name = Unity SmartMerge`, `driver = '<path>' merge -h -p --force %O %B %A %A`, `recursive = binary`. By default it only handles `.unity`, `.prefab` and `.meta` files, and other YAML files need `--force`. For a solo dev conflicts are rare, but they become likely when an AI agent works on branches.
- **Git LFS on GitHub (2026)** ([LFS billing](https://docs.github.com/en/billing/concepts/product-billing/git-lfs), [LFS rates](https://docs.github.com/en/billing/using-the-new-billing-platform/about-billing-for-git-large-file-storage)):
  - **Included:** **10 GiB storage + 10 GiB bandwidth per month** on Free and Pro. Bandwidth doesn't roll over.
  - **Overage:** **$0.07 per GiB-month** of storage and **$0.0875 per GiB** downloaded.
  - **With a $0 budget**, LFS is **blocked for the rest of the month** once you exceed the quota. Older accounts may still be on legacy $5 per month packs of 50 GB storage and 50 GB bandwidth ([pricing](https://github.com/pricing)).
  - **Per-file limits:** 2 GB per LFS file on Free/Pro ([about LFS](https://docs.github.com/en/repositories/working-with-files/managing-large-files/about-git-large-file-storage)). Normal git blocks files >100 MiB and recommends repos stay <1 GB, at most 5 GB ([large files](https://docs.github.com/en/repositories/working-with-files/managing-large-files/about-large-files-on-github)).
  - **The real risk is CI bandwidth.** Every fresh hosted checkout with `lfs: true` downloads the whole LFS set. For example, 3 GB of assets × 20 CI runs = 60 GB, about **$4.40 per month** overage. Mitigate by caching `.git/lfs`, using the self-hosted runner, and **importing only the parts of asset packs you actually use** (delete demo scenes and unused textures). Example: 25 GB stored is about $1.05 per month in storage overage, which is cheap. Bandwidth is what grows.
- **Paid Asset Store packs and licensing:**
  - The [Asset Store EULA](https://unity.com/legal/as-terms) (updated 2024-12-04) grants a **Single Entity** license to use assets "on an unlimited number of computers provided that these computers belong to END-USER". It forbids making copies "except incidental transient or temporary copies", and allows distribution only "as incorporated and embedded" in a product.
  - **A private repo used only by you (and your CI) is the commonly accepted practice** for a solo licensee ([Unity forum](https://discussions.unity.com/t/using-git-for-paid-for-assets-if-set-the-repository-to-private/879286), [Unity support on seats](https://support.unity.com/hc/en-us/articles/208601846)).
  - **A public repo would redistribute the source assets: not allowed.** Collaborators or contractors need their own licenses (EULA §2.4), and Editor-extension assets are seat-based (2 computers).
  - This also rules out the "free unlimited minutes on public repos" option for this project.
  - Not legal advice. Check each pack's own license for Restricted Assets.

---

## 5. Step-by-step plan

**Phase 0: accounts.** Start now, because verification takes days.
1. Create a **GitHub** account and a **private** repo. Optionally get Pro (~$4 per month, $18 of included Actions). Add a **payment method + budget** (e.g. $10 Actions, $5 LFS) so jobs don't hard-block.
2. Create a **Unity ID** (Personal), activate it in Hub, and save `C:\ProgramData\Unity\Unity_lic.ulf`.
3. Register for **Google Play Console** ($25). Complete identity verification and line up **12 closed testers** early.
4. Join the **Apple Developer Program** ($99 per year, individual enrollment). Approval can take days.

**Phase 1: repo.** Claude Code can do all of this.
5. `git init`, then add `Unity.gitignore` plus secret patterns, add `.gitattributes`, and run `git lfs install`. Set Force Text and Visible Meta Files. Configure UnityYAMLMerge in `.git/config`.
6. Push. Then check LFS usage under Settings > Billing.

**Phase 2: Android CI.**
7. Create the keystore in Unity and back it up.
8. Add the secrets with `gh secret set`: `UNITY_LICENSE`, `UNITY_EMAIL`, `UNITY_PASSWORD`, `ANDROID_KEYSTORE_BASE64`, `ANDROID_KEYSTORE_PASS`, `ANDROID_KEYALIAS_NAME`, `ANDROID_KEYALIAS_PASS`.
9. Add a workflow `build-android.yml` that runs on push/PR: checkout with LFS (plus LFS cache), Library cache, `unity-test-runner@v4` (EditMode/PlayMode), `unity-builder@v6` (AAB), then upload the artifact with `retention-days: 1`.
10. In Play Console, create the app, **upload the first AAB manually** to internal testing, and finish the store-listing basics.
11. Create a Google Cloud service account, enable the Play Android Developer API, and grant release rights in Play Console. Add the secrets `GOOGLE_PLAY_SERVICE_ACCOUNT_JSON` and `ANDROID_PACKAGE_NAME`.
12. Add `deploy-android.yml` (on tag `v*` or `workflow_dispatch`): build, then `r0adkll/upload-google-play@v1` to `tracks: internal`.

**Phase 3: iOS CI.**
13. In the Apple portal, register the Bundle ID and create the app record in App Store Connect. Create an **ASC Team API key** (App Manager) and download the `.p8`, which is only downloadable once.
14. Create a second **private** repo for `match` certificates and a deploy key for it. Add the secrets `APPSTORE_ISSUER_ID`, `APPSTORE_KEY_ID`, `APPSTORE_P8`, `APPLE_TEAM_ID`, `IOS_BUNDLE_ID`, `MATCH_REPOSITORY`, `MATCH_PASSWORD`, `MATCH_DEPLOY_KEY` and `GH_PAT`.
15. Run GameCI's certificate-generation workflow once on a macOS runner. No Mac is needed for this.
16. Add `deploy-ios.yml` (manual or tag only): Linux export job, then a `macos-26` job running Xcode 26.x and fastlane `match` + `build_app` + `upload_to_testflight(skip_waiting_for_build_processing: true)`.
17. Install TestFlight on your iPhone and add yourself as an internal tester.

**Phase 4: optimize.**
18. Register the Windows PC as a self-hosted runner for daily Android builds. It's free, uses a warm Library and needs no LFS re-download. Keep hosted runners for releases.
19. Review Actions and LFS usage monthly. If macOS minutes or fastlane setup become a pain, move iOS builds to Unity Build Automation (100 free Mac minutes per month).

**Expected recurring cost:** $99 per year Apple, $25 once for Google, and **about $0–15 per month** for GitHub (Actions overage plus LFS), depending on build frequency and asset size.
