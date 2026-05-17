Attention contract authors!

Contracts should have their prestige set based on the following standards:

* `prestige = Trivial` for <span style="color:blue">Optional</span> contracts.

* `prestige = Significant` for <span style="color:green">Required</span> contracts.

* `prestige = Exceptional` for <span style="color:red">Capstone</span> contracts.

Forgetting to do so won't cause any physical issues however, as contract prestige is only visual in RP-1.

You can run the below powershell script to set it automatically if you want:

```powershell
# Set the folder you want to scan
$FolderPath = "(put folder path here)"

# Find all files in the folder (including subfolders)
Get-ChildItem -Path $FolderPath -File -Recurse | ForEach-Object {

    $FilePath = $_.FullName
    $Lines = Get-Content -Path $FilePath

    # Skip empty files
    if ($Lines.Count -eq 0) { return }

    # First line must be CONTRACT_TYPE
    if ($Lines[0].Trim() -ne "CONTRACT_TYPE") { return }

    $Content = $Lines -join "`n"

    # Get contract name
    $ContractName = "UNKNOWN"
    if ($Content -match 'name\s*=\s*(.+)') {
        $ContractName = $matches[1].Trim()
    }

    # Determine prestige level
    $NewPrestige = $null

    if ($Content -match '<color=blue>Optional</color>') {
        $NewPrestige = "Trivial"
    }
    elseif ($Content -match '<color=green>Required</color>') {
        $NewPrestige = "Significant"
    }
    elseif ($Content -match '<color=red>CAPSTONE</color>') {
        $NewPrestige = "Exceptional"
    }
    else {
        return
    }

    # Replace prestige line
    $NewContent = $Content -replace 'prestige\s*=\s*\S+', "prestige = $NewPrestige"

    if ($NewContent -ne $Content) {
        Set-Content -Path $FilePath -Value $NewContent -Encoding UTF8
    }

    # Log result
    Write-Host "$ContractName set to prestige = $NewPrestige"
}
```