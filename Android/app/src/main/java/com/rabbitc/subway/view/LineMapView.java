package com.rabbitc.subway.view;

import android.annotation.SuppressLint;
import android.content.Context;
import android.graphics.Canvas;
import android.graphics.Color;
import android.graphics.Paint;
import android.graphics.Path;
import android.graphics.PointF;
import android.graphics.RectF;
import android.graphics.Typeface;
import android.view.View;

import androidx.annotation.NonNull;

import com.rabbitc.subway.data.DataCache;
import com.rabbitc.subway.data.LineDirection;
import com.rabbitc.subway.utils.GraphKeyUtils;
import com.rabbitc.subway.utils.GraphicsViewUtils;
import com.rabbitc.subway.utils.StationUtils;

import java.util.ArrayList;
import java.util.List;
import java.util.Map;

/**
 * 单条线路的示意图视图，用于显示线路走向、站点及高亮。
 * <p>
 * 自动适配坐标范围，支持未来（规划）线路灰色显示。
 * </p>
 */
@SuppressLint("ViewConstructor")
public class LineMapView extends View {

    // ==================== 常量 ====================
    private static final float LINE_STROKE_WIDTH = 8f;
    private static final float HIGHLIGHT_CIRCLE_RADIUS = 10f;
    private static final int SMOOTH_SEGMENTS = 12;

    // ==================== 成员变量 ====================
    private final List<LineDirection> directions;
    private final Map<String, double[]> coords;
    private final int lineColor;                 // 线路默认颜色
    private String highlightedStation = null;

    // 绘制缓存：边界及变换参数
    private double drawMinLon = 0, drawMaxLon = 0;
    private double drawMinLat = 0, drawMaxLat = 0;
    private float scale = 0;
    private float offsetX = 0, offsetY = 0;

    // ==================== Paint 对象（复用） ====================
    private final Paint mainLinePaint = new Paint(Paint.ANTI_ALIAS_FLAG);
    private final Paint futureLinePaint = new Paint(Paint.ANTI_ALIAS_FLAG);
    private final Paint highlightPaint = new Paint(Paint.ANTI_ALIAS_FLAG);

    // ==================== 构造方法 ====================
    /**
     * 构造单线路示意图视图。
     *
     * @param context    上下文
     * @param directions 线路方向列表
     * @param coords     站点名称到经纬度坐标的映射
     * @param lineColor  线路默认颜色
     */
    public LineMapView(Context context, List<LineDirection> directions,
                       Map<String, double[]> coords, int lineColor) {
        super(context);
        this.directions = directions;
        this.coords = coords;
        this.lineColor = lineColor;
        initPaints();
    }

    // ==================== 初始化 Paint ====================
    private void initPaints() {
        // 主线画笔（粗线）
        mainLinePaint.setColor(lineColor);
        mainLinePaint.setStrokeWidth(LINE_STROKE_WIDTH);
        mainLinePaint.setStyle(Paint.Style.STROKE);

        // 未来（规划）线路画笔（灰色叠加）
        futureLinePaint.setStrokeWidth(LINE_STROKE_WIDTH);
        futureLinePaint.setStyle(Paint.Style.STROKE);

        // 高亮画笔（填充圆）
        highlightPaint.setColor(Color.RED);
        highlightPaint.setStyle(Paint.Style.FILL);
    }

    // ==================== 公共 API ====================
    /**
     * 高亮显示指定站点（红色圆圈）。
     *
     * @param stationName 站点名称
     */
    public void highlightStation(String stationName) {
        this.highlightedStation = stationName;
        invalidate();
    }

    // ==================== 绘制主流程 ====================
    /**
     * 绘制单线路示意图的主流程，按顺序执行：
     * <ol>
     * <li>计算数据边界（经纬度范围）</li>
     * <li>计算屏幕变换参数（缩放、偏移）</li>
     * <li>绘制线路（粗线，未来线路灰色显示）</li>
     * <li>绘制高亮站点（红色圆圈 + 文字标签）</li>
     * </ol>
     *
     * @param canvas 画布对象
     * @see #computeBounds()
     * @see #computeTransformParams()
     * @see #drawLines(Canvas)
     * @see #drawHighlight(Canvas)
     */
    @Override
    @SuppressLint("DrawAllocation")
    protected void onDraw(@NonNull Canvas canvas) {
        super.onDraw(canvas);
        if (directions == null || directions.isEmpty() || coords == null || coords.isEmpty()) {
            return;
        }

        // 1. 计算数据边界
        computeBounds();

        // 2. 计算变换参数
        computeTransformParams();

        // 3. 绘制线路
        drawLines(canvas);

        // 4. 绘制高亮站点
        drawHighlight(canvas);
    }

    // ==================== 边界计算 ====================
    private void computeBounds() {
        // 仅统计有坐标的站点；缺失坐标不会影响其他方向的边界计算。
        double minLon = Double.MAX_VALUE, maxLon = -Double.MAX_VALUE;
        double minLat = Double.MAX_VALUE, maxLat = -Double.MAX_VALUE;

        for (LineDirection dir : directions) {
            for (String station : dir.stations) {
                double[] c = coords.get(station);
                if (c != null) {
                    if (c[0] < minLon) minLon = c[0];
                    if (c[0] > maxLon) maxLon = c[0];
                    if (c[1] < minLat) minLat = c[1];
                    if (c[1] > maxLat) maxLat = c[1];
                }
            }
        }

        if (minLon == Double.MAX_VALUE) return;

        double lonRange = Math.max(maxLon - minLon, 0.001);
        double latRange = Math.max(maxLat - minLat, 0.001);
        double marginLon = lonRange * 0.1;
        double marginLat = latRange * 0.1;

        drawMinLon = minLon - marginLon;
        drawMaxLon = maxLon + marginLon;
        drawMinLat = minLat - marginLat;
        drawMaxLat = maxLat + marginLat;
    }

