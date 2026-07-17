using MetroEditor.MetroLang.Compiler;
using MetroEditor.MetroLang.Decompiler;
using MetroEditor.MetroLang.JsonModel;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace MetroEditor.MetroLang.Tool
{
    /// <summary>
    /// 提供旧版 MetroLang (.metro) 语法向新版语法迁移的工具方法。
    /// 核心策略：旧源码 -> 编译器解析 -> JSON 中间层 -> 反编译器生成新源码。
    /// </summary>
    public static class MigrationHelper
    {
        /// <summary>
        /// 将旧语法源文件字符串迁移为新语法字符串。
        /// </summary>
        /// <param name="legacySourceCode">旧语法源文本</param>
        /// <param name="errors">输出错误列表（仅在 throwOnError 为 false 时有效）</param>
        /// <param name="throwOnError">
        /// 为 true 时遇到编译错误直接抛出异常；
        /// 为 false 时返回 null 并通过 out 参数返回错误信息。
        /// </param>
        /// <returns>新语法源文本；若失败且未抛出异常，则返回 null</returns>
        public static string? Migrate(string legacySourceCode, string filePath, out List<string>? errors, bool throwOnError = false)
        {
            errors = null;
            try
            {
                // 1. 使用旧编译器解析旧语法
                var compiler = new MetorCompiler();
                compiler.Parse(legacySourceCode, filePath);

                // 2. 导出为标准化 JSON（作为中间表示层）
                JsonRoot jsonRoot = compiler.ExportJson();

                // 3. 使用反编译器将 JSON 转换为新语法
                var decompiler = new MetroDecompiler(jsonRoot);
                return decompiler.Decompile();
            }
            catch (Exception ex)
            {
                errors = new List<string> { ex.Message };
                if (throwOnError)
                    throw;
                return null;
            }
        }

        /// <summary>
        /// 将单个旧语法文件迁移到新语法，并写入目标文件。
        /// </summary>
        /// <param name="sourceFilePath">旧文件路径</param>
        /// <param name="targetFilePath">新文件路径（若为空则自动在原文件名后追加 "_new"）</param>
        /// <param name="overwrite">是否覆盖已存在的目标文件</param>
        /// <param name="throwOnError">是否遇到错误时停止并抛出异常</param>
        /// <returns>是否成功迁移</returns>
        public static bool MigrateFile(
            string sourceFilePath,
            string? targetFilePath = null,
            bool overwrite = false,
            bool throwOnError = false)
        {
            if (!File.Exists(sourceFilePath))
                throw new FileNotFoundException($"源文件不存在: {sourceFilePath}", sourceFilePath);

            // 读取旧源码
            string legacySourceCode = File.ReadAllText(sourceFilePath, Encoding.UTF8);

            // 若未指定输出路径，则自动生成（原文件名 + _new）
            if (string.IsNullOrEmpty(targetFilePath))
            {
                string? directory = Path.GetDirectoryName(sourceFilePath);
                string fileNameWithoutExt = Path.GetFileNameWithoutExtension(sourceFilePath);
                string extension = Path.GetExtension(sourceFilePath);
                targetFilePath = Path.Combine(
                    directory ?? Environment.CurrentDirectory,
                    $"{fileNameWithoutExt}_new{extension}");
            }

            // 检查目标文件是否存在
            if (File.Exists(targetFilePath) && !overwrite)
                throw new IOException($"目标文件已存在且未允许覆盖: {targetFilePath}");

            // 执行迁移
            string? migratedSourceCode = Migrate(legacySourceCode, Path.GetDirectoryName(sourceFilePath), out var errors, throwOnError);
            if (migratedSourceCode == null)
            {
                if (throwOnError)
                    throw new InvalidOperationException($"迁移失败: {string.Join("; ", errors ?? new List<string>())}");
                return false;
            }

            // 写入新文件
            File.WriteAllText(targetFilePath, migratedSourceCode, Encoding.UTF8);
            return true;
        }

        /// <summary>
        /// 批量迁移目录下的所有 .metro 文件。
        /// </summary>
        /// <param name="sourceDirectory">包含 .metro 文件的源目录</param>
        /// <param name="targetDirectory">
        /// 输出目录（若为空则覆盖原文件，并可选进行备份）
        /// </param>
        /// <param name="backupExtension">
        /// 覆盖原文件时的备份扩展名（如 ".bak"）；设为 null 则不备份
        /// </param>
        /// <param name="throwOnError">是否遇到错误时停止并抛出异常</param>
        /// <returns>成功迁移的文件数量</returns>
        public static int MigrateDirectory(
            string sourceDirectory,
            string? targetDirectory = null,
            string? backupExtension = ".bak",
            bool throwOnError = false)
        {
            if (!Directory.Exists(sourceDirectory))
                throw new DirectoryNotFoundException($"源目录不存在: {sourceDirectory}");

            // 递归查找所有 .metro 文件
            var metroFiles = Directory.GetFiles(sourceDirectory, "*.metro", SearchOption.AllDirectories);
            int successCount = 0;

            foreach (string sourceFilePath in metroFiles)
            {
                // 确定目标文件路径
                string destinationFilePath;
                if (!string.IsNullOrEmpty(targetDirectory))
                {
                    // 保持原有的目录结构
                    string relativePath = Path.GetRelativePath(sourceDirectory, sourceFilePath);
                    destinationFilePath = Path.Combine(targetDirectory, relativePath);
                    Directory.CreateDirectory(Path.GetDirectoryName(destinationFilePath)!);
                }
                else
                {
                    // 覆盖原文件模式：先备份（如果指定了备份扩展名）
                    destinationFilePath = sourceFilePath;
                    if (!string.IsNullOrEmpty(backupExtension))
                    {
                        string backupPath = sourceFilePath + backupExtension;
                        // 避免重复备份（如果 bak 文件已存在则跳过）
                        if (!File.Exists(backupPath))
                            File.Copy(sourceFilePath, backupPath, true);
                    }
                }

                try
                {
                    bool isSuccess = MigrateFile(sourceFilePath, destinationFilePath, overwrite: true, throwOnError);
                    if (isSuccess)
                        successCount++;
                    else
                        Console.WriteLine($"[WARN] 迁移失败（未抛出异常）: {sourceFilePath}");
                }
                catch (Exception ex)
                {
                    if (throwOnError)
                        throw;
                    Console.WriteLine($"[ERROR] 迁移 {sourceFilePath} 出错: {ex.Message}");
                }
            }

            return successCount;
        }
    }
}