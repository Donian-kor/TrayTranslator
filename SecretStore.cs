using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace TrayTranslator;

// API 키를 Windows DPAPI로 암호화해 저장/복호화.
// CurrentUser 범위라 이 PC의 로그인 계정만 풀 수 있다.
// 다른 계정/PC로 settings.json을 옮겨도 키는 복구되지 않는다(의도된 동작).
static class SecretStore
{
    private const string Prefix = "dpapi:";

    public static bool IsEncrypted(string? v) =>
        !string.IsNullOrEmpty(v) && v.StartsWith(Prefix, StringComparison.Ordinal);

    // 평문 → 암호문. 이미 암호문이면 그대로 반환(멱등).
    public static string Protect(string? plain)
    {
        if (string.IsNullOrEmpty(plain)) return "";
        if (IsEncrypted(plain)) return plain;
        try
        {
            byte[] raw = Encoding.UTF8.GetBytes(plain);
            byte[] cipher = ProtectedData.Protect(raw, null, DataProtectionScope.CurrentUser);
            return Prefix + Convert.ToBase64String(cipher);
        }
        catch { return plain; } // DPAPI 불가 환경에서는 평문 유지(앱이 죽지 않도록)
    }

    // 암호문 → 평문. 평문이면 그대로 반환하므로 기존 설정 파일도 읽힌다.
    public static string Unprotect(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        if (!IsEncrypted(value)) return value;
        try
        {
            string b64 = value[Prefix.Length..];
            byte[] cipher = Convert.FromBase64String(b64);
            byte[] raw = ProtectedData.Unprotect(cipher, null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(raw);
        }
        catch
        {
            // 다른 계정/PC에서 열었거나 파일이 손상됨 → 키가 없다고 취급
            return "";
        }
    }
}
