# CI and repository security

The repo is public (since 2026-10-28), so Actions minutes on standard runners are free, and anyone can read the code,
the workflows and the run logs. Everything below is set up to keep that safe.

## Workflows
| Workflow | Runs on | What it does |
|----------|---------|--------------|
| [`ci.yml`](../.github/workflows/ci.yml) | every pull request, every push to `main`, by hand | **Workflow lint** (actionlint + zizmor), **Secret scan** (gitleaks on the new commits), **Sim tests (dotnet)** (`dotnet test Tools/SimTests`, .NET 8, NuGet cached, no secrets), **Unity tests** (EditMode, GameCI) |
| [`ios-testflight.yml`](../.github/workflows/ios-testflight.yml) | by hand only, from `main` | iOS export, signing, upload to TestFlight. See [testflight.md](testflight.md) |

Unity tests need the Unity license secrets. GitHub never passes secrets to pull requests from forks, so that job is
skipped for fork PRs. A maintainer runs the tests by pushing the branch to this repo, or tests after merging.
**Never switch a workflow to `pull_request_target`** to "fix" that: it runs fork code with our secrets (GameCI's
own workflows had exactly this bug in 2021).

Test results are uploaded as the `test-results` artifact (14 days).

## Working on `main` (ruleset "main")
- No direct pushes: changes land through a pull request (no approval needed, since this is a one-person project).
- The CI checks must pass: **Workflow lint**, **Secret scan**, **Unity tests**, **Sim tests (dotnet)** (required on `main`
  and `dev` since 2026-10-30).
- No force pushes, no deleting `main`.
- An admin can still merge a PR whose checks fail (bypass on pull requests only), for emergencies like an expired
  Unity license.

Agents: branch, push, open a PR with `gh pr create`, wait for the checks, then merge with `gh pr merge --squash`
(or `--merge`). Never push to `main` directly.

## Security measures in the workflows
Based on GitHub's [secure use reference](https://docs.github.com/en/actions/reference/security/secure-use) plus
zizmor's audits.

- **Actions pinned to full commit SHAs** with a `# vX.Y.Z` comment. Tags can be moved; SHAs can't. The repo setting
  "require SHA pinning" enforces it. Dependabot (`.github/dependabot.yml`) bumps the pins weekly, after a 7-day cooldown.
- **Least-privilege token**: `permissions: {}` at the top of each workflow; each job asks only for what it needs
  (`contents: read`, plus `actions: write` to delete the Xcode artifact). Repo default is read-only, and Actions
  can't create or approve pull requests.
- **`persist-credentials: false`** on every checkout, so the token isn't left in `.git/config` for later steps.
- **No `${{ }}` inside `run:` scripts**: values go through `env:` first, so text like a branch name can't inject shell
  commands.
- **Secrets only where they're used**: the Apple secrets are passed per step, never to third-party actions. The `.p8`
  key file is written with `umask 077` and deleted at the end, even on failure.
- **`testflight` environment** restricted to `main`: an upload can only come from merged code.
- **Scanner images pinned by digest** (actionlint, gitleaks) and run with `docker run`, so they get no token.
- **Fork PRs need approval** before any workflow runs for first-time and outside contributors.
- No self-hosted runners (they're unsafe on public repos).

## Repository settings
- Secret scanning + push protection: on (blocks a push containing a recognisable key).
- Dependabot alerts + security updates: on.
- Private vulnerability reporting: on (see [SECURITY.md](../SECURITY.md)).

## Possible next steps (not done)
- Move the four Apple secrets from repository secrets into the `testflight` environment, so only that job can read
  them. It needs the values typed in again: `gh secret set NAME --env testflight -R TommasoRibaudo/aquarium-civ`, then
  delete the repo-level copies. zizmor's `auditor` persona flags the Unity secrets for the same reason; they stay
  repo-level because the test job runs on every branch.
- StepSecurity Harden-Runner (egress monitoring) and OpenSSF Scorecard: worth it if the project gains contributors.
- CODEOWNERS on `.github/` only matters once there's more than one maintainer.
