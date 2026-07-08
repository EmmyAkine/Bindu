using Bindu_csharp_sdk.src;

namespace TestApp {
    internal class Program {
        static async Task Main(string[] args) {
            var bindu = new BindufyInit();
            var details = new AgentConfig {
                Author = "dev@example.com",
                Name = "my-test-agent",
                Description = "description",
                DeploymentUrl = "http://localhost:3773",
                ExposeDeployment = false,
                Skills = [],
                GrpcCallbackPort = 5052
            };

            //Console.CancelKeyPress += (s, e) => {
            //    e.Cancel = true;
            //    Console.WriteLine("\n[bindu-sdk] Shutting down...");
            //    bindu.CleanUp();
            //    Environment.Exit(0);
            //};

            try {

                var value = await bindu.Bindufy(details, async (messages) => $"Echo: {messages.Last().Content}");
                if (value != null)
                    Console.WriteLine($"AgentId: {value.AgentId}, \n AgentUrl: {value.AgentUrl}, \n Did: {value.Did}");

                Console.WriteLine("Press any key to stop...");
                Console.ReadKey();
                }
            finally {
                //bindu.CleanUp();
            }
        }

    }
}
