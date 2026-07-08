using Bindu;
using Bindu.Grpc;
using Grpc.Net.Client;
using System.Text.Json;
using static Bindu.Grpc.BinduService;

namespace Bindu_csharp_sdk.src {
    public class GrpcClient {
        private BinduServiceClient? _binduClient;
        //private class AgentRegData {
        //    public string Author { get; set; } = "";
        //    public string Name { get; set; } = "";
        //    public string DeploymentUrl { get; set; } = "";
        //}

        //public async Task Deploy() {
        //    InitializeBinduClient();
        //    await RegisterAgent();
        //}



        public BinduServiceClient InitializeBinduClient() {
            var channel = GrpcChannel.ForAddress("http://localhost:3774/");

            var client = new BinduServiceClient(channel);
            _binduClient = client;
            return _binduClient;
        }

        public async Task<RegisterAgentResponse?> RegisterAgent(AgentConfig regDetails) {
            if (_binduClient == null) { return null; }
            //var configJson = new {
            //    author = "dev@example.com",
            //    name = "my-agent",
            //    description = "My C# agent",
            //    deployment = new {
            //        url = "http://localhost:3773",
            //        expose = false
            //    }
            //};

            var configJson = new {
                author = regDetails.Author,
                name = regDetails.Name,
                description = regDetails.Description,
                deployment = new {
                    url = regDetails.DeploymentUrl,
                    expose = regDetails.ExposeDeployment.ToString(),
                },
                skills = regDetails.Skills
            };


            var json = JsonSerializer.Serialize(configJson);


            var regRequest = new RegisterAgentRequest {
                ConfigJson = json,
                GrpcCallbackAddress = $"localhost:{regDetails.GrpcCallbackPort}"
            };
            var returnValue = await _binduClient.RegisterAgentAsync(regRequest);

            return returnValue;
        }

        public UnregisterAgentResponse? UnRegisterAgent(BindufyInit.RegistrationResult regDetails) {
            var unRegister = new UnregisterAgentRequest {
                AgentId = regDetails.AgentId                
            };
            var returnValue = _binduClient?.UnregisterAgent(unRegister);
            _binduClient = null;
            return returnValue;
        }

    }

}
