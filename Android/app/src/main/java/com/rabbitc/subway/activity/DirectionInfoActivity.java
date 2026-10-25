package com.rabbitc.subway.activity;

import android.annotation.SuppressLint;
import android.content.Intent;
import android.graphics.Color;
import android.graphics.Paint;
import android.graphics.drawable.GradientDrawable;
import android.net.Uri;
import android.os.Bundle;
import android.text.TextUtils;
import android.util.Log;
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
import androidx.appcompat.app.AlertDialog;

import com.rabbitc.subway.R;
import com.rabbitc.subway.data.parse.CrowdRule;
import com.rabbitc.subway.data.DataCache;
import com.rabbitc.subway.data.DisplayItem;
import com.rabbitc.subway.data.LineDirection;
import com.rabbitc.subway.data.parse.OpenTimeRule;
import com.rabbitc.subway.data.parse.SpeedLimit;
import com.rabbitc.subway.data.TransferLineInfo;
import com.rabbitc.subway.data.parse.HeadwayRule;
import com.rabbitc.subway.data.parse.VirtualTransfer;
import com.rabbitc.subway.data.json.Direction;
import com.rabbitc.subway.data.json.DirectionStation;
import com.rabbitc.subway.data.json.Line;
import com.rabbitc.subway.data.json.MetroData;
import com.rabbitc.subway.data.json.Station;
import com.rabbitc.subway.data.json.StationLine;
import com.rabbitc.subway.listener.OnStationActionListener;
import com.rabbitc.subway.utils.GraphKeyUtils;
import com.rabbitc.subway.utils.GraphicsViewUtils;
import com.rabbitc.subway.utils.StationUtils;
import com.rabbitc.subway.utils.TextMatchUtils;
import com.rabbitc.subway.utils.WidgetUtils;
import com.rabbitc.subway.view.LineMapView;
import com.rabbitc.subway.widget.StartEndBar;

import java.text.SimpleDateFormat;
import java.util.ArrayList;
import java.util.Collections;
import java.util.Comparator;
import java.util.Date;
import java.util.HashSet;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Locale;
import java.util.Map;
import java.util.Objects;
import java.util.Set;

/**
 * 展示某条线路的详细信息，包括基本信息、标签、站点列表及换乘信息，
 * 并内置线路示意图（LineMapView）。
 */
public class DirectionInfoActivity extends BaseActivity {

    // ---------- 常量 ----------
    public static final String EXTRA_LINE_ID = "line_id";
    private static final int REQUEST_LINE_DETAIL = 200;

    // ---------- 视图控件 ----------
    private TextView tvLineName, tvCity, tvFormation, tvOpenYear, tvMaxSpeed, tvOpenTimes,
            tvPower, tvBrake, tvSpeedLimits, tvVirtualTransfer, tvCurrentCrowd, tvMaxCrowd, tvMinCrowd,
            tvCurrentHeadway, tvMaxHeadway, tvMinHeadway, tvShortTurnCount;
    private TextView tvCurrentLength, tvFutureLength;
    private ListView listViewStations;
    private FrameLayout mapContainer;
    private LinearLayout lineTagsContainer, tvLink;

    // ---------- 数据 ----------
    private String selectedLineId;
    private String selectedLineDisplayName;
    private String startStation;
    private String endStation;
    private final Set<String> fastStations = new HashSet<>();
    private int mLineColor; // 存放线路颜色，供后台线程计算使用
    private LineMapView lineMapView;
    private StartEndBar startEndBar;

    // ---------- 生命周期 ----------

    /**
     * 数据就绪后加载并展示线路详细信息。
     *
     * <p>依次执行以下步骤：
     * <ol>
     *   <li>初始化视图和起终点显示</li>
     *   <li>在主线程极速渲染线路头部基本信息（名称、城市、编组、开通时间等）</li>
     *   <li>后台计算复杂的站点列表、运营时间、发车间隔、拥挤度、技术参数、限速区间和虚拟换乘</li>
     *   <li>根据计算结果刷新 UI 并绘制线路示意图</li>
     * </ol>
     *
     * @param savedInstanceState 保存的实例状态
     */
    @Override
    protected void onDataReady(Bundle savedInstanceState) {
        setContentView(R.layout.activity_direction_info);

        startStation = getIntent().getStringExtra("start_station");
        endStation = getIntent().getStringExtra("end_station");

        initViews();
        updateStartEndDisplay();

        selectedLineId = getIntent().getStringExtra(EXTRA_LINE_ID);
        if (selectedLineId == null) {
            Toast.makeText(this, "未指定线路", Toast.LENGTH_SHORT).show();
            finish();
            return;
        }

        DataCache cache = DataCache.getInstance();
        if (!cache.isCacheValid()) {
            Toast.makeText(this, "数据未加载", Toast.LENGTH_SHORT).show();
            finish();
            return;
        }

        try {
            // 1. 主线程极速渲染头部信息
            loadBasicLineInfo();

            // 2. 执行复杂的列表构建与规则计算
            LoadResult result = buildComplexDisplayListInBackground();
            applyResultToUI(result);
            setupMap();
            WidgetUtils.setListViewHeightBasedOnChildren(listViewStations);
        } catch (Exception e) {
            Log.e("ERROR", "加载地铁数据失败", e);
            Toast.makeText(this, "数据加载失败，请检查文件格式", Toast.LENGTH_LONG).show();
            finish();
        }
    }

