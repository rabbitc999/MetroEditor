package com.rabbitc.subway.activity;

import android.annotation.SuppressLint;
import android.graphics.Color;
import android.graphics.drawable.GradientDrawable;
import android.content.Intent;
import android.os.Bundle;
import android.text.TextUtils;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;
import android.widget.AutoCompleteTextView;
import android.widget.ArrayAdapter;
import android.widget.Button;
import android.widget.ImageView;
import android.widget.ListView;
import android.widget.LinearLayout;
import android.widget.TextView;
import android.widget.Toast;

import com.rabbitc.subway.R;
import com.rabbitc.subway.adapter.FuzzyStationAdapter;
import com.rabbitc.subway.data.DataCache;
import com.rabbitc.subway.data.DataLoader;
import com.rabbitc.subway.data.CheckInRecord;
import com.rabbitc.subway.data.FavoriteRoute;
import com.rabbitc.subway.data.json.ElevatorInfo;
import com.rabbitc.subway.data.json.StationFacilities;
import com.rabbitc.subway.data.parse.VirtualTransfer;
import com.rabbitc.subway.data.json.Line;
import com.rabbitc.subway.data.json.MetroData;
import com.rabbitc.subway.utils.CheckInManager;
import com.rabbitc.subway.utils.FavoriteRouteManager;
import com.rabbitc.subway.utils.GraphicsViewUtils;
import com.rabbitc.subway.utils.StationUtils;
import com.rabbitc.subway.utils.TextMatchUtils;

import java.util.ArrayList;
import java.util.HashSet;
import java.util.List;
import java.util.Map;
import java.util.Set;

/**
 * 站点详情页，提供站点信息查询功能。
 *
 * <p>用户可通过输入框输入站点名称（支持模糊匹配），查询该站点的基本信息，包括：
 * 经纬度坐标、所属线路、换乘线路、站点运营状态、虚拟换乘信息和无障碍设施等。
 * 查询结果以标签形式展示各线路的颜色和名称，支持去重显示换乘线路对。
 *
 * <p>本页面继承自 {@link BaseActivity}，通过 {@link #onDataReady(Bundle)}
 * 回调确保在数据缓存有效后执行初始化。
 *
 * @see BaseActivity
 * @see DataCache
 * @see FuzzyStationAdapter
 */
public class StationInfoActivity extends BaseActivity {

    private AutoCompleteTextView stationInput;
    private Button queryBtn, backBtn;
    private TextView stationName, stationCoord, stationStatus, stationMode, emptyHint;
    private TextView tvVirtualTransfer, tvFacilities, tvFavoriteInfo, tvCheckInTitle;
    private LinearLayout stationCheckInContainer;
    private ListView stationFavoriteList;
    private ViewGroup stationLinesContainer, stationTransfersContainer;
    private LinearLayout infoContainer;

    private List<String> allStations;
    private String currentMatchedStation;

    /**
     * 数据就绪后的初始化回调。
     *
     * <p>加载数据、初始化视图和监听器，设置自动完成适配器，
     * 并显示初始空状态提示界面。
     *
     * @param savedInstanceState 保存的实例状态
     */
    @Override
    protected void onDataReady(Bundle savedInstanceState) {
        setContentView(R.layout.activity_station_info);

        boolean loaded = DataLoader.loadData(this);
        if (!loaded) {
            Toast.makeText(this, "数据加载失败", Toast.LENGTH_LONG).show();
            finish();
            return;
        }

        allStations = StationUtils.getStationList();
        initViews();
        setupListeners();
        setupAutoComplete();
        showEmptyHint();

        displayStationInfo("");
        emptyHint.setVisibility(View.VISIBLE);
    }

