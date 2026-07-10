# Code Style and Formatting

Please save these code format rules so that when you generate code, you will always follow them.

ALWAYS make sure to check for changes before editing any files.

## Project-wide Code Style

Code style (enforced via .editorconfig)
- Indent with 2 spaces.
- Private fields: _camelCase; const fields: KEBAB_CASE.
- All methods: PascalCase; All members: PascalCase.
- Variables inside methods: camelCase.
- When a method has parameters, add a space before and after the parenthesis `method( int i, string x )`.
- Methods with no parameters do not have a space `method()`
- Always use braces on control blocks; never single-line bodies!
- Braces on new lines for classes; same line for everything else.

## C# coding conventions

- Prefer var over explicit types.
- Prefer string interpolation and nameof().
- Prefer pattern matching where it makes sense.
- Prefer null-coalescing and null-conditional operators where it makes sense.
- Use async/await for asynchronous code.
- Use file-scoped namespaces at top of file
- Use namespaces that match folder structure.
- Usings after namespaces; System.* usings first, then third-party, then project.

That '.editorconfig' and 'omnisharp.json' files match the format I have specified here (and more).
This is just the most important parts.

## Code Generation Best Practices

- Follow patterns in existing code: Use DI, async/await, pattern matching.
- Generate code in appropriate projects: Shared for models/services, WebAPI for controllers, WebAngular for components.
- Avoid hardcoding; use configuration options.
- Test generated code with unit tests and Resty suites.
- Update Swagger XML docs for API changes.
- Ensure generated code adheres to .editorconfig rules for consistent style.

## Commit Message Guidelines

When committing changes to the repository, please follow these guidelines for writing commit messages:
- Use the present tense ("Add feature" not "Added feature").
- Use the imperative mood ("Move cursor to..." not "Moves cursor to...").
- Limit the first line to 72 characters or less.
- Reference issues and pull requests liberally after the first line.
- Separate subject from body with a blank line.
- Use the body to explain what and why vs. how.
- Use bullet points and lists to organize information when necessary.
- Be concise but descriptive.
- Avoid using "I" or "we" in commit messages.
- Proofread your messages for clarity and typos.
- Use consistent formatting for similar types of changes.
