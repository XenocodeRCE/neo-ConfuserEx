param(
    [Parameter(Mandatory)] [string]$FileA,
    [Parameter(Mandatory)] [string]$FileB,
    [string]$OutputFile = "",
    [string]$DnlibPath = "D:\dev\confuser-lab\lab-tools\bin\de4dot-cex\bin\dnlib.dll"
)

$ErrorActionPreference = "Continue"

if (-not (Test-Path $FileA)) { throw "FileA not found: $FileA" }
if (-not (Test-Path $FileB)) { throw "FileB not found: $FileB" }

$hashA = (Get-FileHash $FileA -Algorithm SHA256).Hash
$hashB = (Get-FileHash $FileB -Algorithm SHA256).Hash
$sizeA = (Get-Item $FileA).Length
$sizeB = (Get-Item $FileB).Length

$result = @{
    fileA = @{ path = $FileA; hash = $hashA; size = $sizeA }
    fileB = @{ path = $FileB; hash = $hashB; size = $sizeB }
    rawIdentical = ($hashA -eq $hashB)
    sizeDelta = $sizeB - $sizeA
    structural = @{}
}

# Dnlib-based structural comparison
if ($DnlibPath -and (Test-Path $DnlibPath)) {
    try {
        $compareCode = @'
using System;
using System.Collections.Generic;
using System.Linq;
using dnlib.DotNet;

public static class StructComparer {
    public static string Compare(string pathA, string pathB) {
        var result = new Dictionary<string, object>();
        try {
            using (var modA = ModuleDefMD.Load(pathA))
            using (var modB = ModuleDefMD.Load(pathB)) {
                var typesA = modA.GetTypes().ToList();
                var typesB = modB.GetTypes().ToList();

                result["typeCountA"] = typesA.Count;
                result["typeCountB"] = typesB.Count;
                result["typeCountDiff"] = typesB.Count - typesA.Count;

                result["methodCountA"] = typesA.Sum(t => t.Methods.Count);
                result["methodCountB"] = typesB.Sum(t => t.Methods.Count);
                result["methodCountDiff"] = result["methodCountB"] - result["methodCountA"];

                result["fieldCountA"] = typesA.Sum(t => t.Fields.Count);
                result["fieldCountB"] = typesB.Sum(t => t.Fields.Count);

                result["resourceCountA"] = modA.Resources.Count;
                result["resourceCountB"] = modB.Resources.Count;

                // Name obfuscation: count short names
                int shortNamesA = typesA.Count(t => t.Name.Length <= 3 && !t.IsGlobalModuleType);
                int shortNamesB = typesB.Count(t => t.Name.Length <= 3 && !t.IsGlobalModuleType);
                result["shortTypeNamesA"] = shortNamesA;
                result["shortTypeNamesB"] = shortNamesB;

                // String literals in #US
                result["usStringsA"] = modA.USStream?.StringsStream?.Count ?? 0;
                result["usStringsB"] = modB.USStream?.StringsStream?.Count ?? 0;

                // KoiVM methods
                int koiA = typesA.Sum(t => t.Methods.Count(m =>
                    m.Body?.Instructions.Any(i =>
                        i.Operand is IMethod im &&
                        (im.DeclaringType?.FullName?.Contains("KoiVM") == true)) == true));
                int koiB = typesB.Sum(t => t.Methods.Count(m =>
                    m.Body?.Instructions.Any(i =>
                        i.Operand is IMethod im &&
                        (im.DeclaringType?.FullName?.Contains("KoiVM") == true)) == true));
                result["koiMethodsA"] = koiA;
                result["koiMethodsB"] = koiB;

                // Structural fingerprint
                result["fingerprintA"] = ComputeFingerprint(modA);
                result["fingerprintB"] = ComputeFingerprint(modB);
                result["fingerprintsDiffer"] = (result["fingerprintA"] != result["fingerprintB"]);
            }
        } catch (Exception ex) {
            result["error"] = ex.Message;
        }
        return Newtonsoft.Json.JsonConvert.SerializeObject(result);
    }

    static string ComputeFingerprint(ModuleDefMD mod) {
        var types = mod.GetTypes().ToList();
        int tc = types.Count;
        int mc = types.Sum(t => t.Methods.Count);
        int rc = mod.Resources.Count;
        string ep = mod.EntryPoint?.FullName ?? "none";
        var raw = $"{mod.Assembly?.Name}|{tc}|{mc}|{rc}|{ep}";
        var hash = System.Security.Cryptography.SHA256.Create()
            .ComputeHash(System.Text.Encoding.UTF8.GetBytes(raw));
        return BitConverter.ToString(hash).Replace("-","").Substring(0,16);
    }
}
'@
        $newtonsoftPath = "$(Split-Path $DnlibPath)\Newtonsoft.Json.dll"
        $refs = @($DnlibPath)
        if (Test-Path $newtonsoftPath) { $refs += $newtonsoftPath }
        
        Add-Type -TypeDefinition $compareCode -ReferencedAssemblies $refs -ErrorAction Stop
        $json = [StructComparer]::Compare($FileA, $FileB)
        $structResult = $json | ConvertFrom-Json
        $result.structural = $structResult
    } catch {
        $result.structural.error = "Dnlib comparison failed: $_"
    }
}

$result.timestamp = (Get-Date -Format "o")

if ($OutputFile) {
    $result | ConvertTo-Json -Depth 4 | Set-Content $OutputFile -Encoding UTF8
}

$result | ConvertTo-Json -Depth 4