    private void initViews() {
        stationInput = findViewById(R.id.stationInput);
        queryBtn = findViewById(R.id.queryBtn);
        backBtn = findViewById(R.id.backToMainBtn);
        stationName = findViewById(R.id.stationName);
        stationCoord = findViewById(R.id.stationCoord);
        stationLinesContainer = findViewById(R.id.stationLinesContainer);
        stationTransfersContainer = findViewById(R.id.stationTransfersContainer);
        stationStatus = findViewById(R.id.stationStatus);
        stationMode = findViewById(R.id.stationMode);
        emptyHint = findViewById(R.id.emptyHint);
        infoContainer = findViewById(R.id.infoContainer);
        tvVirtualTransfer = findViewById(R.id.tvVirtualTransfer);
        tvFacilities = findViewById(R.id.tvFacilities);
        tvFavoriteInfo = findViewById(R.id.tvFavoriteInfo);
        tvCheckInTitle = findViewById(R.id.tvCheckInTitle);
        stationCheckInContainer = findViewById(R.id.stationCheckInContainer);
        stationFavoriteList = findViewById(R.id.stationFavoriteList);
    }

    private void setupListeners() {
        backBtn.setOnClickListener(v -> finish());
        queryBtn.setOnClickListener(v -> performQuery());
    }

    private void setupAutoComplete() {
        FuzzyStationAdapter adapter = new FuzzyStationAdapter(this, allStations);
        stationInput.setAdapter(adapter);
        stationInput.setThreshold(1);
    }

    @SuppressLint("SetTextI18n")
    private void performQuery() {
        String input = stationInput.getText().toString().trim();
        if (TextUtils.isEmpty(input)) {
            Toast.makeText(this, "请输入站点名", Toast.LENGTH_SHORT).show();
            return;
        }

        GraphicsViewUtils.hideKeyboard(this, getCurrentFocus());

        allStations = StationUtils.getActiveStationList();
        String matched = TextMatchUtils.findBestMatch(input, allStations);
        if (matched == null) {
            Toast.makeText(this, "未找到匹配站点", Toast.LENGTH_SHORT).show();
            showEmptyHint();
            return;
        }

        if (!matched.equals(input)) {
            stationInput.setText(matched);
            Toast.makeText(this, "已自动匹配：" + matched, Toast.LENGTH_SHORT).show();
        }

        currentMatchedStation = matched;
        displayStationInfo(matched);
    }

