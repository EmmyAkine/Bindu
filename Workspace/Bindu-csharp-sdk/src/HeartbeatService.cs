using Bindu.Grpc;
using Grpc.Core;
using static Bindu.Grpc.BinduService;

namespace Bindu_csharp_sdk.src {
    internal class HeartbeatService {

        private Timer? _timer;
        private BinduServiceClient? _binduClient;

        public HeartbeatService(BindufyInit.RegistrationResult regInfo, BinduServiceClient client) {

            TimeSpan dueTime = TimeSpan.FromSeconds(30);
            TimeSpan period = TimeSpan.FromSeconds(30);

            _timer = new Timer(HeartBeat, regInfo, dueTime, period);
            _binduClient = client;
        }

        private async void HeartBeat(object? state) {
            try {
                if (state == null) {
                    throw new RpcException(new Status(StatusCode.Internal, ""));
                }
                BindufyInit.RegistrationResult info = (BindufyInit.RegistrationResult)state;

                var heartbeatRequest = new HeartbeatRequest {
                    AgentId = info.AgentId,
                    Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                };

                var response = await SendHeartBeat(heartbeatRequest);
                Console.WriteLine($"Acknowledged: {response.Acknowledged}, TimeStamp: {response.ServerTimestamp}");
            }
            catch (RpcException ex) {
                Console.WriteLine($"[bindu-sdk:err] Heartbeat failed: {ex.Status.Detail}");
                Console.WriteLine($"[bindu-sdk:err] Heartbeat failed: {ex.Message}");
            }
            catch (Exception ex) {
                Console.WriteLine($"[bindu-sdk:err] Heartbeat unexpected error: {ex.Message}");
                Console.WriteLine($"[bindu-sdk:err] Heartbeat unexpected error: {ex.StackTrace}");
            }
        }

        private async Task<HeartbeatResponse> SendHeartBeat(HeartbeatRequest heartbeatRequest) {

            var response = await _binduClient!.HeartbeatAsync(heartbeatRequest, new CallOptions());
            return response;
        }

        public void CleanUp() {
            _timer?.Dispose();
            _timer = null;
        }

    }


}
