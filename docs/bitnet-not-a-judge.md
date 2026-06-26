# BitNet is a test-double, not a judge — with evidence

This stack makes it trivial to stand up Microsoft's BitNet b1.58-2B-4T locally as a cheap
model **under test**. It deliberately does **not** position BitNet as the *judge* in an
LLM-as-judge evaluation. Two independent reasons, one cited and one measured.

## 1. Capability — below the reliable-judge threshold

LLM-as-judge reliability scales with model capacity. The literature places trustworthy
agreement on **quality *and safety*** judging at roughly **≥14B** parameters (e.g. Qwen2.5-14B);
the smallest models score *worse than random* on the "Safety" / "Chat-Hard" splits. BitNet
b1.58-2B-4T is strong *for its size* (it beats Qwen2.5-1.5B on GSM8K/WinoGrande) but is a
~2B-class model and trails on knowledge-heavy benchmarks (MMLU 53.2 vs 60.3). As the *judge*
— the component whose discernment defines an evaluation's validity — a 2B model is not enough.

Sources: BitNet b1.58 2B4T Technical Report (arXiv:2504.12285); LLM-as-judge reliability
studies (arXiv:2606.19544, arXiv:2509.13332).

## 2. Runtime — measured, on commodity/emulated CPU

Brought up `mcr.microsoft.com/appsvc/docs/sidecars/sample-experiment` pinned by digest
`sha256:9d5f7f4e…cd243a` (the same digest this repo's fixture pins). On an Apple-Silicon host
via Docker/OrbStack (x86 `i2_s` kernels emulated):

- `/v1/models` answered instantly; the server loaded the model and processed a 68-token prompt.
- **A single 150-token judge call returned 0 bytes after 240 s** (HTTP 000, timeout), CPU pegged
  at ~92% the whole time — i.e. it was computing, just `< 0.6 tok/s`.
- The server exposes **`n_slots = 1`**, so concurrent reviewer calls serialize.
- Under that emulated load the Docker daemon became unstable (dropped twice).

A judge that can't finish one verdict in four minutes, run ~200× per evaluation against a
single slot, is not a gate judge on this class of hardware. On a native x86 box it is faster,
but the capability ceiling in §1 stands regardless.

## Where BitNet *does* fit here

- **System-under-test (SUT)** — the cheap local agent being *evaluated* by a stronger judge.
- **Free, local, low-confidence smoke** — exercising the real prompt→response→JSON path offline.
- **Air-gapped** evaluation where no hosted API is permitted.

For an authoritative judge, use a capable hosted model. This repo keeps BitNet in the role it
earns, and says so out loud.
