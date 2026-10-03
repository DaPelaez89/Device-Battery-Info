# Contributing

Thanks for looking at Device Battery Info. The project only covers the devices someone has actually
plugged in and tested - the fastest way to make it more useful is to add the one you own.

## Ways to contribute

- **Add a device backend.** [`docs/adding-a-device.md`](docs/adding-a-device.md) walks through the
  cases: one line for another model of a known brand, one `HidProtocol` class for a new USB HID brand, or
  one device family for anything else. Each registers itself; most device PRs touch one new folder under
  `Sources/`, the supported-devices table in the README, and a handful of tests.
- **Report a bug** or **request a device** you don't have time to implement yourself, using the issue
  templates.
- **Improve the docs.** [AGENTS.md](AGENTS.md) and [`docs/adding-a-device.md`](docs/adding-a-device.md)
  are meant to be enough on their own; if you had to guess or ask, that's a documentation bug.

## Before you open a PR

1. Read [AGENTS.md](AGENTS.md). It is the actual rule set this codebase is written against - lifecycle,
   async/concurrency, localization, logging, comment style - not a suggestion. A PR that violates it
   (blocking calls in a capability handler, a user-facing literal instead of a `Strings.resx` key,
   decorative comments, etc.) will be sent back before anything else is reviewed.
2. Run, and make sure they pass:
   ```bash
   dotnet build
   dotnet test
   ```
3. If you touched a device backend, **test it against the real hardware**. A parser with unit tests but
   no real-device confirmation is a draft, not a contribution - say so explicitly in the PR description
   if you could not test against real hardware (for example, you're contributing a backend for a device
   a maintainer will need to verify).
4. Keep the PR focused. Unrelated reformatting or drive-by refactors make a device-support PR harder to
   review and slower to merge.
5. Update [README.md](README.md) or [AGENTS.md](AGENTS.md) if the change affects what they describe
   (a new supported device, a new capability, a changed build/pack step).

## AI-assisted contributions

Using AI tools (Claude, Copilot, ChatGPT, or anything else) to help write code, tests or docs for this
project is fine. What matters is what lands in the PR, not how it was produced:

- **You are responsible for every line.** Before opening a PR, be able to explain what each change does
  and why, and have actually run it. "The AI wrote it and it compiled" is not a review.
- **It has to meet the same bar as hand-written code** - [AGENTS.md](AGENTS.md)'s rules apply
  regardless of how the code was produced: no leftover placeholder comments, no invented APIs, no
  hallucinated device protocols, no restating-the-code comments, correct localization, real test
  coverage.
- **Device backends still need real hardware.** An AI can write a very convincing HID or Bluetooth
  parser for a device it has never seen data from. If you have not run it against the actual device,
  say so in the PR - do not present untested, generated protocol-handling code as working.
- **Low-effort, unreviewed AI output will be closed, not iterated on.** A PR that ignores AGENTS.md, its
  own tests, or basic review feedback in a way that suggests nobody read the diff before submitting it
  gets closed rather than turned into a multi-round editing session. Feel free to reopen once it has had
  an actual human pass.

None of this is about banning AI assistance - it is about making sure a human contributor stands behind
what they submit, the same expectation as any other open-source project.

## License

By contributing, you agree that your contribution is licensed under the project's [MIT license](LICENSE).
