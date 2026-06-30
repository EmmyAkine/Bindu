# Bindu C# SDK — Implementation Plan (Draft)

## Goal

Bring Bindu to C# / .NET developers — especially Unity — with the same one-call simplicity as the TypeScript SDK:

```csharp
await Bindufy(config, async (messages) => {
    return "Hello from C#!";
});
```

Everything else (identity, gRPC, payments, A2A) stays invisible to the developer using the SDK.

## Project Setup

- **Type**: .NET Class Library (`dotnet new classlib -n Bindu.Sdk`)
- **Package target**: NuGet, published as `Bindu.Sdk`
- **gRPC stub generation**: `Grpc.Tools`, reading directly from `proto/agent_handler.proto` — no manual translation, generated at build time
- **Dependencies**: `Grpc.Tools`, `Grpc.Net.Client`, `Google.Protobuf`
- **DotnetVersion** `.NET 10.0LTS`

## Public API

Single entry point, mirroring the TypeScript SDK's `bindufy()`:

```csharp
Bindufy(AgentConfig config, Func<List<ChatMessage>, Task<string>> handler)
```

Returns the agent's identity info (`AgentId`, `Did`, `AgentUrl`) so the developer can use it if needed (e.g. surfacing identity to their own users, or using it when calling other agents).

## Startup Sequence

1. **Start the Python core** as a background process: `System.Diagnostics.Process.Start(bindu serve --grpc --grpc-port 3774)`
2. **Listen to stdout and stderr** for any errors and outputs from bindu startup and report it
3. **Start local gRPC server** (`AgentHandler` service) on a dynamic port (e.g. `5052`)
4. **Poll port 3774** until Bindu core is ready to accept connections
5. **Call `RegisterAgent`** on port 3774, carrying:
   - `config_json` — developer's config, serialized
   - `grpc_callback_address` — `localhost:5052` (where Bindu reaches the SDK)
   - A2A deployment URL from config (e.g. `localhost:3773`, where external agents reach this agent)
5. **Store** the returned `agent_id` and `did` internally (for heartbeats) and return them to the developer
6. **Start heartbeat loop** — ping port 3774 every 30 seconds with `agent_id`

## Incoming Request Handling

1. Bindu core calls `HandleMessages` on the SDK's gRPC server (port 5052)
2. SDK invokes the developer's registered callback with the message history
3. Awaits the callback's return value
4. Wraps the result into a `HandleResponse` proto message:
   - Plain string → `{content, state: ""}`
   - Needs more info → `{state: "input-required", prompt}`
   - Exception thrown → gRPC `INTERNAL` error
5. Returns to Bindu core, which delivers it via A2A to the original requester

## Outgoing Requests (Agent-to-Agent)

Not handled by the SDK directly — Bindu provides the agent's DID and credentials at registration, and the developer's agent code uses those to call other agents' A2A endpoints directly via HTTP.

## Shutdown

1. Call `UnregisterAgent` on port 3774
2. Kill the Python core child process
3. Stop the local gRPC server

## Proposed File Structure

```
Bindu.Sdk/
├── Bindu.Sdk.csproj
├── Bindufy.cs              # public entry point
├── AgentConfig.cs          # config class
├── BinduResponse.cs        # response state types
├── GrpcServer.cs           # AgentHandler service (incoming)
├── GrpcClient.cs           # BinduService client (registration, heartbeat)
├── CoreLauncher.cs         # Python core process management
├── HeartbeatService.cs     # 30s keep-alive loop
└── Proto/
    └── agent_handler.proto # source of truth, unmodified
```

## Open Questions

- Preferred NuGet namespace / package ID conventions beyond `Bindu.Sdk`?
- Should the core launcher assume `bindu` CLI is pip-installed, or handle bootstrapping Python itself for devs with no Python environment (relevant for Unity devs who may have zero Python tooling)?
