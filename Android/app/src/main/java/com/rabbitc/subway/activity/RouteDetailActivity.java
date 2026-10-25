package com.rabbitc.subway.activity;

import android.annotation.SuppressLint;
import android.content.Context;
import android.graphics.Color;
import android.graphics.Paint;
import android.graphics.drawable.GradientDrawable;
import android.os.Bundle;
import android.view.Gravity;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;
import android.widget.BaseAdapter;
import android.widget.Button;
import android.widget.FrameLayout;
import android.widget.LinearLayout;
import android.widget.ListView;
import android.widget.TextView;
import android.widget.Toast;

import androidx.annotation.NonNull;

import com.rabbitc.subway.R;
import com.rabbitc.subway.algroithm.MetroGraph;
import com.rabbitc.subway.algroithm.TimeCalculator;
import com.rabbitc.subway.data.DataCache;
import com.rabbitc.subway.data.RouteSegment;
import com.rabbitc.subway.data.SearchWeight;
import com.rabbitc.subway.data.TransferLineInfo;
import com.rabbitc.subway.data.json.Line;
import com.rabbitc.subway.data.json.MetroData;
import com.rabbitc.subway.utils.GraphKeyUtils;
import com.rabbitc.subway.utils.GraphicsViewUtils;
import com.rabbitc.subway.utils.StationUtils;
import com.rabbitc.subway.utils.TextMatchUtils;
import com.rabbitc.subway.utils.WidgetUtils;
import com.rabbitc.subway.widget.StartEndBar;

import java.util.ArrayList;
import java.util.Collections;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;

/**
 * 路径详情：分段展示沿途站点、换乘信息及时间。
 */
public class RouteDetailActivity extends BaseActivity {

    // ---------- 常量 ----------
    public static final String EXTRA_PATH = "path";

    // ---------- 视图 ----------
    private ListView listView;

    // ---------- 数据 ----------
    private String startStation;
    private String endStation;
    private final List<Object> displayItems = new ArrayList<>();
    private StartEndBar startEndBar;
    long timeMillis;

    // ---------- 内部数据类 ----------
    private static class HeaderItem {
        String title;
        int color;
        boolean hasColor;
        HeaderItem(String title, int color) {
            this.title = title;
            this.color = color;
            hasColor = true;
        }
        HeaderItem(String title) {
            this.title = title;
            this.color = color;
            hasColor = false;
        }
    }

    private static class RouteStationItem {
        int index;
        String stationName;
        String timeStr;
        String extraText;
        boolean isInnerTransfer = false;
        boolean isOutTransfer = false;
        boolean isDirText = false;
        List<TransferLineInfo> transferLines;
        String distanceStr;

        RouteStationItem(int index, String stationName, String timeStr, String extraText,
                         String distanceStr, List<TransferLineInfo> transferLines) {
            this.index = index;
            this.stationName = stationName;
            this.timeStr = timeStr;
            this.distanceStr = distanceStr;
            this.transferLines = transferLines;
            this.extraText = extraText;
        }

        RouteStationItem(String stationName, String timeStr, String extraText) {
            this.stationName = stationName;
            this.timeStr = timeStr;
            this.extraText = extraText;
            isDirText = true;
        }
    }

    // ---------- 生命周期 ----------

    /**
     * 数据就绪后构建并展示路径详情。
     *
     * <p>接收上级页面传入的路径节点列表，通过 {@link MetroGraph} 解析为分段信息，
     * 计算每个站点的累计时间和距离，逐段展示沿途站点、换乘提示和预计到达时间。
     * 展示内容包括：行程开始（进站）、各分段标题及行驶方向、沿途站点列表、
     * 换乘信息（站内/出站）、行程结束（出站）。
     *
     * @param savedInstanceState 保存的实例状态
     */
    @Override
    protected void onDataReady(Bundle savedInstanceState) {
        setContentView(R.layout.activity_route_detail);

        listView = findViewById(R.id.listViewStations);
        startEndBar = findViewById(R.id.startEndBar);
        startEndBar.setOnStartEndClickListener(this::finish);

        ArrayList<String> path = getIntent().getStringArrayListExtra(EXTRA_PATH);
        if (path == null || path.size() < 2) {
            Toast.makeText(this, "路径数据无效", Toast.LENGTH_SHORT).show();
            finish();
            return;
        }

        DataCache cache = DataCache.getInstance();
        MetroGraph metroGraph = new MetroGraph();
        MetroData metroData = cache.getMetroData();
        metroGraph.buildGraphs(metroData, cache.getKeyCity());

        List<RouteSegment> segments = metroGraph.getRouteSegments(path);
        if (segments.isEmpty()) {
            Toast.makeText(this, "无法解析路径", Toast.LENGTH_SHORT).show();
            finish();
            return;
        }

        buildDisplayItems(metroGraph, path, segments);

        RouteAdapter adapter = new RouteAdapter(this, displayItems);
        listView.setAdapter(adapter);

        // 显示起终点
        startStation = getIntent().getStringExtra("start_station");
        endStation = getIntent().getStringExtra("end_station");
        updateStartEndDisplay();

        WidgetUtils.setListViewHeightBasedOnChildren(listView);
    }

