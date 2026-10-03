PROJECT  := src/DeviceBatteryInfo
MANIFEST := $(PROJECT)/manifest.json

STATE    := $(PROJECT)/.macrodeck-dev-state

UTF8     := $(if $(filter Windows_NT,$(OS)),chcp.com 65001 >/dev/null &&)
RUN      := $(UTF8) macrodeck-plugin run --project $(PROJECT) --state-directory $(STATE)

# The SDK version, for keeping the macrodeck-plugin CLI in step. Read inside recipes rather than with
# $(shell): GnuWin32's make 3.81 sometimes runs $(shell) with an empty command line.
SDK      := grep -o 'MacroDeck.Sdk" Version="[^"]*' Directory.Packages.props | cut -d'"' -f3
TESTS    := dotnet test DeviceBatteryInfo.slnx --configuration Release --filter "Category!=Hardware"

RID      := $(if $(filter Windows_NT,$(OS)),win-x64,osx-arm64)

# Store images: every [UiPreview] scenario at each deck shape. Override on the command line,
# e.g. make preview CELLS="--cells 2x2" PREVIEW_ARGS="--theme light".
CELLS    := --cells 1x1 --cells 2x1 --cells 2x2
PREVIEWS := artifacts/previews

.DEFAULT_GOAL := help
.PHONY: help cli build test test-hardware run watch stub preview pack conformance update release

help:
	@echo "make cli            install/update the macrodeck-plugin CLI to the SDK version ($$($(SDK)))"
	@echo "make build          build the solution"
	@echo "make test           unit tests, as the release workflow runs them"
	@echo "make test-hardware  the [Explicit] hardware tests (real devices attached)"
	@echo "make run            run the plugin against the running Macro Deck"
	@echo "make watch          the same, with hot reload / restart on every saved change"
	@echo "make stub           run the plugin against a disposable stub host (no Macro Deck needed)"
	@echo "make preview        render the widget previews to PNGs in $(PREVIEWS)/ (store images)"
	@echo "make pack           build this platform's .macroDeckPlugin ($(RID)) into artifacts/ and inspect it"
	@echo "make conformance    run the conformance suite, report in conformance.md"
	@echo "make update         bump every package to its newest release (review the diff)"
	@echo "make release VERSION=x.y.z"
	@echo "                    test + pack, bump manifest.json, commit, tag vx.y.z, push"

cli:
	dotnet tool update --global MacroDeck.Plugin.Cli --version "$$($(SDK))"

build:
	dotnet build DeviceBatteryInfo.slnx

test:
	$(TESTS)

test-hardware:
	dotnet test DeviceBatteryInfo.slnx --filter "Category=Hardware"

run:
	$(RUN)

watch:
	$(RUN) --watch

stub:
	$(UTF8) macrodeck-plugin run --project $(PROJECT) --stub-host

preview:
	rm -rf $(PREVIEWS)
	$(UTF8) macrodeck-plugin preview render --project $(PROJECT) $(CELLS) --output $(PREVIEWS) $(PREVIEW_ARGS)

pack:
	rm -f artifacts/*.macroDeckPlugin
	macrodeck-plugin build --source $(PROJECT) --rid $(RID) --output ./artifacts
	macrodeck-plugin inspect --artifact "$$(ls artifacts/*.macroDeckPlugin)"

conformance:
	macrodeck-plugin test --project $(PROJECT) --report markdown --output conformance.md

update:
	dotnet package update

# Pushing the tag starts .github/workflows/release.yml, which checks the manifest version against the
# tag, creates the GitHub release and publishes to the Creator Portal. Everything that can fail runs
# before the bump commit, so a failed check leaves nothing to undo.
release:
	@case "$(VERSION)" in \
	  [0-9]*.[0-9]*.[0-9]*) ;; \
	  *) echo "usage: make release VERSION=x.y.z (current: $$(sed -n 's/^  "version": "\(.*\)",$$/\1/p' $(MANIFEST)))"; exit 1 ;; \
	esac
	@test "$$(git rev-parse --abbrev-ref HEAD)" = main || { echo "release from main only"; exit 1; }
	@test -z "$$(git status --porcelain)" || { echo "working tree is not clean"; exit 1; }
	@! git rev-parse -q --verify "refs/tags/v$(VERSION)" >/dev/null || { echo "tag v$(VERSION) already exists"; exit 1; }
	git pull --ff-only
	$(TESTS)
	$(MAKE) pack
	sed -i 's/^  "version": ".*",$$/  "version": "$(VERSION)",/' $(MANIFEST)
	@grep -q '^  "version": "$(VERSION)",$$' $(MANIFEST) || { echo "could not set the version in $(MANIFEST)"; git checkout -- $(MANIFEST); exit 1; }
	git commit -m "chore: bump version" -- $(MANIFEST)
	git tag v$(VERSION)
	git push --atomic origin main v$(VERSION)
