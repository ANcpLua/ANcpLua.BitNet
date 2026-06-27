# ANcpLua.BitNet — roadmap / resume anchor

Goal: clean, publishable re-home of the BitNet stack purged from `ANcpLua.Agents`
(`83a8b5d Purge agents toolkit to instrumentation core`). Principle: BitNet is a
test-double / SUT, **never** forced as a judge.

## Done
- Repo scaffold: `global.json` (ANcpLua.NET.Sdk 3.4.42, MTP), `Version.props`,
  `Directory.Build.props` / `Directory.Packages.props` (CPM), LICENSE (MIT), `.gitignore`.
- Recovered the purged stack from history and renamed `Qyl*` -> `BitNet*` (clean public API;
  breaking vs the published 3.x alpha -> new major **4.0.0**).
- `ANcpLua.Agents.Hosting.BitNet` (+ bundled `.Generators`) **builds and packs**
  (analyzer ships under `analyzers/dotnet/cs/` in the nupkg).
- `ANcpLua.Agents.Testing.BitNet` (`BitNetFixture`, `BitNetAttribute`, `BitNetTestGroup`) **builds**.
- README + `docs/bitnet-not-a-judge.md` (the honest capability/runtime verdict, with measured evidence).

## Next (each its own small increment)
1. **Fixture factory-dedup** — DONE (preview.2). Extracted `ANcpLua.Agents.BitNet.Core`
   (`BitNetClientOptions` / `BitNetChatClientFactory` / `LegacyMaxTokensPolicy`, no ASP.NET Core);
   `BitNetFixture` delegates to `BitNetChatClientFactory.Create`, duplicated policy deleted.
2. **`[BitNet]` maximal generator** — DONE (preview.3). `ANcpLua.Agents.Testing.BitNet.Generators`
   turns `[BitNet]` on a `partial` test class into `[Collection]` + the fixture-injecting ctor +
   a `BitNet` accessor + `SkipUnlessBitNetAvailable()`, and emits the `[CollectionDefinition]` per
   assembly. `BITNET001/002/003` diagnostics guard partial/top-level/non-generic. Bundled in the
   Testing nupkg. **Deferred to v2:** per-class `[BitNet(Port=…, Model=…)]` config (it fights the
   shared single-container model and needs a configurable fixture).
3. **Idempotent entry** — refine `scripts/bitnet-docker.sh` to a true idempotent `up`
   (skip if already running + healthy, don't re-`rm`), add a `Makefile`
   (`make bitnet-up/down/status`, `make test`) and MSBuild props/targets in the Testing package.
4. **Smoke tests** — DONE (preview.3). `ANcpLua.Agents.Testing.BitNet.Tests` (xUnit v3 + MTP):
   a generator-wiring test (runs in CI) + an auto-Docker round-trip smoke test (`[DockerEnabledFact]`,
   opt-in via `BITNET_SMOKE_TEST=1`). CI runs them Docker-gated via `BITNET_FIXTURE_NO_DOCKER=1`.
5. **CI + publish** — DONE (preview channel): `.github/workflows/nuget-publish.yml` is the
   ANcpLua fleet trusted-publishing pattern (keyless OIDC, no stored key), made **preview-aware**
   (`vX.Y.Z-preview.N` tags; floor `4.0.0-preview.1`). Push-to-main is the release; the 3-OS build
   matrix gates it; a one-time nuget.org Trusted Publishing policy authorizes the OIDC push. Stable
   `4.0.0` is a deliberate `workflow_dispatch version=4.0.0` once the items above land.
6. **Re-home `ANcpLua.NET.Sdk.BitNet`** — the orphaned SDK meta-package, into this repo.

## Notes / facts
- Pinned image digest: `mcr.microsoft.com/appsvc/docs/sidecars/sample-experiment@sha256:9d5f7f4e…cd243a`.
- Versions live in `Version.props` (local override; mirrors the ANcpLua cross-repo pattern).
  `Microsoft.CodeAnalysis.CSharp` pinned to 5.3.0 (transitive floor from ANcpLua.Roslyn.Utilities 2.2.27).