    @SuppressLint("SetTextI18n")
    private void updateStartEndDisplay() {
        startEndBar.setStartStation(startStation);
        startEndBar.setEndStation(endStation);
    }

    // ---------- 辅助：从节点中提取站名 ----------
    private String getStationFromNode(String node) {
        String[] parts = node.split("\\|");
        if (parts.length >= 4) {
            return parts[3];
        }
        return node; // fallback
    }

    // ---------- 构建显示数据 ----------
    @SuppressLint("DefaultLocale")
    private void buildDisplayItems(MetroGraph metroGraph, List<String> path,
                                   List<RouteSegment> segments) {
        DataCache cache = DataCache.getInstance();
        // 详情列表按“段标题 + 站点明细”交替组织，适配器只负责渲染已计算好的展示模型。

        // ========== 核心修改：使用 MetrodData 代替 parsedMap ==========
        MetroData metroData = cache.getMetroData();
        Map<String, Line> linesObj = (metroData != null) ? metroData.lines : Collections.emptyMap();
        // ===============================================================

        double lastMinute = 0;
        String prevEndStation = null;
        displayItems.add(new HeaderItem("行程开始"));
        displayItems.add(new RouteStationItem(
                "通过闸机后进入地铁站",
                "0.0 分钟",
                "进站"
        ));

        for (int segIdx = 0; segIdx < segments.size(); segIdx++) {
            RouteSegment seg = segments.get(segIdx);
            RouteSegment nextSeg = (segIdx + 1 < segments.size()) ? segments.get(segIdx + 1) : null;

            // 1. 分段标题
            int titleColor = getSegmentTitleColor(seg, linesObj);
            displayItems.add(new HeaderItem(seg.lineName, titleColor));

            // 2. 乘坐方向
            String timeStr = buildTransferTimeStr(segIdx, lastMinute, seg,
                    segIdx > 0 ? segments.get(segIdx - 1) : null);
            RouteStationItem dirItem = new RouteStationItem(
                    "到站台等待" + seg.direction + "方向列车",
                    timeStr,
                    "等待"
            );
            dirItem.isDirText = true;
            displayItems.add(dirItem);

            // 3. 提取分段节点
            List<String> segmentNodes = extractSegmentNodes(path, seg);
            if (segmentNodes.isEmpty()) continue;

            // 若当前分段的起点与上一分段终点相同，去掉第一个节点
            if (seg.startStation.equals(prevEndStation)) {
                segmentNodes.remove(0);
            }
            prevEndStation = seg.endStation;

            // 4. 计算累计时间和距离
            List<Double> accumMinutes = computeAccumulatedMinutes(path, segmentNodes, metroGraph);
            List<Double> accumDistances = computeAccumulatedDistances(path, segmentNodes, metroGraph);

            int stationIndex = 1;
            for (int i = 0; i < segmentNodes.size(); i++) {
                String node = segmentNodes.get(i);
                String station = getStationFromNode(node);
                double accumMin = accumMinutes.get(i);
                double accumDist = accumDistances.get(i);
                String nodeTime = String.format("%.1f 分钟", accumMin);
                String nodeDist = String.format("%.1f km", accumDist);
                String extraText = null;
                if (i == 0) extraText = "发车";
                if (i == segmentNodes.size() - 1) extraText = "下车";

                RouteStationItem item = new RouteStationItem(
                        stationIndex,
                        station,
                        nodeTime,
                        extraText, nodeDist,
                        null
                );
                displayItems.add(item);
                stationIndex++;
                lastMinute = accumMin;
            }

            // 5. 换乘或下车提示
            if (nextSeg != null) {
                double transferSeconds = StationUtils.getTransferTime(seg.endStation, seg.dirKey, nextSeg.dirKey);
                double transferMinutes = transferSeconds / 60.0;
                String transferType = nextSeg.isOutStationTransfer ? "出站换乘" : "站内换乘";
                String lineName = nextSeg.lineName;
                String transferDesc = String.format("下车换乘 %s（%s约%.0f分钟）",
                        lineName, transferType, transferMinutes);
                String transferTime = String.format("%.1f 分钟", lastMinute);

                RouteStationItem transferItem = new RouteStationItem(
                        Integer.MAX_VALUE,
                        transferDesc,
                        transferTime,
                        null, null,   // 距离不显示
                        null
                );
                if (nextSeg.isOutStationTransfer) {
                    transferItem.isOutTransfer = true;
                } else {
                    transferItem.isInnerTransfer = true;
                }
                displayItems.add(transferItem);
            } else {
                String endTime = String.format("%.1f 分钟", lastMinute);
                RouteStationItem endItem = new RouteStationItem(
                        "在" + seg.endStation + "准备下车",
                        endTime,
                        "下车"
                );
                displayItems.add(endItem);
            }
        }

        double avgEntryTime = DataCache.getInstance().getAvgEntryTime() / 60;
        String endTime = String.format("%.1f 分钟", lastMinute + avgEntryTime);
        displayItems.add(new HeaderItem("行程结束"));
        displayItems.add(new RouteStationItem(
                "通过闸机后离开地铁站",
                endTime,
                "出站"
        ));
    }

