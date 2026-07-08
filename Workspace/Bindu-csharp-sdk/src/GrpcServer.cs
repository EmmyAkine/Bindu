using Bindu.Grpc;
using Grpc.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using static Bindu.Grpc.AgentHandler;


namespace Bindu_csharp_sdk.src {

    //Temporarily public to help with testing but should be internal
    public class GrpcServer {
        private int _port = 5052;
        private WebApplication? _app;

        public GrpcServer(int port) {
            _port = port;
        }
        public async Task StartServerAsync(Func<IReadOnlyList<ChatMessage>, Task<string>> handler) {

            var builder = WebApplication.CreateBuilder();

            builder.Services.AddSingleton(handler);

            builder.WebHost.ConfigureKestrel(options => {
                options.Listen(IPAddress.Any, _port, listenOptions => {
                    listenOptions.Protocols = HttpProtocols.Http2;
                });
            });

            builder.Services.AddGrpc();

            var app = builder.Build();

            app.MapGrpcService<AgentHandler>();

            await app.StartAsync();
            _app = app;
        }

        public async void StopServerAsync() {
            if (_app is not null) {
                await _app.StopAsync();
                await _app.DisposeAsync();
                _app = null;
            }
        }
    }

    public class AgentHandler : AgentHandlerBase {
        private Func<IReadOnlyList<ChatMessage>, Task<string>> _handler;

        public AgentHandler(Func<IReadOnlyList<ChatMessage>, Task<string>> handler) {
            _handler = handler;
        }
        public override async Task<HandleResponse> HandleMessages(HandleRequest request, ServerCallContext context) {
            try {
                var messages = request.Messages.ToArray();
                var resp = await _handler(messages);
                var response = new HandleResponse {
                    Content = resp,
                    State = "",
                    IsFinal = true
                };

                return response;
            }
            catch (Exception ex) {
                var trailers = new Metadata {
                    { "exception-type", ex.GetType().Name },
                    { "stack-trace", ex.StackTrace ?? "no stack trace" }
                };
                throw new RpcException(new Status(StatusCode.Internal, ex.Message), trailers);
            }
        }
        //public override Task HandleMessagesStream(HandleRequest handleRequest, IServerStreamWriter<HandleResponse> responseStream, ServerCallContext context) {
            
        //}

        public override Task<GetCapabilitiesResponse> GetCapabilities(GetCapabilitiesRequest request, ServerCallContext context) {
            var capabilities = new GetCapabilitiesResponse {
                Name = "test-agent",
                Description = "This is a test",
                Version = "1.0.0",
                SupportsStreaming = false
            };

            return Task.FromResult(capabilities);
        }

        public override Task<HealthCheckResponse> HealthCheck(HealthCheckRequest request, ServerCallContext context) {
            return Task.FromResult( new HealthCheckResponse {
                Healthy = true,
                Message = "OK"
            });
        }
    }


}

