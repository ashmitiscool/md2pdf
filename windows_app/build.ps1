$ErrorActionPreference = "Stop"

$AppDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Push-Location $AppDir

try {
    $CscPath = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
    if (-not (Test-Path $CscPath)) {
        throw "Could not find 64-bit C# compiler at $CscPath"
    }

    # Ensure app.ico exists
    if (-not (Test-Path (Join-Path $AppDir "app.ico"))) {
        Write-Host "Generating app.ico..."
        python (Join-Path $AppDir "generate_icon.py")
        if ($LASTEXITCODE -ne 0) {
            throw "Icon generation failed."
        }
    }

    # Ensure WebView2 dependencies are present in windows_app\
    $WebViewDeps = @(
        @{ Src = "C:\Program Files\Listary\Microsoft.Web.WebView2.Core.dll"; Dst = "Microsoft.Web.WebView2.Core.dll" },
        @{ Src = "C:\Program Files\Listary\Microsoft.Web.WebView2.WinForms.dll"; Dst = "Microsoft.Web.WebView2.WinForms.dll" },
        @{ Src = "C:\Program Files\Listary\runtimes\win-x64\native\WebView2Loader.dll"; Dst = "WebView2Loader.dll" }
    )
    foreach ($dep in $WebViewDeps) {
        $dstPath = Join-Path $AppDir $dep.Dst
        if (-not (Test-Path $dstPath)) {
            if (Test-Path $dep.Src) {
                Write-Host "Copying $($dep.Dst)..."
                Copy-Item $dep.Src $dstPath -Force
            } else {
                throw "Missing required WebView2 dependency: $($dep.Dst)"
            }
        }
    }

    $OutExe = Join-Path $AppDir "md2pdf.exe"
    $SrcCs = Join-Path $AppDir "src\Program.cs"
    $IconFile = Join-Path $AppDir "app.ico"
    $CoreDll = Join-Path $AppDir "Microsoft.Web.WebView2.Core.dll"
    $WinFormsDll = Join-Path $AppDir "Microsoft.Web.WebView2.WinForms.dll"

    Write-Host "Compiling md2pdf.exe (x64)..."
    & $CscPath `
        /nologo `
        /target:winexe `
        /platform:x64 `
        /optimize+ `
        /win32icon:"$IconFile" `
        /out:"$OutExe" `
        /r:System.dll `
        /r:System.Core.dll `
        /r:System.Drawing.dll `
        /r:System.Windows.Forms.dll `
        /r:System.Web.Extensions.dll `
        /r:"$CoreDll" `
        /r:"$WinFormsDll" `
        "$SrcCs"

    if ($LASTEXITCODE -ne 0) {
        throw "csc.exe failed with exit code $LASTEXITCODE"
    }

    $exeInfo = Get-Item $OutExe
    Write-Host "Build succeeded: $($exeInfo.FullName) ($($exeInfo.Length) bytes)"
}
finally {
    Pop-Location
}
