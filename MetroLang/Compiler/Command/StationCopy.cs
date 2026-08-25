using MetroEditor.MetroLang.DataModel;

namespace MetroEditor.MetroLang.Compiler.Command
{
    /// <summary>
    /// 处理 STATION_COPY 指令，复制已有车站的基础定义。
    /// </summary>
    internal class StationCopy : ICommandHandler
    {
        /// <summary>
        /// 读取源车站和目标车站名称，校验唯一性后执行复制。
        /// </summary>
        /// <param name="parser">当前源文件解析器</param>
        /// <param name="context">编译中的路网数据上下文</param>
        public void Handle(Parser parser, Context context)
        {
            string sourceStation = parser.ParseString();
            string targetStation = parser.ParseString();

            if (!context.Stations.ContainsKey(sourceStation))
                throw new SyntaxException($"车站 '{sourceStation}' 未定义", parser.Peek());

            if (context.Stations.ContainsKey(targetStation))
                throw new SyntaxException($"目标车站 '{targetStation}' 已存在", parser.Peek());

            Utils.CopyStation(context, sourceStation, targetStation);
        }
    }
}