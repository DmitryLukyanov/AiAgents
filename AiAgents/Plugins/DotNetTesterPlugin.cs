using Microsoft.SemanticKernel;
using System.ComponentModel;
using Newtonsoft.Json.Schema;
using Newtonsoft.Json.Linq;

namespace AiAgents.Plugins
{
    public sealed class DotNetTesterPlugin(DotNetAppSettings settings)
    {
        [KernelFunction("TestGeneratedWorkflowJson"), Description(@"
Test a previouly created workflow.json")]
        [return: Description("Determine if the testing has been successfull.  If so, the response will be just: true. Any other text will contain an error details")]
        public async Task<string> TestApplication()
        {
            var schemaUrl = "https://schema.management.azure.com/providers/Microsoft.Logic/schemas/2016-06-01/workflowdefinition.json#";
            using HttpClient client = new HttpClient();
            string schemaJson = await client.GetStringAsync(schemaUrl);

            JSchema schema = JSchema.Parse(schemaJson);
            var content = File.ReadAllText(settings.FilePath);
            JObject jsonObject;
            try
            {
                jsonObject = JObject.Parse(content);
            }
            catch (Exception ex)
            {
                return $"The fileContent is not valid json document. Error: {ex.Message}";
            }

            bool isValid = jsonObject.IsValid(schema, errorMessages: out var errors);

            if (isValid)
            {
                Console.WriteLine("\n\n!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!Testing has been passed!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!\n\n");
                return true.ToString();
            }
            else
            {
                return $"The fileContent failed testing with errors: ['{string.Join("','", errors)}']";
            }
        }
    }
}
