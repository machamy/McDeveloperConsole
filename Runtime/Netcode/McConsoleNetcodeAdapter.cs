#if MCDEVCONSOLE_USE_NGO
using System;
using System.Collections.Generic;
using Machamy.DeveloperConsole.Commands;
using Machamy.Utils;
using Unity.Netcode;
using UnityEngine;

namespace Machamy.DeveloperConsole
{
    [DisallowMultipleComponent]
    public class McConsoleNetcodeAdapter : MonoBehaviour, IConsoleRuntimeContext, IConsoleRemoteCommandExecutor, IConsoleResponseDispatcher
    {
        private const string CapabilityMessageName = "McConsole.Capability";
        private const string CommandRequestMessageName = "McConsole.CommandRequest";
        private const string CommandResultMessageName = "McConsole.CommandResult";

        [SerializeField] private bool allowClientToServerCommandsInEditor = true;
        [SerializeField] private bool allowClientToServerCommandsInDevelopmentBuild = true;
        [SerializeField] private bool allowClientToServerCommandsInReleaseBuild = false;

        [SerializeField] private bool _serverAllowsClientToServerCommands;
        [SerializeField] private bool _logClientCommandRequests;

        public static McConsoleNetcodeAdapter Instance { get; private set; }
        public static ulong? CurrentRemoteSenderClientId =>
            RemoteSenderClientIdStack.Count > 0 ? RemoteSenderClientIdStack.Peek() : null;

        private static readonly Stack<ulong> RemoteSenderClientIdStack = new();


        private static Coroutine _registerCoroutine;
        private bool _handlersRegistered;
        private bool _networkCallbacksRegistered;

        public bool IsClient => NetworkManager.Singleton != null && NetworkManager.Singleton.IsClient;
        public bool IsServer => NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;
        public bool AllowClientToServerCommands
        {
            get => _serverAllowsClientToServerCommands;
            set
            {
                _serverAllowsClientToServerCommands = value;
                if (IsServer)
                {
                    BroadcastCapability();
                }
            }
        }
        public bool LogClientCommandRequests
        {
            get => _logClientCommandRequests;
            set => _logClientCommandRequests = value;
        }
        public static bool IsExecutingRemoteCommand => CurrentRemoteSenderClientId.HasValue;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            if (Instance != null)
            {
                return;
            }

            var adapterObject = new GameObject("McConsoleNetcodeAdapter");
            adapterObject.AddComponent<McConsoleNetcodeAdapter>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            AllowClientToServerCommands = GetDefaultRemoteCommandPermission();
            CommandLibrary.SetRuntimeContext(this);
            CommandLibrary.SetRemoteCommandExecutor(this);
            McConsole.SetResponseDispatcher(this);
        }

        private void OnEnable()
        {
            if (NetworkManager.Singleton != null)
            {
                RegisterNetworkManagerCallbacks(NetworkManager.Singleton);
            }
            else
            {
                _registerCoroutine = StartCoroutine(RegisterWhenNetworkManagerAvailable());

                static System.Collections.IEnumerator RegisterWhenNetworkManagerAvailable()
                {
                    while (NetworkManager.Singleton == null)
                    {
                        yield return null;
                    }

                    Instance.RegisterNetworkManagerCallbacks(NetworkManager.Singleton);
                    _registerCoroutine = null;
                }
            }
        }

        private void OnDisable()
        {
            if(_registerCoroutine != null)
            {
                StopCoroutine(_registerCoroutine);
                _registerCoroutine = null;
            }

            UnregisterNetworkManagerCallbacks();

            if (Instance == this)
            {
                Instance = null;
                McConsole.SetResponseDispatcher(null);
            }
        }

        public bool CanRequestServerCommand(IConsoleCommand command)
        {
            return command != null &&
                   CommandLibrary.GetCommandScope(command) == ConsoleCommandScope.ClientToServer &&
                   IsClient &&
                   !IsServer &&
                   _serverAllowsClientToServerCommands;
        }

