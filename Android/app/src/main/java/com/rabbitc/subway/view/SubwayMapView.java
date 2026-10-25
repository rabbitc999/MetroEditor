package com.rabbitc.subway.view;

import android.annotation.SuppressLint;
import android.content.Context;
import android.os.Handler;
import android.os.Looper;
import android.util.AttributeSet;
import android.util.Log;
import android.webkit.ConsoleMessage;
import android.webkit.JavascriptInterface;
import android.webkit.WebView;
import android.webkit.WebChromeClient;
import android.webkit.WebViewClient;

import com.rabbitc.subway.algroithm.MetroGraph;
import com.rabbitc.subway.data.DataCache;
import com.rabbitc.subway.data.PathResult;
import com.rabbitc.subway.data.SearchWeight;
import com.rabbitc.subway.listener.OnStationLongClickListener;
import com.rabbitc.subway.utils.GraphKeyUtils;

import org.json.JSONException;
import org.json.JSONObject;

import java.util.ArrayList;
import java.util.Collections;
import java.util.List;
import java.util.Set;

/**
 * 基于 WebView 的地铁线路图视图，加载 HTML/JS 地图并实现交互。
 * <p>
 * 对外接口与旧版 SubwayMapView 保持一致，以便 MapActivity 无缝切换。
 * </p>
 */
public class SubwayMapView extends WebView {

    // ==================== 常量 ====================
    private static final String MAP_HTML_PATH = "file:///android_asset/map.html";
    private static final String JS_INJECT_DATA = "javascript:loadDataFromAndroid(JSON.parse(\"%s\"));";
    private static final String JS_HIGHLIGHT_LINES = "javascript:highlightLinesFromAndroid(%s);";
    private static final String JS_CLEAR_PATH = "javascript:clearRouteFromAndroid();";
    private static final String JS_DRAW_PATH = "javascript:drawPathFromAndroid(%s);";
    private static final String JS_ZOOM_TO_STATIONS = "javascript:zoomToStationsFromAndroid(%s);";
    private static final String JS_ZOOM_TO_LINES = "javascript:zoomToLinesFromAndroid(%s);";
    private static final String JS_SHOW_ERROR = "javascript:showErrorFromAndroid('%s');";
    private static final String JS_CHECK_FUNCTION = "javascript:typeof loadDataFromAndroid === 'function';";

    private static final long INJECT_RETRY_DELAY_MS = 500;
    private static final long DRAW_PATH_DELAY_MS = 1000;

    // ==================== 成员变量 ====================
    private OnStationLongClickListener stationLongClickListener;
    private OnBlankLongClickListener blankLongClickListener;

    private String cachedJsonData = null;
    private boolean isPageLoaded = false;
    private final Handler mainHandler = new Handler(Looper.getMainLooper());
    private MetroGraph metroGraph;

    // 用于延迟绘制路径（页面未加载时暂存起终点）
    private String pendingStartStation = null;
    private String pendingEndStation = null;

    // ==================== 构造函数 ====================
    /**
     * 通过代码创建 SubwayMapView 实例。
     *
     * @param context 上下文
     */
    public SubwayMapView(Context context) {
        super(context);
        init();
    }

    /**
     * 从 XML 布局中创建 SubwayMapView 实例。
     *
     * @param context 上下文
     * @param attrs   属性集
     */
    public SubwayMapView(Context context, AttributeSet attrs) {
        super(context, attrs);
        init();
    }

    /**
     * 从 XML 布局中创建 SubwayMapView 实例（含默认样式属性）。
     *
     * @param context      上下文
     * @param attrs        属性集
     * @param defStyleAttr 默认样式属性
     */
    public SubwayMapView(Context context, AttributeSet attrs, int defStyleAttr) {
        super(context, attrs, defStyleAttr);
        init();
    }

