# ==============================================================================
# Root entrypoint to run the NpOn.Net-V2 Agent Harness
# Usage:
#   .\run-harness.ps1
#   .\run-harness.ps1 -SkipBuild
#   .\run-harness.ps1 -DetailedDebug
# ==============================================================================
param(
    [switch]$SkipBuild = $false,
    [switch]$DetailedDebug = $false,
    [string]$Configuration = "Release"
)

& "$PSScriptRoot\.agents\harness\harness.ps1" -SkipBuild:$SkipBuild -DetailedDebug:$DetailedDebug -Configuration $Configuration