        public bool RequestServerCommand(string input)
        {
            if (!IsClient || IsServer || string.IsNullOrWhiteSpace(input))
            {
                return false;
            }

            var customMessagingManager = NetworkManager.Singleton?.CustomMessagingManager;
            if (customMessagingManager == null)
            {
                McConsole.MessageError("Network console messaging is not available.");
                return true;
            }

            using var writer = new FastBufferWriter(GetStringWriteSize(input), Unity.Collections.Allocator.Temp);
            writer.WriteValueSafe(input);
            customMessagingManager.SendNamedMessage(CommandRequestMessageName, NetworkManager.ServerClientId, writer);
            McConsole.MessageInfo($"Requested server command: {input}");
            return true;
        }

        public bool Respond(ConsoleResponseTarget target, MessageType type, string message)
        {
            if (!target.IsValid || !IsServer)
            {
                return false;
            }

            SendCommandResult(
                target.ClientId,
                true,
                string.Empty,
                new[] { new ConsoleOutputMessage(type, message) });
            return true;
        }

        public void BroadcastCapability()
        {
            if (!IsServer)
            {
                return;
            }

            var customMessagingManager = NetworkManager.Singleton?.CustomMessagingManager;
            if (customMessagingManager == null)
            {
                return;
            }

            foreach (ulong clientId in NetworkManager.Singleton.ConnectedClientsIds)
            {
                SendCapability(clientId);
            }
        }

        private bool GetDefaultRemoteCommandPermission()
        {
#if UNITY_EDITOR
            return allowClientToServerCommandsInEditor;
#elif DEVELOPMENT_BUILD
            return allowClientToServerCommandsInDevelopmentBuild;
#else
            return allowClientToServerCommandsInReleaseBuild;
#endif
        }

        private void RegisterNetworkHandlers()
        {
            if (_handlersRegistered)
            {
                return;
            }

            LogEx.Log("Registering network message handlers for McConsoleNetcodeAdapter...");
            var networkManager = NetworkManager.Singleton;
            var customMessagingManager = networkManager?.CustomMessagingManager;
            if (networkManager == null || customMessagingManager == null)
            {
                return;
            }

            customMessagingManager.UnregisterNamedMessageHandler(CapabilityMessageName);
            customMessagingManager.UnregisterNamedMessageHandler(CommandRequestMessageName);
            customMessagingManager.UnregisterNamedMessageHandler(CommandResultMessageName);
            customMessagingManager.RegisterNamedMessageHandler(CapabilityMessageName, HandleCapabilityMessage);
            customMessagingManager.RegisterNamedMessageHandler(CommandRequestMessageName, HandleCommandRequestMessage);
            customMessagingManager.RegisterNamedMessageHandler(CommandResultMessageName, HandleCommandResultMessage);
            networkManager.OnClientConnectedCallback -= HandleClientConnected;
            networkManager.OnClientConnectedCallback += HandleClientConnected;

            _handlersRegistered = true;
            LogEx.Log("Registered network message handlers.");
        }

        private void UnregisterNetworkHandlers()
        {
            if (!_handlersRegistered)
            {
                return;
            }

            var networkManager = NetworkManager.Singleton;
            var customMessagingManager = networkManager?.CustomMessagingManager;
            if (customMessagingManager != null)
            {
                customMessagingManager.UnregisterNamedMessageHandler(CapabilityMessageName);
                customMessagingManager.UnregisterNamedMessageHandler(CommandRequestMessageName);
                customMessagingManager.UnregisterNamedMessageHandler(CommandResultMessageName);
            }

            if (networkManager != null)
            {
                networkManager.OnClientConnectedCallback -= HandleClientConnected;
            }

            _handlersRegistered = false;
        }

        private void RegisterNetworkManagerCallbacks(NetworkManager networkManager)
        {
            if (_networkCallbacksRegistered || networkManager == null)
            {
                return;
            }

            _networkCallbacksRegistered = true;
            networkManager.OnServerStarted -= HandleServerStarted;
            networkManager.OnServerStarted += HandleServerStarted;
            networkManager.OnServerStopped -= HandleServerStopped;
            networkManager.OnServerStopped += HandleServerStopped;
            networkManager.OnClientStarted -= HandleClientStarted;
            networkManager.OnClientStarted += HandleClientStarted;
            networkManager.OnClientStopped -= HandleClientStopped;
            networkManager.OnClientStopped += HandleClientStopped;

            if (networkManager.IsServer || networkManager.IsClient)
            {
                RegisterNetworkHandlers();
            }
        }

