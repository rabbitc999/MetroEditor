package com.rabbitc.subway.utils;

import static android.content.Context.INPUT_METHOD_SERVICE;

import android.content.Context;
import android.graphics.Color;
import android.graphics.Path;
import android.graphics.PointF;
import android.view.View;
import android.view.inputmethod.InputMethodManager;

import java.util.ArrayList;
import java.util.List;

/**
 * 图形与视图工具类。
 * <p>
 * 提供平滑路径生成（Catmull-Rom 样条插值）、颜色亮度判断及软键盘隐藏等辅助方法，
 * 供地铁线路图绘制及界面交互使用。
 * </p>
 */
public class GraphicsViewUtils {
    /**
     * 通过 Catmull-Rom 样条插值生成平滑路径。
     *
     * @param points              控制点列表
     * @param closed              是否闭合路径
     * @param segmentsPerSegment  每段插值细分数
     * @return 平滑 Path 对象
     */
    public static Path createSmoothPath(List<PointF> points, boolean closed, int segmentsPerSegment) {
        Path path = new Path();
        if (points == null || points.size() < 2) return path;
        if (points.size() == 2) {
            path.moveTo(points.get(0).x, points.get(0).y);
            path.lineTo(points.get(1).x, points.get(1).y);
            return path;
        }
        List<PointF> pts = new ArrayList<>(points);
        if (pts.size() < 4) {
            path.moveTo(pts.get(0).x, pts.get(0).y);
            for (int i = 1; i < pts.size(); i++) {
                path.lineTo(pts.get(i).x, pts.get(i).y);
            }
            if (closed) path.close();
            return path;
        }
        List<PointF> expanded = new ArrayList<>();
        if (closed) {
            int n = pts.size();
            expanded.add(pts.get(n - 2));
            expanded.add(pts.get(n - 1));
            expanded.addAll(pts);
            expanded.add(pts.get(0));
            expanded.add(pts.get(1));
        } else {
            PointF first = pts.get(0);
            PointF second = pts.get(1);
            PointF last = pts.get(pts.size() - 1);
            PointF prevLast = pts.get(pts.size() - 2);
            expanded.add(new PointF(first.x - (second.x - first.x), first.y - (second.y - first.y)));
            expanded.addAll(pts);
            expanded.add(new PointF(last.x + (last.x - prevLast.x), last.y + (last.y - prevLast.y)));
        }

        boolean firstPoint = true;
        for (int i = 0; i < expanded.size() - 3; i++) {
            PointF p0 = expanded.get(i);
            PointF p1 = expanded.get(i + 1);
            PointF p2 = expanded.get(i + 2);
            PointF p3 = expanded.get(i + 3);
            for (int j = 0; j < segmentsPerSegment; j++) {
                float t = (float) j / segmentsPerSegment;
                float t2 = t * t;
                float t3 = t2 * t;
                float x = 0.5f * ((2 * p1.x) +
                        (-p0.x + p2.x) * t +
                        (2 * p0.x - 5 * p1.x + 4 * p2.x - p3.x) * t2 +
                        (-p0.x + 3 * p1.x - 3 * p2.x + p3.x) * t3);
                float y = 0.5f * ((2 * p1.y) +
                        (-p0.y + p2.y) * t +
                        (2 * p0.y - 5 * p1.y + 4 * p2.y - p3.y) * t2 +
                        (-p0.y + 3 * p1.y - 3 * p2.y + p3.y) * t3);
                if (firstPoint) {
                    path.moveTo(x, y);
                    firstPoint = false;
                } else {
                    path.lineTo(x, y);
                }
            }
        }
        return path;
    }

    /**
     * 判断颜色是否明亮（用于决定文字颜色）。
     *
     * @param color 颜色值
     * @return true 表示亮色
     */
    public static boolean isColorBright(int color) {
        double luminance = 0.299 * Color.red(color) + 0.587 * Color.green(color) + 0.114 * Color.blue(color);
        return luminance > 160;
    }

    /**
     * 隐藏软键盘。
     *
     * @param context 上下文
     * @param view    当前获得焦点的视图
     */
    public static void hideKeyboard(Context context, View view) {
        if (view != null) {
            InputMethodManager imm = (InputMethodManager) context.getSystemService(INPUT_METHOD_SERVICE);
            if (imm != null) {
                imm.hideSoftInputFromWindow(view.getWindowToken(), 0);
            }
        }
    }
}
