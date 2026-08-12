using MetroEditor.MetroLang.DataModel;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MetroEditor.MetroLang.Compiler.Command
{
    /// <summary>
    /// 运营公司定义命令处理器。
    /// 解析 OPERATOR_DEFINE 块，注册一家运营公司及其运营的线路。
    /// </summary>
    internal class OperatorDefine : ICommandHandler
    {
        public void Handle(Parser parser, Context context)
        {
            // 1. 解析运营公司 ID
            string companyId = parser.ParseId();
            if (context.OperatorCompanies.ContainsKey(companyId))
                parser.AddWarning($"运营公司 '{companyId}' 已存在，将覆盖");

            var company = new OperatorCompanyInfo();
            context.OperatorCompanies[companyId] = company;

            // 2. 解析主体
            while (true)
            {
                parser.ParseNewlines();
                if (parser.IsEndDefine()) break;

                string cmd = parser.ParseId().ToUpperInvariant();
                switch (cmd)
                {
                    case "NAME":
                        company.Name = parser.ParseString();
                        break;

                    case "SHORT_NAME":
                        company.ShortName = parser.ParseString();
                        break;

                    case "EN_NAME":
                        company.EnName = parser.ParseString();
                        break;

                    case "COUNTRY":
                        company.Country = parser.ParseString();
                        break;

                    case "CITY":
                        company.City = parser.ParseString();
                        break;

                    case "FOUNDED":
                        company.Founded = parser.ParseInt();
                        break;

                    case "WEBSITE":
                        company.Website = parser.ParseString();
                        break;

                    case "HOTLINE":
                        company.Hotline = parser.ParseString();
                        break;

                    case "DESCRIPTION":
                        company.Description = parser.ParseString();
                        break;

                    case "LOGO":
                        string logoPath = parser.ParseString();
                        string sourceDir = parser.GetCurrentTokenSourceDirectory();
                        string fullPath = Path.GetFullPath(Path.Combine(sourceDir, logoPath));
                        if (!File.Exists(fullPath))
                            throw new SyntaxException($"LOGO 图片文件不存在: {fullPath}", parser.Peek());
                        byte[] bytes = File.ReadAllBytes(fullPath);
                        company.LogoBase64 = Convert.ToBase64String(bytes);
                        break;

                    case "LINES":
                        company.Lines = parser.ParseStringList();
                        break;

                    default:
                        throw new SyntaxException($"OPERATOR_DEFINE 内未知子命令 '{cmd}'", parser.Peek());
                }
            }
        }
    }
}
