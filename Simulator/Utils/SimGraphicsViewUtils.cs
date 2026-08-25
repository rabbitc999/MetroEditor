using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace MetroEditor.Simulator.Utils
{
    /// <summary>
    /// 图形绘制辅助类（从 Android GraphicsViewUtils 转换）
    /// </summary>
    public static class SimGraphicsViewUtils
    {
        /// <summary>
        /// 通过 Catmull-Rom 样条插值生成平滑路径点列表。
        /// </summary>
        /// <param name="points">控制点列表</param>
        /// <param name="closed">是否闭合路径</param>
        /// <param name="segmentsPerSegment">每段插值细分数</param>
        /// <returns>平滑路径点列表（可用于构建 WPF Path）</returns>
        public static List<Point> CreateSmoothPoints(List<Point> points, bool closed, int segmentsPerSegment)
        {
            var result = new List<Point>();
            if (points == null || points.Count < 2) return result;

            if (points.Count == 2)
            {
                result.Add(points[0]);
                result.Add(points[1]);
                return result;
            }

            var pts = new List<Point>(points);
            if (pts.Count < 4)
            {
                result.Add(pts[0]);
                for (int i = 1; i < pts.Count; i++)
                    result.Add(pts[i]);
                if (closed) result.Add(pts[0]); // 闭合处理
                return result;
            }

            // 扩展控制点
            var expanded = new List<Point>();
            if (closed)
            {
                int n = pts.Count;
                expanded.Add(pts[n - 2]);
                expanded.Add(pts[n - 1]);
                expanded.AddRange(pts);
                expanded.Add(pts[0]);
                expanded.Add(pts[1]);
            }
            else
            {
                Point first = pts[0];
                Point second = pts[1];
                Point last = pts[pts.Count - 1];
                Point prevLast = pts[pts.Count - 2];

                expanded.Add(new Point(first.X - (second.X - first.X), first.Y - (second.Y - first.Y)));
                expanded.AddRange(pts);
                expanded.Add(new Point(last.X + (last.X - prevLast.X), last.Y + (last.Y - prevLast.Y)));
            }

            bool firstPoint = true;
            for (int i = 0; i < expanded.Count - 3; i++)
            {
                Point p0 = expanded[i];
                Point p1 = expanded[i + 1];
                Point p2 = expanded[i + 2];
                Point p3 = expanded[i + 3];

                for (int j = 0; j < segmentsPerSegment; j++)
                {
                    float t = (float)j / segmentsPerSegment;
                    float t2 = t * t;
                    float t3 = t2 * t;

                    float x = (float)(0.5f * ((2 * p1.X) +
                        (-p0.X + p2.X) * t +
                        (2 * p0.X - 5 * p1.X + 4 * p2.X - p3.X) * t2 +
                        (-p0.X + 3 * p1.X - 3 * p2.X + p3.X) * t3));

                    float y = (float)(0.5f * ((2 * p1.Y) +
                        (-p0.Y + p2.Y) * t +
                        (2 * p0.Y - 5 * p1.Y + 4 * p2.Y - p3.Y) * t2 +
                        (-p0.Y + 3 * p1.Y - 3 * p2.Y + p3.Y) * t3));

                    result.Add(new Point(x, y));
                }
            }

            if (closed && result.Count > 0)
                result.Add(result[0]); // 闭合

            return result;
        }

        /// <summary>
        /// 判断颜色是否明亮（用于决定文字颜色）。
        /// </summary>
        /// <param name="color">ARGB 颜色值（WPF Color 结构）</param>
        /// <returns>true 表示亮色</returns>
        public static bool IsColorBright(Color color)
        {
            double luminance = 0.299 * color.R + 0.587 * color.G + 0.114 * color.B;
            return luminance > 160;
        }

        // WPF 中没有直接隐藏键盘的方法，此功能在桌面应用中通常由焦点控制实现。
        // 如需实现类似功能，可以调用 Win32 API，但此处忽略。
    }
}