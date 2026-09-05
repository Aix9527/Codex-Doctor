$ErrorActionPreference='Stop'
$module=Join-Path $PSScriptRoot '..\lib\Diagnosis.psm1'
$text=Get-Content $module -Raw

function Assert-Contains([string]$Text,[string]$Needle,[string]$Message){
    if(-not $Text.Contains($Needle)){throw $Message}
}

# Under Set-StrictMode -Version Latest, a pipeline that emits exactly one
# string is a scalar and does not have a Count property in Windows PowerShell 5.1.
# Both Git and npm proxy collectors must force the filtered result back to an array.
Assert-Contains $text '$vals=@(@($http,$https)|Where-Object{$_})' 'Git proxy values must be array-wrapped before using .Count.'
Assert-Contains $text '$vals=@(@($proxy,$httpsProxy)|Where-Object{$_})' 'npm proxy values must be array-wrapped before using .Count.'

Write-Host 'Diagnosis scalar Count regression tests passed.'
