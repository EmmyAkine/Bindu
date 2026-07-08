using Bindu.Grpc;

namespace Bindu_csharp_sdk.src {
    public class BindufyInit : IDisposable, IAsyncDisposable {
        private CoreLauncher? _launcher;
        private RegistrationResult? _registrationResult;
        private HeartbeatService? _heartbeatService;
        private GrpcServer? _grpcServer;
        private GrpcClient? _grpcClient;
        public class RegistrationResult(string agentId, string did, string agentUrl) {
            public string? AgentId { get; } = agentId;
            public string? Did { get; } = did;
            public string? AgentUrl { get; } = agentUrl;
        }

        public async Task<RegistrationResult> Bindufy(AgentConfig config, Func<IReadOnlyList<ChatMessage>, Task<string>> handler) {
            
            //TODO: Verify the config files if needed before starting up all the server
            
            var launcher = new CoreLauncher();
            var grpcServer = new GrpcServer(config.GrpcCallbackPort);
            var grpcClient = new GrpcClient();

            await launcher.LaunchBinduServer();
            await grpcServer.StartServerAsync(handler);
            await CoreLauncher.WaitForPortAsync(config.GrpcCallbackPort);
            var binduClient = grpcClient.InitializeBinduClient();
            var response = await grpcClient.RegisterAgent(config);
            var regResult = new RegistrationResult(response?.AgentId ?? "", response?.Did ?? "", response?.AgentUrl ?? "");
            _registrationResult = regResult;

            var heartBeat = new HeartbeatService(regResult, binduClient);

            AppDomain.CurrentDomain.ProcessExit += OnApplicationShutdown;
            Console.CancelKeyPress += Console_CancelKeyPress;

            //Assign all class Fields for shutdown handling
            _launcher = launcher;
            _heartbeatService = heartBeat;
            _grpcServer = grpcServer;
            _grpcClient = grpcClient;

            return regResult;
        }

        private void Console_CancelKeyPress(object? sender, ConsoleCancelEventArgs e) {
            e.Cancel = true;
            Console.WriteLine("\n[bindu-sdk] Shutting down...");
            CleanUp();
            Environment.Exit(0);
        }

        private void OnApplicationShutdown(object? sender, EventArgs e) {
            Console.WriteLine("\n[bindu-sdk] Shutting down...");
            CleanUp();
        }

        public void CleanUp() {
            _grpcClient?.UnRegisterAgent(_registrationResult!);
            _launcher?.CleanUp();
            _grpcServer?.StopServerAsync();
            _heartbeatService?.CleanUp();
        }

        public void Dispose() {
            CleanUp();
        }
        public ValueTask DisposeAsync() {
            CleanUp();
            return new ValueTask();
        }

    }

    public class AgentConfig {
        public required string Author { get; set; }
        public required string Name { get; set; }
        public required string Description { get; set; }
        public required int GrpcCallbackPort { get; set; }
        public string DeploymentUrl { get; set; } = "http://localhost:3773";
        public bool ExposeDeployment { get; set; } = false;
        public string[] Skills { get; set; } = [];

    }

    /*public class Test {
        public void start() {
            var re = new AgentConfig {
                Author = "you@example.com",
                Name = "name",
                Description = "description",
                DeploymentUrl = "http://localhost:3773",
                ExposeDeployment = false,
                Skills = [],
                GrpcCallbackPort = 5052
            };

            //var test = new Bindufy(re, TakeRequest);
        }

        private async Task<string> TakeRequest(ChatMessage[] incomingMessages) {
            var response = incomingMessages[incomingMessages.Length - 1].Content;
            return response;
        }
    }*/


}
