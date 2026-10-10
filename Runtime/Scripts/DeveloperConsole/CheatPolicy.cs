namespace Machamy.DeveloperConsole
{
    /// <summary>
    /// (kor) 치트(개발) 명령 허용 범위.
    /// Off = 아무도 불가(호스트 포함), Host = 호스트(서버) 또는 단독 환경만, All = 모든 클라이언트.
    /// </summary>
    public enum CheatPermission
    {
        Off = 0,
        Host = 1,
        All = 2,
    }

    /// <summary>
    /// (kor) 콘솔 치트 명령 허용 정책. 패키지가 소유하며 게임/네트워크 코드에 의존하지 않는다.
    /// allowcheat 빌트인 명령이 권한을 바꾸고, IsCheat=true 명령의 자동완성/실행 가용성을 이 정책으로 게이트한다.
    /// NGO든 단독(비네트워크) 환경이든 동일하게 동작한다(단독 환경은 isRemote가 항상 false).
    /// </summary>
    public static class CheatPolicy
    {
        /// <summary>현재 치트 허용 범위. 기본값은 Off(아무도 불가) — 치트 명령은 기본적으로 숨김+차단.</summary>
        public static CheatPermission Permission { get; private set; } = CheatPermission.Off;

        /// <summary>권한이 바뀔 때 발생. NGO 어댑터가 구독해 클라→서버 릴레이 전송 권한(transport)을 맞춘다.</summary>
        public static event System.Action<CheatPermission> PermissionChanged;

        public static void SetPermission(CheatPermission permission)
        {
            Permission = permission;
            PermissionChanged?.Invoke(permission);
        }

        /// <summary>
        /// 이 권한 하에서 치트 명령 실행이 허용되는지.
        /// <paramref name="isRemote"/> = 원격 클라이언트가 보낸 명령이면 true(호스트 본인/단독 로컬은 false).
        /// </summary>
        public static bool IsCheatAllowed(bool isRemote)
        {
            return Permission switch
            {
                CheatPermission.All => true,
                CheatPermission.Host => !isRemote,
                _ => false,
            };
        }
    }
}
