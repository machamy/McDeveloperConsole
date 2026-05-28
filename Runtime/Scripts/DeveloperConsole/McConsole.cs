using Machamy.Utils;
using System;
using System.Collections.Generic;
using Machamy.DeveloperConsole.Commands;
using Machamy.DeveloperConsole.Attributes;
using UnityEngine;
using UnityEngine.Scripting;

namespace Machamy.DeveloperConsole
{
    public readonly struct ConsoleOutputMessage
    {
        public ConsoleOutputMessage(MessageType type, string message)
        {
            Type = type;
            Message = message;
        }

        public MessageType Type { get; }
        public string Message { get; }
    }

    public readonly struct ConsoleResponseTarget
    {
        internal ConsoleResponseTarget(ulong clientId)
        {
            ClientId = clientId;
            IsValid = true;
        }

        public ulong ClientId { get; }
        public bool IsValid { get; }
    }

    public interface IConsoleResponseDispatcher
    {
        bool Respond(ConsoleResponseTarget target, MessageType type, string message);
    }

    /// <summary>
    /// (eng) The main class for the debug console.<br/>
    /// It serves as the Model and Controller for the console.<br/>
    /// (kor) 디버그 콘솔의 메인 클래스입니다.<br/>
    /// 콘솔의 모델이자 컨트롤러 역할을 합니다.
    /// </summary>
    public class McConsole
    {
        private static McConsole _mcConsole;
        public static McConsole Instance => _mcConsole ??= new McConsole();

        private IConsoleCommand _lastCommand;
        private IConsoleCommand _currentCommand;

        private IConsoleWindow _window;
        private LogLevel _logPrintLevel = LogLevel.Warning;
        private static readonly Stack<ResponseCaptureContext> ResponseCaptureStack = new();
        private static IConsoleResponseDispatcher _responseDispatcher;

        public static Action<bool> OnConsoleWindowToggled;

        public IConsoleCommand LastCommand
        {
            get => _lastCommand;
        }
        public IConsoleCommand CurrentCommand
        {
            get => _currentCommand;
        }

        public LogLevel LogPrintLevel
        {
            get => _logPrintLevel;
            set => _logPrintLevel = value;
        }

        public bool IsWindowOpen => _window?.IsOpen ?? false;
        public static bool IsCapturingResponse => ResponseCaptureStack.Count > 0;
#if !DO_NOT_USE_DEBUG_CONSOLE
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Initialize()
        {
            if (_mcConsole != null) return;
            _mcConsole = new McConsole();

            Application.logMessageReceived += _mcConsole.OnLogReceived;
        }

        ~McConsole()
        {
            Application.logMessageReceived -= OnLogReceived;
        }
#endif
        /// <summary>
        /// (eng) Registers a console window to the console.<br/>
        /// If a window is already registered, it will be replaced.<br/>
        /// (kor) 콘솔 윈도우를 콘솔에 등록합니다.<br/>
        /// 이미 윈도우가 등록되어 있으면 대체됩니다.
        /// </summary>
        /// <param name="window"></param>
        public void RegisterWindow(IConsoleWindow window)
        {
            if (_window != null)
            {
                LogEx.LogWarning("A console window is already registered. Replacing it with the new one.");
                _window.Close();
                if (_window is MonoBehaviour behaviour)
                {
                    UnityEngine.Object.Destroy(behaviour);
                }
            }
            _window = window;
        }

        public static void SetResponseDispatcher(IConsoleResponseDispatcher responseDispatcher)
        {
            _responseDispatcher = responseDispatcher;
        }

        public static IDisposable BeginResponseCapture(ConsoleResponseTarget target, List<ConsoleOutputMessage> outputMessages)
        {
            if (!target.IsValid)
            {
                throw new ArgumentException("Response target is invalid.", nameof(target));
            }

            if (outputMessages == null)
            {
                throw new ArgumentNullException(nameof(outputMessages));
            }

            ResponseCaptureStack.Push(new ResponseCaptureContext(target, outputMessages));
            return new ResponseCaptureScope();
        }

        public static ConsoleResponseTarget CreateResponseTarget(ulong clientId)
        {
            return new ConsoleResponseTarget(clientId);
        }

