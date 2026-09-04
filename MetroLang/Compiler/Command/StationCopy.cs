using MetroEditor.MetroLang.DataModel;

namespace MetroEditor.MetroLang.Compiler.Command
{
    /// <summary>
    /// 复制一个已存在的车站定义，并以新的名称写入编译上下文。
    /// </summary>
    internal class StationCopy : ICommandHandler
    {
        /// <summary>
        /// 读取源站和目标站，校验名称冲突后委托工具方法完成深复制。
        /// </summary>
        public void Handle(Parser parser, Context context)
        {
            // 先解析两个名称，再统一做存在性检查，避免复制过程中留下半成品。
            string sourceStation = parser.ParseString();
            string targetStation = parser.ParseString();

            if (!context.Stations.ContainsKey(sourceStation))
                throw new SyntaxException($"车站 '{sourceStation}' 未定义", parser.Peek());

            if (context.Stations.ContainsKey(targetStation))
                throw new SyntaxException($"目标车站 '{targetStation}' 已存在", parser.Peek());

            // CopyStation 负责复制车站关联的设施、坐标等完整属性。
            Utils.CopyStation(context, sourceStation, targetStation);
        }
    }
}
