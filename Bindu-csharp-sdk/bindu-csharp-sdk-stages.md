# Bindu C# SDK — Build Stages

Format inspired by CodeCrafters. Each stage is a single testable unit. Don't move to the next stage until the current one passes.

---

## Phase 1: Project Setup

### Stage 1 — Create the class library #s01

In this stage, you will create the empty SDK project that everything else builds on top of.

**Task**

Run `dotnet new classlib -n Bindu.Sdk` to scaffold the project. Confirm the project builds with zero code changes.

**Tests**

```bash
cd Bindu.Sdk
dotnet build
```

Expected output: `Build succeeded.` with 0 errors.

**Notes**

- If `dotnet` is not recognized, you need the .NET SDK installed first (not just the runtime) — check with `dotnet --version`.
- Delete the default `Class1.cs` file that the template generates; you won't need it.

---

### Stage 2 — Add gRPC dependencies #s02

In this stage, you will add the NuGet packages your SDK needs to speak gRPC and understand Raahul's proto file.

**Task**

Add these packages to `Bindu.Sdk.csproj`:
- `Grpc.Tools`
- `Grpc.Net.Client`
- `Google.Protobuf`
- `Grpc.AspNetCore` (needed because your SDK also runs a gRPC *server*, not just a client)

**Tests**

```bash
dotnet add package Grpc.Tools
dotnet add package Grpc.Net.Client
dotnet add package Google.Protobuf
dotnet add package Grpc.AspNetCore
dotnet build
```

Expected: build still succeeds, and your `.csproj` file now lists all four `<PackageReference>` entries.

**Notes**

- `Grpc.Tools` does nothing visible yet — it only activates once you add an actual `.proto` file in the next stage.

---

### Stage 3 — Add the proto file and confirm stub generation #s03

In this stage, you will drop in Raahul's `agent_handler.proto` file and confirm `Grpc.Tools` auto-generates C# classes from it at build time.

**Task**

1. Create a `Protos/` folder in your project
2. Copy `proto/agent_handler.proto` from the Bindu repo into it, unmodified
3. Add this to your `.csproj` inside an `<ItemGroup>`:
   ```xml
   <Protobuf Include="Protos/agent_handler.proto" GrpcServices="Both" />
   ```
4. Build the project

**Tests**

```bash
dotnet build
```

Then check the generated files exist:

```bash
dir obj\Debug\net10.0\Protos
```

