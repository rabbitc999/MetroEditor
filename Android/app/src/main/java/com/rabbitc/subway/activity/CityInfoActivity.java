package com.rabbitc.subway.activity;

import android.annotation.SuppressLint;
import android.content.Intent;
import android.graphics.Color;
import android.os.Bundle;
import android.text.TextUtils;
import android.view.Gravity;
import android.view.View;
import android.widget.ListView;
import android.widget.TableLayout;
import android.widget.TableRow;
import android.widget.TextView;

import androidx.annotation.NonNull;
import androidx.core.content.ContextCompat;

import com.rabbitc.subway.R;
import com.rabbitc.subway.adapter.LineLegendAdapter;
import com.rabbitc.subway.data.DataCache;
import com.rabbitc.subway.data.LineLegendItem;
import com.rabbitc.subway.data.parse.SpecialLineRule;
import com.rabbitc.subway.data.parse.Surcharge;
import com.rabbitc.subway.data.parse.FareSegment;
import com.rabbitc.subway.data.parse.LinearExtension;
import com.rabbitc.subway.utils.LegendBuilder;
import com.rabbitc.subway.utils.WidgetUtils;
import com.rabbitc.subway.widget.LegendListView;
import com.rabbitc.subway.widget.StartEndBar;

import java.util.ArrayList;
import java.util.List;
import java.util.Locale;
import java.util.Map;

/**
 * 城市信息页面：展示城市基本信息、票价表、线路列表（图例）。
 */
public class CityInfoActivity extends BaseActivity {

    // ---------- 常量 ----------
    private static final int REQUEST_LINE_DETAIL = 100;

    // ---------- 视图 ----------
    private TextView tvCityName, tvVersion, tvUpdateDate, tvTotalMileage, tvFutureMileage;
    private TableLayout priceTable;
    private LegendListView lineListView;

    // ---------- 数据 ----------
    private final List<LineLegendItem> legendItems = new ArrayList<>();
    private LineLegendAdapter legendAdapter;
    private String startStation;
    private String endStation;
    private StartEndBar startEndBar;

    // ---------- 生命周期 ----------

    /**
     * 数据就绪后加载并展示城市综合信息。
     *
     * <p>依次执行以下展示：
     * <ol>
     *   <li><b>基本信息</b>：城市名称、文件版本、更新日期、线路总里程（含规划建设里程）</li>
     *   <li><b>票价表</b>：分段计价规则、线性扩展规则和特殊线路计价规则</li>
     *   <li><b>线路列表</b>：使用图例列表展示所有线路，支持点击跳转到 {@link DirectionInfoActivity}</li>
     * </ol>
     *
     * @param savedInstanceState 保存的实例状态
     */
    @Override
    protected void onDataReady(Bundle savedInstanceState) {
        setContentView(R.layout.activity_city_info);

        initViews();
        restoreIntentState();
        displayCityInfo();
        displayPriceTable();
        displayLineList();
    }

