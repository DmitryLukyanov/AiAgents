using Microsoft.SemanticKernel;
using System.ComponentModel;

namespace AiAgents.Plugins
{
    public sealed class DotNetDeveloperPlugin(DotNetAppSettings settings)
    {
        [KernelFunction("SaveAzureLogicAppJsonFile"), Description("Save the generated workflow.json file")]
        [return: Description("Determine if the saving has been successfull.  If so, the response will be just: yes. Any other text will contain an error details")]
        public async Task<string> SaveTheGeneratedWorkflowJson(
            [Description("The list of consideration used to generate workflow.json content")] string considerations,
            [Description("The workflow.json content")] string fileContent,
            [Description("The $history value")] string testOutput)
        {
            try
            {
                Console.WriteLine("*******************Developer plugin***************************");
                Console.WriteLine($"The testing output: {testOutput}");
                Console.WriteLine($"Considiration: {considerations}");
                Console.WriteLine($"Generated workflow.json: {fileContent}");
                Console.WriteLine(fileContent);
                await File.WriteAllTextAsync(settings.FilePath, fileContent!.ToString());
                Console.WriteLine("*******************Developer plugin completed*****************");
                return "The fileContent has been saved";
            }
            catch (Exception ex)
            {
                return $"Error: {ex.Message}";
            }
        }
    }
}