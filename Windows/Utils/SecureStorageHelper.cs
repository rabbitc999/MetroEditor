using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace MetroEditor.Windows.Utils
{
    /// <summary>
    /// 安全存储辅助类：基于 Windows DPAPI（数据保护API）的敏感信息加密工具
    /// 核心用途：保护 API Key、令牌、数据库连接字符串等敏感配置
    /// 安全边界：加密范围限定为当前 Windows 用户，同一用户上下文下可解密
    /// </summary>
    /// <remarks>
    /// 重要安全特性：
    /// 1. 用户隔离：仅加密该数据的 Windows 用户可解密
    /// 2. 会话绑定：加密数据无法在其他用户会话或机器上直接使用
    /// 3. 透明集成：Windows 自动管理密钥，无需手动存储密钥文件
    /// 
    /// 局限性说明：
    /// - 仅支持 Windows 平台（不适用于 Linux/macOS）
    /// - 若用户重置密码或系统重装，加密数据将无法恢复
    /// - 拥有用户权限的代码均可解密（防外不防内）
    /// </remarks>
    public static class SecureStorageHelper
    {
        /// <summary>
        /// 使用 Windows DPAPI 加密字节数组（当前用户范围）
        /// 这是最核心的安全方法，所有字符串加密最终都调用此方法
        /// </summary>
        /// <param name="plainBytes">原始字节数组</param>
        /// <returns>加密后的字节数组</returns>
        public static byte[] EncryptBytes(byte[] plainBytes)
        {
            if (plainBytes == null || plainBytes.Length == 0)
                return Array.Empty<byte>();

            return ProtectedData.Protect(plainBytes, null, DataProtectionScope.CurrentUser);
        }

        /// <summary>
        /// 使用 Windows DPAPI 解密字节数组（当前用户范围）
        /// </summary>
        /// <param name="encryptedBytes">加密后的字节数组</param>
        /// <returns>解密后的原始字节数组</returns>
        /// <exception cref="CryptographicException">解密失败时抛出</exception>
        public static byte[] DecryptBytes(byte[] encryptedBytes)
        {
            if (encryptedBytes == null || encryptedBytes.Length == 0)
                return Array.Empty<byte>();

            return ProtectedData.Unprotect(encryptedBytes, null, DataProtectionScope.CurrentUser);
        }

        /// <summary>
        /// 判断给定的字节数组是否可能是 DPAPI 加密后的数据
        /// 注意：这是启发式检测，基于 DPAPI 加密数据的特征
        /// </summary>
        /// <param name="bytes">待检测的字节数组</param>
        /// <returns>是否可能是加密数据</returns>
        /// <remarks>
        /// DPAPI 加密特征：
        /// 1. 最小长度：通常大于 16 字节
        /// 2. 包含 DPAPI 特定的头部信息
        /// 3. 随机性高（熵值高）
        /// 
        /// 局限性：无法100%确定，只能给出概率判断
        /// </remarks>
        public static bool IsProbablyEncryptedBytes(byte[] bytes)
        {
            if (bytes == null || bytes.Length < 16)
                return false;

            // DPAPI 加密数据通常以特定字节序列开始
            // 检查常见的 DPAPI 头部特征
            if (bytes.Length >= 4)
            {
                // DPAPI BLOB 头部通常有特定的魔数
                // 这里使用简单的启发式规则
                int nonPrintableCount = 0;
                int sampleSize = Math.Min(bytes.Length, 32);

                for (int i = 0; i < sampleSize; i++)
                {
                    byte b = bytes[i];
                    if (b < 32 || b > 126) // 非可打印ASCII字符
                        nonPrintableCount++;
                }

                // 如果样本中大部分是不可打印字符，很可能是加密数据
                return nonPrintableCount > sampleSize * 0.7;
            }

            return false;
        }

        /// <summary>
        /// 将字节数组转换为 Base64 编码字符串
        /// 核心编码方法，所有字符串编码最终都调用此方法
        /// </summary>
        /// <param name="bytes">原始字节数组</param>
        /// <returns>Base64 编码字符串</returns>
        public static string Base64EncodeBytes(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
                return string.Empty;

            return Convert.ToBase64String(bytes);
        }

        /// <summary>
        /// 将 Base64 编码字符串还原为字节数组
        /// </summary>
        /// <param name="base64String">Base64 编码字符串</param>
        /// <returns>原始字节数组</returns>
        /// <exception cref="FormatException">格式无效时抛出</exception>
        public static byte[] Base64DecodeBytes(string base64String)
        {
            if (string.IsNullOrEmpty(base64String))
                return Array.Empty<byte>();

            return Convert.FromBase64String(base64String);
        }

        /// <summary>
        /// 判断给定的字节数组是否符合 Base64 编码的特征
        /// </summary>
        /// <param name="bytes">待检测的字节数组</param>
        /// <returns>是否符合 Base64 编码特征</returns>
        /// <remarks>
        /// Base64 编码特征：
        /// 1. 字节值在 0-255 范围内
        /// 2. 编码后的字节数组长度总是 4 的倍数
        /// 3. 只包含 Base64 字符集对应的字节值
        /// </remarks>
        public static bool IsBase64Bytes(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
                return false;

            // Base64 编码后的字节数组长度必须是 4 的倍数
            if (bytes.Length % 4 != 0)
                return false;

            // Base64 字符集对应的字节值范围
            // A-Z: 65-90, a-z: 97-122, 0-9: 48-57, +: 43, /: 47, =: 61
            foreach (byte b in bytes)
            {
                bool isValid = b >= 65 && b <= 90 ||   // A-Z
                              b >= 97 && b <= 122 ||  // a-z
                              b >= 48 && b <= 57 ||   // 0-9
                              b == 43 || b == 47 ||     // + /
                              b == 61;                  // =

                if (!isValid)
                    return false;
            }

            // 检查填充字符 '=' (61) 的位置
            int paddingCount = 0;
            for (int i = bytes.Length - 1; i >= 0 && bytes[i] == 61; i--)
                paddingCount++;

            return paddingCount <= 2;
        }

        /// <summary>
        /// 尝试将字节数组从 Base64 解码，返回是否成功
        /// </summary>
        /// <param name="base64Bytes">Base64 编码的字节数组</param>
        /// <param name="decodedBytes">解码后的字节数组</param>
        /// <returns>解码是否成功</returns>
        public static bool TryBase64DecodeBytes(byte[] base64Bytes, out byte[] decodedBytes)
        {
            decodedBytes = null;
            if (base64Bytes == null || base64Bytes.Length == 0)
                return false;

            try
            {
                decodedBytes = Convert.FromBase64String(Encoding.ASCII.GetString(base64Bytes));
                return true;
            }
            catch (FormatException)
            {
                return false;
            }
        }

        /// <summary>
        /// 将普通字符串转换为 Base64 编码
        /// 内部实现：字符串 → UTF8字节数组 → Base64编码
        /// </summary>
        public static string Base64Encode(string plainText)
        {
            if (string.IsNullOrEmpty(plainText))
                return string.Empty;

            byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
            return Base64EncodeBytes(plainBytes);
        }

        /// <summary>
        /// 将 Base64 编码还原为普通字符串
        /// 内部实现：Base64解码 → UTF8字节数组 → 字符串
        /// </summary>
        public static string Base64Decode(string base64Text)
        {
            if (string.IsNullOrEmpty(base64Text))
                return string.Empty;

            byte[] plainBytes = Base64DecodeBytes(base64Text);
            return Encoding.UTF8.GetString(plainBytes);
        }

        /// <summary>
        /// 加密明文字符串为 Base64 格式密文
        /// </summary>
        /// <param name="plainText">待加密的明文（如 API Key）</param>
        /// <returns>Base64 编码的加密字符串；空输入返回空字符串</returns>
        /// <example>
        /// string encrypted = SecureStorageHelper.Encrypt("amap_api_key_123456");
        /// </example>
        public static string Encrypt(string plainText)
        {
            // 防御性编程：空输入直接返回，避免无效加密操作
            if (string.IsNullOrEmpty(plainText))
                return string.Empty;

            // 将明文字符串转换为 UTF-8 字节数组（统一编码标准）
            byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);

            // 调用 Windows DPAPI 执行加密
            // 参数说明：
            // - plainBytes：待加密的原始字节
            // - null：可选熵值（Entropy），此处未使用（详见备注）
            // - DataProtectionScope.CurrentUser：限定为当前用户可解密
            byte[] encrypted = ProtectedData.Protect(plainBytes, null, DataProtectionScope.CurrentUser);

            // 将二进制密文转换为 Base64 字符串，便于文本存储（如配置文件）
            return Convert.ToBase64String(encrypted);
        }

        /// <summary>
        /// 解密 Base64 格式密文为明文字符串
        /// </summary>
        /// <param name="cipherText">Base64 编码的加密字符串</param>
        /// <returns>解密后的明文；空输入返回空字符串</returns>
        /// <exception cref="CryptographicException">
        /// 触发场景：
        /// 1. 密文被其他用户加密
        /// 2. 密文在另一台机器上生成
        /// 3. 密文已损坏或被篡改
        /// 4. Windows 用户凭据已重置
        /// </exception>
        /// <example>
        /// string decrypted = SecureStorageHelper.Decrypt(encryptedKey);
        /// </example>
        public static string Decrypt(string cipherText)
        {
            // 防御性编程：空输入直接返回
            if (string.IsNullOrEmpty(cipherText))
                return string.Empty;

            // 将 Base64 密文还原为二进制数据
            byte[] encrypted = Convert.FromBase64String(cipherText);

            // 调用 Windows DPAPI 执行解密（参数需与加密时完全一致）
            byte[] plainBytes = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);

            // 将解密后的字节数组转换回 UTF-8 字符串
            return Encoding.UTF8.GetString(plainBytes);
        }

        /// <summary>
        /// 启发式检测字符串是否可能为 DPAPI 加密后的 Base64 密文
        /// 注意：此为格式校验，不保证解密一定能成功
        /// </summary>
        /// <param name="value">待检测的字符串</param>
        /// <returns>
        /// true：符合 Base64 密文特征；
        /// false：明显非密文（如普通文本）
        /// </returns>
        /// <remarks>
        /// 检测逻辑：
        /// 1. 长度特征：Base64 字符串长度通常为 4 的倍数
        /// 2. 字符集特征：仅包含 A-Z、a-z、0-9、+、/、=（填充符）
        /// 
        /// 局限性：
        /// - 无法区分加密数据和普通 Base64（如图片编码）
        /// - 无法验证数据完整性或解密权限
        /// - 仅用于预处理阶段的格式过滤
        /// </remarks>
        public static bool IsProbablyEncrypted(string value)
        {
            if (string.IsNullOrEmpty(value))
                return false;

            // 综合检测：长度对齐 + 字符集合规
            return value.Length % 4 == 0 &&
                   value.All(c => char.IsLetterOrDigit(c) || c == '+' || c == '/' || c == '=');
        }
    }
}