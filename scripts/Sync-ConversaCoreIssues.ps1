param([int]$WorkPackage = -1, [switch]$Finalize)
$ErrorActionPreference = 'Stop'
$repo = 'lg061870/InsuranceSemanticV2'
$root = Split-Path $PSScriptRoot -Parent
$plan = Get-Content (Join-Path $root 'docs/ConversaCore.TransformationWorkBreakdown.md') -Raw
$source = 'https://github.com/' + $repo + '/blob/codex/conversacore-runtime-foundation/docs/'
function Api($method, $path, $payload) {
    if ($null -eq $payload) { $result = & gh api $path }
    else { $result = ($payload | ConvertTo-Json -Depth 15) | & gh api --method $method $path --input - }
    if ($LASTEXITCODE -ne 0) { throw "GitHub request failed: $method $path" }
    if ($result) { return ($result | ConvertFrom-Json) }
}
$existing = @(& gh issue list --repo $repo --state all --limit 1000 --json number,title,state | ConvertFrom-Json)
if ($LASTEXITCODE -ne 0) { throw 'Unable to list issues' }
$script:issues = @{}
foreach ($issue in $existing) { $script:issues[$issue.title] = $issue }
function EnsureIssue($title, $body) {
    if ($script:issues.ContainsKey($title)) { return $script:issues[$title] }
    $created = Api POST "repos/$repo/issues" @{ title = $title; body = $body }
    $script:issues[$title] = $created
    Write-Host "Created #$($created.number) $title"
    Start-Sleep -Milliseconds 1100
    return $created
}
function LinkChild($parent, $child) {
    $children = @(Api GET "repos/$repo/issues/$($parent.number)/sub_issues?per_page=100" $null)
    if ($children.number -contains $child.number) { return }
    $detail = Api GET "repos/$repo/issues/$($child.number)" $null
    $null = Api POST "repos/$repo/issues/$($parent.number)/sub_issues" @{ sub_issue_id = $detail.id }
    Start-Sleep -Milliseconds 1100
}
$tracking = @'
## Execution and completion policy

GitHub issues are the execution record. Before changing a task, add a progress comment explaining the intended change and why. Record implementation decisions, affected files, commit/PR links, verification commands/results, and remaining blockers as work proceeds.

Keep partial work open. Close a task only when its entire scope and applicable definition of done are satisfied, its changes are available in GitHub, and validation evidence is recorded. A passing test that reproduces a legacy defect does not mean the defect is fixed. Close a WP only when all its tasks and phase acceptance criteria are satisfied. Update this hierarchy and the source work breakdown together when scope changes.
'@
$masterTitle = '[ConversaCore] Transformation master tracker'
$master = EnsureIssue $masterTitle "Full delivery tracker for the ConversaCore upgrade. WPs are phase groupings; each CC identifier has its own child issue.`n`n[Detailed work breakdown](${source}ConversaCore.TransformationWorkBreakdown.md) | [Target architecture](${source}ConversaCore.TargetArchitecture.md)`n`n$tracking"
$sections = [regex]::Matches($plan, '(?ms)^## \d+\. (WP\d) — ([^\r\n]+)\r?\n(.*?)(?=^## \d+\.|\z)')
if ($sections.Count -ne 9) { throw "Expected 9 WPs, found $($sections.Count)" }
$done = [regex]::Match($plan, '(?ms)^## 19\. Definition of done for every ticket\r?\n(.*?)(?=^## 20\.)').Groups[1].Value.Trim()
foreach ($section in $sections) {
    $id = $section.Groups[1].Value
    if (-not $Finalize -and $WorkPackage -ge 0 -and $id -ne "WP$WorkPackage") { continue }
    $title = "[ConversaCore][$id] " + $section.Groups[2].Value.Trim()
    $content = $section.Groups[3].Value.Trim()
    $tasks = [regex]::Matches($content, '(?m)^- \[([ x])\] \*\*(CC-\d+) — (.*?)\*\* (.+)\r?$')
    $intro = ($content -split '### Tasks')[0].Trim()
    $acceptance = ($content -split '### Acceptance criteria')[1].Trim()
    $parent = EnsureIssue $title "Parent: #$($master.number)`n`n$intro`n`n## Phase acceptance criteria`n`n$acceptance`n`n$tracking"
    LinkChild $master $parent
    if ($Finalize) { continue }
    $taskLinks = @()
    foreach ($task in $tasks) {
        $taskId = $task.Groups[2].Value
        $taskTitle = "[ConversaCore][$taskId] " + $task.Groups[3].Value.Trim().TrimEnd('.')
        $scope = $task.Groups[4].Value.Trim()
        $body = "Parent: #$($parent.number) ($id). Master: #$($master.number).`n`n## Scope`n`n$scope`n`n## Purpose and phase dependencies`n`n$intro`n`n## Shared phase acceptance criteria`n`nThese are phase-level gates; apply the relevant criteria to this task without treating this task alone as completion of the entire phase.`n`n$acceptance`n`n## Definition of done`n`n$done`n`n$tracking`n`nSource: [Detailed work breakdown](${source}ConversaCore.TransformationWorkBreakdown.md), task $taskId."
        if ($taskId -eq 'CC-004') {
            $body += "`n`n## Imported status`n`nIn progress. Four local legacy isolation tests pass, but their changes have not yet been committed/pushed. Keep this issue open. Positive replacement-runtime isolation remains part of CC-212; the legacy tests demonstrate defects rather than certify isolation."
        } elseif ($taskId -in @('CC-000','CC-001','CC-002','CC-005')) {
            $body += "`n`n## Imported status`n`nInitial inventory available in commit 29877ff and [WP0 inventory](${source}ConversaCore.WP0CurrentStateInventory.md). This is partial evidence; exhaustive coverage and task acceptance are still pending."
        } elseif ($taskId -eq 'CC-003') {
            $body += "`n`n## Imported status`n`nPartial local work: three host-event characterization tests pass (deferred notification, dropped inline response, stale cancellation markers). Start, active input, fallback, required cards, subtopic return, and semantic completion remain. Changes are not yet committed/pushed."
        }
        $child = EnsureIssue $taskTitle $body
        LinkChild $parent $child
        $taskLinks += "- [ ] #$($child.number) — $taskId"
    }
    $null = Api PATCH "repos/$repo/issues/$($parent.number)" @{body = "Parent: #$($master.number)`n`n$intro`n`n## Detailed tasks`n`n$($taskLinks -join "`n")`n`n## Phase acceptance criteria`n`n$acceptance`n`n$tracking"}
    Write-Host "$id verified: $($tasks.Count) detailed tasks."
}
if ($Finalize) {
    $phaseLinks = foreach ($section in $sections) {
        $title = "[ConversaCore][$($section.Groups[1].Value)] " + $section.Groups[2].Value.Trim()
        "- [ ] #$($script:issues[$title].number) — $($section.Groups[1].Value): $($section.Groups[2].Value.Trim())"
    }
    $remaining = [regex]::Replace($plan, '(?ms)^## (?:[5-9]|1[0-3])\. WP\d.*?(?=^## \d+\.|\z)', '')
    $null = Api PATCH "repos/$repo/issues/$($master.number)" @{body = "Full delivery tracker. Each WP is a phase parent; every numbered CC task is a separate native sub-issue.`n`n## Phases`n`n$($phaseLinks -join "`n")`n`n$tracking`n`n## Planning context, quality gates, risks, and deferred scope`n`n$remaining"}
    Write-Host "Master tracker: https://github.com/$repo/issues/$($master.number)"
}
