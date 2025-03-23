using AiAgents.Plugins;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Agents.Chat;
using Microsoft.SemanticKernel.ChatCompletion;

internal class Program
{
    private static async Task Main(string[] args)
    {
        // Define the agent names for use in the function template
        const string DeveloperName = "Developer";
        const string TesterName = "Tester";

        var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? throw new InvalidOperationException("OPENAI_API_KEY must be configured");

        var kernelBuilder = Kernel.CreateBuilder();
        kernelBuilder.AddOpenAIChatCompletion("gpt-4o-mini", apiKey);
        var kernel = kernelBuilder.Build();

        IServiceProvider CreatedServiceProvider<T>(T value) where T : class
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton<T>(value);
            return new DefaultServiceProviderFactory().CreateServiceProvider(serviceCollection);
        };

        var settings = new DotNetAppSettings("../../../GeneratedCode", "class_lib");
        kernel.Plugins.AddFromType<DotNetDeveloperPlugin>(pluginName: nameof(DotNetDeveloperPlugin), serviceProvider: CreatedServiceProvider(settings));
        kernel.Plugins.AddFromType<DotNetTesterPlugin>(pluginName: nameof(DotNetTesterPlugin), serviceProvider: CreatedServiceProvider(settings));

        // Create the agents
        ChatCompletionAgent developerAgent =
             new()
             {
                 Name = DeveloperName,
                 Instructions = "Create a C# project. Then develop method that calculate factorial of provided number. Save the created method in the csharp file",
                 Kernel = kernel,
                 Arguments = new KernelArguments(
                    new PromptExecutionSettings()
                    {
                        FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(),
                    })
             };

        ChatCompletionAgent testerAgent =
            new()
            {
                Name = TesterName,
                Instructions = "Test that the provided method can calcualte the factorial of 10",
                Kernel = kernel,
                Arguments = new KernelArguments(
                    new PromptExecutionSettings()
                    {
                        FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(),
                    })
            };

        // Define a kernel function for the selection strategy
#pragma warning disable SKEXP0110 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
        KernelFunction selectionFunction =
            AgentGroupChat.CreatePromptFunctionForStrategy(
                $$$"""
        Determine which participant takes the next turn in a conversation based on the the most recent participant.
        State only the name of the participant to take the next turn.
        No participant should take more than one turn in a row.

        Choose only from these participants:
        - {{{TesterName}}}
        - {{{DeveloperName}}}

        Always follow these rules when selecting the next participant:
        - After {{{DeveloperName}}}, it is {{{TesterName}}}'s turn.
        - After {{{TesterName}}}, it is {{{DeveloperName}}}'s turn.

        History:
        {{$history}}
        """,
                safeParameterNames: "history");

        // Define the selection strategy
#pragma warning disable SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
        KernelFunctionSelectionStrategy selectionStrategy =
          new(selectionFunction, kernel)
          {
              // Always start with the writer agent.
              InitialAgent = developerAgent,
              // Parse the function response.
              ResultParser = (result) => result.GetValue<string>() ?? DeveloperName,
              // The prompt variable name for the history argument.
              HistoryVariableName = "history",
              // Save tokens by not including the entire history in the prompt
              HistoryReducer = new ChatHistoryTruncationReducer(3),
          };


        // Define a kernel function for the selection strategy
        KernelFunction terminationFunction =
            AgentGroupChat.CreatePromptFunctionForStrategy(
                $$$"""
        Determine if the testing has been successfull.  If so, respond with a single word: yes

        History:
        {{$history}}
        """,
                safeParameterNames: "history");
        // Define the termination strategy
        KernelFunctionTerminationStrategy terminationStrategy =
          new(terminationFunction, kernel)
          {
              // Only the reviewer may give approval.
              Agents = [testerAgent],
              // Parse the function response.
              ResultParser = (result) =>
                result.GetValue<string>()?.Contains("yes", StringComparison.OrdinalIgnoreCase) ?? false,
              // The prompt variable name for the history argument.
              HistoryVariableName = "history",
              // Save tokens by not including the entire history in the prompt
              HistoryReducer = new ChatHistoryTruncationReducer(1),
              // Limit total number of turns no matter what
              MaximumIterations = 10,
          };
#pragma warning restore SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

        // Create a chat using the defined selection strategy.
        AgentGroupChat chat =
            new(developerAgent, testerAgent)
            {
                ExecutionSettings = new() { SelectionStrategy = selectionStrategy, TerminationStrategy = terminationStrategy }
            };
        chat.AddChatMessage(new ChatMessageContent(AuthorRole.Developer, "Make sure that the generated code follow all based practices"));
        // invoke
        await foreach (ChatMessageContent response in chat.InvokeAsync())
        {
            Console.WriteLine("\n-----------------------------------------------------------");
            Console.WriteLine($"{response.Role}: {response.Content}");
            Console.WriteLine("============================================================\n");
        }
    }
}