    @SuppressLint("SetTextI18n")
    private void displayStationInfo(String station) {
        DataCache cache = DataCache.getInstance();

        // ========== 核心修改：用 MetroData 替换 parsedMap ==========
        MetroData metroData = cache.getMetroData();
        Map<String, Line> linesObj = (metroData != null) ? metroData.lines : null;
        Map<String, StationFacilities> facilitiesObj = (metroData != null) ? metroData.facilities : null;
        // ===========================================================

        // 坐标
        Map<String, double[]> coords = cache.getStationCoords();
        if (coords != null && coords.containsKey(station)) {
            double[] lonLat = coords.get(station);
            stationCoord.setText(String.format("经度：%.6f  纬度：%.6f", lonLat[0], lonLat[1]));
        } else {
            stationCoord.setText("经度：无  纬度：无");
        }

        // -------- 所属线路（使用标签） --------
        stationLinesContainer.removeAllViews();
        Map<String, Map<String, String>> stationLineStatusMap = cache.getStationLineStatusMap();
        if (stationLineStatusMap != null && stationLineStatusMap.containsKey(station)) {
            Map<String, String> lineStatusMap = stationLineStatusMap.get(station);
            for (Map.Entry<String, String> entry : lineStatusMap.entrySet()) {
                String lineId = entry.getKey();
                String status = entry.getValue();
                String lineName = getLineDisplayName(lineId, linesObj, cache.getKeyCity());
                int color = getLineColor(lineId, linesObj);
                TextView tag = createLineTag(color, lineName);
                if (!"operating".equals(status)) {
                    tag.setText(lineName + "(" + translateStatus(status) + ")");
                    tag.setBackground(createRoundedBackground(Color.GRAY));
                    tag.setTextColor(Color.WHITE);
                }
                stationLinesContainer.addView(tag);
            }
        }
        if (stationLinesContainer.getChildCount() == 0) {
            TextView noTag = new TextView(this);
            noTag.setText("无");
            noTag.setTextColor(Color.GRAY);
            stationLinesContainer.addView(noTag);
        }

        // -------- 换乘线路（独立卡片，并去重） --------
        stationTransfersContainer.removeAllViews();
        Map<String, List<String>> dirStations = cache.getDirStations();

        Set<String> stationLines = new HashSet<>(); // 当前站点所属的所有线路 ID

        if (dirStations != null) {
            for (Map.Entry<String, List<String>> entry : dirStations.entrySet()) {
                String dirKey = entry.getKey();
                List<String> stations = entry.getValue();
                if (stations != null && stations.contains(station)) {
                    // 从 dirKey 解析线路 ID（格式：lineId|start|end）
                    String lineId = dirKey.split("\\|")[0];
                    stationLines.add(lineId);
                }
            }
        }

        // 如果当前站点没有出现在任何方向中（极少情况），则回退使用 stationLineStatusMap 中的线路
        if (stationLines.isEmpty() && stationLineStatusMap != null && stationLineStatusMap.containsKey(station)) {
            stationLines.addAll(stationLineStatusMap.get(station).keySet());
        }

        // 生成所有线路对（无序，去重）
        Set<String> transferPairs = new HashSet<>();
        List<String> lineList = new ArrayList<>(stationLines);
        for (int i = 0; i < lineList.size(); i++) {
            for (int j = i + 1; j < lineList.size(); j++) {
                String lineA = lineList.get(i);
                String lineB = lineList.get(j);
                // 排序保证一致性
                String pairKey = lineA.compareTo(lineB) < 0 ? lineA + "|" + lineB : lineB + "|" + lineA;
                transferPairs.add(pairKey);
            }
        }

        // 显示每对换乘
        for (String pairKey : transferPairs) {
            String[] parts = pairKey.split("\\|");
            String lineA = parts[0];
            String lineB = parts[1];

            String nameA = getLineDisplayName(lineA, linesObj, cache.getKeyCity());
            String nameB = getLineDisplayName(lineB, linesObj, cache.getKeyCity());
            int colorA = getLineColor(lineA, linesObj);
            int colorB = getLineColor(lineB, linesObj);

            // 判断换乘状态：如果某条线路在此站状态非 operating，则整体标记为暂缓
            String statusA = "operating";
            String statusB = "operating";
            if (stationLineStatusMap != null && stationLineStatusMap.containsKey(station)) {
                Map<String, String> lineStatus = stationLineStatusMap.get(station);
                statusA = lineStatus.getOrDefault(lineA, "operating");
                statusB = lineStatus.getOrDefault(lineB, "operating");
            }
            boolean isPending = !"operating".equals(statusA) || !"operating".equals(statusB);

            // 创建水平容器
            LinearLayout pairLayout = new LinearLayout(this);
            pairLayout.setOrientation(LinearLayout.HORIZONTAL);
            pairLayout.setGravity(android.view.Gravity.CENTER_VERTICAL);
            pairLayout.setPadding(0, 4, 0, 4);

            TextView tagA = createLineTag(colorA, nameA);
            if (isPending) {
                tagA.setText(nameA + "(暂缓)");
                tagA.setBackground(createRoundedBackground(Color.GRAY));
                tagA.setTextColor(Color.WHITE);
            }
            pairLayout.addView(tagA);

            TextView arrow = new TextView(this);
            arrow.setText("↔");
            arrow.setTextSize(14);
            arrow.setTextColor(Color.GRAY);
            arrow.setPadding(4, 0, 4, 0);
            pairLayout.addView(arrow);

            TextView tagB = createLineTag(colorB, nameB);
            if (isPending) {
                tagB.setText(nameB + "(暂缓)");
                tagB.setBackground(createRoundedBackground(Color.GRAY));
                tagB.setTextColor(Color.WHITE);
            }
            pairLayout.addView(tagB);

            ViewGroup.MarginLayoutParams lp = new ViewGroup.MarginLayoutParams(
                    ViewGroup.LayoutParams.WRAP_CONTENT, ViewGroup.LayoutParams.WRAP_CONTENT);
            int marginRight = (int) (getResources().getDisplayMetrics().density * 8);
            lp.setMargins(0, 0, marginRight, 0);
            pairLayout.setLayoutParams(lp);

            stationTransfersContainer.addView(pairLayout);
        }

        if (stationTransfersContainer.getChildCount() == 0) {
            TextView noTag = new TextView(this);
            noTag.setText("无");
            noTag.setTextColor(Color.GRAY);
            stationTransfersContainer.addView(noTag);
        }

        // 站点状态
        if (stationLineStatusMap != null && stationLineStatusMap.containsKey(station)) {
            Map<String, String> statusMap = stationLineStatusMap.get(station);
            long operatingCount = statusMap.values().stream().filter("operating"::equals).count();
            long total = statusMap.size();

            if (total == 0 || operatingCount == total) {
                stationStatus.setText("站点状态：正常运营");
            } else if (operatingCount == 0) {
                stationStatus.setText("站点状态：暂缓开通");
            } else {
                stationStatus.setText("站点状态：部分线路暂缓开通");
            }
        } else {
            stationStatus.setText("站点状态：无");
        }

        // ========== 虚拟换乘 ==========
        tvVirtualTransfer.setText("虚拟换乘：无");
        List<VirtualTransfer> virtualTransfers = cache.getVirtualTransfers();
        if (virtualTransfers != null) {
            StringBuilder vtBuilder = new StringBuilder();
            for (VirtualTransfer vt : virtualTransfers) {
                if (station.equals(vt.stationA)) {
                    vtBuilder.append("出站换乘至 ").append(vt.stationB)
                            .append("，步行 ").append(vt.timeMinutes).append(" 分钟")
                            .append(vt.securityCheck ? "（需安检）" : "").append("；");
                } else if (station.equals(vt.stationB)) {
                    vtBuilder.append("出站换乘自 ").append(vt.stationA)
                            .append("，步行 ").append(vt.timeMinutes).append(" 分钟")
                            .append(vt.securityCheck ? "（需安检）" : "").append("；");
                }
            }
            if (vtBuilder.length() > 0) {
                tvVirtualTransfer.setText("虚拟换乘：" + vtBuilder.toString());
            }
        }

        // ========== 无障碍设施 ==========
        // 普通卫生间默认存在（根据需求）
        StringBuilder facBuilder = new StringBuilder();
        if (facilitiesObj != null && facilitiesObj.containsKey(station)) {
            StationFacilities fac = facilitiesObj.get(station);

            // 检查 toilet 是否存在且 accessible == false
            boolean hasToilet = fac.toilet != null;
            boolean toiletAccessible = true;
            if (hasToilet) {
                toiletAccessible = fac.toilet.accessible;
            }

            if (hasToilet && !toiletAccessible) {
                // 卫生间在站外，只显示卫生间（站外），不显示其他设施
                facBuilder.append("卫生间（站外）");
            } else {
                // 普通卫生间
                if (hasToilet) {
                    facBuilder.append("卫生间（普通）");
                } else {
                    facBuilder.append("卫生间");
                }
                // 无障碍卫生间
                if (fac.accessibleToilet != null) {
                    facBuilder.append("、无障碍卫生间");
                }
                // 电梯：根据 accessible 判断
                if (fac.elevators != null && !fac.elevators.isEmpty()) {
                    boolean hasAccessibleElevator = false;
                    for (ElevatorInfo elevator : fac.elevators) {
                        if (elevator.accessible) {
                            hasAccessibleElevator = true;
                            break;
                        }
                    }
                    if (hasAccessibleElevator) {
                        facBuilder.append("、无障碍电梯");
                    } else {
                        facBuilder.append("、电梯");
                    }
                }
                // 宽闸机
                if (fac.wideGate != null) {
                    facBuilder.append("、宽闸机");
                }
                // 扶梯
                if (fac.escalators != null && !fac.escalators.isEmpty()) {
                    facBuilder.append("、扶梯");
                }
                // 盲道
                if (fac.tactilePaving != null) {
                    facBuilder.append("、盲道");
                }
            }
        } else {
            // 无设施数据，仅显示默认卫生间
            facBuilder.append("卫生间");
        }
        if (facBuilder.length() > 0) {
            tvFacilities.setText("无障碍设施：" + facBuilder.toString());
        } else {
            tvFacilities.setText("无障碍设施：无");
        }

        // ========== 收藏信息 ==========
        FavoriteRouteManager favoriteRouteManager = new FavoriteRouteManager(this);
        List<FavoriteRoute> favoriteRoutes = favoriteRouteManager.loadRoutes();
        int asStart = 0;
        int asEnd = 0;
        StringBuilder favLabels = new StringBuilder();
        String currentCity = cache.getKeyCity();
        for (FavoriteRoute route : favoriteRoutes) {
            if (!currentCity.equals(route.keyCity)) continue;
            if (station.equals(route.start)) {
                asStart++;
                if (favLabels.length() > 0) favLabels.append("；");
                favLabels.append(route.start).append(" → ").append(route.end);
            }
            if (station.equals(route.end)) {
                asEnd++;
                if (favLabels.length() > 0) favLabels.append("；");
                favLabels.append(route.start).append(" → ").append(route.end);
            }
        }
        if (asStart > 0 || asEnd > 0) {
            tvFavoriteInfo.setText("收藏信息：作为起点 " + asStart + " 次，作为终点 " + asEnd + " 次"
                    + (favLabels.length() > 0 ? "（" + favLabels + "）" : ""));
        } else {
            tvFavoriteInfo.setText("收藏信息：无");
        }

        // ========== 打卡信息 ==========
        CheckInManager checkInManager = new CheckInManager(this);
        List<CheckInRecord> allCheckIns = checkInManager.loadRecords();
        List<CheckInRecord> stationCheckIns = new ArrayList<>();
        for (CheckInRecord record : allCheckIns) {
            String recordCity = TextUtils.isEmpty(record.city) ? currentCity : record.city;
            if (recordCity.equals(currentCity) && station.equals(record.station)) {
                stationCheckIns.add(record);
            }
        }
        stationCheckInContainer.removeAllViews();
        if (stationCheckIns.isEmpty()) {
            tvCheckInTitle.setText("打卡信息：无");
        } else {
            tvCheckInTitle.setText("打卡信息：" + stationCheckIns.size() + " 条");
            LayoutInflater inflater = LayoutInflater.from(this);
            for (CheckInRecord record : stationCheckIns) {
                View item = inflater.inflate(R.layout.item_checkin_record, stationCheckInContainer, false);

                TextView tvCity = item.findViewById(R.id.tvCheckInCity);
                TextView tvName = item.findViewById(R.id.tvStationName);
                TextView tvTime = item.findViewById(R.id.tvCheckInTime);
                TextView tvDate = item.findViewById(R.id.tvCheckInDate);
                TextView tvNote = item.findViewById(R.id.tvCheckInNote);
                ImageView ivImage = item.findViewById(R.id.ivCheckInImage);
                Button btnEdit = item.findViewById(R.id.btnEdit);
                Button btnDelete = item.findViewById(R.id.btnDelete);

                String city = TextUtils.isEmpty(record.city) ? currentCity : record.city;
                tvCity.setText("🏙️ " + city);
                tvName.setText(record.station);
                tvTime.setText(new java.text.SimpleDateFormat("yyyy-MM-dd HH:mm:ss", java.util.Locale.getDefault())
                        .format(new java.util.Date(record.timestamp)));

                if (!TextUtils.isEmpty(record.date)) {
                    tvDate.setVisibility(View.VISIBLE);
                    tvDate.setText("📅 " + record.date);
                } else {
                    tvDate.setVisibility(View.GONE);
                }
                if (!TextUtils.isEmpty(record.note)) {
                    tvNote.setVisibility(View.VISIBLE);
                    String shortNote = record.note.length() > 15 ? record.note.substring(0, 15) + "..." : record.note;
                    tvNote.setText("📝 " + shortNote);
                } else {
                    tvNote.setVisibility(View.GONE);
                }

                if (record.images != null && !record.images.isEmpty()) {
                    android.graphics.Bitmap bitmap = com.rabbitc.subway.utils.ImageUtils.decodeBase64(record.images.get(0));
                    if (bitmap != null) {
                        ivImage.setImageBitmap(bitmap);
                        ivImage.setVisibility(View.VISIBLE);
                    }
                } else {
                    ivImage.setVisibility(View.GONE);
                }

                btnEdit.setVisibility(View.GONE);
                btnDelete.setVisibility(View.GONE);
                stationCheckInContainer.addView(item);
            }
        }

        // 站点模式（暂未实现，隐藏）
        stationMode.setVisibility(View.GONE);

        // 站点名称
        if (station.isEmpty())
        {
            stationName.setText("请输入站点名称");
        }
        else
        {
            stationName.setText(station);
        }

        emptyHint.setVisibility(View.GONE);
        infoContainer.setVisibility(View.VISIBLE);
    }