    // ==================== 初始化 ====================
    @SuppressLint({"SetJavaScriptEnabled", "JavascriptInterface"})
    private void init() {
        // WebView 基本设置
        getSettings().setJavaScriptEnabled(true);
        getSettings().setDomStorageEnabled(true);
        getSettings().setAllowFileAccessFromFileURLs(true);
        getSettings().setAllowUniversalAccessFromFileURLs(true);
        getSettings().setBuiltInZoomControls(true);
        getSettings().setDisplayZoomControls(false);

        // 添加 Android ↔ JS 桥接；JS 只能通过 AndroidBridge 暴露的白名单方法回调。
        addJavascriptInterface(new AndroidBridge(), "Android");

        // 设置 WebViewClient
        setWebViewClient(new WebViewClient() {
            @Override
            public void onPageFinished(WebView view, String url) {
                super.onPageFinished(view, url);
                isPageLoaded = true;
                // 页面完成后再注入缓存，避免 evaluateJavascript 早于脚本定义。
                // 若有缓存数据，注入
                if (cachedJsonData != null) {
                    injectData(cachedJsonData);
                }
            }
        });

        // 设置 WebChromeClient 以查看 console 日志
        setWebChromeClient(new WebChromeClient() {
            @Override
            public boolean onConsoleMessage(ConsoleMessage consoleMessage) {
                Log.d("SubwayMapView", "JS Console: " + consoleMessage.message());
                return true;
            }
        });

        // 加载地图 HTML
        loadUrl(MAP_HTML_PATH);
    }

    // ==================== 数据注入 ====================
    /**
     * 将 JSON 数据注入到 JS 环境。
     *
     * @param json 完整的线路数据 JSON 字符串
     */
    private void injectData(String json) {
        if (json == null || json.isEmpty()) return;

        try {
            // 压缩 JSON（去除换行，转义引号），确保拼接 javascript: URL 时语法完整。
            JSONObject obj = new JSONObject(json);
            String compact = obj.toString();
            String escaped = compact.replace("\\", "\\\\").replace("\"", "\\\"");

            // 检查 JS 函数是否存在，若不存在则延迟重试
            evaluateJavascript(JS_CHECK_FUNCTION, value -> {
                if (!"true".equals(value)) {
                    Log.w("SubwayMapView", "loadDataFromAndroid 未定义，延迟重试注入");
                    mainHandler.postDelayed(() -> injectData(json), INJECT_RETRY_DELAY_MS);
                    return;
                }
                // 函数确认存在后才执行注入，失败会由上面的重试逻辑兜底。
                // 执行注入
                String js = String.format(JS_INJECT_DATA, escaped);
                evaluateJavascript(js, null);
            });
        } catch (JSONException e) {
            Log.e("SubwayMapView", "JSON 压缩失败", e);
        }
    }

    /**
     * 将 DataCache 中的 JSON 注入 WebView；页面未就绪时延迟到加载完成后注入。
     * 实际上只需要 DataCache 中的完整 JSON，忽略传入的 Map 参数。
     */
    public void setData() {
        DataCache cache = DataCache.getInstance();
        String json = cache.getJsonData();
        this.cachedJsonData = json;
        if (isPageLoaded && json != null) {
            injectData(json);
        }
    }

    // ==================== 对外交互方法 ====================

    /**
     * 高亮指定线路。
     *
     * @param lineIds 线路ID集合
     */
    public void highlightLines(Set<String> lineIds) {
        if (!isPageLoaded) return;
        if (lineIds == null) lineIds = Collections.emptySet();

        StringBuilder sb = new StringBuilder("[");
        int count = 0;
        for (String id : lineIds) {
            if (id == null || id.isEmpty()) continue;
            if (DataCache.getInstance().getAllFutureMap().get(id)) continue;
            if (count > 0) sb.append(",");
            sb.append("'").append(id).append("'");
            count++;
        }
        sb.append("]");

        String js = String.format(JS_HIGHLIGHT_LINES, sb);
        Log.d("SubwayMapView", "调用 JS: " + js);
        evaluateJavascript(js, null);
    }

    // SubwayMapView.java 中添加

    private List<String> pendingStationNames = null; // 新增成员变量

