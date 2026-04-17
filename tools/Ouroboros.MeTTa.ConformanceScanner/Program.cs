// <copyright file="Program.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Text;
using System.Text.Json;
using Ouroboros.MeTTa.ConformanceScanner.Inventory;
using Ouroboros.MeTTa.ConformanceScanner.Mismatch;
using MismatchRow = Ouroboros.MeTTa.ConformanceScanner.Mismatch.Mismatch;
using Ouroboros.MeTTa.ConformanceScanner.Patches;
using Ouroboros.MeTTa.ConformanceScanner.Reporting;
using Ouroboros.MeTTa.ConformanceScanner.Spec;

static string RepoRoot()
{
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    while (dir is not null)
    {
        if (File.Exists(Path.Combine(dir.FullName, "ouroboros-foundation", "src", "Ouroboros.Core", "Ouroboros.Core.csproj")))
        {
            return dir.FullName;
        }

        dir = dir.Parent;
    }

    return Directory.GetCurrentDirectory();
}

var root = RepoRoot();
var argsList = args.ToList();
var includeLateBound = argsList.Remove("--include-late-bound");
var writeArtifacts = argsList.Remove("--write-artifacts");

string? outputDirOverride = null;
int outputDirFlagIdx = argsList.IndexOf("--output-dir");
if (outputDirFlagIdx >= 0 && outputDirFlagIdx + 1 < argsList.Count)
{
    outputDirOverride = argsList[outputDirFlagIdx + 1];
    argsList.RemoveAt(outputDirFlagIdx + 1);
    argsList.RemoveAt(outputDirFlagIdx);
}

var snapshotPath = argsList.Count > 0
    ? argsList[0]
    : Path.Combine(root, ".planning", "milestones", "v32.0-metta-conformance", "reference", "stdlib.metta");

if (!File.Exists(snapshotPath))
{
    await Console.Error.WriteLineAsync($"[scanner] snapshot not found: {snapshotPath}").ConfigureAwait(false);
    return 2;
}

var source = await File.ReadAllTextAsync(snapshotPath).ConfigureAwait(false);
var parser = new StdlibMettaParser();
var parseResult = parser.Parse(source);

if (!parseResult.IsSuccess)
{
    await Console.Error.WriteLineAsync($"[scanner] parse failed: {parseResult.Error}").ConfigureAwait(false);
    return 3;
}

var spec = parseResult.Value;
await Console.Out.WriteLineAsync($"[scanner] parsed {spec.Operations.Count} operation names from {spec.TotalForms} top-level forms ({spec.UnparseableLines} unparseable)").ConfigureAwait(false);
await Console.Out.WriteLineAsync($"[scanner] sha256={spec.SourceSha256}").ConfigureAwait(false);

var collector = new InventoryCollector();
var inventory = collector.Collect(includeLateBound);
var mismatches = MismatchDetector.Detect(spec, inventory);
await Console.Out.WriteLineAsync($"[scanner] mismatches={mismatches.Count}").ConfigureAwait(false);

if (writeArtifacts)
{
    var milestoneDir = outputDirOverride ?? Path.Combine(root, ".planning", "milestones", "v32.0-metta-conformance");
    Directory.CreateDirectory(milestoneDir);

    var gapPath = Path.Combine(milestoneDir, "gap-report.md");
    await GapReportWriter.WriteAsync(gapPath, spec, inventory, mismatches).ConfigureAwait(false);
    await Console.Out.WriteLineAsync($"[scanner] wrote {gapPath}").ConfigureAwait(false);

    var proposals = PatchEmitter.Emit(mismatches);
    var patchPath = Path.Combine(milestoneDir, "patch-proposals.json");
    var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
    await File.WriteAllTextAsync(
        patchPath,
        JsonSerializer.Serialize(proposals, jsonOptions),
        Encoding.UTF8).ConfigureAwait(false);
    await Console.Out.WriteLineAsync($"[scanner] wrote {patchPath}").ConfigureAwait(false);

    var specCount = spec.Operations.Count;
    var missing = mismatches.Count(static m => m.Kind == MismatchKind.MissingInEngine);
    var ratio = specCount == 0 ? 0.0 : Math.Clamp(1.0 - (missing / (double)specCount), 0.0, 1.0);
    var baselineObj = new
    {
        GeneratedAt = DateTimeOffset.UtcNow,
        SpecSha256 = spec.SourceSha256,
        SpecOperationCount = specCount,
        EngineDistinctNameCount = inventory.AllNames.Count(),
        MismatchCounts = CountKinds(mismatches),
        ConformanceProxy = ratio,
    };
    var baselinePath = Path.Combine(milestoneDir, "baseline.json");
    await File.WriteAllTextAsync(
        baselinePath,
        JsonSerializer.Serialize(baselineObj, jsonOptions),
        Encoding.UTF8).ConfigureAwait(false);
    await Console.Out.WriteLineAsync($"[scanner] wrote {baselinePath}").ConfigureAwait(false);
}

return 0;

static Dictionary<string, int> CountKinds(IReadOnlyList<MismatchRow> mismatches)
{
    var d = new Dictionary<string, int>(StringComparer.Ordinal);
    foreach (var k in Enum.GetValues<MismatchKind>())
    {
        d[k.ToString()] = 0;
    }

    foreach (var m in mismatches)
    {
        d[m.Kind.ToString()] = d[m.Kind.ToString()] + 1;
    }

    return d;
}