    // ---------- 辅助方法 ----------
    // ========== 参数类型修改为 Map<String, Line> ==========
    private String getLineDisplayName(String lineId, Map<String, Line> linesObj, String keyCity) {
        if (linesObj == null) return lineId;
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

    // ========== 参数类型修改为 Map<String, Line> ==========
    private int getLineColor(String lineId, Map<String, Line> linesObj) {
        if (linesObj == null) return Color.GRAY;
        Line line = linesObj.get(lineId);
        if (line == null) return Color.GRAY;
        String colorStr = line.color != null ? line.color : "#999999";
        if (!colorStr.startsWith("#")) colorStr = "#" + colorStr;
        try {
            return Color.parseColor(colorStr);
        } catch (Exception e) {
            return Color.GRAY;
        }
    }

    private String translateStatus(String status) {
        if (status == null) return "未知";
        switch (status) {
            case "operating": return "运营中";
            case "pending": return "暂缓";
            case "out_station": return "出站换乘";
            default: return status;
        }
    }

    private TextView createLineTag(int color, String text) {
        TextView tag = new TextView(this);
        tag.setText(text);
        tag.setTextColor(isColorBright(color) ? Color.BLACK : Color.WHITE);
        tag.setTextSize(12);
        tag.setPadding(16, 4, 16, 4);
        tag.setBackground(createRoundedBackground(color));
        LinearLayout.LayoutParams params = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.WRAP_CONTENT, ViewGroup.LayoutParams.WRAP_CONTENT);
        params.setMarginEnd(8);
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

    private boolean isColorBright(int color) {
        double brightness = (0.299 * Color.red(color) + 0.587 * Color.green(color) + 0.114 * Color.blue(color)) / 255;
        return brightness > 0.5;
    }

    private void showEmptyHint() {
        stationName.setText("站点名称");
        stationCoord.setText("经度：无  纬度：无");
        stationLinesContainer.removeAllViews();
        stationTransfersContainer.removeAllViews();
        stationStatus.setText("站点状态：无");
        tvVirtualTransfer.setText("虚拟换乘：无");
        tvFacilities.setText("无障碍设施：无");
        tvFavoriteInfo.setText("收藏信息：无");
        tvCheckInTitle.setText("打卡信息：无");
        stationCheckInContainer.removeAllViews();
        stationMode.setVisibility(View.GONE);
        emptyHint.setVisibility(View.VISIBLE);
        infoContainer.setVisibility(View.VISIBLE);
    }
}