    /**
     * 直接绘制给定的站点名称列表（不进行路径搜索）
     * @param stationNames 按顺序的站点名称列表
     */
    public void drawPathWithStations(List<String> stationNames) {
        if (!isPageLoaded) {
            // WebView 尚未完成初始化时只记录请求，页面加载回调会负责重放。
            // 页面未加载，暂存
            this.pendingStationNames = stationNames;
            return;
        }
        if (stationNames == null || stationNames.size() < 2) {
            showError("路径站点不足");
            return;
        }
        // 构建 JSON 数组交给 JS，保持 Android 侧不参与地图坐标计算。
        StringBuilder sb = new StringBuilder("[");
        for (int i = 0; i < stationNames.size(); i++) {
            if (i > 0) sb.append(",");
            sb.append("\"").append(stationNames.get(i)).append("\"");
        }
        sb.append("]");
        String js = String.format(JS_DRAW_PATH, sb);
        evaluateJavascript(js, null);
    }

    /**
     * 清除已绘制的路径。
     */
    public void clearPath() {
        if (!isPageLoaded) return;
        evaluateJavascript(JS_CLEAR_PATH, null);
    }

    /**
     * 设置 MetroGraph 实例，用于路径计算。
     *
     * @param metroGraph MetroGraph 实例
     */
    public void setMetroGraph(MetroGraph metroGraph) {
        this.metroGraph = metroGraph;
    }

    /**
     * 绘制起点到终点的路径。
     *
     * @param startStation 起点站名
     * @param endStation   终点站名
     */
    /** 根据起终点调用 MetroGraph 规划路线，并把站点序列交给 JavaScript 绘制。 */
    public void drawPath(String startStation, String endStation) {
        if (!isPageLoaded) {
            // 页面未加载，暂存起终点，等待 onMapReady 回调
            this.pendingStartStation = startStation;
            this.pendingEndStation = endStation;
            return;
        }

        if (metroGraph == null) {
            Log.e("SubwayMapView", "MetroGraph 未设置，无法规划路径");
            showError("无法规划路径：数据未就绪");
            return;
        }

        SearchWeight weight = SearchWeight.fromContext(getContext());
        PathResult result = metroGraph.findTransferPath(startStation, endStation, weight);
        if (result == null || result.transfers == -1) {
            showError("无法规划路径");
            return;
        }

        // 提取路径中的站点名（保留顺序）
        List<String> stationNames = new ArrayList<>();
        for (String node : result.path) {
            String[] parts = GraphKeyUtils.parseNode(node);
            stationNames.add(parts[1]);
        }

        // 构建 JSON 数组字符串
        StringBuilder sb = new StringBuilder("[");
        for (int i = 0; i < stationNames.size(); i++) {
            if (i > 0) sb.append(",");
            sb.append("\"").append(stationNames.get(i)).append("\"");
        }
        sb.append("]");

        String js = String.format(JS_DRAW_PATH, sb);
        evaluateJavascript(js, null);
    }

    /**
     * 缩放至指定站点集合。
     *
     * @param stationNames 站点名称集合
     */
    public void zoomToFitStations(Set<String> stationNames) {
        if (!isPageLoaded || stationNames == null || stationNames.isEmpty()) return;
        String jsArray = buildJsonArray(stationNames);
        String js = String.format(JS_ZOOM_TO_STATIONS, jsArray);
        evaluateJavascript(js, null);
    }

    /**
     * 缩放至指定线路（由 JS 内部处理）。
     *
     * @param lineIds 线路ID集合
     */
    public void zoomToFitLines(Set<String> lineIds) {
        if (!isPageLoaded || lineIds == null || lineIds.isEmpty()) return;
        String jsArray = buildQuotedArray(lineIds);
        String js = String.format(JS_ZOOM_TO_LINES, jsArray);
        evaluateJavascript(js, null);
    }