        public static ConsoleResponseTarget CaptureResponseTarget()
        {
            return ResponseCaptureStack.Count > 0 ? ResponseCaptureStack.Peek().Target : default;
        }

        public static bool Respond(ConsoleResponseTarget target, MessageType type, string message)
        {
            if (!target.IsValid || _responseDispatcher == null)
            {
                return false;
            }

            return _responseDispatcher.Respond(target, type, message);
        }

        public static bool RespondInfo(ConsoleResponseTarget target, string message)
        {
            return Respond(target, MessageType.Info, message);
        }

        public static bool RespondWarning(ConsoleResponseTarget target, string message)
        {
            return Respond(target, MessageType.Warning, message);
        }

        public static bool RespondError(ConsoleResponseTarget target, string message)
        {
            return Respond(target, MessageType.Error, message);
        }

        public static bool RespondSuccess(ConsoleResponseTarget target, string message)
        {
            return Respond(target, MessageType.Success, message);
        }

        /// <summary>
        /// (eng) Unregisters the console window from the console.<br/>
        /// If the provided window is null or matches the registered window, it will be unregistered.<br/>
        /// (kor) 콘솔 윈도우를 콘솔에서 등록 해제합니다.<br/>
        /// 제공된 윈도우가 null이거나 등록된 윈도우와 일치하면 등록 해제됩니다.
        /// </summary>
        /// <param name="window"></param>
        public void UnregisterWindow(IConsoleWindow window)
        {
            if (_window == null) return;

            if (window == null || _window == window)
            {
                _window.Close();
                if (_window is MonoBehaviour behaviour)
                {
                    UnityEngine.Object.Destroy(behaviour);
                }
                _window = null;
            }
        }

        /// <summary>
        /// (eng) Provides auto-completion suggestions based on the current input.<br/>
        /// (kor) 현재 입력을 기반으로 자동 완성 제안을 제공합니다.
        /// </summary>
        /// <param name="input"></param>
        /// <param name="suggestions"></param>
        public void GetAutoCompleteSuggestions(string[] input, ref List<string> suggestions)
        {
            suggestions.Clear();

        }

        /// <summary>
        /// (eng) Handles log messages received from the application.<br/>
        /// It filters messages based on the set log print level and forwards them to the console window.<br/>
        /// (kor) 애플리케이션에서 수신된 로그 메시지를 처리합니다.<br/>
        /// 설정된 로그 출력 레벨에 따라 메시지를 필터링하고 콘솔 창으로 전달합니다.
        /// </summary>
        /// <param name="condition"></param>
        /// <param name="stackTrace"></param>
        /// <param name="type"></param>
        private void OnLogReceived(string condition, string stackTrace, LogType type)
        {
            if(_window == null) return;
            if ((int)type > (int)_logPrintLevel) return;
            _window.Print(type, condition);
        }


        /// <summary>
        /// (eng) Executes a console command based on the input string.<br/>
        /// It parses the command and its arguments, finds the corresponding command in the CommandLibrary,
        /// and executes it. If the command is not found, it logs an error message.<br/>
        /// (kor) 입력 문자열을 기반으로 콘솔 명령을 실행합니다.<br/>
        /// 명령과 그 인수를 구문 분석하고 CommandLibrary에서 해당 명령을 찾아 실행합니다.
        /// 명령을 찾을 수 없으면 오류 메시지를 기록합니다.
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public bool ExecuteCommand(string input)
        {

#if !DO_NOT_USE_DEBUG_CONSOLE
            if (_window == null) return false;
            if (string.IsNullOrWhiteSpace(input)) return false;

            if (!ConsoleCommandTokenizer.TryTokenizeForExecution(input, out var tokens, out string parseError))
            {
                Message(MessageType.Error, parseError);
                return false;
            }

            if (tokens.Count == 0)
            {
                return false;
            }

            var commandName = tokens[0].Value;
            var args = new string[tokens.Count - 1];
            for (int i = 1; i < tokens.Count; i++)
            {
                args[i - 1] = tokens[i].Value;
            }

#if MCDEVCONSOLE_USE_NGO
            if (CommandLibrary.TryGetAvailableCommand(commandName, out var command))
#else
            if (CommandLibrary.TryGetCommand(commandName, out var command))
#endif
            {
                _currentCommand = command;
                try
                {
                    LogEx.Log($"Executing command: {commandName} with args: {string.Join(", ", args)}");
#if MCDEVCONSOLE_USE_NGO
                    if (CommandLibrary.GetCommandScope(command) == ConsoleCommandScope.ClientToServer && CommandLibrary.TryRequestRemoteCommand(input, command))
                    {
                        _lastCommand = _currentCommand;
                        return true;
                    }
#endif
                    command.Execute(args);
                }
                catch (Exception ex)
                {
                    Message(MessageType.Error, $"Error executing command '{commandName}': {ex.Message}\n{ex.StackTrace}");
                    _window.MessageInfo(_currentCommand.Signature);
                }
                _lastCommand = _currentCommand;
                return true;
            }
            else
            {
                Message(MessageType.Error, $"Unknown command: '{commandName}'. Type 'help' for a list of commands.");
                return false;
            }
#else
            return false;
#endif
        }

