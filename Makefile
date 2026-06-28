# ANcpLua.BitNet — developer entry points. `make help` lists targets.
SHELL := /usr/bin/env bash
SLN   := ANcpLua.BitNet.slnx
TESTS := tests/ANcpLua.Agents.Testing.BitNet.Tests/ANcpLua.Agents.Testing.BitNet.Tests.csproj

.DEFAULT_GOAL := help
.PHONY: help build test test-live pack bitnet-up bitnet-down bitnet-status clean

help: ## List available targets
	@grep -E '^[a-zA-Z_-]+:.*?## ' $(MAKEFILE_LIST) \
		| awk 'BEGIN{FS=":.*?## "}{printf "  \033[1m%-14s\033[0m %s\n", $$1, $$2}'

build: ## Build the solution (Release)
	dotnet build $(SLN) -c Release

test: ## Run tests Docker-gated (round-trip + smoke skip without a server)
	BITNET_FIXTURE_NO_DOCKER=1 dotnet test $(TESTS) -c Release

test-live: bitnet-up ## Start BitNet, then run the auto-Docker round-trip smoke test
	BITNET_SMOKE_TEST=1 dotnet test $(TESTS) -c Release

pack: ## Pack the three publishable packages into ./artifacts
	dotnet pack src/ANcpLua.Agents.BitNet.Core/ANcpLua.Agents.BitNet.Core.csproj       -c Release -o artifacts
	dotnet pack src/ANcpLua.Agents.Hosting.BitNet/ANcpLua.Agents.Hosting.BitNet.csproj -c Release -o artifacts
	dotnet pack src/ANcpLua.Agents.Testing.BitNet/ANcpLua.Agents.Testing.BitNet.csproj -c Release -o artifacts

bitnet-up: ## Start the digest-pinned BitNet server (idempotent: no-op if already healthy)
	scripts/bitnet-docker.sh up

bitnet-down: ## Stop + remove the BitNet server (idempotent)
	scripts/bitnet-docker.sh down

bitnet-status: ## Show the BitNet server status
	scripts/bitnet-docker.sh status

clean: ## Remove build artifacts
	dotnet clean $(SLN) -c Release || true
	rm -rf artifacts