        private void UnregisterNetworkManagerCallbacks()
        {
            if (!_networkCallbacksRegistered)
            {
                return;
            }

            var networkManager = NetworkManager.Singleton;
            if (networkManager != null)
            {
                networkManager.OnServerStarted -= HandleServerStarted;
                networkManager.OnServerStopped -= HandleServerStopped;
                networkManager.OnClientStarted -= HandleClientStarted;
                networkManager.OnClientStopped -= HandleClientStopped;
            }

            _networkCallbacksRegistered = false;
            UnregisterNetworkHandlers();
        }

        private void HandleServerStarted()
        {
            RegisterNetworkHandlers();
        }

        private void HandleServerStopped(bool isHost)
        {
            UnregisterNetworkHandlers();
        }

        private void HandleClientStarted()
        {
            RegisterNetworkHandlers();
        }

        private void HandleClientStopped(bool isHost)
        {
            UnregisterNetworkHandlers();
        }

        private void HandleClientConnected(ulong clientId)
        {
            if (IsServer)
            {
                SendCapability(clientId);
            }
        }

        private void SendCapability(ulong clientId)
        {
            var customMessagingManager = NetworkManager.Singleton?.CustomMessagingManager;
            if (customMessagingManager == null)
            {
                return;
            }

            using var writer = new FastBufferWriter(sizeof(bool), Unity.Collections.Allocator.Temp);
            writer.WriteValueSafe(_serverAllowsClientToServerCommands);
            customMessagingManager.SendNamedMessage(CapabilityMessageName, clientId, writer);
        }

        private void HandleCapabilityMessage(ulong senderClientId, FastBufferReader reader)
        {
            if (!IsMessageFromServer(senderClientId))
            {
                return;
            }

            reader.ReadValueSafe(out bool serverAllowsClientToServerCommands);
            _serverAllowsClientToServerCommands = serverAllowsClientToServerCommands;
        }

        private void HandleCommandRequestMessage(ulong senderClientId, FastBufferReader reader)
        {
            if (!IsServer)
            {
                return;
            }

            reader.ReadValueSafe(out string input);
            if (_logClientCommandRequests)
            {
                LogServerCommandRequest(senderClientId, input);
            }

            string resultMessage;
            List<ConsoleOutputMessage> outputMessages;
            bool success;

            if (!_serverAllowsClientToServerCommands)
            {
                success = false;
                resultMessage = "Server remote console commands are disabled.";
                outputMessages = new List<ConsoleOutputMessage>();
            }
            else
            {
                success = TryExecuteServerCommand(input, senderClientId, out resultMessage, out outputMessages);
            }

            if (_logClientCommandRequests)
            {
                LogServerCommandResult(senderClientId, success, resultMessage);
            }

            SendCommandResult(senderClientId, success, resultMessage, outputMessages);
        }

        private bool TryExecuteServerCommand(string input, ulong senderClientId, out string resultMessage, out List<ConsoleOutputMessage> outputMessages)
        {
            var capturedMessages = new List<ConsoleOutputMessage>();
            outputMessages = capturedMessages;
            if (string.IsNullOrWhiteSpace(input))
            {
                resultMessage = "Empty command.";
                return false;
            }

            if (!ConsoleCommandTokenizer.TryTokenizeForExecution(input, out var tokens, out string parseError))
            {
                resultMessage = parseError;
                return false;
            }

            if (tokens.Count == 0 || !CommandLibrary.TryGetCommand(tokens[0].Value, out IConsoleCommand command))
            {
                resultMessage = $"Unknown server command: '{input}'.";
                return false;
            }

            if (CommandLibrary.GetCommandScope(command) != ConsoleCommandScope.ClientToServer)
            {
                resultMessage = $"Command '{command.Command}' cannot be requested by clients.";
                return false;
            }

            try
            {
                string[] args = new string[tokens.Count - 1];
                for (int i = 1; i < tokens.Count; i++)
                {
                    args[i - 1] = tokens[i].Value;
                }

                RemoteSenderClientIdStack.Push(senderClientId);
                using (McConsole.BeginResponseCapture(McConsole.CreateResponseTarget(senderClientId), capturedMessages))
                {
                    try
                    {
                        command.Execute(args);
                    }
                    finally
                    {
                        RemoteSenderClientIdStack.Pop();
                    }
                }

                resultMessage = $"Server executed: {input}";
                return true;
            }
            catch (Exception ex)
            {
                resultMessage = $"Server command failed: {ex.Message}";
                return false;
            }
        }

