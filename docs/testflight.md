# TestFlight builds (iOS, no Mac)

Workflow: [`.github/workflows/ios-testflight.yml`](../.github/workflows/ios-testflight.yml), run by hand
(GitHub → Actions → "iOS TestFlight" → Run workflow, or `gh workflow run ios-testflight.yml -f notes="P1 lab"`).

1. **Linux job:** GameCI exports the Unity project as an Xcode project (Unity Personal license from secrets).
2. **macOS job** (`macos-26`, Xcode 26): archives with **cloud-managed signing**. `xcodebuild -allowProvisioningUpdates` +
   the App Store Connect API key create the distribution certificate and profile on Apple's side, so there is no
   fastlane `match` repo and no certificates to manage. It then uploads straight to App Store Connect.
   Build number = the workflow run number. Export compliance is pre-answered (`ITSAppUsesNonExemptEncryption = false`).

Cost: about $1.50 of GitHub Actions per iOS build (macOS minutes). Only run it when there's something to test.

## App identity
| | |
|---|---|
| Name | Aquarium Civ |
| Bundle ID | `com.tommasoribaudo.aquariumciv` |
| Version | 0.1.0 (PlayerSettings), build = run number |
| Orientation | Portrait only |
| Icon | `AquariumCiv/Assets/Art/Icon/AppIcon.png` (placeholder render of the look-test diorama) |

## One-time setup (the user, logged in)

### Unity license for CI
1. Unity Hub → Preferences (gear) → Licenses → **Add** → "Get a free personal license".
   This writes `C:\ProgramData\Unity\Unity_lic.ulf`. On this machine it didn't exist yet: the Hub's newer
   entitlement license isn't enough for CI.
2. Claude sets `UNITY_LICENSE` from that file.
3. **You** set `UNITY_EMAIL` and `UNITY_PASSWORD` in your own terminal, so the password never passes through the chat:
   `gh secret set UNITY_EMAIL -R TommasoRibaudo/aquarium-civ` and `gh secret set UNITY_PASSWORD -R TommasoRibaudo/aquarium-civ`
   (each one prompts for the value). If your Unity account uses 2FA on login, CI activation can fail. In that case
   we need a separate Unity ID for CI.

### Apple
1. **Bundle ID:** developer.apple.com → Account → Certificates, IDs & Profiles → Identifiers → **+** → App IDs → App →
   Description "Aquarium Civ", Bundle ID **Explicit** `com.tommasoribaudo.aquariumciv`, no capabilities → Register.
2. **App record:** appstoreconnect.apple.com → Apps → **+** → New App → iOS, Name "Aquarium Civ" (if taken, try
   "Aquarium Civ Prototype"), language English, Bundle ID from step 1, SKU `aquariumciv`, User Access: Full.
3. **API key:** App Store Connect → Users and Access → Integrations → App Store Connect API → **Team Keys** →
   Generate. Name "GitHub CI", Access **Admin** (cloud-managed signing needs it). Download the `.p8` (you can only
   download it once) and note the **Key ID** plus the **Issuer ID** shown above the table.
4. **Team ID:** developer.apple.com → Account → Membership details → Team ID.
5. Claude sets `APPSTORE_KEY_ID`, `APPSTORE_ISSUER_ID`, `APPLE_TEAM_ID` and `APPSTORE_P8` (from the downloaded file,
   without printing it). Then delete the `.p8` from Downloads and keep a copy in your password manager.

### Testers
- **You:** after the first build has processed, App Store Connect → the app → TestFlight → Internal Testing → **+**
  group "Team" → add yourself. Install TestFlight on the iPhone and accept the email.
- **Friends** (choose one):
  - *External group + public link* (recommended): TestFlight → External Testing → **+** group "Friends" → add the
    build → fill in test info (feedback email, what to test) → Apple runs a short Beta App Review (usually under a
    day, first build only per version) → enable the **public link** and send it. Up to 10,000 testers, no accounts.
  - *Internal:* add each friend as an App Store Connect user (Users and Access → **+**, role "Developer" or
    "Marketing"), then put them in the internal group. No review, but they get access to your App Store Connect account.

## Secrets checklist
| Secret | From |
|---|---|
| `UNITY_LICENSE` | `C:\ProgramData\Unity\Unity_lic.ulf` contents |
| `UNITY_EMAIL`, `UNITY_PASSWORD` | your Unity ID (you set these) |
| `APPSTORE_KEY_ID`, `APPSTORE_ISSUER_ID` | App Store Connect API key page |
| `APPSTORE_P8` | the downloaded `AuthKey_XXXX.p8` contents |
| `APPLE_TEAM_ID` | Membership details |

## Notes
- Release builds hide the debug panel (`Debug.isDebugBuild` is false). Lab tests that need the away buttons run in the
  Editor for now.
- If the macOS job fails on signing, check that the API key has **Admin** access and that the bundle ID is
  registered under the same team.
