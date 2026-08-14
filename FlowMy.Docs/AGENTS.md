# AGENTS / AI INSTRUCTIONS FOR THIS REPOSITORY

All AI Agents (Antigravity, Gemini, Copilot, Cursor) working on this codebase MUST strictly follow the coding standards and modularity rules defined in:
👉 [AI_CODING_STANDARDS.md](AI_CODING_STANDARDS.md)

### Key Rules Summary:
1. **File Length Limit**: Max ~500-1000 lines. Any file reaching 1000-1500 lines MUST be split into partial classes (e.g. `[Class].[Feature].cs`), separate services, or helpers.
2. **Function Length Limit**: Max ~50-80 lines per method. Break large methods into well-named private sub-methods.
3. **Logic Grouping**: Group related functionalities into cohesive partial files or dedicated services.
4. **Header Comment**: Every new or modified source file should contain the standardized AI header comment referencing `README.md` and `FlowMy.Docs/AI_CODING_STANDARDS.md`.
5. **Thread Safety**: Never call blocking `Dispatcher.Invoke(...)` from background threads or network interception handlers.
