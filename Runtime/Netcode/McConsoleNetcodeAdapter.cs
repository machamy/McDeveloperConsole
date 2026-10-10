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
        private const string PingRequestMessageName = "McConsole.PingRequest";
        private const string PingResultMessageName = "McConsole.PingResult";

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

        // ping 왕복 측정용: 시퀀스 id → 전송 시각(realtime). 응답이 오면 경과 ms를 계산해 출력한다.
        private uint _pingSequence;
        private readonly Dictionary<uint, double> _pendingPings = new();

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
            McConsole.SetNetworkPingHandler(HandleConsolePingRequested);
            CheatPolicy.PermissionChanged += HandleCheatPermissionChanged;
        }

        // 코어(McDeveloperConsole.asmdef)는 넷코드 어셈블리를 참조할 수 없으므로,
        // 핑 측정과 치트-전송권한 동기화를 어댑터가 훅/이벤트로 받아 처리한다.

        // 'ping'/'net.serverPing'의 실제 왕복 측정 위임. 연결돼 있으면 핑 전송 후 true(코어는 출력 안 함),
        // 비연결이면 false(코어가 "Pong!" 응답성 체크).
        private bool HandleConsolePingRequested()
        {
            if (!IsClient && !IsServer)
            {
                return false;
            }

            RequestPing();
            return true;
        }

        // 치트 허용 범위가 바뀌면 클라→서버 릴레이 전송 권한을 맞춘다(All일 때만 허용).
        private void HandleCheatPermissionChanged(CheatPermission permission)
        {
            AllowClientToServerCommands = permission == CheatPermission.All;
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
                McConsole.SetNetworkPingHandler(null);
                CheatPolicy.PermissionChanged -= HandleCheatPermissionChanged;
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
            customMessagingManager.UnregisterNamedMessageHandler(PingRequestMessageName);
            customMessagingManager.UnregisterNamedMessageHandler(PingResultMessageName);
            customMessagingManager.RegisterNamedMessageHandler(CapabilityMessageName, HandleCapabilityMessage);
            customMessagingManager.RegisterNamedMessageHandler(CommandRequestMessageName, HandleCommandRequestMessage);
            customMessagingManager.RegisterNamedMessageHandler(CommandResultMessageName, HandleCommandResultMessage);
            customMessagingManager.RegisterNamedMessageHandler(PingRequestMessageName, HandlePingRequestMessage);
            customMessagingManager.RegisterNamedMessageHandler(PingResultMessageName, HandlePingResultMessage);
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
                customMessagingManager.UnregisterNamedMessageHandler(PingRequestMessageName);
                customMessagingManager.UnregisterNamedMessageHandler(PingResultMessageName);
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
            // 세션 종료 시 치트 허용을 Off로 되돌려, 다음 세션이 항상 잠긴 상태로 시작하게 한다.
            CheatPolicy.SetPermission(CheatPermission.Off);
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

            // 서버측 치트 게이트(방어선): 원격 클라가 보낸 치트 명령은 권한(allowcheat all)이 있을 때만 실행.
            if (command.IsCheat && !CommandLibrary.IsCheatAllowed(isRemote: true))
            {
                resultMessage = "치트 명령이 허용되지 않았습니다. (호스트: allowcheat all)";
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

        /// <summary>
        /// 서버까지의 왕복 지연(ms)을 측정해 콘솔에 출력한다(클라 전용 측정).
        /// 호스트(=서버 로컬)는 네트워크 왕복이 없으므로 즉시 0ms를 출력하고, 미연결이면 에러를 낸다.
        /// </summary>
        public void RequestPing()
        {
            if (NetworkManager.Singleton == null || !IsClient)
            {
                if (IsServer)
                {
                    McConsole.MessageSuccess("Ping: 0.0 ms (호스트=서버 로컬)");
                    return;
                }
                McConsole.MessageError("핑은 네트워크에 연결된 상태에서만 사용할 수 있습니다.");
                return;
            }

            // 호스트(서버이자 클라)는 자기 자신으로의 왕복이 의미 없으므로 로컬로 처리.
            if (IsServer)
            {
                McConsole.MessageSuccess("Ping: 0.0 ms (호스트=서버 로컬)");
                return;
            }

            var customMessagingManager = NetworkManager.Singleton.CustomMessagingManager;
            if (customMessagingManager == null)
            {
                McConsole.MessageError("Network console messaging is not available.");
                return;
            }

            uint seq = unchecked(++_pingSequence);
            // 응답이 끝내 안 오는 비정상 누적을 막는 단순 안전장치.
            if (_pendingPings.Count > 64)
            {
                _pendingPings.Clear();
            }
            _pendingPings[seq] = Time.realtimeSinceStartupAsDouble;

            using var writer = new FastBufferWriter(sizeof(uint), Unity.Collections.Allocator.Temp);
            writer.WriteValueSafe(seq);
            customMessagingManager.SendNamedMessage(PingRequestMessageName, NetworkManager.ServerClientId, writer);
        }

        // 서버: 클라가 보낸 ping 요청의 시퀀스를 그대로 echo해서 돌려준다.
        private void HandlePingRequestMessage(ulong senderClientId, FastBufferReader reader)
        {
            if (!IsServer)
            {
                return;
            }

            reader.ReadValueSafe(out uint seq);
            var customMessagingManager = NetworkManager.Singleton?.CustomMessagingManager;
            if (customMessagingManager == null)
            {
                return;
            }

            using var writer = new FastBufferWriter(sizeof(uint), Unity.Collections.Allocator.Temp);
            writer.WriteValueSafe(seq);
            customMessagingManager.SendNamedMessage(PingResultMessageName, senderClientId, writer);
        }

        // 클라: echo된 시퀀스로 전송 시각을 찾아 경과 ms를 계산해 출력한다.
        private void HandlePingResultMessage(ulong senderClientId, FastBufferReader reader)
        {
            if (!IsMessageFromServer(senderClientId))
            {
                return;
            }

            reader.ReadValueSafe(out uint seq);
            if (!_pendingPings.TryGetValue(seq, out double sentAt))
            {
                return; // 이미 정리됐거나 알 수 없는 응답 → 무시.
            }

            _pendingPings.Remove(seq);
            double ms = (Time.realtimeSinceStartupAsDouble - sentAt) * 1000.0;
            McConsole.MessageSuccess($"Ping: {ms:0.0} ms (서버 왕복)");
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
