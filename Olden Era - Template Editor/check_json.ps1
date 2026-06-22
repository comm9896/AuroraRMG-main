$json = Get-Content 'G:\table\AuroraRMG-main\Olden Era - Template Editor\GameData.json' -Raw | ConvertFrom-Json
$pool = $json.pools[0]
Write-Host "Name: $($pool.name)"
Write-Host "Groups count: $($pool.groups.Count)"
Write-Host "First group weight: $($pool.groups[0].weight)"
Write-Host "First group includeLists: $($pool.groups[0].includeLists -join ', ')"
