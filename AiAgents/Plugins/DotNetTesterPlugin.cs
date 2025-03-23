using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Diagnostics;

namespace AiAgents.Plugins
{
    public sealed class DotNetTesterPlugin(DotNetAppSettings settings)
    {
        private readonly string GeneratedCodeOutputDirectory = Path.Combine(settings.ProjectPath, "output_project");

        [KernelFunction("TestCsharpApplication"), Description("Test a previouly created class library via building project")]
        [return: Description("Determine if the testing has been successfull.  If so, the response will be just: yes. Any other text will contain an error details")]
        public async Task<string> TestApplication(KernelArguments arguments)
        {
            if (!Directory.Exists(settings.ProjectPath))
            {
                return "Error: The empty project must be created before this step";
            }
            var class1 = Path.Combine(GeneratedCodeOutputDirectory, "class1.cs");
            if (!File.Exists(class1))
            {
                return "Error: The empty project must be created before this step";
            }
            try
            {
                using var process = new Process();
                var startInfo = new ProcessStartInfo
                {
                    FileName = "dotnet.exe",
                    Arguments = $"build",
                    WorkingDirectory = GeneratedCodeOutputDirectory,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                process.StartInfo = startInfo;
                process.Start();
                process.WaitForExit(TimeSpan.FromMinutes(1));
                var output = await process.StandardOutput.ReadToEndAsync();
                if (output.Contains("Build succeeded"))
                {
                    return "Testing has been succeeded";
                }
                else
                {
                    return $"Failed: {settings.ProjectName} testing has been failed";
                }
            }
            catch (Exception ex)
            {
                return $"Failed testing: {settings.ProjectName} failed the test procedure with this error: {ex.Message}";
            }
        }
    }

}
