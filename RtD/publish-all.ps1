$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
$project = Join-Path $scriptDir "RtD.csproj"
$outputBase = Join-Path $scriptDir "publish"

# Self-contained targets
$sc_rids = @(
    "linux-x64",
    "osx-x64",
    "win-x64",
    "win-x86"
)

# Framework-dependent targets
$fd_rids = @(
    "linux-x64",
    "osx-x64",
    "win-x64",
    "win-x86",
    "portable"
)

function ZipFolder($folder) {
    Write-Host "Zipping $folder..."

    $zipPath = "$folder.zip"
    if (Test-Path $zipPath) { Remove-Item $zipPath -Force }

    Compress-Archive -Path "$folder\*" -DestinationPath $zipPath
}

Write-Host "Cleaning old .zip archives..."
Get-ChildItem "$outputBase" -Filter *.zip -Recurse | Remove-Item -Force -ErrorAction SilentlyContinue

foreach ($rid in $sc_rids) {
    $folder = "$outputBase\$($rid)_sc"
    Write-Host "Publishing self-contained for RID: $rid to folder: $folder"

    dotnet publish $project `
        -c Release `
        -r $rid `
        --self-contained `
        -o $folder `
        "/p:PublishSingleFile=true" `
        "/p:PublishTrimmed=true" `
        "/p:DebugType=None" `
        "/p:DebugSymbols=false"

    if ($LASTEXITCODE -ne 0) {
        Write-Error "dotnet publish failed for RID $rid (self-contained). Exiting."
        exit 1
    }

    Get-ChildItem $folder -Filter *.pdb -Recurse | Remove-Item -Force -ErrorAction SilentlyContinue
    
    ZipFolder $folder
}

foreach ($rid in $fd_rids) {
    if ($rid -eq "portable") {
        $folder = "$outputBase\portable"
        Write-Host "Publishing framework-dependent portable to folder: $folder"

        dotnet publish $project `
            -c Release `
            --self-contained false `
            -o $folder `
            "/p:PublishSingleFile=true" `
            "/p:DebugType=None" `
            "/p:DebugSymbols=false"
        
        if ($LASTEXITCODE -ne 0) {
            Write-Error "dotnet publish failed for portable (framework-dependent). Exiting."
            exit 1
        }
        
        Get-ChildItem $folder -Filter *.pdb -Recurse | Remove-Item -Force -ErrorAction SilentlyContinue

        ZipFolder $folder
    }
    else {
        $folder = "$outputBase\$($rid)_fd"
        Write-Host "Publishing framework-dependent for RID: $rid to folder: $folder"

        dotnet publish $project `
            -c Release `
            -r $rid `
            --self-contained false `
            -o $folder `
            "/p:PublishSingleFile=true" `
            "/p:DebugType=None" `
            "/p:DebugSymbols=false"
        
        if ($LASTEXITCODE -ne 0) {
            Write-Error "dotnet publish failed for RID $rid (framework-dependent). Exiting."
            exit 1
        }
        
        Get-ChildItem $folder -Filter *.pdb -Recurse | Remove-Item -Force -ErrorAction SilentlyContinue

        ZipFolder $folder
    }
}