    // ---------- 初始化视图 ----------
    private void initViews() {
        tvLineName = findViewById(R.id.tvLineName);
        tvCity = findViewById(R.id.tvCity);
        tvFormation = findViewById(R.id.tvFormation);
        tvOpenYear = findViewById(R.id.tvOpenYear);
        tvLink = findViewById(R.id.tvLink);
        tvFutureLength = findViewById(R.id.tvFutureLength);
        tvCurrentLength = findViewById(R.id.tvCurrentLength);
        listViewStations = findViewById(R.id.listViewStations);
        tvMaxSpeed = findViewById(R.id.tvMaxSpeed);
        tvShortTurnCount = findViewById(R.id.tvShortTurnCount);
        mapContainer = findViewById(R.id.mapContainer);
        lineTagsContainer = findViewById(R.id.lineTagsContainer);
        tvOpenTimes = findViewById(R.id.tvOpenTimes);
        tvPower = findViewById(R.id.tvPower);
        tvBrake = findViewById(R.id.tvBrake);
        tvSpeedLimits = findViewById(R.id.tvSpeedLimits);
        tvVirtualTransfer = findViewById(R.id.tvVirtualTransfer);
        tvCurrentCrowd = findViewById(R.id.tvCurrentCrowd);
        tvMaxCrowd = findViewById(R.id.tvMaxCrowd);
        tvMinCrowd = findViewById(R.id.tvMinCrowd);
        tvCurrentHeadway = findViewById(R.id.tvCurrentHeadway);
        tvMaxHeadway = findViewById(R.id.tvMaxHeadway);
        tvMinHeadway = findViewById(R.id.tvMinHeadway);
        startEndBar = findViewById(R.id.startEndBar);
        startEndBar.setOnStartEndClickListener(this::finish);
    }

    private void updateStartEndDisplay() {
        startEndBar.setStartStation(startStation);
        startEndBar.setEndStation(endStation);
    }

    // ========== 1. 仅做基础信息展示（在主线程执行） ==========
    @SuppressLint({"SetTextI18n", "DefaultLocale"})
    private void loadBasicLineInfo() {
        DataCache cache = DataCache.getInstance();
        MetroData metroData = cache.getMetroData();
        if (metroData == null) return;
        Map<String, Line> linesObj = metroData.lines;
        if (linesObj == null || !linesObj.containsKey(selectedLineId)) return;

        Line lineInfo = linesObj.get(selectedLineId);
        String keyCity = cache.getKeyCity();

        String baseName = lineInfo.name != null ? lineInfo.name : selectedLineId;
        String city = lineInfo.city != null ? lineInfo.city : keyCity;
        String formation = lineInfo.formation != null ? lineInfo.formation : "未知";
        int openYearInt = lineInfo.openYear;
        String openYear = openYearInt > 0 ? String.valueOf(openYearInt) : "未知";
        String link = lineInfo.link != null ? lineInfo.link : "";
        if (TextUtils.isEmpty(link)) link = "暂无";
        int maxSpeed = lineInfo.maxspeed;

        selectedLineDisplayName = city.equals(keyCity) ? baseName : city + baseName;
        if (lineInfo.englishName != null && !lineInfo.englishName.isEmpty()) {
            selectedLineDisplayName += " (" + lineInfo.englishName + ")";
        }
        String colorStr = lineInfo.color != null ? lineInfo.color : "#999999";
        if (!colorStr.startsWith("#")) colorStr = "#" + colorStr;
        mLineColor = Color.parseColor(colorStr);

        tvLineName.setText("线路名称：" + selectedLineDisplayName);
        tvCity.setText("所属城市：" + city);
        tvFormation.setText("编组信息：" + formation);
        tvOpenYear.setText("开通时间：" + openYear);
        tvMaxSpeed.setText("最高速度：" + (maxSpeed > 0 ? maxSpeed + " km/h" : "未知"));

        // 计算小交路数量（站点数少于最大方向站点数的非快车、非未来方向，按站点集合去重）
        List<Direction> directions = lineInfo.direction != null ? lineInfo.direction : Collections.emptyList();
        int maxStationCount = 0;
        for (Direction dir : directions) {
            if (dir.isFast || dir.isFuture) continue;
            int cnt = dir.station != null ? dir.station.size() : 0;
            if (cnt > maxStationCount) maxStationCount = cnt;
        }
        Set<Set<String>> shortTurnSets = new HashSet<>();
        for (Direction dir : directions) {
            if (dir.isFast || dir.isFuture) continue;
            int cnt = dir.station != null ? dir.station.size() : 0;
            if (cnt < maxStationCount && cnt > 0) {
                Set<String> stationSet = new HashSet<>();
                for (DirectionStation ds : dir.station) {
                    if (ds.name != null) stationSet.add(ds.name);
                }
                shortTurnSets.add(stationSet);
            }
        }
        tvShortTurnCount.setText("小交路数量：" + (shortTurnSets.size() > 0 ? shortTurnSets.size() : "无"));

        setupTagsAndLink(lineInfo, cache, link);

        Boolean hasFuture = cache.getHasFutureMap() != null ? cache.getHasFutureMap().get(selectedLineId) : Boolean.FALSE;

        if (hasFuture)
        {
            Double totalLength = cache.getLineTotalLengthMap().get(selectedLineId);
            tvFutureLength.setText(totalLength != null ? String.format("建设规划总长：%.1f km", totalLength) : "未来线路总长：0.0 km");
            totalLength = cache.getLineTotalLengthMapWithoutFuture().get(selectedLineId);
            tvCurrentLength.setText(totalLength != null ? String.format("当前线路总长：%.1f km", totalLength) : "目前线路总长：0.0 km");
        }
        else
        {
            Double totalLength = cache.getLineTotalLengthMapWithoutFuture().get(selectedLineId);
            tvCurrentLength.setText(totalLength != null ? String.format("线路总长：%.1f km", totalLength) : "线路总长：0.0 km");
            tvFutureLength.setVisibility(View.GONE);
        }
    }


    // ==================== 2. 结果包装类 ====================
    private static class LoadResult {
        List<DisplayItem> displayList;
        String openTimeStr;
        String currentHeadwayStr, maxHeadwayStr, minHeadwayStr;
        String currentCrowdStr, maxCrowdStr, minCrowdStr;
        String powerStr, brakeStr, speedLimitsStr, virtualTransferStr;
    }

