using System;
using System.Collections.Generic;
using Machamy.DeveloperConsole.Attributes;

namespace Machamy.DeveloperConsole.Commands
{
    /// <summary>
    /// (kor) 치트(개발 명령) 허용 범위를 설정하는 빌트인 명령. 호스트(서버)/단독 전용.
    /// off=아무도 불가, host=호스트만, all=모든 클라이언트, status=현재 상태. 무인자 = off↔host 토글.
    /// </summary>
    [ConsoleCommandClass]
    public sealed class AllowCheatCommand : IConsoleCommand
    {
        public string Command => "allowcheat";
        public string Description => "치트(개발 명령) 허용 범위 설정. off=아무도, host=호스트만, all=모든 클라.";
        public string Signature => "allowcheat [off|host|all|status]";
        public bool IsCheat => false; // 권한 제어 명령 자체는 치트가 아님(항상 보이고 실행 가능해야 잠김 방지).

        public void Execute(string[] args) => CheatCommandCore.Execute(args, Signature);

        public void AutoComplete(Span<string> args, ref List<string> suggestions) =>
            CheatCommandCore.AutoComplete(args, ref suggestions);
    }

    /// <summary>(kor) 'allowcheat'의 별칭.</summary>
    [ConsoleCommandClass]
    public sealed class AllowCheatsAliasCommand : IConsoleCommand
    {
        public string Command => "allowcheats";
        public string Description => "'allowcheat'의 별칭.";
        public string Signature => "allowcheats [off|host|all|status]";
        public bool IsCheat => false;

        public void Execute(string[] args) => CheatCommandCore.Execute(args, Signature);

        public void AutoComplete(Span<string> args, ref List<string> suggestions) =>
            CheatCommandCore.AutoComplete(args, ref suggestions);
    }

    /// <summary>allowcheat / allowcheats 공용 실행 로직.</summary>
    internal static class CheatCommandCore
    {
        public static void Execute(string[] args, string usage)
        {
#if MCDEVCONSOLE_USE_NGO
            // 호스트(서버) 전용. (런타임 컨텍스트 없으면 = 단독 환경 → 허용)
            if (!CommandLibrary.IsLocalAuthority)
            {
                McConsole.MessageError("호스트(서버)에서만 변경할 수 있습니다.");
                return;
            }
#endif

            string action = args.Length >= 1 ? args[0].ToLowerInvariant() : string.Empty;

            // 무인자 → off ↔ host 토글.
            if (action.Length == 0)
            {
                CheatPermission toggled =
                    CheatPolicy.Permission == CheatPermission.Off ? CheatPermission.Host : CheatPermission.Off;
                Apply(toggled, "(토글)");
                return;
            }

            if (action == "status")
            {
                McConsole.MessageInfo($"현재 치트 허용: {Describe(CheatPolicy.Permission)}. 사용법: {usage}");
                return;
            }

            CheatPermission? desired = action switch
            {
                "off" => CheatPermission.Off,
                "host" => CheatPermission.Host,
                "all" or "on" => CheatPermission.All, // on = 구버전 별칭(=all)
                _ => (CheatPermission?)null,
            };
            if (desired == null)
            {
                McConsole.MessageError($"알 수 없는 인자 '{args[0]}'. 사용법: {usage}");
                return;
            }

            Apply(desired.Value, null);
        }

        private static void Apply(CheatPermission permission, string suffix)
        {
            // 권한 변경 → CheatPolicy.PermissionChanged 발생. NGO 어댑터가 이를 구독해 클라→서버 릴레이 전송 권한
            // (All일 때만 허용)을 맞춘다. 코어는 넷코드 어셈블리를 참조할 수 없으므로 어댑터를 직접 건드리지 않는다.
            CheatPolicy.SetPermission(permission);

            string tail = string.IsNullOrEmpty(suffix) ? string.Empty : " " + suffix;
            McConsole.MessageSuccess($"치트 허용: {Describe(permission)}{tail}.");
        }

        public static void AutoComplete(Span<string> args, ref List<string> suggestions)
        {
            if (args.Length != 1)
            {
                return;
            }

            string current = args[0];
            foreach (string v in new[] { "off", "host", "all", "status" })
            {
                if (string.IsNullOrEmpty(current) || v.StartsWith(current, StringComparison.OrdinalIgnoreCase))
                {
                    suggestions.Add(v);
                }
            }
        }

        private static string Describe(CheatPermission permission)
        {
            return permission switch
            {
                CheatPermission.Off => "OFF (아무도 불가)",
                CheatPermission.Host => "HOST (호스트만)",
                CheatPermission.All => "ALL (모든 클라이언트)",
                _ => permission.ToString(),
            };
        }
    }
}
