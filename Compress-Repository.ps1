<#
.SYNOPSIS
Creates a lightweight zip archive of this repository.

.DESCRIPTION
Adds working-tree files that do not match the root or nested .gitignore files.
Git evaluates the ignore rules, including negated patterns. The .git directory
and ignored files are not added to the archive.

.PARAMETER OutputPath
The destination zip path. Relative paths are resolved from the current working
directory. The default is a zip named after the repository in the directory
containing this script.

.EXAMPLE
.\Compress-Repository.ps1

.EXAMPLE
.\Compress-Repository.ps1 -OutputPath .\artifacts\RoslynCSharpAnalysisRunner.zip
#>

[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [string] $OutputPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = [System.IO.Path]::GetFullPath($PSScriptRoot)
$repositoryName = 'RoslynCSharpAnalysisRunner'

$gitRoot = & git -C $repositoryRoot rev-parse --show-toplevel 2>$null
if ($LASTEXITCODE -ne 0) {
    throw "'$repositoryRoot' is not inside a Git repository."
}

$gitRoot = [System.IO.Path]::GetFullPath(($gitRoot | Select-Object -First 1))
if (-not [System.StringComparer]::OrdinalIgnoreCase.Equals($repositoryRoot, $gitRoot)) {
    throw "The script must be located at the repository root ('$gitRoot')."
}

if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $archivePath = Join-Path $repositoryRoot "$repositoryName.zip"
}
else {
    $archivePath = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutputPath)
}

$archivePath = [System.IO.Path]::GetFullPath($archivePath)
$archiveDirectory = [System.IO.Path]::GetDirectoryName($archivePath)
if (-not [System.IO.Directory]::Exists($archiveDirectory)) {
    [System.IO.Directory]::CreateDirectory($archiveDirectory) | Out-Null
}

# --exclude-per-directory makes Git evaluate every applicable .gitignore file
# while --cached and --others find versioned and non-ignored working-tree files.
$relativePaths = @(
    & git -C $repositoryRoot -c core.quotePath=false ls-files --cached --others --exclude-per-directory=.gitignore
)
if ($LASTEXITCODE -ne 0) {
    throw 'Git could not determine the files to archive.'
}

# Git normally keeps tracked files even when they later match an ignore rule.
# Remove those too so the archive follows every .gitignore rule literally.
$ignoredTrackedPaths = @(
    & git -C $repositoryRoot -c core.quotePath=false ls-files --cached --ignored --exclude-per-directory=.gitignore
)
if ($LASTEXITCODE -ne 0) {
    throw 'Git could not determine the ignored tracked files.'
}

if ($ignoredTrackedPaths.Count -gt 0) {
    $ignoredTrackedSet = [System.Collections.Generic.HashSet[string]]::new(
        [System.StringComparer]::Ordinal
    )
    foreach ($ignoredTrackedPath in $ignoredTrackedPaths) {
        $ignoredTrackedSet.Add($ignoredTrackedPath) | Out-Null
    }
    $relativePaths = @($relativePaths | Where-Object { -not $ignoredTrackedSet.Contains($_) })
}

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

$archiveStream = $null
$archive = $null
$fileCount = 0

try {
    $archiveStream = [System.IO.File]::Open(
        $archivePath,
        [System.IO.FileMode]::Create,
        [System.IO.FileAccess]::Write,
        [System.IO.FileShare]::None
    )
    $archive = [System.IO.Compression.ZipArchive]::new(
        $archiveStream,
        [System.IO.Compression.ZipArchiveMode]::Create,
        $false
    )

    foreach ($relativePath in $relativePaths) {
        if ([string]::IsNullOrWhiteSpace($relativePath)) {
            continue
        }

        $platformPath = $relativePath.Replace('/', [System.IO.Path]::DirectorySeparatorChar)
        $sourcePath = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot $platformPath))

        # Prevent a caller-selected archive inside the repository from adding
        # an older copy of itself.
        if ([System.StringComparer]::OrdinalIgnoreCase.Equals($sourcePath, $archivePath)) {
            continue
        }

        # A tracked file can be absent in a dirty working tree.
        if (-not [System.IO.File]::Exists($sourcePath)) {
            continue
        }

        $entryPath = "$repositoryName/$($relativePath.Replace('\', '/'))"
        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
            $archive,
            $sourcePath,
            $entryPath,
            [System.IO.Compression.CompressionLevel]::Optimal
        ) | Out-Null
        $fileCount++
    }
}
catch {
    if ($null -ne $archive) {
        $archive.Dispose()
        $archive = $null
    }
    if ($null -ne $archiveStream) {
        $archiveStream.Dispose()
        $archiveStream = $null
    }
    if ([System.IO.File]::Exists($archivePath)) {
        [System.IO.File]::Delete($archivePath)
    }
    throw
}
finally {
    if ($null -ne $archive) {
        $archive.Dispose()
    }
    if ($null -ne $archiveStream) {
        $archiveStream.Dispose()
    }
}

Write-Host "Created '$archivePath' with $fileCount files."
