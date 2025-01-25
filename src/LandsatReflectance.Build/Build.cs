using System;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Nuke.Common;
using Nuke.Common.CI;
using Nuke.Common.CI.GitHubActions;
using Nuke.Common.Execution;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.Docker;
using Nuke.Common.Utilities.Collections;

using static Nuke.Common.EnvironmentInfo;
using static Nuke.Common.IO.PathConstruction;

[GitHubActions(
    "Build and Deploy (C# Nuke)",
    GitHubActionsImage.UbuntuLatest,
    ImportSecrets = [ 
        nameof(DockerHubPassword), 
        nameof(DockerHubUsername), 
        nameof(LandsatApiRenderDeployHook), 
        nameof(LandsatUiRenderDeployHook) ],
    On = [ GitHubActionsTrigger.Push ])]
class Build : NukeBuild
{
    [Parameter] [Secret] readonly string DockerHubPassword;
    [Parameter] [Secret] readonly string DockerHubUsername;
    [Parameter] [Secret] readonly string LandsatApiRenderDeployHook;
    [Parameter] [Secret] readonly string LandsatUiRenderDeployHook;
    
    /// Support plugins are available for:
    ///   - JetBrains ReSharper        https://nuke.build/resharper
    ///   - JetBrains Rider            https://nuke.build/rider
    ///   - Microsoft VisualStudio     https://nuke.build/visualstudio
    ///   - Microsoft VSCode           https://nuke.build/vscode

    public static int Main () => Execute<Build>(x => x.Deploy);
    
    [Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)")]
    readonly Configuration Configuration = IsLocalBuild ? Configuration.Debug : Configuration.Release;

    private const string UiDockerImageTag = "chronoalpha/landsat_ui";
    private const string ApiDockerImageTag = "chronoalpha/fs_landsat_api";

    public Target BuildUiDockerImage => _ => _
        .Executes(() =>
        {
            DockerTasks.DockerBuild(settings => settings
                .SetPath(".")
                .SetFile("./src/LandsatReflectance.UI/Dockerfile")
                .SetTag(UiDockerImageTag));
        });
    
    public Target PublishUiDockerImage => _ => _
        .DependsOn(BuildUiDockerImage)
        .Executes(() =>
        {
            var dockerHubUsername = Environment.GetEnvironmentVariable("DOCKER_HUB_USERNAME") 
                                    ?? DockerHubUsername 
                                    ?? string.Empty;
            var dockerHubPassword = Environment.GetEnvironmentVariable("DOCKER_HUB_PASSWORD") 
                                    ?? DockerHubPassword 
                                    ?? string.Empty;
            using var dockerLoginProcess = Process.Start(new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = $"login --username {dockerHubUsername} --password {dockerHubPassword}",
                RedirectStandardOutput = true,
                CreateNoWindow = true
            });

            if (dockerLoginProcess is not null)
            {
                using var stream = dockerLoginProcess.StandardOutput;
                Console.WriteLine(stream.ReadToEnd());
            }
            
            
            using var dockerPushProcess = Process.Start(new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = $"push {UiDockerImageTag}",
                RedirectStandardOutput = true,
                CreateNoWindow = true
            });

            if (dockerPushProcess is not null)
            {
                using var stream = dockerPushProcess.StandardOutput;
                Console.WriteLine(stream.ReadToEnd());
            }
        });
    
    
    
    public Target BuildDockerApiImage => _ => _
        .Executes(() =>
        {
            DockerTasks.DockerBuild(settings => settings
                .SetPath(".")
                .SetFile("./src/LandsatReflectance.Api/Dockerfile")
                .SetTag(ApiDockerImageTag));
        });
    
    public Target PublishApiDockerImage => _ => _
        .DependsOn(BuildDockerApiImage)
        .Executes(() =>
        {
            var dockerHubUsername = Environment.GetEnvironmentVariable("DOCKER_HUB_USERNAME") 
                                    ?? DockerHubUsername 
                                    ?? string.Empty;
            var dockerHubPassword = Environment.GetEnvironmentVariable("DOCKER_HUB_PASSWORD") 
                                    ?? DockerHubPassword 
                                    ?? string.Empty;
            using var dockerLoginProcess = Process.Start(new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = $"login --username {dockerHubUsername} --password {dockerHubPassword}",
                RedirectStandardOutput = true,
                CreateNoWindow = true
            });

            if (dockerLoginProcess is not null)
            {
                using var stream = dockerLoginProcess.StandardOutput;
                Console.WriteLine(stream.ReadToEnd());
            }
            
            
            using var dockerPushProcess = Process.Start(new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = $"push {ApiDockerImageTag}",
                RedirectStandardOutput = true,
                CreateNoWindow = true
            });

            if (dockerPushProcess is not null)
            {
                using var stream = dockerPushProcess.StandardOutput;
                Console.WriteLine(stream.ReadToEnd());
            }
        });
    
    
    
    public Target Deploy => _ => _
        .DependsOn(PublishApiDockerImage, PublishUiDockerImage)
        .Executes(async () =>
        {
            var uiDeployHook = Environment.GetEnvironmentVariable("LANDSAT_UI_RENDER_DEPLOY_HOOK") 
                               ?? LandsatUiRenderDeployHook 
                               ?? string.Empty;
            var apiDeployHook = Environment.GetEnvironmentVariable("LANDSAT_API_RENDER_DEPLOY_HOOK")
                                ?? LandsatApiRenderDeployHook 
                                ?? string.Empty;

            
            Console.WriteLine($"UI Deploy Hook: \t\"{uiDeployHook}\"");
            var uiHttpClient = new HttpClient();
            var uiDeployRequest = new HttpRequestMessage(HttpMethod.Post, uiDeployHook);
            var uiDeployResponse = uiHttpClient.SendAsync(uiDeployRequest).Result;
            Console.WriteLine(uiDeployResponse.Content.ReadAsStringAsync().Result);

            Console.WriteLine($"\nAPI Deploy Hook:\t\"{apiDeployHook}\"");
            var apiHttpClient = new HttpClient();
            var apiDeployRequest = new HttpRequestMessage(HttpMethod.Post, apiDeployHook);
            var apiDeployResponse = apiHttpClient.SendAsync(apiDeployRequest).Result;
            Console.WriteLine(apiDeployResponse.Content.ReadAsStringAsync().Result);
            
            return Task.CompletedTask;
        });
}