        private void SendCommandResult(ulong clientId, bool success, string message, IReadOnlyList<ConsoleOutputMessage> outputMessages)
        {
            var customMessagingManager = NetworkManager.Singleton?.CustomMessagingManager;
            if (customMessagingManager == null)
            {
                return;
            }

            int messageCount = outputMessages?.Count ?? 0;
            int writeSize = sizeof(bool) + GetStringWriteSize(message) + sizeof(int);
            for (int i = 0; i < messageCount; i++)
            {
                writeSize += GetStringWriteSize(outputMessages[i].Type.ToString()) + GetStringWriteSize(outputMessages[i].Message);
            }

            using var writer = new FastBufferWriter(writeSize, Unity.Collections.Allocator.Temp);
            writer.WriteValueSafe(success);
            writer.WriteValueSafe(message ?? string.Empty);
            writer.WriteValueSafe(messageCount);
            for (int i = 0; i < messageCount; i++)
            {
                writer.WriteValueSafe(outputMessages[i].Type.ToString());
                writer.WriteValueSafe(outputMessages[i].Message ?? string.Empty);
            }

            customMessagingManager.SendNamedMessage(CommandResultMessageName, clientId, writer);
        }

        private void HandleCommandResultMessage(ulong senderClientId, FastBufferReader reader)
        {
            if (!IsMessageFromServer(senderClientId))
            {
                return;
            }

            reader.ReadValueSafe(out bool success);
            reader.ReadValueSafe(out string message);
            reader.ReadValueSafe(out int outputMessageCount);
            for (int i = 0; i < outputMessageCount; i++)
            {
                reader.ReadValueSafe(out string type);
                reader.ReadValueSafe(out string outputMessage);
                McConsole.Message(ToMessageType(type), outputMessage);
            }

            if (outputMessageCount > 0 && success)
            {
                return;
            }

            if (success)
            {
                McConsole.MessageSuccess(message);
            }
            else
            {
                McConsole.MessageError(message);
            }
        }

        private static int GetStringWriteSize(string value)
        {
            return 64 + ((value?.Length ?? 0) * 4);
        }

        private static bool IsMessageFromServer(ulong senderClientId)
        {
            return NetworkManager.Singleton != null &&
                   senderClientId == NetworkManager.ServerClientId;
        }

        private static MessageType ToMessageType(string type)
        {
            return type switch
            {
                "info" => MessageType.Info,
                "warn" => MessageType.Warning,
                "error" => MessageType.Error,
                "debug" => MessageType.Debug,
                "success" => MessageType.Success,
                "gray" => MessageType.Gray,
                "white" => MessageType.White,
                "cyan" => MessageType.Cyan,
                _ => MessageType.Default
            };
        }

        private static void LogServerCommandRequest(ulong senderClientId, string input)
        {
            string message = $"[McConsole] Client {senderClientId} requested server command: {input}";
            LogEx.Log(message);
            McConsole.MessageInfo(message);
        }

        private static void LogServerCommandResult(ulong senderClientId, bool success, string resultMessage)
        {
            string message = $"[McConsole] Server command request from client {senderClientId} {(success ? "succeeded" : "failed")}: {resultMessage}";
            if (success)
            {
                LogEx.Log(message);
                McConsole.MessageSuccess(message);
            }
            else
            {
                LogEx.LogWarning(message);
                McConsole.MessageError(message);
            }
        }

    }
}
#endif
