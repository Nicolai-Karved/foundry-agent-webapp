# Agent Frontmatter Schema

```yaml
---
name: <string>                 # Optional display name; defaults to filename
description: <string>          # Placeholder/instructions shown in chat input
argument-hint: <string>        # Optional hint text for users
model: <string>                # Optional model override (e.g. "GPT-5.2 (copilot)")
tools:                         # List of allowed tools for this agent
  - <tool-id>
  - <tool-id>/*
agents:                        # Subagents allowed for this agent
  - <agent-name>
user-invocable: <boolean>      # Controls whether this agent is shown in dropdown
disable-model-invocation: <boolean>  # Prevent invocation as subagent
target: <"vscode"|"github-copilot">  # Where the agent is targeted
mcp-servers:                   # Optional MCP server configs (for cloud contexts)
  - <MCP server config object>
handoffs:                      # Optional list of handoff definitions
  - label: <string>
    agent: <agent-name>
    prompt: <string>
    send: <boolean>
---
```

Notes:
- Omit optional fields unless you need to override defaults.
- You can comment out optional fields to keep placeholders without changing behavior.
