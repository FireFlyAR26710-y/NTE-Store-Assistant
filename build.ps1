$ErrorActionPreference = 'Stop'
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$compiler = Join-Path $framework 'csc.exe'
$sdk = Get-ChildItem 'C:\Program Files (x86)\Windows Kits\10\UnionMetadata\*\Windows.winmd' -ErrorAction SilentlyContinue |
    Where-Object { $_.Directory.Name -match '^\d' } |
    Sort-Object FullName -Descending | Select-Object -First 1 -ExpandProperty FullName
$facades = 'C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8\Facades'
if (!(Test-Path -LiteralPath $compiler) -or !$sdk -or !(Test-Path -LiteralPath $facades)) {
    throw '需要安装 .NET Framework 4.8 Developer Pack 和 Windows SDK。'
}
$source = Join-Path $PSScriptRoot 'src'
$output = Join-Path $PSScriptRoot 'dist'
New-Item -ItemType Directory -Force -Path $output | Out-Null
$arguments = @(
    '/nologo', '/target:winexe', '/platform:x64', '/optimize+',
    '/reference:System.Windows.Forms.dll', '/reference:System.Drawing.dll',
    '/reference:System.Web.Extensions.dll', '/reference:System.Xaml.dll',
    "/reference:$sdk",
    "/reference:$framework\System.Runtime.WindowsRuntime.dll",
    "/reference:$facades\System.Runtime.dll",
    "/reference:$facades\System.Threading.Tasks.dll",
    "/reference:$framework\WPF\WindowsBase.dll",
    "/reference:$framework\WPF\PresentationCore.dll",
    "/reference:$framework\WPF\PresentationFramework.dll",
    "/resource:$source\ModernWindow.xaml,NTEAssistant.ModernWindow.xaml",
    "/resource:$source\DefaultSettings.json,NTEAssistant.DefaultSettings.json",
    "/out:$output\NTEStoreAssistant.exe"
)
$arguments += Get-ChildItem -LiteralPath $source -Filter '*.cs' | Select-Object -ExpandProperty FullName
& $compiler @arguments
if ($LASTEXITCODE -ne 0) { throw '编译失败。' }
Write-Host "编译完成：$output\NTEStoreAssistant.exe"
