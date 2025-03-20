using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Diagnostics;

namespace AiAgents
{
    public sealed class DotNetPlugin
    {
        //private readonly IPromptTemplateFactory _promptTemplateFactory = new KernelPromptTemplateFactory();
        // TODO: take this as arguments
        private const string GeneratedCodeDirectory = "../../../GeneratedCode";
        private readonly string GeneratedCodeOutputDirectory = Path.Combine(GeneratedCodeDirectory, "output_project");
        private const string ClassLibraryName = "class_lib";

        [KernelFunction("CreateCsharpApp"), Description("Create an empty .net/c# class library")]
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        public async Task<string> CreateNetAppAsync(KernelArguments arguments)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            // var renderedPrompt = await _promptTemplateFactory.Create(new PromptTemplateConfig(template: prompt)).RenderAsync(kernel, arguments);
            using var process = new Process();
            var startInfo = new ProcessStartInfo
            {
                FileName = "dotnet.exe",
                Arguments = $"new classlib -f net8.0 -n {ClassLibraryName} -o output_project --force",
                WorkingDirectory = GeneratedCodeDirectory
            };
            process.StartInfo = startInfo;
            process.Start();

            return $"{ClassLibraryName} has been created";
        }

        [KernelFunction("SaveCsharpContent"), Description("Save the generated c# method into file")]
        public async Task<string> DevelopContentOfTheApplication(KernelArguments arguments)
        {
            var content = arguments["content"];
            if (!Directory.Exists(GeneratedCodeDirectory))
            {
                return "Error: The empty project must be created before this step";
            }
            var class1 = Path.Combine(GeneratedCodeOutputDirectory, "class1.cs");
            if (!File.Exists(class1))
            {
                return "Error: The empty project must be created before this step";
            }
            File.WriteAllText(class1, content!.ToString());
            return "The created method has been successfully saved";
        }

        [KernelFunction("TestCsharpApplication"), Description("Test a previouly created class library via building project")]
        public async Task<string> TestApplication(KernelArguments arguments)
        {
            if (!Directory.Exists(GeneratedCodeDirectory))
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
                    return $"Building of {ClassLibraryName} has been successfully tested";
                }
                else
                {
                    return $"Failed: {ClassLibraryName} testing has been failed";
                }
            }
            catch (Exception ex) 
            {
                return $"Failed testing: {ClassLibraryName} failed the test procedure with this error: {ex.Message}";
            }
        }
    }
}
