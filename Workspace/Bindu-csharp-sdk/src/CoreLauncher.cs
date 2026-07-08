using System.Diagnostics;
using System.Net.Sockets;


namespace Bindu_csharp_sdk.src {
    public class CoreLauncher {

        private int _grpcPort = 3774;
        private Process? _process;
        //private int _httpPort = 3773;

        public async Task LaunchBinduServer() {
            var binduPath = FindBinduExecutable();
            var command = "";
            var argsList = Array.Empty<string>();
            if (binduPath != null) {
                command = binduPath;
                argsList = [ "serve", "--grpc", "--grpc-port", _grpcPort.ToString() ];
            }
            else if (IsUvInstalled()) {
                command = "uv";
                argsList = ["run", "bindu", "serve", "--grpc", "--grpc-port", _grpcPort.ToString()];
            }
            else if(IsPython3Installed()) {
                command = "python3";
                argsList = ["-m", "bindu.cli", "serve", "--grpc", "--grpc-port", _grpcPort.ToString()];
            }
            else {
                throw new InvalidOperationException("Cannot find bindu, uv, or python3. Ensure at least one is installed.");
            }

            var processInfo = new ProcessStartInfo {
                FileName = command,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            for (int i = 0; i < argsList.Length; i++) {
                processInfo.ArgumentList.Add(argsList[i]);
            }

            Console.WriteLine($"Starting Bindu core: {command} {string.Join(" ", argsList)}");
            var binduProcess = Process.Start(processInfo);
            if (binduProcess == null) {
                throw new InvalidOperationException("Failed to start Bindu core process.");
            }
            _process = binduProcess;

            binduProcess.Exited += BinduProcess_Exited;

            binduProcess.EnableRaisingEvents = true;

            binduProcess.OutputDataReceived += (sender, e) => {
                if (e.Data != null) Console.WriteLine($"[bindu-core] {e.Data}");
            };
            binduProcess.ErrorDataReceived += (s, e) => {
                if (e.Data != null) Console.WriteLine($"[bindu-core:err] {e.Data}");
            };
            binduProcess.BeginOutputReadLine();
            binduProcess.BeginErrorReadLine();


            await WaitForPortAsync(_grpcPort);
            Console.WriteLine("[bindu-sdk] Core is ready and accepting registrations.");
        }

        private void BinduProcess_Exited(object? sender, EventArgs e) {
            Console.WriteLine($"Bindu core exited unexpectedly with code {_process!.ExitCode}");
            CleanUp();
        }

        public void CleanUp() {
            _process?.Kill(entireProcessTree: true);
            _process?.Dispose();
            _process = null;
        }

        public static async Task WaitForPortAsync(int port, string host = "localhost", int timeoutMs = 30000) {
            using var cts = new CancellationTokenSource(timeoutMs);

            while (!cts.IsCancellationRequested) {
                try {
                    using var client = new TcpClient();

                    await client.ConnectAsync(host, port, cts.Token);

                    return;
                }
                catch (SocketException) {

                }
                catch (OperationCanceledException) {
                    break;
                }

                await Task.Delay(500, cts.Token);
            }
            throw new TimeoutException($"Bindu core did not start within {timeoutMs / 1000}s on port {port}");
        }

        private static string? FindExecutable(string executableName) {
            var fileName = OperatingSystem.IsWindows() ? "where.exe" : "which";
            try {
                using var process = Process.Start(new ProcessStartInfo {
                    FileName = fileName,
                    Arguments = executableName,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                });
                if (process == null) {
                    return null;
                }
                var path = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode != 0) {
                    return null;
                }
                else if (string.IsNullOrWhiteSpace(path)) {
                    return null;
                }
                else {
                    return path.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                }
            }
            catch {
                return null;
            }
        }
        private static bool IsPython3Installed() => FindExecutable("python3") is not null;
        private static string? FindBinduExecutable() => FindExecutable("bindu");
        private static bool IsUvInstalled() => FindExecutable("uv") is not null;
    }
}