    // ==================== 3. 后台复杂计算 ====================
    private LoadResult buildComplexDisplayListInBackground() {
        DataCache cache = DataCache.getInstance();
        MetroData metroData = cache.getMetroData();
        Map<String, Line> linesObj = metroData.lines;
        String keyCity = cache.getKeyCity();
        Line lineInfo = linesObj.get(selectedLineId);
        List<Direction> directions = lineInfo.direction != null ? lineInfo.direction : Collections.emptyList();
        boolean isLoop = Boolean.TRUE.equals(cache.getHasLoopMap().get(selectedLineId));

        // 收集快车站
        fastStations.clear();
        for (Direction dir : directions) {
            if (dir.isFast && dir.station != null) {
                for (DirectionStation ds : dir.station) {
                    if (ds.name != null) fastStations.add(ds.name);
                }
            }
        }

        List<Direction> mainDirs = filterDirections(directions, isLoop);

        LoadResult result = new LoadResult();
        result.displayList = buildDisplayList(mainDirs, mLineColor);
        attachTransferInfo(result.displayList, metroData.stations, linesObj, keyCity);

        // ====== 计算时间、发车间隔、拥挤度、基础参数 ======
        String currentTime = new SimpleDateFormat("HH:mm", Locale.getDefault()).format(new Date());
        String currentDay = new SimpleDateFormat("EEEE", Locale.US).format(new Date());

        // 1. 运营时间
        List<OpenTimeRule> openTimes = cache.getOpenTimes();
        String openTimeStr = "未知";
        String opStart = null, opEnd = null;
        if (openTimes != null) {
            for (OpenTimeRule rule : openTimes) {
                if (rule.lineId == null || rule.lineId.equals(selectedLineId)) {
                    if (rule.days == null || rule.days.contains(currentDay)) {
                        openTimeStr = rule.startTime + " - " + rule.endTime;
                        opStart = rule.startTime;
                        opEnd = rule.endTime;
                        break;
                    }
                }
            }
        }
        result.openTimeStr = "运营时间：" + openTimeStr;

        // 2. 发车间隔
        List<HeadwayRule> headwayRules = cache.getHeadwayRules();
        int currentMinInterval = -1, currentMaxInterval = -1;
        int globalMinInterval = -1, globalMaxInterval = -1;

        if (headwayRules != null) {
            List<HeadwayRule> lineRules = new ArrayList<>();
            for (HeadwayRule rule : headwayRules) {
                if (rule.lineId.equals(selectedLineId)) {
                    if (opStart != null && opEnd != null) {
                        if (rule.startTime.compareTo(opStart) >= 0 && rule.endTime.compareTo(opEnd) <= 0) {
                            lineRules.add(rule);
                        }
                    } else {
                        lineRules.add(rule);
                    }
                }
            }

            if (!lineRules.isEmpty()) {
                globalMinInterval = Collections.min(lineRules, Comparator.comparingInt(r -> r.intervalSeconds)).intervalSeconds;
                globalMaxInterval = Collections.max(lineRules, Comparator.comparingInt(r -> r.intervalSeconds)).intervalSeconds;

                boolean inOperation = false;
                if (opStart != null && opEnd != null) {
                    inOperation = isTimeInRange(currentTime, opStart, opEnd);
                } else {
                    inOperation = true;
                }

                if (inOperation) {
                    for (HeadwayRule rule : lineRules) {
                        if ((rule.days == null || rule.days.contains(currentDay)) &&
                                isTimeInRange(currentTime, rule.startTime, rule.endTime)) {
                            if (currentMinInterval == -1 || rule.intervalSeconds < currentMinInterval)
                                currentMinInterval = rule.intervalSeconds;
                            if (currentMaxInterval == -1 || rule.intervalSeconds > currentMaxInterval)
                                currentMaxInterval = rule.intervalSeconds;
                        }
                    }
                    if (currentMinInterval == -1) {
                        currentMinInterval = globalMinInterval;
                        currentMaxInterval = globalMaxInterval;
                    }
                } else {
                    currentMinInterval = -1;
                    currentMaxInterval = -1;
                }
            }
        }

        result.currentHeadwayStr = currentMinInterval == -1 ? "当前间隔：无数据" : String.format(Locale.getDefault(), "当前间隔：%d秒", currentMinInterval);
        result.maxHeadwayStr = globalMaxInterval == -1 ? "最大间隔：无数据" : String.format(Locale.getDefault(), "最大间隔：%d秒", globalMaxInterval);
        result.minHeadwayStr = globalMinInterval == -1 ? "最小间隔：无数据" : String.format(Locale.getDefault(), "最小间隔：%d秒", globalMinInterval);

        // 3. 拥挤度
        List<CrowdRule> crowdRules = cache.getCrowdRules();
        double currentFactor = -1;
        double globalMaxFactor = -1;
        double globalMinFactor = -1;

        if (crowdRules != null) {
            List<CrowdRule> lineRules = new ArrayList<>();
            for (CrowdRule rule : crowdRules) {
                if (rule.directionKey != null && rule.directionKey.startsWith(selectedLineId + "|")) {
                    if (opStart != null && opEnd != null) {
                        if (rule.startTime.compareTo(opStart) >= 0 && rule.endTime.compareTo(opEnd) <= 0) {
                            lineRules.add(rule);
                        }
                    } else {
                        lineRules.add(rule);
                    }
                }
            }

            if (!lineRules.isEmpty()) {
                globalMaxFactor = Collections.max(lineRules, Comparator.comparingDouble(r -> r.factor)).factor;
                globalMinFactor = Collections.min(lineRules, Comparator.comparingDouble(r -> r.factor)).factor;

                boolean inOperation = false;
                if (opStart != null && opEnd != null) {
                    inOperation = isTimeInRange(currentTime, opStart, opEnd);
                } else {
                    inOperation = true;
                }

                if (inOperation) {
                    double currentMax = -1;
                    for (CrowdRule rule : lineRules) {
                        if ((rule.days == null || rule.days.contains(currentDay)) &&
                                isTimeInRange(currentTime, rule.startTime, rule.endTime)) {
                            if (rule.factor > currentMax) currentMax = rule.factor;
                        }
                    }
                    if (currentMax >= 0) currentFactor = currentMax;
                }
            }
        }

        double displayCurrent = (currentFactor == -1) ? 1.0 : currentFactor;
        double displayMax = (globalMaxFactor == -1) ? 1.0 : globalMaxFactor;
        double displayMin;
        if (globalMinFactor == -1) {
            displayMin = 1.0;
        } else {
            displayMin = Math.max(globalMinFactor, 1.0);
        }
        displayMin = Math.min(displayCurrent, displayMin);

        result.currentCrowdStr = String.format(Locale.getDefault(), "当前拥挤度：%.1f (%s)", displayCurrent, getCrowdLevel(displayCurrent));
        result.maxCrowdStr = String.format(Locale.getDefault(), "最大拥挤度：%.1f (%s)", displayMax, getCrowdLevel(displayMax));
        result.minCrowdStr = String.format(Locale.getDefault(), "最小拥挤度：%.1f (%s)", displayMin, getCrowdLevel(displayMin));

        // 4. 技术参数
        Double power = cache.getLinePowerKwMap().get(selectedLineId);
        Double brake = cache.getLineBrakeMap().get(selectedLineId);
        int powerVal = (power != null && power > 0) ? power.intValue() : 2000;
        double brakeVal = (brake != null && brake > 0) ? brake : 0.85;
        result.powerStr = "运行功率：" + powerVal + " kW";
        result.brakeStr = "制动加速度：" + String.format(Locale.getDefault(), "%.2f m/s²", brakeVal);

        // 5. 限速区间
        List<SpeedLimit> speedLimits = cache.getSpeedLimits();
        StringBuilder limitsSb = new StringBuilder();
        if (speedLimits != null) {
            for (SpeedLimit sl : speedLimits) {
                if (sl.directionKey != null && sl.directionKey.startsWith(selectedLineId + "|")) {
                    limitsSb.append(sl.fromStation).append("→").append(sl.toStation)
                            .append(": ").append(sl.speedKmh).append(" km/h; ");
                }
            }
        }
        result.speedLimitsStr = limitsSb.length() > 0 ? "限速区间：" + limitsSb.toString() : "限速区间：无";

        // 6. 虚拟换乘
        List<VirtualTransfer> virtualTransfers = cache.getVirtualTransfers();
        StringBuilder virtualSb = new StringBuilder();
        if (virtualTransfers != null) {
            Set<String> lineStations = new HashSet<>();
            for (List<String> stations : cache.getDirStations().values()) {
                if (stations != null) lineStations.addAll(stations);
            }
            for (VirtualTransfer vt : virtualTransfers) {
                if (lineStations.contains(vt.stationA) || lineStations.contains(vt.stationB)) {
                    virtualSb.append(vt.stationA).append(" ↔ ").append(vt.stationB)
                            .append(" (").append(vt.timeMinutes).append("分钟)");
                    if (vt.securityCheck) virtualSb.append(" 需安检");
                    virtualSb.append("; ");
                }
            }
        }
        result.virtualTransferStr = virtualSb.length() > 0 ? "虚拟换乘：" + virtualSb.toString() : "虚拟换乘：无";

        return result;
    }

