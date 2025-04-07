using AiAgents.Filters;
using AiAgents.Plugins;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
        var inputArgs = args?.SingleOrDefault() ?? @"
- Take the message from service bus with this connectionId: 'serviceBus-1'
- Send this message to another service bus with the connectionId: 'serviceBus-2'""";

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

        var settings = new DotNetAppSettings("../../../", Path.Combine("../../../", "GeneratedCode", "workflow.json"));
        kernel.Plugins.AddFromType<DotNetDeveloperPlugin>(pluginName: nameof(DotNetDeveloperPlugin), serviceProvider: CreatedServiceProvider(settings));
        kernel.Plugins.AddFromType<DotNetTesterPlugin>(pluginName: nameof(DotNetTesterPlugin), serviceProvider: CreatedServiceProvider(settings));
        kernel.AutoFunctionInvocationFilters.Add(new EarlyPluginChainTerminationFilter());

        var examples = Directory
            .EnumerateFiles(Path.Combine(settings.ProjectPath, "Examples"), "*.json", new EnumerationOptions() { MaxRecursionDepth = 3, RecurseSubdirectories = true })
            .Select(f => File.ReadAllText(f))
            .ToList();

        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        // Create the agents
        ChatCompletionAgent developerAgent =
             new()
             {
                 Name = DeveloperName,
                 Instructions = $@"
Create an Azure LogicApp workflow.json with the following content:
{inputArgs}

if the chat history already has records, modify the previously created 'fileContent' based on the errors in the history.

You can use the following workflow.json files as examples: ['{(string.Join("','", examples))}'] 
Always save the content of the created or updated workflow file in the workflow.json file. 
Use 'fileContent' variable name to put initialy generated content or updated. 
Also, put explanation and consideration into 'considerations' input variable. 
Put the content of the last message from {{{TesterName}}} agent into 'testOutput' input argument",

                 Kernel = kernel,
                 Arguments = new KernelArguments(
                    new PromptExecutionSettings()
                    {
                        FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
                    }),
                 LoggerFactory = loggerFactory
             };

        ChatCompletionAgent testerAgent =
            new()
            {
                Name = TesterName,
                Instructions = "Test the previously generated workflow.json. Never provide any parameters to plugins",
                Kernel = kernel,
                Arguments = new KernelArguments(
                    new PromptExecutionSettings()
                    {
                        FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(),
                    }),
                LoggerFactory = loggerFactory
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

        Always start with {{{DeveloperName}}}
        Always use output from {{{TesterName}}} as input for {{{DeveloperName}}}

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
              HistoryReducer = new ChatHistoryTruncationReducer(3)
          };


        // Define a kernel function for the selection strategy
        KernelFunction terminationFunction =
            AgentGroupChat.CreatePromptFunctionForStrategy(
                $$$"""
        Determine if the testing has been successfull.  If so, respond with a single word: yes
        Ensure that last step is always testing step.
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
                result.GetValue<string>()?.Equals("yes", StringComparison.OrdinalIgnoreCase) ?? false,
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
                ExecutionSettings = new()
                {
                    SelectionStrategy = selectionStrategy,
                    TerminationStrategy = terminationStrategy
                },
                LoggerFactory = loggerFactory
            };
        chat.AddChatMessage(new ChatMessageContent(AuthorRole.User, "Make sure that the generated workflow.json corresponds to azure logic app rules and can be then deployed to azure portal"));
        // invoke
        await foreach (ChatMessageContent response in chat.InvokeAsync())
        {
            Console.WriteLine("\n-----------------------------------------------------------");
            Console.WriteLine($"{response.Role}: {response.Content}");
            Console.WriteLine("============================================================\n");
        }
    }
}