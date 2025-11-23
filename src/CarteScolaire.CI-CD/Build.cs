using System;
using Nuke.Common;
using Nuke.Common.CI.GitHubActions;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Utilities.Collections;
using static Nuke.Common.Tools.DotNet.DotNetTasks;



[DotNetVerbosityMapping]
[GitHubActions(
    "continuous-integration",
    GitHubActionsImage.UbuntuLatest,
    OnPushBranches = ["main", "feature/*"],
    InvokedTargets = [nameof(Test), nameof(PublishApp)],
    FetchDepth = 0)]
class Build : NukeBuild
{
    public static int Main() => Execute<Build>(x => x.Default);

    #region Configuration Properties

    // Automatically injects the solution model from the .sln file.
    [Solution] readonly Solution Solution;

    // Defines the configuration used for the build (e.g., Debug, Release).
    [Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)")]
    readonly Configuration Configuration = IsLocalBuild ? Configuration.Debug : Configuration.Release;

    // Directory to output artifacts like packages or published files.
    AbsolutePath ArtifactsDirectory => RootDirectory / "artifacts";

    // REQUIRED: Set the Runtime Identifier for the EXE (e.g., win-x64, linux-arm64)
    // This is mandatory for creating a self-contained executable.
    [Parameter("Runtime Identifier for self-contained deployment (e.g., win-x64, linux-x64)")]
    readonly string RuntimeIdentifier = "win-x64"; // Defaulting to Windows 64-bit EXE

    // REQUIRED: Specify the main application project to publish
    [Parameter("The name of the console or desktop application project.")]
    readonly string AppProjectName = "CarteScolaire"; // Placeholder: Update this to your project name!

    #endregion

    #region Targets (The Build Steps)

    // 1. Cleanup Phase
    // Deletes temporary files and output directories to ensure a fresh build.
    Target Clean => _ => _
        .Before(Restore)
        .Executes(() =>
        {
            // Clean solution output directories (bin, obj)
            DotNetClean(s => s
                .SetConfiguration(Configuration)
                .SetProject(Solution));

            // Clean artifacts directory
            ArtifactsDirectory.CreateOrCleanDirectory();
        });

    // 2. Dependency Restoration
    // Restores NuGet packages for all projects in the solution.
    Target Restore => _ => _
        .Executes(() => DotNetRestore(s => s.SetProjectFile(Solution)));

    // 3. Compilation / Build Phase (The core CI step)
    // Compiles the entire solution.
    Target Compile => _ => _
        .DependsOn(Restore)
        .Executes(() =>
        {
            DotNetBuild(s => s
                .SetProjectFile(Solution)
                .SetConfiguration(Configuration)
                .EnableNoRestore()); // Don't re-restore packages
        });

    // 4. Testing Phase (Critical for CI)
    // Runs all unit and integration tests. The build will fail if any test fails.
    Target Test => _ => _
        .DependsOn(Compile)
        .Executes(() =>
        {
            // Find all projects ending with '.Tests' and run them
            Solution.GetAllProjects("*Tests*").ForEach(project =>
            {
                DotNetTest(s => s
                    .SetProjectFile(project)
                    .SetConfiguration(Configuration)
                    .SetLoggers("trx")// Output test results in a standard format
                    .EnableNoBuild() // No need to re-compile tests
                    .EnableNoRestore());
            });
        });

    // 5. Application Publication Phase (The CD step for EXE)
    // Publishes a self-contained application, resulting in an executable (.exe).
    Target PublishApp => _ => _
        .DependsOn(Test)
        .Produces(ArtifactsDirectory / "publish-app")
        .Executes(() =>
        {
            var appProject = Solution.GetProject(AppProjectName);

            if (appProject is null)
            {
                throw new InvalidOperationException($"Project '{AppProjectName}' not found in solution.");
            }

            //ControlFlow.NotNull(appProject, $"Project '{AppProjectName}' not found in solution.");

            DotNetPublish(s => s
                .SetProject(appProject)
                .SetConfiguration(Configuration)
                .SetOutput(ArtifactsDirectory / "publish-app")
                .SetRuntime(RuntimeIdentifier) // THIS IS KEY to generating the native executable
                .SetSelfContained(true)        // Generates an independent, runnable folder structure
                .EnablePublishSingleFile()     // Optional: Packages output into a single EXE file (requires .NET Core 3.0+)
                .EnableNoRestore());
        });

    // 6. Default Target
    // The target executed if no specific target is specified (useful for quick local runs).
    Target Default => _ => _.DependsOn(Test); // Running tests is usually a good default for local development

    #endregion
    //PublishApp --configuration Release --RuntimeIdentifier win-x64 -- for debug purposes
}