    // ==================== 4. 切回主线程刷新全量 UI ====================
    private void applyResultToUI(LoadResult result) {
        tvOpenTimes.setText(result.openTimeStr);
        if (tvCurrentHeadway != null) {
            tvCurrentHeadway.setText(result.currentHeadwayStr);
            tvMaxHeadway.setText(result.maxHeadwayStr);
            tvMinHeadway.setText(result.minHeadwayStr);
        }
        if (tvCurrentCrowd != null) {
            tvCurrentCrowd.setText(result.currentCrowdStr);
            tvMaxCrowd.setText(result.maxCrowdStr);
            tvMinCrowd.setText(result.minCrowdStr);
        }
        tvPower.setText(result.powerStr);
        tvBrake.setText(result.brakeStr);
        tvSpeedLimits.setText(result.speedLimitsStr);
        tvVirtualTransfer.setText(result.virtualTransferStr);

        StationLineAdapter adapter = new StationLineAdapter(result.displayList, mLineColor,
                new OnStationActionListener() {
                    @Override
                    public void onStationClick(String stationName) {
                        if (lineMapView != null) {
                            lineMapView.highlightStation(stationName);
                        }
                    }

                    @Override
                    public void onStationLongClick(String stationName) {
                        showSetEndpointDialog(stationName);
                    }
                });
        listViewStations.setAdapter(adapter);
    }

    /**
     * 根据拥挤度因子返回等级文字
     */
    private String getCrowdLevel(double factor) {
        if (factor >= 1.7) return "拥挤";
        else if (factor >= 1.5) return "还好";
        else if (factor >= 1.2) return "一般";
        else return "舒适";
    }

    // 辅助方法：判断当前时间是否在区间内
    private boolean isTimeInRange(String current, String start, String end) {
        try {
            SimpleDateFormat sdf = new SimpleDateFormat("HH:mm", Locale.getDefault());
            Date now = sdf.parse(current);
            Date s = sdf.parse(start);
            Date e = sdf.parse(end);
            return now.after(s) && now.before(e);
        } catch (Exception e) {
            return false;
        }
    }

    // ---------- 辅助方法：收集快车站 ----------
    private void collectFastStations(List<Direction> directions) {
        fastStations.clear();
        for (Direction dir : directions) {
            if (dir.isFast) {
                if (dir.station != null) {
                    for (DirectionStation ds : dir.station) {
                        if (ds.name != null) fastStations.add(ds.name);
                    }
                }
            }
        }
    }