    /**
     * 处理从线路详情页返回的结果。
     *
     * <p>当用户在 {@link DirectionInfoActivity} 中设置了新的起终点后，
     * 回传结果更新当前页面的起终点显示。
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
        tvCityName = findViewById(R.id.cityName);
        tvVersion = findViewById(R.id.cityVersion);
        tvUpdateDate = findViewById(R.id.cityUpdateDate);
        tvTotalMileage = findViewById(R.id.cityTotalMileage);
        priceTable = findViewById(R.id.cityPriceTable);
        lineListView = findViewById(R.id.legendListView);
        tvFutureMileage = findViewById(R.id.cityFutureMileage);
        legendAdapter = new LineLegendAdapter(this, legendItems);
        lineListView.setAdapter(legendAdapter);
        startEndBar = findViewById(R.id.startEndBar);
        startEndBar.setOnStartEndClickListener(this::finish);
    }

    private void restoreIntentState() {
        startStation = getIntent().getStringExtra("start_station");
        endStation = getIntent().getStringExtra("end_station");
        updateStartEndDisplay();
    }

    // ---------- 显示城市基本信息 ----------
    @SuppressLint("SetTextI18n")
    private void displayCityInfo() {
        DataCache cache = DataCache.getInstance();
        String city = cache.getKeyCity();
        String version = cache.getVersion();
        String updateDate = cache.getUpdateDate();

        // 计算总里程
        Map<String, Double> lengthFutureMap = cache.getLineTotalLengthMap();
        Map<String, Double> lengthCurrentMap = cache.getLineTotalLengthMapWithoutFuture();

        double totalFuture = 0, totalCurrent = 0;
        if (lengthFutureMap != null) {
            for (double len : lengthFutureMap.values()) totalFuture += len;
            for (double len : lengthCurrentMap.values()) totalCurrent += len;
        }

        boolean hasFuture = (Math.abs(totalFuture - totalCurrent) > 1e-3);

        String mileageFutureStr = String.format(Locale.CHINA, "%.1f km", totalFuture);
        String mileageStr = String.format(Locale.CHINA, "%.1f km", totalCurrent);

        tvCityName.setText("城市：" + (TextUtils.isEmpty(city) ? "未知" : city));
        tvVersion.setText("文件版本：" + (TextUtils.isEmpty(version) ? "未知" : version));
        tvUpdateDate.setText("更新日期：" + (TextUtils.isEmpty(updateDate) ? "未知" : updateDate));

        if (hasFuture)
        {
            tvFutureMileage.setText("建设规划里程：" + mileageFutureStr);
            tvTotalMileage.setText("当前线路里程：" + mileageStr);
        }
        else
        {
            tvFutureMileage.setVisibility(View.GONE);
            tvTotalMileage.setText("总里程：" + mileageStr);
        }
    }

    // ---------- 显示票价表 ----------
    @SuppressLint("DefaultLocale")
    private void displayPriceTable() {
        if (priceTable == null) return;
        priceTable.removeAllViews();

        DataCache cache = DataCache.getInstance();

        TableRow titleNormalRow = new TableRow(this);
        TextView titleNormal = new TextView(this);
        titleNormal.setText("⭐ 分段计价");
        titleNormal.setTextSize(16);
        titleNormal.setTextColor(ContextCompat.getColor(this, R.color.blue));
        titleNormal.setTypeface(android.graphics.Typeface.DEFAULT_BOLD);
        titleNormal.setPadding(0, 16, 0, 8);
        titleNormalRow.addView(titleNormal);

        // ========== 表头 ==========
        TableRow headerRow = new TableRow(this);
        headerRow.setBackgroundColor(ContextCompat.getColor(this, R.color.blue));
        headerRow.setPadding(0, 0, 0, 0);

        TextView tv1 = createTableTextView("里程区间(km)", Color.WHITE);
        tv1.setPadding(16, 10, 8, 10);
        tv1.setGravity(Gravity.START);
        headerRow.addView(tv1);

        TextView tv2 = createTableTextView("票价(元)", Color.WHITE);
        tv2.setPadding(8, 10, 16, 10);
        tv2.setGravity(Gravity.END);
        headerRow.addView(tv2);

        priceTable.addView(titleNormalRow);
        priceTable.addView(headerRow);

        // ========== 分段规则 ==========
        List<FareSegment> segments = cache.getFareSegments();
        boolean hasData = false;
        int rowIndex = 0;

        if (segments != null && !segments.isEmpty()) {
            for (FareSegment fs : segments) {
                String range = String.format("%.0f ~ %.0f", fs.rangeStart, fs.rangeEnd);
                String fare = String.format("%.1f 元", fs.fare);
                addStyledRow(range, fare, rowIndex++ % 2 == 0);
                hasData = true;
            }
        }

        // ========== 线性扩展 ==========
        LinearExtension le = cache.getLinearExtension();
        if (le != null && le.threshold > 0) {
            String range = "> " + (int) le.threshold + " km";
            String fare = String.format("%.1f + %.2f×(km-%.0f)", le.baseFare, le.ratePerKm, le.threshold);
            addStyledRow(range, fare, rowIndex++ % 2 == 0);
            hasData = true;
        }

        if (!hasData) {
            addStyledRow("暂无分段规则", "—", true);
        }

        // ========== 特殊线路 ==========
        List<SpecialLineRule> specials = cache.getSpecialLineRules();
        if (specials != null && !specials.isEmpty()) {

            // 特殊线路标题（占两列）
            TableRow titleRow = new TableRow(this);
            TextView titleTv = new TextView(this);
            titleTv.setText("⭐ 特殊线路");
            titleTv.setTextSize(16);
            titleTv.setTextColor(ContextCompat.getColor(this, R.color.blue));
            titleTv.setTypeface(android.graphics.Typeface.DEFAULT_BOLD);
            titleTv.setPadding(0, 16, 0, 8);
            titleRow.addView(titleTv);
            priceTable.addView(titleRow);

            // 特殊线路表头
            TableRow specialHeader = new TableRow(this);
            specialHeader.setBackgroundColor(ContextCompat.getColor(this, R.color.blue));
            specialHeader.setPadding(0, 0, 0, 0);

            TextView sh1 = createTableTextView("线路名称", Color.WHITE);
            sh1.setPadding(16, 10, 8, 10);
            sh1.setGravity(Gravity.START);
            specialHeader.addView(sh1);

            TextView sh2 = createTableTextView("计价规则", Color.WHITE);
            sh2.setPadding(8, 10, 16, 10);
            sh2.setGravity(Gravity.END);
            specialHeader.addView(sh2);

            priceTable.addView(specialHeader);

            Map<String, String> lineNameMap = cache.getLineNameMap();
            int specialIndex = 0;
            for (SpecialLineRule r : specials) {
                String displayName = lineNameMap != null ? lineNameMap.get(r.lineId) : r.lineId;
                if (TextUtils.isEmpty(displayName)) displayName = r.lineId;
                String fareInfo = getFareInfo(r);
                addStyledRow(displayName, fareInfo, specialIndex++ % 2 == 0);
            }
        }
    }

    // ========== 新增辅助方法 ==========
    private void addStyledRow(String col1, String col2, boolean isEven) {
        TableRow row = new TableRow(this);
        int bgColor = isEven ? Color.parseColor("#F5F8FC") : Color.WHITE;
        row.setBackgroundColor(bgColor);
        row.setPadding(0, 0, 0, 0);

        TextView tv1 = new TextView(this);
        tv1.setText(col1);
        tv1.setTextColor(Color.parseColor("#333333"));
        tv1.setTextSize(14);
        tv1.setPadding(16, 10, 8, 10);
        tv1.setGravity(Gravity.START);
        row.addView(tv1);

        TextView tv2 = new TextView(this);
        tv2.setText(col2);
        tv2.setTextColor(Color.parseColor("#1A73E8"));
        tv2.setTextSize(14);
        tv2.setPadding(8, 10, 16, 10);
        tv2.setGravity(Gravity.END);
        // 价格加粗
        if (col2.contains("元") || col2.contains("+")) {
            tv2.setTypeface(android.graphics.Typeface.DEFAULT_BOLD);
            tv2.setTextColor(Color.parseColor("#D32F2F"));
        }
        row.addView(tv2);

        priceTable.addView(row);
    }

    @NonNull
    private TextView createTableTextView(String text, int textColor) {
        TextView tv = new TextView(this);
        tv.setText(text);
        tv.setPadding(8, 8, 8, 8);
        tv.setGravity(Gravity.CENTER);
        tv.setTextColor(textColor);
        return tv;
    }

    @NonNull
    private static String getFareInfo(SpecialLineRule r) {
        if (r.all != null) {
            return "全程 " + r.all + "元";
        } else if (r.surcharge != null && !r.surcharge.isEmpty()) {
            double max = 0;
            for (Surcharge sur : r.surcharge) {
                if (sur.price > max) max = sur.price;
            }
            return "基础+附加费(最高" + max + "元)";
        } else {
            return "未知";
        }
    }

    // ---------- 显示线路列表（图例） ----------
    private void displayLineList() {
        legendItems.clear();
        legendItems.addAll(LegendBuilder.buildLegendItems(DataCache.getInstance(), true));
        legendAdapter.notifyDataSetChanged();

        // 点击跳转线路详情
        lineListView.setItems(legendItems);
        lineListView.setOnItemSelectedListener(this::openStationLineDetailActivity);
        lineListView.setOnItemLongClickListener(this::openStationLineDetailActivity);

        // 关键：强制重新测量 ListView 高度，使其完全展开
        WidgetUtils.setListViewHeightBasedOnChildren(lineListView);
    }

    private void openStationLineDetailActivity(LineLegendItem item) {
        Intent intent = new Intent(this, DirectionInfoActivity.class);
        intent.putExtra(DirectionInfoActivity.EXTRA_LINE_ID, item.lineId);
        String start = startEndBar.getStartStation();
        String end = startEndBar.getEndStation();
        if (!TextUtils.isEmpty(start)) intent.putExtra("start_station", start);
        if (!TextUtils.isEmpty(end)) intent.putExtra("end_station", end);
        startActivityForResult(intent, REQUEST_LINE_DETAIL);
    }

    // ---------- 更新起终点显示 ----------
    @SuppressLint("SetTextI18n")
    private void updateStartEndDisplay() {
        startEndBar.setStartStation(startStation);
        startEndBar.setEndStation(endStation);
    }
}