    // ==================== 变换参数计算 ====================
    private void computeTransformParams() {
        // 经度和纬度分别计算缩放比例，取较小值以完整容纳线路并保持居中。
        int width = getWidth();
        int height = getHeight();
        if (width == 0 || height == 0) return;

        double lonRange = (drawMaxLon - drawMinLon) * 1.1;
        double latRange = (drawMaxLat - drawMinLat) * 1.1;

        float scaleX = (float) (width / lonRange);
        float scaleY = (float) (height / latRange);
        scale = Math.min(scaleX, scaleY);

        float contentWidth = (float) (lonRange * scale);
        float contentHeight = (float) (latRange * scale);
        offsetX = (width - contentWidth) / 2f;
        offsetY = (height - contentHeight) / 2f;
    }

    // ==================== 绘制线路 ====================
    private void drawLines(Canvas canvas) {
        // 每个方向独立绘制，环线通过闭合参数生成平滑路径，未来方向再覆盖灰色。
        for (LineDirection dir : directions) {
            List<PointF> points = getPointsForStations(dir.stations);
            if (points.size() < 2) continue;

            boolean isLoop = DataCache.getInstance().getDirLoopMap().get(GraphKeyUtils.makeDirKey(dir.lineID, dir.start, dir.end));
            Path path = GraphicsViewUtils.createSmoothPath(points, isLoop, SMOOTH_SEGMENTS);

            // 绘制线路（粗线）
            canvas.drawPath(path, mainLinePaint);

            // 未来（规划）线路以灰色叠加显示
            if (dir.isFuture) {
                futureLinePaint.setColor(Color.GRAY);
                canvas.drawPath(path, futureLinePaint);
            }
        }
    }

    // ==================== 绘制高亮站点 ====================
    private void drawHighlight(Canvas canvas) {
        if (highlightedStation == null || !coords.containsKey(highlightedStation)) return;

        double[] c = coords.get(highlightedStation);
        float x = (float) ((c[0] - drawMinLon) * scale) + offsetX;
        float y = (float) ((drawMaxLat - c[1]) * scale) + offsetY;

        // 1. 绘制圆（原有）
        canvas.drawCircle(x, y, HIGHLIGHT_CIRCLE_RADIUS, highlightPaint);

        // 2. 确定文字目标中心点（圆正上方）
        float desiredX = x;
        float desiredY = y - HIGHLIGHT_CIRCLE_RADIUS - 15; // 间距10px

        // 3. 文字画笔（推荐复用，这里仅示例）
        Paint textPaint = new Paint(Paint.ANTI_ALIAS_FLAG);
        textPaint.setColor(Color.WHITE);
        textPaint.setTextSize(20);
        textPaint.setTypeface(Typeface.DEFAULT_BOLD);
        textPaint.setTextAlign(Paint.Align.CENTER);
        textPaint.setStyle(Paint.Style.FILL);

        Paint.FontMetrics fm = textPaint.getFontMetrics();
        float baseline = desiredY - (fm.descent + fm.ascent) / 2f;

        // 4. 测量文字宽度和高度
        float textWidth = textPaint.measureText(highlightedStation);
        float textHeight = fm.descent - fm.ascent;

        // 5. 背景参数（内边距，建议使用dp转px）
        float padding = 8; // 可根据需要调整
        float bgLeft = desiredX - textWidth / 2 - padding;
        float bgTop = baseline + fm.ascent - padding; // 文字顶部向上偏移
        float bgRight = desiredX + textWidth / 2 + padding;
        float bgBottom = baseline + fm.descent + padding;

        // 6. 绘制背景（半透明圆角矩形）
        Paint bgPaint = new Paint(Paint.ANTI_ALIAS_FLAG);
        bgPaint.setColor(Color.argb(180, 0, 0, 0)); // 半透黑，可根据需要修改
        bgPaint.setStyle(Paint.Style.FILL);
        canvas.drawRoundRect(bgLeft, bgTop, bgRight, bgBottom, 8, 8, bgPaint);

        // 7. 绘制文字
        canvas.drawText(highlightedStation, desiredX, baseline, textPaint);
    }

    // ==================== 辅助方法 ====================
    /**
     * 将站点列表转换为屏幕坐标点列表。
     */
    private List<PointF> getPointsForStations(List<String> stationNames) {
        // Android Canvas 的 y 轴向下，因此纬度越大需要映射到越小的屏幕 y。
        List<PointF> points = new ArrayList<>(stationNames.size());
        for (String station : stationNames) {
            double[] c = coords.get(station);
            if (c != null) {
                float x = (float) ((c[0] - drawMinLon) * scale) + offsetX;
                float y = (float) ((drawMaxLat - c[1]) * scale) + offsetY;
                points.add(new PointF(x, y));
            }
        }
        return points;
    }
}