    private List<Double> computeAccumulatedDistances(List<String> path, List<String> segmentNodes,
                                                     MetroGraph metroGraph) {
        // 每个站点都从完整路径前缀重新计算，保证换乘后距离仍是全程累计值。
        List<Double> distances = new ArrayList<>();
        for (String node : segmentNodes) {
            int idx = path.indexOf(node);
            if (idx == -1) continue;
            List<String> subPath = path.subList(0, idx + 1);
            double dist = metroGraph.computePathDistance(subPath);
            distances.add(dist);
        }
        return distances;
    }

    // ========== 修改了参数类型 (Map<String, Object> -> Map<String, Line>) ==========
    private int getSegmentTitleColor(RouteSegment seg, Map<String, Line> linesObj) {
        Line line = linesObj.get(seg.lineId);
        String colorStr = (line != null && line.color != null) ? line.color : "#999999";
        if (!colorStr.startsWith("#")) colorStr = "#" + colorStr;
        return Color.parseColor(colorStr);
    }

    @SuppressLint("DefaultLocale")
    private String buildTransferTimeStr(int segIdx, double lastMinute, RouteSegment seg, RouteSegment lastSeg) {
        double avgEntryTime = DataCache.getInstance().getAvgEntryTime() / 60;
        if (segIdx == 0) return String.format("%.1f 分钟", avgEntryTime);
        SearchWeight weight = SearchWeight.fromContext(this);
        double transferTime = 0.0;
        if (lastSeg != null)
        {
            transferTime = StationUtils.getTransferTime(
                    seg.startStation,        // 换乘站（当前段的起点）
                    lastSeg.dirKey,          // 上一方向
                    seg.dirKey               // 当前方向
            );
        }
        double minute = lastMinute + (transferTime / 60.0);
        return String.format("%.1f 分钟", minute);
    }

    private List<String> extractSegmentNodes(List<String> path, RouteSegment seg) {
        // 通过方向段的起终点定位原始图节点，保留方向信息供时间计算使用。
        List<String> nodes = new ArrayList<>();
        boolean inSegment = false;
        for (String node : path) {
            String station = getStationFromNode(node);
            if (station.equals(seg.startStation) && !inSegment) {
                inSegment = true;
            }
            if (inSegment) {
                nodes.add(node);
                if (station.equals(seg.endStation)) {
                    break;
                }
            }
        }
        return nodes;
    }

    private List<Double> computeAccumulatedMinutes(List<String> path, List<String> segmentNodes,
                                                   MetroGraph metroGraph) {
        double avgEntryTime = DataCache.getInstance().getAvgEntryTime() / 60;
        List<Double> minutes = new ArrayList<>();
        SearchWeight weight = SearchWeight.fromContext(this);
        for (String node : segmentNodes) {
            int idx = path.indexOf(node);
            if (idx == -1) continue;
            List<String> subPath = path.subList(0, idx + 1);
            double timeSec = TimeCalculator.computeTotalTime(subPath, metroGraph, weight, timeMillis);
            if (timeSec < 1e-3) { minutes.add(avgEntryTime); }
            else { minutes.add(timeSec / 60.0 - avgEntryTime); }
        }
        return minutes;
    }

