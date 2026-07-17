using MetroEditor.MetroLang.DataModel;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MetroEditor.MetroLang.Compiler.Command
{
    internal class DirectionReverse : ICommandHandler
    {
        public void Handle(Parser parser, Context context)
        {
            // 1. 解析源方向ID
            string sourceId = parser.ParseId();
            if (!context.Directions.TryGetValue(sourceId, out var sourceDir))
                throw new SyntaxException($"方向 '{sourceId}' 未定义", parser.Peek());

            // 2. 检测是否指定了新ID
            bool hasNewId = parser.Peek().TokenType == TokenType.Id;
            string targetId = hasNewId ? parser.ParseId() : sourceId;

            if (hasNewId && context.Directions.ContainsKey(targetId))
                throw new SyntaxException($"目标方向 '{targetId}' 已存在", parser.Peek());

            // 3. 检查距离是否存在
            if (!context.Distances.TryGetValue(sourceId, out var distances))
                throw new SyntaxException($"方向 '{sourceId}' 缺少 DISTANCE 定义", parser.Peek());

            // 4. 反转站点列表
            var reversedStations = new List<string>(sourceDir.Stations);
            reversedStations.Reverse();

            // 5. 反转距离列表
            bool isLoop = sourceDir.Attrs.Contains("LOOP");
            var reversedDist = Utils.ReverseDistanceList(distances, isLoop);

            // 6. 处理属性（如果是环线，交换 inner/outer）
            var newAttrs = new HashSet<string>(sourceDir.Attrs);
            string newDirectionType = sourceDir.DirectionType;
            if (isLoop)
            {
                if (string.IsNullOrEmpty(newDirectionType))
                    newDirectionType = "inner"; // 默认
                else
                    newDirectionType = newDirectionType == "inner" ? "outer" : "inner";
            }

            // 7. 创建或更新方向
            if (hasNewId)
            {
                // 创建新方向
                var newDir = new DirectionInfo
                {
                    LineId = sourceDir.LineId,
                    Stations = reversedStations,
                    Attrs = newAttrs,
                    Alias = sourceDir.Alias,
                    AliasColor = sourceDir.AliasColor,
                    DirectionType = newDirectionType,
                    IsFuture = sourceDir.IsFuture,
                    ConnectStation = sourceDir.ConnectStation,
                    ViaDirections = new List<string>(sourceDir.ViaDirections)
                };
                context.Directions[targetId] = newDir;
                context.Distances[targetId] = reversedDist;
            }
            else
            {
                // 原地反转：更新现有方向
                sourceDir.Stations = reversedStations;
                sourceDir.Attrs = newAttrs;
                sourceDir.DirectionType = newDirectionType;
                context.Distances[sourceId] = reversedDist;

                // 更新引用该方向的外部键规则
                UpdateDirectionReferences(context, sourceId, sourceDir);
            }
        }

        /// <summary>
        /// 原地反转后，更新所有引用该方向的外部键规则
        /// </summary>
        private void UpdateDirectionReferences(Context context, string dirId, DirectionInfo dirInfo)
        {
            string oldExternalKey = Utils.ConvertToExternalDirKey(context, dirId);
            // 反转后，重新计算外部键（起止站已变）
            string newExternalKey = Utils.ConvertToExternalDirKey(context, dirId);

            if (oldExternalKey == newExternalKey) return; // 可能无变化（如单站？但不会）

            // 更新 CrowdRules
            foreach (var rule in context.CrowdRules.Where(r => r.DirectionKey == oldExternalKey))
                rule.DirectionKey = newExternalKey;

            // 更新 SpeedLimits
            foreach (var rule in context.SpeedLimits.Where(r => r.DirectionKey == oldExternalKey))
                rule.DirectionKey = newExternalKey;

            // 更新 Depots
            foreach (var depot in context.Depots.Where(d => d.DirectionKey == oldExternalKey))
                depot.DirectionKey = newExternalKey;
        }
    }
}