#if MCDEVCONSOLE_USE_NGO
using System;
using System.Collections;
using System.Collections.Generic;
using McDeveloperConsole.Netcode.Tests;
using Machamy.DeveloperConsole.Commands;
using NUnit.Framework;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Machamy.DeveloperConsole.Tests.Netcode
{
    public class McConsoleNetcodeAdapterIntegrationTests : NetcodeIntegrationTest
    {
        private const string CommandRequestMessageName = "McConsole.CommandRequest";
        private const string CommandResultMessageName = "McConsole.CommandResult";
        private const string RemoteCommandName = "test.remoteEcho";

        private McConsoleNetcodeAdapter _adapter;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            RemoteEchoCommand.Reset();
            CommandLibrary.RegisterCommand(new RemoteEchoCommand());
            CheatPolicy.SetPermission(CheatPermission.All);

            yield return StartServerAndClients();

            ServerNetworkManager.SetSingleton();
            if (McConsoleNetcodeAdapter.Instance != null)
            {
                Object.DestroyImmediate(McConsoleNetcodeAdapter.Instance.gameObject);
                yield return null;
            }

            var adapterObject = new GameObject("McConsoleNetcodeAdapter Test Instance");
            _adapter = adapterObject.AddComponent<McConsoleNetcodeAdapter>();
            _adapter.AllowClientToServerCommands = true;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            CommandLibrary.UnregisterCommand(RemoteCommandName);
            CheatPolicy.SetPermission(CheatPermission.Off);

            if (_adapter)
            {
                Object.DestroyImmediate(_adapter.gameObject);
                _adapter = null;
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator ClientRequestExecutesClientToServerCommandOnServerAndReturnsCapturedOutput()
        {
            CommandResult result = default;
            bool received = false;
            RegisterClientResultHandler(ClientNetworkManager, parsedResult =>
            {
                result = parsedResult;
                received = true;
            });

            SendClientCommand(ClientNetworkManager, $"{RemoteCommandName} alpha beta");

            yield return WaitForConditionOrTimeout(
                () => received,
                "Timed out waiting for remote console command result.");

            Assert.IsTrue(result.Success);
            Assert.AreEqual($"Server executed: {RemoteCommandName} alpha beta", result.Message);
            Assert.AreEqual(1, result.Output.Count);
            Assert.AreEqual(MessageType.Success.ToString(), result.Output[0].Type);
            Assert.AreEqual("echo:alpha,beta", result.Output[0].Message);
            Assert.AreEqual(1, RemoteEchoCommand.ExecutionCount);
            Assert.AreEqual(ClientNetworkManager.LocalClientId, RemoteEchoCommand.LastSenderClientId);
        }

        [UnityTest]
        public IEnumerator ClientRequestIsRejectedWhenServerRemoteConsoleIsDisabled()
        {
            _adapter.AllowClientToServerCommands = false;

            CommandResult result = default;
            bool received = false;
            RegisterClientResultHandler(ClientNetworkManager, parsedResult =>
            {
                result = parsedResult;
                received = true;
            });

            SendClientCommand(ClientNetworkManager, $"{RemoteCommandName} blocked");

            yield return WaitForConditionOrTimeout(
                () => received,
                "Timed out waiting for rejected remote console command result.");

            Assert.IsFalse(result.Success);
            Assert.AreEqual("Server remote console commands are disabled.", result.Message);
            Assert.AreEqual(0, result.Output.Count);
            Assert.AreEqual(0, RemoteEchoCommand.ExecutionCount);
        }

        private static void RegisterClientResultHandler(NetworkManager client, Action<CommandResult> onResult)
        {
            client.CustomMessagingManager.UnregisterNamedMessageHandler(CommandResultMessageName);
            client.CustomMessagingManager.RegisterNamedMessageHandler(CommandResultMessageName, (senderClientId, reader) =>
            {
                if (senderClientId != NetworkManager.ServerClientId)
                {
                    return;
                }

                reader.ReadValueSafe(out bool success);
                reader.ReadValueSafe(out string message);
                reader.ReadValueSafe(out int outputCount);

                var output = new List<(string Type, string Message)>(outputCount);
                for (int i = 0; i < outputCount; i++)
                {
                    reader.ReadValueSafe(out string type);
                    reader.ReadValueSafe(out string outputMessage);
                    output.Add((type, outputMessage));
                }

                onResult(new CommandResult(success, message, output));
            });
        }

        private static void SendClientCommand(NetworkManager client, string input)
        {
            using var writer = new FastBufferWriter(64 + input.Length * 4, Allocator.Temp);
            writer.WriteValueSafe(input);
            client.CustomMessagingManager.SendNamedMessage(
                CommandRequestMessageName,
                NetworkManager.ServerClientId,
                writer);
        }

        private readonly struct CommandResult
        {
            public CommandResult(bool success, string message, IReadOnlyList<(string Type, string Message)> output)
            {
                Success = success;
                Message = message;
                Output = output;
            }

            public bool Success { get; }
            public string Message { get; }
            public IReadOnlyList<(string Type, string Message)> Output { get; }
        }

        private sealed class RemoteEchoCommand : IConsoleCommand
        {
            public static int ExecutionCount { get; private set; }
            public static ulong? LastSenderClientId { get; private set; }

            public string Command => RemoteCommandName;
            public string Description => "Integration-test-only remote echo command.";
            public string Signature => $"{RemoteCommandName} <args...>";
            public ConsoleCommandScope Scope => ConsoleCommandScope.ClientToServer;

            public static void Reset()
            {
                ExecutionCount = 0;
                LastSenderClientId = null;
            }

            public void Execute(string[] args)
            {
                ExecutionCount++;
                LastSenderClientId = McConsoleNetcodeAdapter.CurrentRemoteSenderClientId;
                McConsole.MessageSuccess($"echo:{string.Join(",", args)}");
            }

            public void AutoComplete(Span<string> args, ref List<string> suggestions)
            {
            }
        }
    }
}
#endif