Expected: you should see auto-generated files like `AgentHandler.cs` and `AgentHandlerGrpc.cs` (exact names depend on the proto's package/service names — check the proto file for the real names).

**Notes**

- `GrpcServices="Both"` tells the tool to generate BOTH client stubs (for `BinduService`, since you call Bindu) and server stubs (for `AgentHandler`, since Bindu calls you).
- If generation silently fails, delete the `obj/` and `bin/` folders and rebuild — stale build artifacts are a common cause of "nothing generated."
- On Windows 11, if `dotnet build` hangs or errors oddly, try running from PowerShell as Administrator once — file lock issues on first NuGet restore are common.

---

## Phase 2: Starting the Python Core

### Stage 4 — Launch Bindu core as a background process #s04

In this stage, you will write the code that starts the Python core (`bindu serve --grpc`) from C#, and confirm it actually launches.

**Task**

Create `CoreLauncher.cs`. Write a method that uses `System.Diagnostics.Process` to start:

```
bindu serve --grpc --grpc-port 3774
```

Set `RedirectStandardOutput = true` and `RedirectStandardError = true` so you can capture what Bindu prints.

**Tests**

Write a tiny throwaway `Program.cs` (a Console App, separate from the SDK, just for testing) that calls your launcher method and prints whatever Bindu outputs to the console for 5 seconds, then exits.

Run it:

```bash
dotnet run
```

Expected: you should see Bindu's actual startup logs printed in your C# console window (things like "gRPC server starting on port 3774" or similar, depending on what the Python core logs).

**Notes**

- You need the actual Bindu Python repo cloned and runnable on your machine first (`uv sync`, etc.) — this stage assumes `bindu` is already a working command in your terminal *outside* of C#. Test that manually first: just run `bindu serve --grpc` directly in your terminal and confirm it starts, before trying to launch it from C#.
- On Windows, `Process.Start` needs `UseShellExecute = false` when redirecting output — if you forget this, you'll get a runtime exception.
- If `bindu` isn't found as a command from inside your C# process even though it works in your terminal, it's a PATH issue specific to how `Process.Start` resolves executables on Windows — you may need the full path to the `bindu` executable for now (revisit this properly in a later stage).

---

### Stage 5 — Confirm the core's gRPC port is alive #s05

In this stage, you will write code that waits until port 3774 is actually accepting connections, instead of assuming it's ready immediately after `Process.Start()` returns.

**Task**

Add a method to `CoreLauncher.cs` that polls `localhost:3774` using a raw `TcpClient` connection attempt every 500ms, up to a 30 second timeout. Return `true` once a connection succeeds, `false` if it times out.

**Tests**

In your throwaway test program: start the core, then call your polling method, and print how long it took to become ready.

```bash
dotnet run
```

Expected output: something like `Bindu core ready after 2.3s` — confirming the poll loop correctly detects readiness and doesn't just guess with a fixed `Thread.Sleep`.

**Notes**

- Don't just `Thread.Sleep(3000)` and assume it's ready — that's exactly the kind of fragile assumption this stage exists to remove. Different machines, different startup speeds.
- A common gotcha: `TcpClient.ConnectAsync` can throw an exception on failed connection rather than just returning false — wrap each poll attempt in try/catch and treat an exception as "not ready yet, try again."

---

## Phase 3: Your Local gRPC Server (Receiving Work)

### Stage 6 — Stand up an empty gRPC server #s06

In this stage, you will get a bare gRPC server running on a local port, with no real logic yet — just confirming the server itself starts and listens.

**Task**

Create `GrpcServer.cs`. Using ASP.NET Core's minimal gRPC hosting (`WebApplication.CreateBuilder`), configure a Kestrel server to listen on `localhost:5052` using HTTP/2 (gRPC requires HTTP/2). Don't implement any service methods yet — just get the server starting and staying alive.

**Tests**

```bash
dotnet run
```

Expected: the console shows Kestrel has started and is listening on `http://localhost:5052`, and the process doesn't immediately exit or crash.

**Notes**

- gRPC requires HTTP/2. If you're not explicitly configuring Kestrel for HTTP/2, you may get cryptic connection failures later that look like a proto problem but are actually a transport problem.
- Don't hardcode `5052` forever — later you'll want this to be dynamic so multiple agents can run on one machine without port collisions. For now, hardcoded is fine to get something working.

---

### Stage 7 — Implement `HealthCheck` #s07

In this stage, you will implement the simplest possible service method — `HealthCheck` — to confirm the proto-generated server stub actually wires up correctly to your code.

**Task**

Implement the `AgentHandler` service's `HealthCheck` method (generated from the proto in Stage 3). Return `{ healthy: true, message: "OK" }` unconditionally — no real logic yet.

**Tests**

Install `grpcurl` if you don't have it (`choco install grpcurl` on Windows, or download the binary directly).

```bash
grpcurl -plaintext -proto Protos/agent_handler.proto -import-path Protos localhost:5052 bindu.grpc.AgentHandler.HealthCheck
```

Expected output:
```json
{
  "healthy": true,
  "message": "OK"
}
```

**Notes**

- If `grpcurl` can't find the service at all (connection refused), go back to Stage 6 — your server isn't actually listening.
- If `grpcurl` connects but says "service not found," your `.proto` package/service name in the command doesn't match what's in the actual file — open the proto file and copy the exact `package` and `service` names.
- This is your first real end-to-end confirmation that proto → generated C# → your implementation → network response actually works. Don't skip celebrating this one.

---

### Stage 8 — Implement `GetCapabilities` #s08

In this stage, you will implement the second simple method, returning static info about your SDK agent.

**Task**

Implement `GetCapabilities`, returning hardcoded values for now:
```
name: "test-agent"
description: "A test agent for SDK development"
version: "0.1.0"
supports_streaming: false
```

**Tests**

```bash
grpcurl -plaintext -proto Protos/agent_handler.proto -import-path Protos localhost:5052 bindu.grpc.AgentHandler.GetCapabilities
```

Expected: the hardcoded values come back correctly as JSON.

**Notes**

- `supports_streaming: false` is intentional and should stay `false` for now — `HandleMessagesStream` isn't implemented in the core yet either (see the known limitations in the docs), so don't build toward it.

---

### Stage 9 — Implement `HandleMessages` with a hardcoded response #s09

In this stage, you will implement the most important method on your server — but with a fake hardcoded response first, before wiring in the real developer callback. This isolates "does the proto round-trip work" from "does my callback logic work."

**Task**

Implement `HandleMessages`. Ignore the actual incoming messages for now — just always return:
```
content: "Hello from C# SDK"
state: ""
is_final: true
```

**Tests**

```bash
grpcurl -plaintext -proto Protos/agent_handler.proto -import-path Protos -d '{"messages":[{"role":"user","content":"test"}],"task_id":"t1","context_id":"c1"}' localhost:5052 bindu.grpc.AgentHandler.HandleMessages
```

Expected:
```json
{
  "content": "Hello from C# SDK",
  "isFinal": true
}
```

**Notes**

- Note `is_final` becomes `isFinal` in JSON output — proto's `snake_case` fields typically map to `camelCase` in generated C#/JSON. Don't be thrown by this when reading responses.
- This stage proves the full request/response shape works before you add real logic. Resist the urge to wire in the developer callback yet — that's the next stage.

---

## Phase 4: Registering With Bindu

### Stage 10 — Build the gRPC client and call `RegisterAgent` #s10

In this stage, you will write the client side — code in your SDK that calls OUT to Bindu's core (port 3774) instead of listening for incoming calls.

**Task**

Create `GrpcClient.cs`. Using the client stub generated from the proto, connect to `localhost:3774` and call `RegisterAgent` with:
- A minimal hardcoded `config_json` (author, name, deployment URL)
- `grpc_callback_address: "localhost:5052"`
- Empty `skills` array for now

**Tests**

With your Stage 9 server still running in one terminal, run a small test program that calls your new client method against the REAL Bindu core (make sure `bindu serve --grpc` is running per Stage 4/5 first).

Expected: you get back a `RegisterAgentResponse` with `success: true`, a populated `agent_id`, and a `did` string starting with `did:bindu:`.

**Notes**

- This is the first stage where TWO of your components talk to each other through a THIRD real system (the actual Python core) — if it fails, isolate which side is wrong: use `grpcurl` directly against port 3774 first to confirm Bindu's `RegisterAgent` works at all, independent of your C# client code.
- If `success: false`, read the `error` field in the response — Bindu core usually tells you exactly what's wrong with your `config_json` (commonly: missing required field, or malformed JSON string).

---

## Phase 5: Keeping the Agent Alive

### Stage 11 — Wire the real developer callback into `HandleMessages` #s11

In this stage, you will replace the hardcoded response from Stage 9 with the actual developer-provided handler function, completing the round trip from Bindu → your SDK → developer code → back to Bindu.

**Task**

Change `HandleMessages` so that instead of returning a hardcoded string, it:
1. Converts the incoming `repeated ChatMessage` into whatever input shape your developer-facing handler expects (likely a `List<ChatMessage>` C# class, not the raw proto type)
2. Invokes the registered handler (`Func<List<ChatMessage>, Task<string>>`) and awaits the result
3. Wraps the returned string into `HandleResponse { content = result, state = "", is_final = true }`
4. Wraps any thrown exception into a gRPC `RpcException` with `StatusCode.Internal`

For this stage, hardcode the handler itself in your test program (e.g. `async (messages) => $"You said: {messages.Last().Content}"`) — you're testing the wiring, not building the public API yet.

**Tests**

```bash
grpcurl -plaintext -proto Protos/agent_handler.proto -import-path Protos -d '{"messages":[{"role":"user","content":"hello there"}],"task_id":"t1","context_id":"c1"}' localhost:5052 bindu.grpc.AgentHandler.HandleMessages
```

Expected:
```json
{
  "content": "You said: hello there",
  "isFinal": true
}
```

Then test the error path — make your test handler throw on purpose (`throw new Exception("test error")`), call `grpcurl` again, and confirm you get a gRPC error response rather than a crash or a silent empty response.

**Notes**

- This is the single most important stage in the whole SDK — it's the actual "translator" function we talked through. Everything before this was plumbing; this is the thing that makes the SDK useful.
- Test the exception path deliberately. A developer's handler WILL throw eventually (network call fails, null reference, whatever) — if your SDK doesn't catch and convert it properly, it'll crash the whole gRPC server instead of just failing that one request.

---

### Stage 12 — Implement the heartbeat loop #s12

In this stage, you will make your SDK proactively tell Bindu "I'm still alive" every 30 seconds, instead of only talking to Bindu when registering once.

**Task**

Create `HeartbeatService.cs`. After a successful `RegisterAgent` call (Stage 10), start a background loop (a `System.Threading.Timer` or a simple `while` loop with `await Task.Delay(30000)`) that calls the `Heartbeat` RPC with the stored `agent_id` every 30 seconds.

**Tests**

Run your full test program (core launched, server up, registered) and let it sit for at least 90 seconds. Watch the Bindu core's own console output/logs — you should see it acknowledge heartbeats roughly every 30 seconds.

You can also manually call `Heartbeat` once via `grpcurl` to confirm the shape works before trusting your timer loop:

```bash
grpcurl -plaintext -proto Protos/agent_handler.proto -import-path Protos -d '{"agent_id":"YOUR_AGENT_ID","timestamp":1234567890}' localhost:3774 bindu.grpc.BinduService.Heartbeat
```

Expected: `{"acknowledged": true, "serverTimestamp": ...}`

**Notes**

- Don't use `Thread.Sleep` for the 30-second wait — it blocks the thread. Use `Task.Delay` so it's non-blocking and your gRPC server can keep serving requests at the same time.
- If you stop your test program WITHOUT stopping the heartbeat loop cleanly, the loop can keep running as an orphaned background task in some setups — make sure the loop checks a cancellation token so Stage 13 (shutdown) can actually stop it.

---

### Stage 13 — Implement graceful shutdown #s13

In this stage, you will make sure that when the developer's application closes, your SDK cleans up properly instead of leaving an orphaned Python process running in the background.

**Task**

Implement a shutdown method that, in order:
1. Cancels the heartbeat loop
2. Calls `UnregisterAgent` with the stored `agent_id`
3. Stops the local gRPC server (Stage 6)
4. Kills the Python core child process (the one started in Stage 4)

Hook this to fire automatically when the developer's app exits — use `AppDomain.CurrentDomain.ProcessExit` and also handle `Ctrl+C` via `Console.CancelKeyPress`, since a developer killing their app with Ctrl+C during testing is extremely common and shouldn't leave Python running forever in the background.

**Tests**

Run your full test program, let it register and send a couple of heartbeats, then press Ctrl+C.

Check Windows Task Manager (or `tasklist | findstr python`) immediately after — there should be NO orphaned `python.exe` or `bindu` process still running.

**Notes**

- This is the stage most people skip and regret. An orphaned Python process holding port 3774 open will silently break your NEXT test run with a confusing "port already in use" error that has nothing to do with whatever you're actually working on that day.
- If you find an orphaned process after testing this stage, that's not a failure of THIS stage necessarily — it might mean an earlier crash (e.g. an unhandled exception in Stage 11's error path) skipped your cleanup entirely. Worth wrapping your whole test flow in try/finally while you're building this out.

---

## Phase 6: The Public API

### Stage 14 — Define `AgentConfig` and the public `Bindufy` signature #s14

In this stage, you will write the actual public-facing types a developer will see and use — no internal plumbing, just the clean surface.

**Task**

Create `AgentConfig.cs`:
```csharp
public class AgentConfig
{
    public string Author { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public string DeploymentUrl { get; set; } = "http://localhost:3773";
}
```

Create `Bindufy.cs` with the public method signature:
```csharp
public static class Bindu
{
    public static Task<AgentInfo> Bindufy(
        AgentConfig config,
        Func<List<ChatMessage>, Task<string>> handler)
    {
        throw new NotImplementedException();
    }
}
```

Don't implement the body yet — just get the public signature compiling and confirm it's the API shape you actually want, since this is the part developers will judge your SDK by.

**Tests**

Write a throwaway snippet (won't run yet, just needs to compile) in a separate test project that references your SDK:

```csharp
await Bindu.Bindufy(new AgentConfig {
    Author = "test@example.com",
    Name = "test-agent"
}, async (messages) => "hello");
```

Expected: this compiles cleanly with zero errors (it'll throw `NotImplementedException` at runtime if you actually run it, that's fine for this stage).

**Notes**

- This is a good moment to pause and re-read the developer-experience snippet from the implementation plan you sent Raahul — does this signature match what you promised? If not, now's the cheap moment to change it, not after everything's wired up.

---

### Stage 15 — Implement the full `Bindufy` orchestration #s15

In this stage, you will wire every previous stage together inside the single public method, so the entire startup sequence (Stages 4 through 12) happens automatically when a developer calls `Bindufy` once.

**Task**

Implement the body of `Bindufy`, in order:
1. Launch Python core (Stage 4)
2. Wait for port 3774 (Stage 5)
3. Start local gRPC server with the REAL handler wired in (Stage 6 + 11), on a dynamically chosen free port instead of hardcoded 5052
4. Call `RegisterAgent` (Stage 10), using the real `config` passed in
5. Start the heartbeat loop (Stage 12)
6. Register shutdown hooks (Stage 13)
7. Return an `AgentInfo` object containing `AgentId`, `Did`, `AgentUrl`

**Tests**

This is the big one — full end-to-end, exactly the developer experience you promised:

```csharp
var info = await Bindu.Bindufy(new AgentConfig {
    Author = "dev@example.com",
    Name = "my-test-agent"
}, async (messages) => $"Echo: {messages.Last().Content}");

Console.WriteLine($"Agent live at {info.AgentUrl}, DID: {info.Did}");
```

Then, from a SEPARATE terminal, send an actual A2A request to confirm the full loop works end-to-end through the real protocol, not just your internal test harness:

```bash
curl -X POST http://localhost:3773 -H "Content-Type: application/json" -d "{\"jsonrpc\":\"2.0\",\"method\":\"message/send\",\"params\":{\"message\":{\"role\":\"user\",\"parts\":[{\"text\":\"hi\"}]}},\"id\":1}"
```

Expected: the curl response contains `"Echo: hi"`, having round-tripped through A2A → Bindu core → your gRPC server → your handler → back out.

**Notes**

- If this stage works, you have a real, working SDK. Everything from here is polish (dynamic ports properly, error messages, NuGet packaging, more config options) rather than core functionality.
- Worth recording a screen capture of this curl round-trip working — it's the single most convincing thing you could show Raahul that this isn't just a plan anymore, it's a working SDK.

---

## What's Deliberately Left Out (For Now)

These were called out as open questions to Raahul and don't need solving before you have something demoable:

- Dynamic Python bootstrapping for devs with no Python environment at all
- Streaming responses (`HandleMessagesStream`) — not implemented in the core either yet
- Skill loading (`SkillDefinition`) — fine to send an empty array until the rest works
- NuGet publishing — only matters once the SDK itself works locally


