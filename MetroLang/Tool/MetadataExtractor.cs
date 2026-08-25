using MetroEditor.MetroLang.JsonModel;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MetroEditor.MetroLang.Tool
{
    /// <summary>
    /// 线路元数据提取工具
    /// 从标准路网 JSON 文件中提取线路的静态属性（颜色、编组、速度、开通年份、外链等）
    /// 常用于从已发布的路网数据反向生成编译器所需的元数据配置
    /// </summary>
    /// <remarks>
    /// 典型使用场景：
    /// 1. 从线上环境的路网 JSON 生成本地编辑器的线路配置
    /// 2. 批量更新线路属性，避免手动修改 MetroLang 源码
    /// 3. 作为数据迁移工具，在不同版本的路网格式间转换
    /// </remarks>
    public static class MetadataExtractor
    {
        // ===== JSON 序列化配置 =====
        // 复用静态实例以避免重复分配，提升性能

        /// <summary>反序列化配置：属性名不区分大小写，适配不同来源的 JSON 文件</summary>
        private static readonly JsonSerializerOptions _deserializeOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        /// <summary>序列化配置：美化输出、忽略空值，生成可读性强的配置文件</summary>
        private static readonly JsonSerializerOptions _serializeOptions = new()
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        /// <summary>
        /// 从指定路径的路网 JSON 文件中提取所有线路的元数据
        /// </summary>
        /// <param name="jsonPath">路网 JSON 文件的完整路径</param>
        /// <returns>
        /// 以线路 ID 为键、线路元数据为值的字典
        /// 仅包含含有有效信息的线路（过滤全空记录）
        /// </returns>
        /// <exception cref="ArgumentNullException">当路径为空时抛出</exception>
        /// <exception cref="FileNotFoundException">当文件不存在时抛出</exception>
        /// <exception cref="InvalidDataException">当 JSON 结构无效或缺少 Lines 节点时抛出</exception>
        /// <example>
        /// var metadata = MetadataExtractor.Extract("network.json");
        /// Console.WriteLine($"共提取 {metadata.Count} 条线路元数据");
        /// </example>
        public static Dictionary<string, LineMetadata> Extract(string jsonPath)
        {
            // 参数防御性检查
            if (string.IsNullOrEmpty(jsonPath))
                throw new ArgumentNullException(nameof(jsonPath));

            if (!File.Exists(jsonPath))
                throw new FileNotFoundException($"文件不存在: {jsonPath}", jsonPath);

            // 读取并反序列化 JSON 文件
            string jsonText = File.ReadAllText(jsonPath, Encoding.UTF8);
            var root = JsonSerializer.Deserialize<JsonRoot>(jsonText, _deserializeOptions)
                ?? throw new InvalidDataException("JSON 反序列化失败，根对象为空");

            // 验证 JSON 结构完整性
            if (root.Lines == null || root.Lines.Count == 0)
                throw new InvalidDataException("输入 JSON 缺少 'lines' 对象或为空");

            // 遍历所有线路，提取元数据
            var result = new Dictionary<string, LineMetadata>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in root.Lines)
            {
                string lineId = kv.Key;
                var line = kv.Value;

                // 构建元数据对象
                var metadata = new LineMetadata
                {
                    Color = line.Color,
                    Formation = line.Formation,
                    Maxspeed = line.Maxspeed,
                    OpenYear = line.OpenYear,
                    Link = line.Link
                };

                // 仅当元数据包含有效信息时加入结果集（过滤占位数据）
                if (HasAnyMetadata(metadata))
                    result[lineId] = metadata;
            }

            return result;
        }

        /// <summary>
        /// 将线路元数据字典保存为格式化的 JSON 文件
        /// </summary>
        /// <param name="metadata">线路元数据字典</param>
        /// <param name="outputPath">输出文件路径</param>
        /// <exception cref="ArgumentNullException">当参数为空时抛出</exception>
        /// <example>
        /// MetadataExtractor.Save(metadata, "line_metadata.json");
        /// </example>
        public static void Save(Dictionary<string, LineMetadata> metadata, string outputPath)
        {
            if (metadata == null)
                throw new ArgumentNullException(nameof(metadata));
            if (string.IsNullOrEmpty(outputPath))
                throw new ArgumentNullException(nameof(outputPath));

            // 序列化为美化格式的 JSON
            string json = JsonSerializer.Serialize(metadata, _serializeOptions);
            File.WriteAllText(outputPath, json, Encoding.UTF8);
        }

        /// <summary>
        /// 判断元数据对象是否包含至少一个有效字段
        /// 用于过滤无意义的数据记录，保持输出文件的整洁性
        /// </summary>
        /// <param name="metadata">待检查的元数据对象</param>
        /// <returns>当存在任意有效字段时返回 true</returns>
        /// <remarks>
        /// 有效字段判定标准：
        /// 1. Color：非空字符串
        /// 2. Formation：非空字符串
        /// 3. Maxspeed：大于 0 的数值
        /// 4. OpenYear：大于 0 的年份
        /// 5. Link：非空字符串
        /// </remarks>
        private static bool HasAnyMetadata(LineMetadata meta)
        {
            return !string.IsNullOrEmpty(meta.Color) ||
                   !string.IsNullOrEmpty(meta.Formation) ||
                   meta.Maxspeed > 0 ||
                   meta.OpenYear > 0 ||
                   !string.IsNullOrEmpty(meta.Link);
        }
    }
}