    // ---------- 辅助方法：设置标签和链接 ----------
    private void setupTagsAndLink(Line lineInfo, DataCache cache, String link) {
        lineTagsContainer.removeAllViews();
        List<String> tags = new ArrayList<>();

        if (Boolean.TRUE.equals(cache.getHasFutureMap().get(selectedLineId))) tags.add("含规划建设");
        if (Boolean.TRUE.equals(cache.getHasLoopMap().get(selectedLineId))) tags.add("环线");
        if (Boolean.TRUE.equals(cache.getLineSegmentedMap().get(selectedLineId))) tags.add("分段运营");
        if (Boolean.TRUE.equals(cache.getLineHasFastMap().get(selectedLineId))) tags.add("有快车");
        if (Boolean.TRUE.equals(cache.getLineSpecialMap().get(selectedLineId))) tags.add("单独计费");

        // 检测是否有小交路（站点数少于最大方向站点数的非快车、非未来方向）
        List<Direction> dirs = lineInfo.direction;
        if (dirs != null) {
            int maxStn = 0;
            for (Direction dir : dirs) {
                if (dir.isFast || dir.isFuture) continue;
                int c = dir.station != null ? dir.station.size() : 0;
                if (c > maxStn) maxStn = c;
            }
            for (Direction dir : dirs) {
                if (dir.isFast || dir.isFuture) continue;
                int c = dir.station != null ? dir.station.size() : 0;
                if (c < maxStn && c > 0) {
                    tags.add("有交路");
                    break;
                }
            }
        }

        // 为每个标签创建圆角 TextView
        for (String tagText : tags) {
            TextView tagView = createTagView(tagText);
            lineTagsContainer.addView(tagView);
        }

        if (tags.isEmpty()) {
            TextView emptyTag = new TextView(this);
            emptyTag.setText("无");
            emptyTag.setTextSize(14);
            emptyTag.setTextColor(Color.GRAY);
            lineTagsContainer.addView(emptyTag);
        }

        // 设置链接（可点击）
        tvLink.removeAllViews();
        if (!TextUtils.isEmpty(link) && !link.equals("暂无")) {
            TextView linkView = new TextView(this);
            linkView.setText("点我");
            linkView.setTextSize(12);
            linkView.setTextColor(Color.WHITE);
            linkView.setPadding(12, 4, 12, 4);
            GradientDrawable drawable = new GradientDrawable();
            drawable.setShape(GradientDrawable.RECTANGLE);
            drawable.setCornerRadius(8);
            drawable.setColor(Color.parseColor("#2196F3"));
            linkView.setBackground(drawable);
            linkView.setOnClickListener(v -> {
                Intent intent = new Intent(Intent.ACTION_VIEW, Uri.parse(link));
                startActivity(intent);
            });
            tvLink.addView(linkView);
        } else {
            TextView noLinkView = new TextView(this);
            noLinkView.setText("暂无");
            noLinkView.setTextSize(12);
            noLinkView.setTextColor(Color.GRAY);
            noLinkView.setPadding(12, 4, 12, 4);
            GradientDrawable drawable = new GradientDrawable();
            drawable.setShape(GradientDrawable.RECTANGLE);
            drawable.setCornerRadius(8);
            drawable.setColor(Color.parseColor("#E0E0E0"));
            noLinkView.setBackground(drawable);
            tvLink.addView(noLinkView);
        }
    }