        public static void Print(string message)
        {
            Instance.PrintInternal(LogType.Log, message);
        }
        public static void Print(LogType type, string message)
        {
            Instance.PrintInternal(type, message);
        }

        public static void Message(string message)
        {
            Instance.MessageInternal(message);
        }

        public static void Message(MessageType type, string message)
        {
            Instance?.MessageInternal(type, message);
        }
        public static void MessageDefault(string message) => Instance?.MessageInternal(message);
        public static void MessageInfo(string message) => Instance?.MessageInternal(MessageType.Info, message);
        public static void MessageWarning(string message) => Instance?.MessageInternal(MessageType.Warning, message);
        public static void MessageError(string message) => Instance?.MessageInternal(MessageType.Error, message);
        public static void MessageDebug(string message) => Instance?.MessageInternal(MessageType.Debug, message);
        public static void MessageSuccess(string message) => Instance?.MessageInternal(MessageType.Success, message);

        private void MessageInternal(string message)
        {
            if (TryCaptureMessage(MessageType.Default, message))
            {
                return;
            }

            _window?.Message(message);
        }
        private void MessageInternal(MessageType type, string message)
        {
            if (TryCaptureMessage(type, message))
            {
                return;
            }

            _window?.Message(type, message);
        }
        private void PrintInternal(LogType type, string message)
        {
            _window?.Print(type, message);
        }


        [Preserve, ConsoleCommand("echo", "Prints the input arguments back to the console.", "echo <message>", new []{"Hello, World!", "This is a test message."})]
        private static void EchoCommand(string[] args)
        {
            string message = string.Join(" ", args);
            McConsole.MessageDefault(message);
        }
        /// <summary>
        /// (eng) Sets the log print level for the console.<br/>
        /// This method is called from the <see cref="CommandLibrary.setLogLevelCommand"/>.<br/>
        /// (kor) 콘솔의 로그 출력 레벨을 설정합니다.<br/>
        /// <see cref="CommandLibrary.setLogLevelCommand"/> 에서 호출됩니다.
        /// </summary>
        /// <param name="level"></param>
        [Preserve, ConsoleCommand("setLogLevel", "Sets the log print level for the console. ", "setLogLevel <level> (0: None, 1: Exception, 2: Error, 3: Warning, 4: Info)", new []{"0","1","2","3","4"})]
        public static void SetLogLevel(LogLevel level)
        {
            Instance.LogPrintLevel = level;
            McConsole.MessageDefault($"Log print level set to: {level}");
        }

        private static bool TryCaptureMessage(MessageType type, string message)
        {
            if (ResponseCaptureStack.Count == 0)
            {
                return false;
            }

            ResponseCaptureStack.Peek().OutputMessages.Add(new ConsoleOutputMessage(type, message));
            return true;
        }

        private readonly struct ResponseCaptureContext
        {
            public ResponseCaptureContext(ConsoleResponseTarget target, List<ConsoleOutputMessage> outputMessages)
            {
                Target = target;
                OutputMessages = outputMessages;
            }

            public ConsoleResponseTarget Target { get; }
            public List<ConsoleOutputMessage> OutputMessages { get; }
        }

        private readonly struct ResponseCaptureScope : IDisposable
        {
            public void Dispose()
            {
                if (ResponseCaptureStack.Count > 0)
                {
                    ResponseCaptureStack.Pop();
                }
            }
        }

    }
}