    // ==================== 监听器设置 ====================
    /**
     * 设置站点长按事件监听器。当用户在 WebView 地图上长按站点时通过 JS 桥接回调。
     *
     * @param listener 站点长按监听器，可为 {@code null}
     * @see OnStationLongClickListener#onStationLongClick(String)
     */
    public void setOnStationLongClickListener(OnStationLongClickListener listener) {
        this.stationLongClickListener = listener;
    }

    /**
     * 设置空白区域长按事件监听器。当用户在 WebView 地图空白处长按时通过 JS 桥接回调。
     *
     * @param listener 空白区域长按监听器，可为 {@code null}
     * @see OnBlankLongClickListener#onBlankLongClick()
     */
    public void setOnBlankLongClickListener(OnBlankLongClickListener listener) {
        this.blankLongClickListener = listener;
    }

    // ==================== 错误显示 ====================
    /**
     * 在地图页面上显示错误提示信息，通过 JS 调用前端展示层实现。
     *
     * @param msg 错误提示文本
     */
    public void showError(String msg) {
        String js = String.format(JS_SHOW_ERROR, msg);
        evaluateJavascript(js, null);
    }

    // ==================== 废弃方法（保留兼容性） ====================
    @Deprecated
    public android.graphics.PointF geoToPixel() {
        return new android.graphics.PointF(0, 0);
    }

    // ==================== 辅助方法 ====================
    private String buildJsonArray(Set<String> items) {
        StringBuilder sb = new StringBuilder("[");
        int count = 0;
        for (String item : items) {
            if (count > 0) sb.append(",");
            sb.append("\"").append(item).append("\"");
            count++;
        }
        sb.append("]");
        return sb.toString();
    }

    private String buildQuotedArray(Set<String> items) {
        StringBuilder sb = new StringBuilder("[");
        int count = 0;
        for (String item : items) {
            if (count > 0) sb.append(",");
            sb.append("'").append(item).append("'");
            count++;
        }
        sb.append("]");
        return sb.toString();
    }

    // ==================== 内部接口（与旧版保持一致） ====================
    /**
     * 地图空白区域长按回调接口。
     */
    public interface OnBlankLongClickListener {
        /**
         * 当用户在地图空白区域执行长按时回调。
         */
        void onBlankLongClick();
    }

    // ==================== Android ↔ JS 桥接 ====================
    /**
     * Android ↔ JavaScript 桥接类，供 WebView 中的 D3.js 地图页面调用。
     * <p>
     * 所有方法均标注 {@link JavascriptInterface}，在页面加载完成、站点长按、空白长按时从 JS 端回调。
     * </p>
     */
    private class AndroidBridge {
        /**
         * JS 页面初始化完成回调，处理暂存的路径绘制请求（起终点路径或直接站点路径）。
         */
        @JavascriptInterface
        public void onMapReady() {
            if (pendingStartStation != null && pendingEndStation != null) {
                mainHandler.postDelayed(() -> {
                    drawPath(pendingStartStation, pendingEndStation);
                    pendingStartStation = null;
                    pendingEndStation = null;
                }, DRAW_PATH_DELAY_MS);
            }
            // 新增：处理暂存的直接路径
            if (pendingStationNames != null && !pendingStationNames.isEmpty()) {
                mainHandler.postDelayed(() -> {
                    drawPathWithStations(pendingStationNames);
                    pendingStationNames = null;
                }, DRAW_PATH_DELAY_MS);
            }
        }

        /**
         * JS 回调：用户在地图页面上长按某一站点。
         *
         * @param stationName 被长按的站点名称
         */
        @JavascriptInterface
        public void onStationLongClick(final String stationName) {
            mainHandler.post(() -> {
                if (stationLongClickListener != null) {
                    stationLongClickListener.onStationLongClick(stationName);
                }
            });
        }

        /**
         * JS 回调：用户在地图页面空白区域执行长按操作。
         */
        @JavascriptInterface
        public void onBlankLongClick() {
            mainHandler.post(() -> {
                if (blankLongClickListener != null) {
                    blankLongClickListener.onBlankLongClick();
                }
            });
        }
    }
}