    // ---------- 内部适配器 ----------
    private static class RouteAdapter extends BaseAdapter {
        private final Context context;
        private final List<Object> data;
        private final LayoutInflater inflater;

        RouteAdapter(Context context, List<Object> data) {
            this.context = context;
            this.data = data;
            this.inflater = LayoutInflater.from(context);
        }

        @Override public int getCount() { return data.size(); }
        @Override public Object getItem(int position) { return data.get(position); }
        @Override public long getItemId(int position) { return position; }

        @Override public int getViewTypeCount() { return 3; }
        @Override public int getItemViewType(int position) {
            Object obj = data.get(position);
            if (obj instanceof HeaderItem) {
                HeaderItem header = (HeaderItem) obj;
                return header.hasColor ? 0 : 2; // 有颜色=0，无颜色=2
            }
            return 1; // 站点
        }

        @Override
        public View getView(int position, View convertView, ViewGroup parent) {
            int type = getItemViewType(position);
            if (type == 0 || type == 2) {
                return getHeaderView(position, convertView);
            } else {
                return getStationView(position, convertView, parent);
            }
        }

        // 标题行（横线 + 居中文字）
        private View getHeaderView(int position, View convertView) {
            final HeaderItem header = (HeaderItem) data.get(position);
            FrameLayout container;
            TextView tvTitle;
            View colorBlock;

            if (!(convertView instanceof FrameLayout)) {
                // 创建新的标题视图
                container = new FrameLayout(context);
                container.setBackgroundColor(Color.WHITE);
                container.setPadding(16, 16, 16, 16);

                // 横线
                View line = new View(context);
                line.setBackgroundColor(Color.parseColor("#CCCCCC"));
                FrameLayout.LayoutParams lineParams = new FrameLayout.LayoutParams(
                        FrameLayout.LayoutParams.MATCH_PARENT, 2);
                lineParams.gravity = Gravity.CENTER_VERTICAL;
                line.setLayoutParams(lineParams);
                container.addView(line);

                // 水平布局（颜色块 + 标题文字）
                LinearLayout horizontal = new LinearLayout(context);
                horizontal.setOrientation(LinearLayout.HORIZONTAL);
                horizontal.setGravity(Gravity.CENTER_VERTICAL);
                horizontal.setBackgroundColor(Color.WHITE);
                horizontal.setPadding(16, 8, 16, 8);

                // 颜色块（可能不存在）
                colorBlock = null;
                if (header.hasColor) {
                    colorBlock = new View(context);
                    LinearLayout.LayoutParams blockParams = new LinearLayout.LayoutParams(48, 48);
                    blockParams.setMargins(0, 0, 16, 0);
                    colorBlock.setLayoutParams(blockParams);
                    horizontal.addView(colorBlock);
                }

                // 标题文字
                tvTitle = new TextView(context);
                tvTitle.setTextSize(18);
                tvTitle.setTypeface(null, android.graphics.Typeface.BOLD);
                tvTitle.setTextColor(Color.BLACK);
                horizontal.addView(tvTitle);

                // 将水平布局居中添加到容器
                FrameLayout.LayoutParams layoutParams = new FrameLayout.LayoutParams(
                        FrameLayout.LayoutParams.WRAP_CONTENT,
                        FrameLayout.LayoutParams.WRAP_CONTENT);
                layoutParams.gravity = Gravity.CENTER;
                container.addView(horizontal, layoutParams);

                // 保存可变子视图到 Tag（使用长度为2的数组，[0]=标题, [1]=颜色块）
                container.setTag(new Object[]{tvTitle, colorBlock});
                convertView = container;
            } else {
                // 复用已有视图，从 Tag 中取出子视图
                container = (FrameLayout) convertView;
                Object[] tag = (Object[]) container.getTag();
                if (tag != null && tag.length == 2) {
                    tvTitle = (TextView) tag[0];
                    colorBlock = (View) tag[1];
                } else {
                    // 若 Tag 丢失（例如被意外清除），则回退查找（但实际不会发生）
                    return convertView;
                }
            }

            // 更新标题文字
            tvTitle.setText(header.title);

            // 更新颜色块（若存在）
            if (colorBlock != null) {
                colorBlock.setBackgroundColor(header.hasColor ? header.color : Color.TRANSPARENT);
                colorBlock.setVisibility(header.hasColor ? View.VISIBLE : View.GONE);
            }

            return convertView;
        }