    private TextView createTagView(String tagText) {
        TextView tagView = new TextView(this);
        tagView.setText(tagText);
        tagView.setTextSize(12);
        tagView.setTextColor(Color.WHITE);
        tagView.setPadding(12, 4, 12, 4);
        GradientDrawable drawable = new GradientDrawable();
        drawable.setShape(GradientDrawable.RECTANGLE);
        drawable.setCornerRadius(8);
        int bgColor;
        switch (tagText) {
            case "单独计费": bgColor = Color.parseColor("#D32F2F"); break;
            case "分段运营": bgColor = Color.parseColor("#FF9800"); break;
            case "有快车":   bgColor = Color.parseColor("#2196F3"); break;
            case "环线":     bgColor = Color.parseColor("#4CAF50"); break;
            case "有交路":   bgColor = Color.parseColor("#7B1FA2"); break; // 紫色
            default:        bgColor = Color.parseColor("#757575");
        }
        drawable.setColor(bgColor);
        tagView.setBackground(drawable);
        LinearLayout.LayoutParams params = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.WRAP_CONTENT, ViewGroup.LayoutParams.WRAP_CONTENT);
        params.setMarginEnd(8);
        tagView.setLayoutParams(params);
        return tagView;
    }

    // ---------- 辅助方法：过滤方向列表 ----------
    private List<Direction> filterDirections(List<Direction> directions, boolean isLoop) {
        List<Direction> raw = new ArrayList<>();
        for (Direction dir : directions) {
            if (dir.isFast) continue;
            raw.add(dir);
        }
        // 去除重复方向（同序、逆序、站点集合相同、子集关系）
        // 仅在同类别（运营 / 未来）内去重，避免误删跨类别方向
        List<Direction> filtered = new ArrayList<>();
        for (Direction dir : raw) {
            List<String> stationNames = extractStationNames(dir);
            boolean isDuplicate = false;
            for (Direction existing : filtered) {
                if (dir.isFuture != existing.isFuture) continue;
                List<String> existingNames = extractStationNames(existing);
                if (GraphKeyUtils.isDuplicateDirection(stationNames, existingNames, isLoop)) {
                    isDuplicate = true;
                    break;
                }
            }
            if (!isDuplicate) {
                filtered.add(dir);
            }
        }
        return filtered;
    }

    private List<String> extractStationNames(Direction dir) {
        List<String> names = new ArrayList<>();
        if (dir.station != null) {
            for (DirectionStation ds : dir.station) {
                if (ds.name != null) names.add(ds.name);
            }
        }
        return names;
    }

    // ---------- 构建显示列表 ----------
    private List<DisplayItem> buildDisplayList(List<Direction> mainDirs, int lineColor) {
        List<DisplayItem> displayList = new ArrayList<>();

        // 获取站点坐标（用于纬度比较）
        DataCache cache = DataCache.getInstance();
        Map<String, double[]> coords = cache.getStationCoords();

        // ---- 运营中方向 ----
        int mainIndex = 1;
        int mainCount = 0;
        for (Direction dir : mainDirs) {
            if (dir.isFuture) continue;
            mainCount++;
        }
        for (Direction dir : mainDirs) {
            if (dir.station == null || dir.station.isEmpty()) continue;
            boolean isLoop = Boolean.TRUE.equals(dir.isLoop);
            boolean isFuture = dir.isFuture;
            if (isFuture) continue;

            String title = buildTitle(isLoop, mainCount, mainIndex, isFuture);
            String tag = buildLineTag();
            displayList.add(new DisplayItem(title, lineColor));

            // ---- 根据起点/终点纬度判断是否需要反转 ----
            String start = dir.start;
            String end = dir.end;
            boolean reverse = false;
            if (coords != null && coords.containsKey(start) && coords.containsKey(end)) {
                double startLat = coords.get(start)[1];
                double endLat = coords.get(end)[1];
                if (startLat > endLat) {
                    reverse = true;
                }
            }

            List<DirectionStation> orderedStations = new ArrayList<>(dir.station);
            if (reverse) {
                Collections.reverse(orderedStations);
            }

            int index = 1;
            for (DirectionStation ds : orderedStations) {
                String name = ds.name;
                if (name == null) continue;
                displayList.add(new DisplayItem(name, "main", index++, tag));
            }
            mainIndex++;
        }

        // ---- 未来（规划）线路 ----
        int futureIndex = 1;
        int futureCount = mainDirs.size() - mainCount;
        for (Direction dir : mainDirs) {
            if (dir.station == null || dir.station.isEmpty()) continue;
            boolean isLoop = Boolean.TRUE.equals(dir.isLoop);
            boolean isFuture = dir.isFuture;
            if (!isFuture) continue;

            String title = buildTitle(isLoop, futureCount, futureIndex, isFuture);
            String tag = buildLineTag();
            displayList.add(new DisplayItem(title, lineColor));

            // ---- 根据起点/终点纬度判断是否需要反转 ----
            String start = dir.start;
            String end = dir.end;
            boolean reverse = false;
            if (coords != null && coords.containsKey(start) && coords.containsKey(end)) {
                double startLat = coords.get(start)[1];
                double endLat = coords.get(end)[1];
                if (startLat > endLat) {
                    reverse = true;
                }
            }

            List<DirectionStation> orderedStations = new ArrayList<>(dir.station);
            if (reverse) {
                Collections.reverse(orderedStations);
            }

            int index = 1;
            for (DirectionStation ds : orderedStations) {
                String name = ds.name;
                if (name == null) continue;
                displayList.add(new DisplayItem(name, "main", index++, tag));
            }
            futureIndex++;
        }

        return displayList;
    }

    private String buildTitle(boolean isLoop, int total, int index, boolean isFuture) {
        String prefix = isFuture ? "规划建设线路" : "线路";
        if (isLoop) {
            return total > 1 ? prefix + index + "（环线）" : prefix + "站点（环线）";
        } else {
            return total > 1 ? prefix + index : prefix + "站点";
        }
    }

    private String buildLineTag() {
        return selectedLineDisplayName;
    }

    // ---------- 附加换乘信息 ----------
    private void attachTransferInfo(List<DisplayItem> displayList,
                                    Map<String, Station> stationsObj,
                                    Map<String, Line> linesObj,
                                    String keyCity) {
        if (stationsObj == null) return;

        // 为每个站点构建换乘列表
        for (DisplayItem item : displayList) {
            if (item.type != 1) continue;
            String station = item.stationName;
            Station stInfo = stationsObj.get(station);
            if (stInfo == null) continue;
            List<StationLine> lines = stInfo.lines;
            if (lines == null) continue;

            Map<String, TransferLineInfo> unique = new LinkedHashMap<>();
            for (StationLine lineEntry : lines) {
                String lid = lineEntry.id;
                if (lid == null) continue;
                String start = lineEntry.start;
                String end = lineEntry.end;
                String dirKey = GraphKeyUtils.makeDirKey(lid, start, end);

                // 获取线路显示名和颜色
                String display = getLineDisplayName(lid, linesObj, keyCity);
                int color = getLineColor(lid, linesObj);

                String transferStatus = StationUtils.getTransferStatusForStation(station, selectedLineId, lid);
                String key = lid + "|" + display;
                if (!unique.containsKey(key)) {
                    unique.put(key, new TransferLineInfo(lid, display, color, transferStatus));
                }
            }

            List<TransferLineInfo> temp = new ArrayList<>(unique.values());
            temp.sort((a, b) -> TextMatchUtils.compareLineName(a.displayName, b.displayName));

            List<TransferLineInfo> result = new ArrayList<>();
            Set<String> seen = new HashSet<>();
            for (TransferLineInfo info : temp) {
                if (seen.add(info.displayName)) { // add返回true表示之前没出现过
                    result.add(info);
                }
            }
            item.transfers = result;
        }
    }

    private String getLineDisplayName(String lineId, Map<String, Line> linesObj, String keyCity) {
        Line line = linesObj.get(lineId);
        if (line == null) return lineId;
        String city = line.city != null ? line.city : keyCity;
        String name = line.name != null ? line.name : lineId;
        String display = city.equals(keyCity) ? name : city + name;
        if (line.englishName != null && !line.englishName.isEmpty()) {
            display += " (" + line.englishName + ")";
        }
        return display;
    }

    private int getLineColor(String lineId, Map<String, Line> linesObj) {
        Line line = linesObj.get(lineId);
        String colorStr = line.color != null ? line.color : "#999999";
        if (!colorStr.startsWith("#")) colorStr = "#" + colorStr;
        return Color.parseColor(colorStr);
    }

    // ---------- 地图绘制 ----------
    private void setupMap() {
        DataCache cache = DataCache.getInstance();
        MetroData metroData = cache.getMetroData();
        if (metroData == null) return;
        Map<String, double[]> stationCoords = cache.getStationCoords();
        if (stationCoords == null) return;

        Map<String, Line> linesObj = metroData.lines;
        if (linesObj == null) return;
        Line lineInfo = linesObj.get(selectedLineId);
        if (lineInfo == null) return;

        String colorStr = lineInfo.color != null ? lineInfo.color : "#999999";
        if (!colorStr.startsWith("#")) colorStr = "#" + colorStr;
        int lineColor = Color.parseColor(colorStr);

        List<Direction> directions = lineInfo.direction;
        if (directions == null) return;

        boolean isLoop = Boolean.TRUE.equals(cache.getHasLoopMap().get(selectedLineId));
        List<Direction> mainDirs = filterDirections(directions, isLoop);

        List<LineDirection> dirs = new ArrayList<>();
        for (Direction dir : mainDirs) {
            String start = dir.start;
            String end = dir.end;
            boolean isFuture = dir.isFuture;
            List<DirectionStation> stationList = dir.station;
            if (stationList == null) continue;
            List<String> stations = new ArrayList<>();
            for (DirectionStation ds : stationList) {
                String name = ds.name;
                if (name != null) stations.add(name);
            }
            dirs.add(new LineDirection(selectedLineId, start, end, stations, isFuture));
        }

        lineMapView = new LineMapView(this, dirs, stationCoords, lineColor);
        mapContainer.removeAllViews();
        mapContainer.addView(lineMapView);
    }

    // ---------- 设置起点/终点对话框 ----------
    private void showSetEndpointDialog(final String stationName) {
        String status = StationUtils.getStationLineStatus(stationName, selectedLineId);
        if (!"operating".equals(status)) {
            Toast.makeText(this, getStatusMessage(status), Toast.LENGTH_LONG).show();
            return;
        }

        new AlertDialog.Builder(this)
                .setTitle("设置站点")
                .setMessage("将 \"" + stationName + "\" 设为起点还是终点？")
                .setNegativeButton("起点", (dialog, which) -> {
                    startStation = stationName;
                    updateStartEndDisplay();
                })
                .setPositiveButton("终点", (dialog, which) -> {
                    endStation = stationName;
                    updateStartEndDisplay();
                })
                .setNeutralButton("取消", null)
                .show();
    }

    @NonNull
    private String getStatusMessage(String status) {
        return "该站点在" + selectedLineDisplayName + "暂缓开通，不可设置为起终点。";
    }

    // ---------- Activity 结果回调 ----------

    /**
     * 处理从子页面返回的结果。
     *
     * <p>当从线路详情页（自身的嵌套跳转）返回时，获取用户设置的
     * 起终点站点信息并更新显示，同时将结果回传给上级页面。
     *
     * @param requestCode 请求码
     * @param resultCode  结果码
     * @param data        返回的 Intent 数据
     */
    @Override
    protected void onActivityResult(int requestCode, int resultCode, Intent data) {
        super.onActivityResult(requestCode, resultCode, data);
        if (requestCode == REQUEST_LINE_DETAIL && resultCode == RESULT_OK && data != null) {
            String newStart = data.getStringExtra("start_station");
            String newEnd = data.getStringExtra("end_station");
            if (newStart != null) startStation = newStart;
            if (newEnd != null) endStation = newEnd;
            updateStartEndDisplay();

            Intent resultIntent = new Intent();
            if (startStation != null) resultIntent.putExtra("start_station", startStation);
            if (endStation != null) resultIntent.putExtra("end_station", endStation);
            setResult(RESULT_OK, resultIntent);
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

    // ---------- 内部适配器 ----------
    private class StationLineAdapter extends BaseAdapter {
        private final List<DisplayItem> data;
        private final LayoutInflater inflater;
        private final OnStationActionListener listener;
        private final int lineColor;

        StationLineAdapter(List<DisplayItem> data, int lineColor, OnStationActionListener listener) {
            this.data = data;
            this.listener = listener;
            this.lineColor = lineColor;
            this.inflater = LayoutInflater.from(DirectionInfoActivity.this);
        }

        @Override public int getCount() { return data.size(); }
        @Override public Object getItem(int position) { return data.get(position); }
        @Override public long getItemId(int position) { return position; }

        @Override public int getViewTypeCount() { return 2; }
        @Override public int getItemViewType(int position) { return data.get(position).type; }

        @Override
        public View getView(int position, View convertView, ViewGroup parent) {
            DisplayItem item = data.get(position);
            if (item.type == 0) {
                return getHeaderView(convertView, item);
            } else {
                return getStationView(position, convertView, parent, item);
            }
        }

        // 标题行（横线 + 居中文字）
        private View getHeaderView(View convertView, DisplayItem item) {
            if (!(convertView instanceof FrameLayout)) {
                FrameLayout container = new FrameLayout(DirectionInfoActivity.this);
                container.setPadding(16, 16, 16, 16);

                View line = new View(DirectionInfoActivity.this);
                line.setBackgroundColor(Color.parseColor("#CCCCCC"));
                FrameLayout.LayoutParams lineParams = new FrameLayout.LayoutParams(
                        FrameLayout.LayoutParams.MATCH_PARENT, 2);
                lineParams.gravity = Gravity.CENTER_VERTICAL;
                line.setLayoutParams(lineParams);
                container.addView(line);

                LinearLayout horizontalLayout = new LinearLayout(DirectionInfoActivity.this);
                horizontalLayout.setOrientation(LinearLayout.HORIZONTAL);
                horizontalLayout.setGravity(Gravity.CENTER_VERTICAL);
                horizontalLayout.setBackgroundColor(Color.WHITE);
                horizontalLayout.setPadding(16, 8, 16, 8);

                View colorBlock = new View(DirectionInfoActivity.this);
                LinearLayout.LayoutParams blockParams = new LinearLayout.LayoutParams(48, 48);
                blockParams.setMargins(0, 0, 16, 0);
                colorBlock.setLayoutParams(blockParams);
                horizontalLayout.addView(colorBlock);

                TextView textView = new TextView(DirectionInfoActivity.this);
                textView.setTextSize(18);
                textView.setTypeface(null, android.graphics.Typeface.BOLD);
                textView.setTextColor(Color.BLACK);
                textView.setBackgroundColor(Color.WHITE);
                horizontalLayout.addView(textView);

                FrameLayout.LayoutParams layoutParams = new FrameLayout.LayoutParams(
                        FrameLayout.LayoutParams.WRAP_CONTENT, FrameLayout.LayoutParams.WRAP_CONTENT);
                layoutParams.gravity = Gravity.CENTER;
                container.addView(horizontalLayout, layoutParams);

                convertView = container;
                convertView.setTag(new Object[]{colorBlock, textView});
            }
            Object[] tag = (Object[]) convertView.getTag();
            ((View) tag[0]).setBackgroundColor(item.headerColor);
            ((TextView) tag[1]).setText(item.headerTitle);
            return convertView;
        }

        // 站点行
        @SuppressLint("SetTextI18n")
        private View getStationView(int position, View convertView, ViewGroup parent, DisplayItem item) {
            if (convertView == null || convertView instanceof TextView) {
                convertView = inflater.inflate(R.layout.item_station_line, parent, false);
            }

            LinearLayout content = convertView.findViewById(R.id.contentLayout);
            TextView tvIndex = convertView.findViewById(R.id.tvIndex);
            TextView tvStationName = convertView.findViewById(R.id.tvStationName);
            LinearLayout llStatus = convertView.findViewById(R.id.llStatus);
            LinearLayout llTransferLines = convertView.findViewById(R.id.llTransferLines);

            GradientDrawable tvIndexDrawable = new GradientDrawable();
            tvIndexDrawable.setShape(GradientDrawable.RECTANGLE);
            tvIndexDrawable.setCornerRadius(8);
            tvIndex.setText(String.valueOf(item.index));

            tvStationName.setText(item.stationName);
            tvStationName.setTextColor(Color.BLACK);
            llTransferLines.removeAllViews();
            llStatus.removeAllViews();

            content.setBackgroundColor(Color.WHITE);
            tvIndexDrawable.setColor(Color.parseColor("#9C27B0"));
            tvIndex.setBackground(tvIndexDrawable);

            // 快车停靠站
            if (fastStations.contains(item.stationName)) {
                addStatusTag(llStatus, "快车停靠站", Color.parseColor("#FF8800"));
            }

            // 站点状态（未开通等）
            String status = StationUtils.getStationLineStatus(item.stationName, selectedLineId);
            if (!"operating".equals(status)) {
                String displayStatus = mapStatusToChinese(status);
                TextView statusTag = createTagTextView(Color.parseColor("#FF9800"), displayStatus);
                llStatus.addView(statusTag);
            }

            // 换乘线路标签
            List<TransferLineInfo> transfers = item.transfers;
            boolean transferEnable = "operating".equals(status);
            if (transfers != null && !transfers.isEmpty()) {
                Set<String> shownNames = new HashSet<>();   // 记录已显示的 displayName
                for (TransferLineInfo info : transfers) {
                    // === 去重核心：如果 name 已经出现过，直接跳过 ===
                    // 1. 过滤掉与当前线路相同的换乘
                    if (Objects.equals(item.lineTag, info.displayName)) {
                        continue;
                    }
                    // 2. 按 displayName 去重（利用 Set.add 返回值）
                    if (!shownNames.add(info.displayName)) {
                        continue;
                    }

                    TextView tag = createTagTextView(info.color, info.displayName);
                    boolean isClosed = StationUtils.isClosed(info.status);
                    if (isClosed) {
                        tag.setBackgroundColor(Color.LTGRAY);
                        tag.setTextColor(Color.DKGRAY);
                        tag.setText(info.displayName);
                        tag.setPaintFlags(tag.getPaintFlags() | Paint.STRIKE_THRU_TEXT_FLAG);
                        tag.setBackground(createRoundedBackground(Color.LTGRAY));
                    } else if (StationUtils.isOutStation(info.status)) {
                        tag.setText(info.displayName + "(出站)");
                    }

                    final String lineId = info.lineId;
                    tag.setOnClickListener(v -> {
                        Intent intent = new Intent(DirectionInfoActivity.this, DirectionInfoActivity.class);
                        intent.putExtra(EXTRA_LINE_ID, lineId);
                        if (isClosed || !transferEnable) {
                            Toast.makeText(DirectionInfoActivity.this, "该换乘暂缓开通", Toast.LENGTH_SHORT).show();
                            return;
                        }
                        if (startStation != null) intent.putExtra("start_station", startStation);
                        if (endStation != null) intent.putExtra("end_station", endStation);
                        startActivityForResult(intent, REQUEST_LINE_DETAIL);
                    });
                    tag.setClickable(true);
                    tag.setFocusable(true);
                    llTransferLines.addView(tag);
                }
            }

            // 整行点击与长按
            content.setOnClickListener(v -> {
                if (item.type == 1 && listener != null) {
                    listener.onStationClick(item.stationName);
                }
            });
            content.setOnLongClickListener(v -> {
                if (item.type == 1 && listener != null) {
                    listener.onStationLongClick(item.stationName);
                    return true;
                }
                return false;
            });

            // 确保 contentLayout 可点击（默认 true，但显式设置更安全）
            content.setClickable(true);
            content.setFocusable(true);

            return convertView;
        }

        private String mapStatusToChinese(String status) {
            switch (status) {
                case "pending": return "未开通";
                default: return status;
            }
        }

        private void addStatusTag(LinearLayout container, String text, int bgColor) {
            container.addView(createTagTextView(bgColor, text));
        }

        private TextView createTagTextView(int color, String text) {
            TextView tag = new TextView(DirectionInfoActivity.this);
            tag.setText(text);
            tag.setTextColor(GraphicsViewUtils.isColorBright(color) ? Color.BLACK : Color.WHITE);
            tag.setTextSize(12);
            tag.setPadding(16, 4, 16, 4);
            tag.setBackground(createRoundedBackground(color));
            LinearLayout.LayoutParams params = new LinearLayout.LayoutParams(
                    ViewGroup.LayoutParams.WRAP_CONTENT, ViewGroup.LayoutParams.WRAP_CONTENT);
            params.setMarginStart(8);
            tag.setLayoutParams(params);
            return tag;
        }

        private GradientDrawable createRoundedBackground(int color) {
            GradientDrawable drawable = new GradientDrawable();
            drawable.setShape(GradientDrawable.RECTANGLE);
            drawable.setCornerRadius(8);
            drawable.setColor(color);
            return drawable;
        }
    }
}