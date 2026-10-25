package com.rabbitc.subway.activity;

import android.annotation.SuppressLint;
import android.content.Intent;
import android.os.Bundle;
import android.util.Log;
import android.widget.Toast;

import androidx.appcompat.app.AlertDialog;

import com.rabbitc.subway.R;
import com.rabbitc.subway.algroithm.MetroGraph;
import com.rabbitc.subway.data.DataCache;
import com.rabbitc.subway.data.LineLegendItem;
import com.rabbitc.subway.data.json.MetroData;
import com.rabbitc.subway.listener.OnStationLongClickListener;
import com.rabbitc.subway.utils.GraphKeyUtils;
import com.rabbitc.subway.utils.LegendBuilder;
import com.rabbitc.subway.utils.StationUtils;
import com.rabbitc.subway.view.SubwayMapView;
import com.rabbitc.subway.widget.LegendListView;
import com.rabbitc.subway.widget.StartEndBar;

import java.util.ArrayList;
import java.util.Collections;
import java.util.HashMap;
import java.util.HashSet;
import java.util.List;
import java.util.Map;
import java.util.Set;

/**
 * 地铁线路地图页面，支持缩放、平移、线路高亮、起终点设置及路径展示。
 */
public class MapViewActivity extends BaseActivity implements OnStationLongClickListener,
        SubwayMapView.OnBlankLongClickListener {

    // ---------- 常量 ----------
    private static final int REQUEST_LINE_DETAIL = 100;

    // ---------- 视图 ----------
    private SubwayMapView mapView;
    private LegendListView legendListView;
    private List<String> incomingPathStations = null;

    // ---------- 数据 ----------
    private final List<LineLegendItem> legendItems = new ArrayList<>();
    private final Map<String, Set<String>> nameToLineIds = new HashMap<>();
    private String highlightedLineName = null;
    private String startStation = null;
    private String endStation = null;
    private MetroGraph metroGraph;
    private Map<String, double[]> stationCoords;
    private StartEndBar startEndBar;

    // ---------- 生命周期 ----------

    /**
     * 数据就绪后初始化交互式地铁地图页面。
     *
     * <p>执行以下初始化流程：
     * <ol>
     *   <li>初始化 {@link SubwayMapView} 和 {@link LegendListView} 视图</li>
     *   <li>加载站点坐标、线路数据和图例列表</li>
     *   <li>构建 {@link MetroGraph} 用于路径搜索</li>
     *   <li>从 Intent 恢复起终点状态，如有外部路径直接绘制</li>
     * </ol>
     *
     * @param savedInstanceState 保存的实例状态
     */
    @SuppressLint("SetTextI18n")
    @Override
    protected void onDataReady(Bundle savedInstanceState) {
        try {
            setContentView(R.layout.activity_map);
            initViews();
            setupListeners();
            loadDataAndBuildGraph();
            restoreStateFromIntent();
        } catch (Exception e) {
            Log.e("MapActivity", "启动失败", e);
            Toast.makeText(this, "错误: " + e.getMessage(), Toast.LENGTH_LONG).show();
            finish();
        }
    }

    /**
     * 处理从线路详情页返回的结果。
     *
     * <p>当用户在 {@link DirectionInfoActivity} 中修改了起终点后，
     * 回传结果更新起终点显示。如果起终点发生变化且均已设置，
     * 则清除外部路径并重新搜索路径。
     *
     * @param requestCode 请求码
     * @param resultCode  结果码
     * @param data        返回的 Intent 数据
     */
    @Override
    protected void onActivityResult(int requestCode, int resultCode, Intent data) {
        super.onActivityResult(requestCode, resultCode, data);
        if (requestCode == REQUEST_LINE_DETAIL) {
            handleLineDetailResult(resultCode, data);
        }
    }

    /**
     * 退出当前页面时将起终点信息回传给上级 Activity。
     *
     * <p>将 {@code startStation} 和 {@code endStation} 封装到结果 Intent 中，
     * 通过 {@link #setResult(int, Intent)} 传递给发起方。
     */
    @Override
    public void finish() {
        Intent resultIntent = new Intent();
        if (startStation != null) resultIntent.putExtra("start_station", startStation);
        if (endStation != null) resultIntent.putExtra("end_station", endStation);
        setResult(RESULT_OK, resultIntent);
        super.finish();
    }

    // ---------- 初始化 ----------
    private void initViews() {
        mapView = findViewById(R.id.subwayMapView);
        mapView.setOnStationLongClickListener(this);
        mapView.setOnBlankLongClickListener(this);
        legendListView = findViewById(R.id.legendListView);
        startEndBar = findViewById(R.id.startEndBar);
        startEndBar.setOnStartEndClickListener(this::finish);
    }

    private void setupListeners() {
        // 图例点击只改变显示状态；真正的路径绘制仍由起终点或外部路径触发。
        legendListView.setItems(legendItems);
        legendListView.setOnItemSelectedListener(item -> {
            String lineName = item.lineName;
            Set<String> lineIds = getLineIdsByName(lineName);

            if (highlightedLineName != null && highlightedLineName.equals(lineName)) {
                // 取消高亮
                highlightedLineName = null;
                legendListView.setSelectedLine(null);
                startEndBar.showHint(true);
                if (startStation != null && endStation != null) {
                    highlightPath();
                } else {
                    mapView.clearPath();
                }
            } else {
                // 高亮新线路
                highlightedLineName = lineName;
                legendListView.setSelectedLine(lineName);
                mapView.clearPath();
                mapView.highlightLines(lineIds);
                mapView.zoomToFitLines(lineIds);
                startEndBar.showHint(false);
            }
        });
        legendListView.setOnItemLongClickListener(item -> openLineDetail(item.lineId));

        // 清除按钮
        findViewById(R.id.clearBtn).setOnClickListener(v -> onBlankLongClick());
    }

    // ---------- 数据加载 ----------
    private void loadDataAndBuildGraph() {
        DataCache cache = DataCache.getInstance();
        // 坐标、图例和路径图都从同一份 DataCache 构建，避免页面之间出现名称不一致。
        stationCoords = cache.getStationCoords();
        if (stationCoords == null || stationCoords.isEmpty()) {
            mapView.showError("站点坐标数据为空");
            return;
        }

        // 构建 nameToLineIds（用于图例点击高亮）
        Map<String, List<String>> dirStations = cache.getDirStations();
        Map<String, String> lineNameMap = cache.getLineNameMap();
        String keyCity = cache.getKeyCity();

        nameToLineIds.clear();
        for (Map.Entry<String, List<String>> entry : dirStations.entrySet()) {
            String dirKey = entry.getKey();
            String[] parts = GraphKeyUtils.parseDirKey(dirKey);
            if (parts.length < 3) continue;
            String lineId = parts[0];
            String lineDisplayName = lineNameMap.get(lineId);
            if (lineDisplayName == null) continue;
            nameToLineIds.computeIfAbsent(lineDisplayName, k -> new HashSet<>()).add(lineId);
        }

        // 构建图例数据
        buildLegendData();

        // 构建 MetroGraph
        metroGraph = new MetroGraph();
        MetroData metroData = cache.getMetroData();
        metroGraph.buildGraphs(metroData, keyCity);
        mapView.setData();
        mapView.setMetroGraph(metroGraph);
    }

    private void buildLegendData() {
        legendItems.clear();
        legendItems.addAll(LegendBuilder.buildLegendItems(DataCache.getInstance(), false));
    }

    // ---------- 恢复 Intent 状态 ----------
    @SuppressLint("SetTextI18n")
    private void restoreStateFromIntent() {
        Intent intent = getIntent();
        startStation = intent.getStringExtra("start_station");
        endStation = intent.getStringExtra("end_station");

        // 获取外部传入的路径站点列表（优先）
        ArrayList<String> pathList = intent.getStringArrayListExtra("path_stations");
        if (pathList != null && !pathList.isEmpty()) {
            incomingPathStations = new ArrayList<>(pathList);
        }

        // 更新UI显示
        startEndBar.setStartStation(startStation);
        startEndBar.setEndStation(endStation);

        // 决策：如果有外部路径，直接绘制；否则按原有逻辑（起终点搜索）
        if (incomingPathStations != null && !incomingPathStations.isEmpty()) {
            drawExternalPath(incomingPathStations);
        } else if (startStation != null && endStation != null) {
            highlightPath(); // 自动搜索
        }
        legendListView.setSelectedLine(null);
    }

    private void drawExternalPath(List<String> pathStations) {
        // 外部传入的是站点名称而非图节点，交给 WebView 统一完成坐标映射和绘制。
        if (mapView != null) {
            mapView.clearPath();
            mapView.drawPathWithStations(pathStations);
            // 缩放至这些站点
            mapView.zoomToFitStations(new HashSet<>(pathStations));
            startEndBar.showHint(false);
        }
    }

    // ---------- 图例辅助 ----------
    private Set<String> getLineIdsByName(String name) {
        Set<String> ids = nameToLineIds.get(name);
        return ids != null ? ids : Collections.emptySet();
    }

    // ---------- 路径绘制 ----------
    private void highlightPath() {
        // 只有起终点齐全时才规划路径；否则清除旧路线避免残留视觉状态。
        if (startStation == null || endStation == null) {
            Toast.makeText(this, "请先设置起点和终点", Toast.LENGTH_SHORT).show();
            return;
        }
        if (metroGraph == null || stationCoords == null || stationCoords.isEmpty()) {
            Toast.makeText(this, "数据未就绪，请重新加载", Toast.LENGTH_SHORT).show();
            return;
        }
        Set<String> allStations = metroGraph.getAllStations();
        if (allStations == null || !allStations.contains(startStation) || !allStations.contains(endStation)) {
            Toast.makeText(this, "起点或终点不存在", Toast.LENGTH_SHORT).show();
            return;
        }
        startEndBar.showHint(false);
        mapView.clearPath();
        mapView.drawPath(startStation, endStation);
        mapView.zoomToFitStations(Set.of(startStation, endStation));
    }

    // ---------- 线路详情跳转 ----------
    private void openLineDetail(String lineId) {
        Intent detailIntent = new Intent(MapViewActivity.this, DirectionInfoActivity.class);
        detailIntent.putExtra(DirectionInfoActivity.EXTRA_LINE_ID, lineId);
        if (startStation != null) detailIntent.putExtra("start_station", startStation);
        if (endStation != null) detailIntent.putExtra("end_station", endStation);
        startActivityForResult(detailIntent, REQUEST_LINE_DETAIL);
    }

    @SuppressLint("SetTextI18n")
    private void handleLineDetailResult(int resultCode, Intent data) {
        // 清除高亮
        highlightedLineName = null;
        mapView.highlightLines(Collections.emptySet());

        if (resultCode == RESULT_OK && data != null) {
            String start = data.getStringExtra("start_station");
            String end = data.getStringExtra("end_station");
            boolean hasNewStart = start != null && !start.equals(startStation);
            boolean hasNewEnd = end != null && !end.equals(endStation);
            if (hasNewStart) {
                startStation = start;
                startEndBar.setStartStation(startStation);
            }
            if (hasNewEnd) {
                endStation = end;
                startEndBar.setEndStation(endStation);
            }

            // 如果起终点发生变化，清除外部路径并重新搜索
            if ((hasNewStart || hasNewEnd) && startStation != null && endStation != null) {
                incomingPathStations = null; // 清除外部路径
                highlightPath();
            } else if (start != null && end != null && incomingPathStations == null) {
                // 如果没有外部路径，且起终点都有效，则搜索（但通常不会发生）
                highlightPath();
            }

            highlightedLineName = null;
            mapView.highlightLines(Collections.emptySet());
            legendListView.setSelectedLine(null);
        }
    }

    // ---------- 长按事件（站点 + 空白区域） ----------

    /**
     * 处理地图上站点的长按事件，用于设置或重置起终点。
     *
     * <p>行为逻辑：
     * <ul>
     *   <li>如果该站点是规划建设站点，直接返回不处理</li>
     *   <li>如果起终点全部为空，弹出对话框询问设为起点还是终点</li>
     *   <li>如果起终点均已设置，询问是否重置（将该站点设为起点并清空终点）</li>
     * </ul>
     *
     * @param stationName 被长按的站点名称
     */
    @SuppressLint("SetTextI18n")
    @Override
    public void onStationLongClick(String stationName) {
        if (StationUtils.isFutureStation(stationName)) return;
        if (startStation == null || endStation == null) {
            // 第一次设置
            showSetStationDialog(stationName);
        } else {
            // 已存在起终点，询问重置
            new AlertDialog.Builder(this)
                    .setTitle("重置起终点")
                    .setMessage("已存在起点和终点，是否重置？")
                    .setPositiveButton("重置（将此站设置为起点）", (dialog, which) -> {
                        startStation = stationName;
                        endStation = null;
                        startEndBar.setStartStation(startStation);
                        startEndBar.setEndStation(null);
                        Toast.makeText(this, "起点已重置，请再长按设置终点", Toast.LENGTH_SHORT).show();
                        mapView.clearPath();
                        startEndBar.showHint(true);
                    })
                    .setNegativeButton("取消", null)
                    .show();
        }
    }

    /**
     * 处理地图空白区域的长按事件，用于清除已设置的起终点。
     *
     * <p>如果未设置起终点则提示用户；否则弹出确认对话框，
     * 确认后清除起终点、外部路径和高亮状态，恢复地图初始状态。
     */
    @Override
    public void onBlankLongClick() {
        if (startStation == null && endStation == null) {
            Toast.makeText(this, "当前未设置起点和终点", Toast.LENGTH_SHORT).show();
            return;
        }
        new AlertDialog.Builder(this)
                .setTitle("清除起终点")
                .setMessage("是否清除已设置的起点和终点？")
                .setPositiveButton("清除", (dialog, which) -> {
                    startStation = null;
                    endStation = null;
                    incomingPathStations = null;
                    startEndBar.setStartStation(null);
                    startEndBar.setEndStation(null);
                    mapView.highlightLines(Collections.emptySet());
                    highlightedLineName = null;
                    startEndBar.showHint(true);
                    Toast.makeText(this, "已清除", Toast.LENGTH_SHORT).show();
                    mapView.clearPath();
                    mapView.highlightLines(Collections.emptySet());
                    highlightedLineName = null;
                    legendListView.setSelectedLine(null);
                })
                .setNegativeButton("取消", null)
                .show();
    }

    @SuppressLint("SetTextI18n")
    private void showSetStationDialog(String stationName) {
        new AlertDialog.Builder(this)
                .setTitle("设置站点")
                .setMessage("将 \"" + stationName + "\" 设为起点还是终点？")
                .setNegativeButton("起点", (dialog, which) -> {
                    startStation = stationName;
                    incomingPathStations = null;
                    startEndBar.setStartStation(startStation);
                    Toast.makeText(this, "起点已设为 " + stationName, Toast.LENGTH_SHORT).show();
                    if (endStation != null) highlightPath();
                })
                .setPositiveButton("终点", (dialog, which) -> {
                    endStation = stationName;
                    incomingPathStations = null;
                    startEndBar.setEndStation(endStation);
                    Toast.makeText(this, "终点已设为 " + stationName, Toast.LENGTH_SHORT).show();
                    if (startStation != null) highlightPath();
                })
                .setNeutralButton("取消", null)
                .show();
    }
}
