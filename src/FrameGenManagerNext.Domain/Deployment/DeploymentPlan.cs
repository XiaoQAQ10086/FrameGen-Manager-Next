namespace FrameGenManagerNext.Domain.Deployment;

public sealed record DeploymentPlan(
    string FamilyId,
    string PackId,
    string PackVersion,
    string TargetDirectory,
    IReadOnlyList<DeploymentFilePlan> Files,
    IReadOnlyDictionary<string, string> Options);
