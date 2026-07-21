# ANcpLua.BitNet — agent notes

BitNet (`bitnet.cpp` / `llama-server`) integration stack: hosting facade,
Roslyn source generator, and xUnit v3 test fixture, published as the
`ANcpLua.Agents.Hosting.BitNet` / `.Generators` / `ANcpLua.Agents.Testing.BitNet`
NuGet packages. `README.md` is the authoritative overview and runbook.

Working contract:

- BitNet is a local test-double / system-under-test, not an LLM judge — keep
  that framing in code, docs, and tests (evidence: `docs/bitnet-not-a-judge.md`).
- The Docker container is digest-pinned and idempotently managed
  (`make bitnet-up|status|down`, or the `BitNetFixture` does it automatically).
- Published package versions are immutable; changes ship as new versions.
