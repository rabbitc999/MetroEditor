using MetroEditor.MetroLang.Compiler;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MetroEditor.MetroLang.Compiler.Command
{
    // ====================================================================
    // VIA 解析：方向经由定义（旧语法兼容）
    // 定义方向之间的经由关系，用于复杂交路组织
    // 语法示例：VIA LINE1_0 LINE2_1
    // ====================================================================
    internal class Via : ICommandHandler
    {
        public void Handle(Parser parser, Context context)
        {
            // 解析源方向和目标方向
            string sourceDirection = parser.ParseId();
            string targetDirection = parser.ParseId();

            // 将目标方向添加到源方向的经由列表中
            if (context.Directions.TryGetValue(sourceDirection, out var directionInfo))
                directionInfo.ViaDirections.Add(targetDirection);
            else
                throw new SyntaxException($"方向 {sourceDirection} 不存在", parser.Peek());
        }
    }
}
