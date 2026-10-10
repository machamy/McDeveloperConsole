#if MCDEVCONSOLE_USE_NGO
using System;
using System.Collections.Generic;
using Machamy.DeveloperConsole;
using Machamy.DeveloperConsole.Attributes;
using Machamy.DeveloperConsole.Commands;
using Unity.Netcode;

namespace Machamy.ConsoleCommands.Netcode
{
    internal static class NetcodeCommandUtility
    {
        public static bool TryParseToggle(string value, out bool enabled)
        {
            enabled = string.Equals(value, "on", StringComparison.OrdinalIgnoreCase) ||
                      string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
                      value == "1";

            return enabled ||
                   string.Equals(value, "off", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "false", StringComparison.OrdinalIgnoreCase) ||
                   value == "0";
        }

        public static void AddToggleSuggestions(string current, List<string> suggestions)
        {
            AddIfMatches("on", current, suggestions);
            AddIfMatches("off", current, suggestions);
        }

        private static void AddIfMatches(string value, string current, List<string> suggestions)
        {
            if (string.IsNullOrWhiteSpace(current) ||
                value.IndexOf(current, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                suggestions.Add(value);
            }
        }
    }

    [ConsoleCommandClass]
    public sealed class NetStatusConsoleCommand : IConsoleCommand
    {
        public string Command => "net.status";
        public string Description => "Prints the current network console status.";
        public string Signature => "net.status";
        public ConsoleCommandScope Scope => ConsoleCommandScope.Local;

        public void Execute(string[] args)
        {
            var networkManager = NetworkManager.Singleton;
            if (networkManager == null)
            {
                McConsole.MessageInfo("NetworkManager is null.");
                return;
            }

            McConsole.MessageInfo($"IsClient={networkManager.IsClient}, IsServer={networkManager.IsServer}, IsHost={networkManager.IsHost}, IsListening={networkManager.IsListening}");
        }
    }

    [ConsoleCommandClass]
    public sealed class NetRemoteConsoleCommand : IConsoleCommand
    {
        public string Command => "net.remoteConsole";
        public string Description => "Toggles client-to-server console commands.";
        public string Signature => "net.remoteConsole <on|off>";
        public ConsoleCommandScope Scope => ConsoleCommandScope.ServerOnly;

        public void Execute(string[] args)
        {
            if (McConsoleNetcodeAdapter.Instance == null)
            {
                McConsole.MessageError("McConsoleNetcodeAdapter is not available.");
                return;
            }

            bool enabled;
            if (args.Length < 1)
            {
                enabled = !McConsoleNetcodeAdapter.Instance.AllowClientToServerCommands;
            }
            else if (!NetcodeCommandUtility.TryParseToggle(args[0], out enabled))
            {
                McConsole.MessageInfo(Signature);
                return;
            }

            McConsoleNetcodeAdapter.Instance.AllowClientToServerCommands = enabled;
            McConsole.MessageInfo($"Client-to-server console commands are now {(enabled ? "enabled" : "disabled")}.");
        }

        public void AutoComplete(Span<string> args, ref List<string> suggestions)
        {
            if (args.Length == 1)
            {
                NetcodeCommandUtility.AddToggleSuggestions(args[0], suggestions);
            }
        }
    }

    [ConsoleCommandClass]
    public sealed class NetShowRequestsConsoleCommand : IConsoleCommand
    {
        public string Command => "net.showRequests";
        public string Description => "Toggles server-side logging for client console requests.";
        public string Signature => "net.showRequests <on|off>";
        public ConsoleCommandScope Scope => ConsoleCommandScope.ServerOnly;

        public void Execute(string[] args)
        {
            if (McConsoleNetcodeAdapter.Instance == null)
            {
                McConsole.MessageError("McConsoleNetcodeAdapter is not available.");
                return;
            }

            bool enabled;
            if (args.Length < 1)
            {
                enabled = !McConsoleNetcodeAdapter.Instance.LogClientCommandRequests;
            }
            else if (!NetcodeCommandUtility.TryParseToggle(args[0], out enabled))
            {
                McConsole.MessageInfo(Signature);
                return;
            }

            McConsoleNetcodeAdapter.Instance.LogClientCommandRequests = enabled;
            McConsole.MessageInfo($"Client command request logging is now {(enabled ? "enabled" : "disabled")}.");
        }

        public void AutoComplete(Span<string> args, ref List<string> suggestions)
        {
            if (args.Length == 1)
            {
                NetcodeCommandUtility.AddToggleSuggestions(args[0], suggestions);
            }
        }
    }

    [ConsoleCommandClass]
    public sealed class NetServerPingConsoleCommand : IConsoleCommand
    {
        public string Command => "net.serverPing";
        public bool IsCheat => false; // 진단용 핑 → 치트 아님(항상 사용 가능)
        public string Description => "Measures round-trip latency to the server and prints it in ms.";
        public string Signature => "net.serverPing";
        // Local: 클라 로컬에서 실행해 전용 ping 메시지로 직접 왕복(ms)을 측정한다(명령 릴레이 X).
        public ConsoleCommandScope Scope => ConsoleCommandScope.Local;

        public void Execute(string[] args)
        {
            if (McConsoleNetcodeAdapter.Instance == null)
            {
                McConsole.MessageError("McConsoleNetcodeAdapter is not available.");
                return;
            }

            McConsoleNetcodeAdapter.Instance.RequestPing();
        }
    }
}
#endif
