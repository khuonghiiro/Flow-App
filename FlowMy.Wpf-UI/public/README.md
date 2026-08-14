# Default Configurations

This folder contains default configuration files for the application.

## Folder Structure

- **FlowMy_CloneGit**: Default Git repositories configuration (git_repos.json)
- **FlowMy-CmdGit**: Default Git command configurations (git_commands.json)
- **Workflow_Json**: Default workflow templates (workflow_templates.json)

## Usage

When building the application, copy the contents of these folders to the actual runtime locations:
- FlowMy_CloneGit → Documents\FlowMy\FlowMy-CmdGit\
- FlowMy-CmdGit → Documents\FlowMy\FlowMy-CmdGit\
- Workflow_Json → Documents\FlowMy\Workflow_Json\

This allows you to ship the application with pre-configured Git repositories and workflows.
