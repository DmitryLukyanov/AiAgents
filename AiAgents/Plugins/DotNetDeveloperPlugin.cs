using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Diagnostics;

namespace AiAgents.Plugins
{
    public sealed class DotNetDeveloperPlugin(DotNetAppSettings settings)
    {
        private readonly string GeneratedCodeOutputDirectory = Path.Combine(settings.ProjectPath, "output_project");
        private readonly TimeSpan __timeout = TimeSpan.FromMinutes(2);

        [KernelFunction("CreateCsharpAppication"), Description("Create an empty .net/c# class library with provided projectName")]
        [return: Description("Determine if the creating has been successfull.  If so, the response will be just: yes. Any other text will contain an error details")]
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        public async Task<string> CreateNetAppAsync(KernelArguments arguments)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            using var process = new Process();
            var startInfo = new ProcessStartInfo
            {
                FileName = "dotnet.exe",
                Arguments = $"new classlib -f net8.0 -n {settings.ProjectName} -o output_project --force",
                WorkingDirectory = settings.ProjectPath,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            process.StartInfo = startInfo;
            process.Start();
            process.WaitForExit(__timeout);
            var output = await process.StandardOutput.ReadToEndAsync();
            if (output.Contains("was created successfully."))
            {
                return true.ToString();
            }
            else
            {
                return "Error: Creating an empty application has been failed";
            }
        }

        [KernelFunction("SaveCsharpContent"), Description("Save the generated c# method into file")]
        [return: Description("Determine if the saving has been successfull.  If so, the response will be just: yes. Any other text will contain an error details")]
        public async Task<string> DevelopContentOfTheApplication(KernelArguments arguments)
        {
            try
            {
                var content = arguments["content"];
                if (!Directory.Exists(settings.ProjectPath))
                {
                    return "Error: The empty project must be created before this step";
                }
                var class1 = Path.Combine(GeneratedCodeOutputDirectory, "class1.cs");
                if (!File.Exists(class1))
                {
                    return "Error: The empty project must be created before this step";
                }
                await File.WriteAllTextAsync(class1, content!.ToString());
                return true.ToString();
            }
            catch (Exception ex)
            {
                return $"Error: {ex.Message}";
            }
        }
    }
}