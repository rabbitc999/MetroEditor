using MetroEditor.MetroLang.DataModel;
using MetroEditor.MetroLang.JsonModel;
using MetroEditor.Simulator.Algorithms;
using MetroEditor.Simulator.Data;
using MetroEditor.Simulator.Utils;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace MetroEditor.MetroLang.Exporter
{
    /// <summary>
    /// MBIN 二进制导出器（小端模式，字符串池为 GBK 编码）
    /// </summary>
    public static class MBINExporter
    {
        /// <summary>
        /// GZip压缩JSON：生成.bmetro格式文件
        /// 设计要点：
        /// 1. 对称设计：与DecompressJson一一对应，保证编解码一致性
        /// 2. 中文保留：使用UnsafeRelaxedJsonEscaping避免中文被转义
        /// </summary>
        private static byte[] CompressJson(string json)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            Encoding StringEncoding = Encoding.GetEncoding(936); // GBK

            byte[] utf8Bytes = StringEncoding.GetBytes(json);
            using var output = new MemoryStream();
            using (var gzip = new GZipStream(output, CompressionLevel.Optimal))
            {
                gzip.Write(utf8Bytes, 0, utf8Bytes.Length);
            }
            return output.ToArray(); // 包含 GZip 标准头（0x1F 0x8B）
        }

        public static byte[] Export(JsonRoot root)
        {
            var jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = false,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };
            string json = JsonSerializer.Serialize(root, jsonOptions);
            return CompressJson(json);
        }
    }
}