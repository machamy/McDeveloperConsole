# Changelog

## [1.1.0] - 2026-10-10

Downwelling과 unity-3d-coop-adventure-framework(`com.machamy.developerconsole`)에서 개발된 내용을 원본 저장소에 반영했다.

### 추가
- 치트 정책: `CheatPolicy`(Off/Host/All)와 `allowcheat` 빌트인 명령(`CheatCommands`). 명령은 `IConsoleCommand.IsCheat`로 치트 여부를 밝히고, 서버는 원격 클라이언트의 치트 명령을 권한이 있을 때만 실행한다. 세션이 끝나면 Off로 되돌린다.
- 실제 왕복 지연 측정: `ping`/`net.serverPing`이 전용 named message로 서버까지 왕복 시간(ms)을 잰다(`McConsoleNetcodeAdapter.RequestPing`, `McConsole.SetNetworkPingHandler`).
- Netcode 통합 테스트(`McDeveloperConsole.Netcode.Tests`).

### 변경
- `com.unity.inputsystem` 의존을 1.6.0에서 1.18.0으로 올렸다(Unity 6.3 레지스트리에 1.6.0이 없다).
- asmdef 참조를 GUID에서 어셈블리 이름(`McDeveloperConsole`, `Unity.InputSystem`)으로 바꿨다.
- `McConsole.OnConsoleWindowToggled`가 `ConsoleUI.Open()`/`Close()`에서 호출되고, 열린 상태로 컴포넌트가 활성/비활성될 때도 호출된다.
