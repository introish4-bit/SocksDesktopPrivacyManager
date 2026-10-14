# SOCKS Desktop Privacy Manager (Windows 7–11 starter project)

This is a buildable C# WinForms starter project targeting **.NET Framework 4.8**.
It is designed for Windows 7 SP1 through Windows 11, subject to the relevant .NET
Framework 4.8 runtime and OS updates being installed. Windows 7 is unsupported by
Microsoft, so current TLS/certificate behavior may vary.

## Included
- SOCKS5, SOCKS4, HTTP, and HTTPS proxy profile fields.
- Username/password fields (password is masked in the UI).
- TCP/protocol handshake test for SOCKS5 (including RFC 1929 username/password),
  SOCKS4/SOCKS4a, and HTTP CONNECT proxy endpoints.
- Save/load proxy profile locally as JSON.
- Add executable paths as launch rules; launch a selected app directly or through
  a proxy where the app supports command-line proxy arguments.
- Launch Chromium-based browsers (Chrome/Edge/Chromium) with proxy and optional
  custom User-Agent arguments.
- WebRTC leak-test link and notes.

## Important limits (not hidden)
- This starter does **not** include a kernel/network driver or a process-level traffic
  redirector. Therefore it cannot force every arbitrary Windows application to use
  a proxy, and the per-app list is a launcher/rule list, not a system-wide packet filter.
  Apps that ignore proxy settings may still connect directly.
- Chromium proxy command-line flags do not guarantee all WebRTC UDP traffic is proxied.
  Use browser policy/settings that disable non-proxied UDP/WebRTC IP exposure and verify
  with a leak test. Do not treat a successful proxy test as proof of anonymity.
- User-Agent changes apply only to Chromium apps launched by this tool and may be
  overridden by client hints or application behavior.
- CPU/RAM/system hardware values are not changed. Browser-exposed test profile fields
  are simulated only where a browser/application explicitly supports them; this project
  intentionally does not spoof system hardware identity or bypass security/fraud checks.
- HTTPS proxy support means an HTTP CONNECT tunnel through a proxy endpoint. For an
  HTTPS proxy server whose own connection uses TLS, this minimal test client does not
  perform TLS-to-proxy negotiation; use a compatible external proxy client or extend
  the transport implementation.

## Build
1. Install Visual Studio 2019/2022 with the **.NET desktop development** workload and
   .NET Framework 4.8 targeting pack.
2. Open `SocksDesktopPrivacyManager.sln`.
3. Build `Release` / `Any CPU`.
4. Run the generated EXE. On older Windows, install the .NET Framework 4.8 runtime
   compatible with that system.

No proxy credentials are hard-coded. The starter stores profile JSON locally in the
user's application data directory. For production use, replace plain JSON credential
storage with Windows DPAPI or Credential Manager.