        // 站点行（含序号、站名、时间、换乘标签）
        @SuppressLint("SetTextI18n")
        private View getStationView(int position, View convertView, ViewGroup parent) {
            if (convertView == null || convertView instanceof TextView) {
                convertView = inflater.inflate(R.layout.item_route_station, parent, false);
            }
            RouteStationItem item = (RouteStationItem) data.get(position);

            TextView tvIndex = convertView.findViewById(R.id.tvIndex);
            TextView tvStationName = convertView.findViewById(R.id.tvStationName);
            TextView tvTime = convertView.findViewById(R.id.tvTime);
            TextView tvDistance = convertView.findViewById(R.id.tvDistance);
            LinearLayout llTransfer = convertView.findViewById(R.id.llTransferLines);

            // 序号（圆形背景）
            GradientDrawable indexBg = new GradientDrawable();
            indexBg.setShape(GradientDrawable.RECTANGLE);
            indexBg.setCornerRadius(8);
            indexBg.setColor(Color.parseColor("#2196F3"));
            tvIndex.setBackground(indexBg);
            tvIndex.setTextColor(Color.WHITE);
            tvIndex.setText(String.valueOf(item.index));

            tvStationName.setText(item.stationName + (item.extraText != null && !item.isDirText ? ("（"+ item.extraText +"）") : ""));
            tvStationName.setTextColor(Color.BLACK);
            tvTime.setText(item.timeStr);
            // 在 getStationView 中，设置时间与距离
            if (item.distanceStr != null && !item.distanceStr.isEmpty()) {
                tvDistance.setText("（" + item.distanceStr + "）");
            } else {
                tvDistance.setText("");
            }

            // 换乘标签
            llTransfer.removeAllViews();
            if (item.transferLines != null && !item.transferLines.isEmpty()) {
                for (TransferLineInfo info : item.transferLines) {
                    TextView tag = createTransferTag(context, info);
                    llTransfer.addView(tag);
                }
            }

            // 特殊类型着色
            if (item.isOutTransfer) {
                tvStationName.setTextColor(Color.parseColor("#FF6B00"));
                tvIndex.setText("换乘");
            } else if (item.isInnerTransfer) {
                tvStationName.setTextColor(Color.parseColor("#4CAF50"));
                tvIndex.setText("换乘");
            } else if (item.isDirText) {
                tvStationName.setTextColor(Color.parseColor("#FF5BAA"));
                tvIndex.setText(item.extraText);
            }

            return convertView;
        }

        @SuppressLint("SetTextI18n")
        @NonNull
        private TextView createTransferTag(Context context, TransferLineInfo info) {
            TextView tag = new TextView(context);
            tag.setText(info.displayName);
            tag.setTextSize(12);
            tag.setPadding(16, 4, 16, 4);
            tag.setTextColor(GraphicsViewUtils.isColorBright(info.color) ? Color.BLACK : Color.WHITE);

            GradientDrawable drawable = new GradientDrawable();
            drawable.setShape(GradientDrawable.RECTANGLE);
            drawable.setCornerRadius(8);
            drawable.setColor(info.color);
            tag.setBackground(drawable);

            // 处理换乘状态
            boolean isClosed = StationUtils.isClosed(info.status);
            if (isClosed) {
                drawable.setColor(Color.LTGRAY);
                tag.setBackground(drawable);
                tag.setTextColor(Color.DKGRAY);
                tag.setText(info.displayName + "(暂缓)");
                tag.setPaintFlags(tag.getPaintFlags() | Paint.STRIKE_THRU_TEXT_FLAG);
            } else if (StationUtils.isOutStation(info.status)) {
                tag.setText(info.displayName + "(出站)");
            }

            LinearLayout.LayoutParams params = new LinearLayout.LayoutParams(
                    ViewGroup.LayoutParams.WRAP_CONTENT,
                    ViewGroup.LayoutParams.WRAP_CONTENT);
            params.setMarginEnd(8);
            tag.setLayoutParams(params);
            return tag;
        }
    }
}
