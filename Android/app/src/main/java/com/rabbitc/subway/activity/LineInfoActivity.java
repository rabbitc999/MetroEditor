package com.rabbitc.subway.activity;

import android.annotation.SuppressLint;
import android.content.Intent;
import android.os.Bundle;
import android.os.Handler;
import android.util.Log;
import android.widget.Button;
import android.widget.ListView;
import android.widget.TextView;
import android.widget.Toast;

import com.rabbitc.subway.R;
import com.rabbitc.subway.adapter.LineLegendAdapter;
import com.rabbitc.subway.data.DataCache;
import com.rabbitc.subway.data.LineLegendItem;
import com.rabbitc.subway.utils.LegendBuilder;
import com.rabbitc.subway.utils.WidgetUtils;
import com.rabbitc.subway.widget.LegendListView;
import com.rabbitc.subway.widget.StartEndBar;

import java.util.ArrayList;
import java.util.List;

/**
 * 展示某条线路的详细信息，包括基本信息、标签、站点列表及换乘信息，
 * 并内置线路示意图（LineMapView）。
 */
public class LineInfoActivity extends BaseActivity {
    // ---------- 常量 ----------
    private static final int REQUEST_LINE_DETAIL = 100;

    // ---------- 视图 ----------
    private LegendListView legendListView;

    // ---------- 数据 ----------
    private final List<LineLegendItem> legendItems = new ArrayList<>();
    private String startStation = null;
    private String endStation = null;
    private StartEndBar startEndBar;

    // ---------- 生命周期 ----------

    /**
     * 数据就绪后初始化线路信息页面。
     *
     * <p>加载当前城市的所有线路图例数据，使用 {@link LegendBuilder} 构建线路列表，
     * 每条线路支持单击/长按跳转到 {@link DirectionInfoActivity} 查看详细站点信息。
     * 同时恢复上级页面传入的起终点状态。
     *
     * @param savedInstanceState 保存的实例状态
     */
    @SuppressLint("SetTextI18n")
    @Override
    protected void onDataReady(Bundle savedInstanceState) {
        try {
            setContentView(R.layout.activity_line_info);
            initViews();
            setupListeners();
            loadDataAndBuildGraph();
            restoreStateFromIntent();
            WidgetUtils.setListViewHeightBasedOnChildren(legendListView);
        } catch (Exception e) {
            Log.e("MapActivity", "启动失败", e);
            Toast.makeText(this, "错误: " + e.getMessage(), Toast.LENGTH_LONG).show();
            finish();
        }
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
        legendListView = findViewById(R.id.legendListView);
        startEndBar = findViewById(R.id.startEndBar);
        startEndBar.setOnStartEndClickListener(this::finish);
    }

    private void setupListeners() {
        legendListView.setItems(legendItems);
        legendListView.setOnItemSelectedListener(item -> openLineDetail(item.lineId));
        legendListView.setOnItemLongClickListener(item -> openLineDetail(item.lineId));
    }

    // ---------- 数据加载 ----------
    private void loadDataAndBuildGraph() {
        // 构建图例数据
        buildLegendData();
    }

    private void buildLegendData() {
        legendItems.clear();
        legendItems.addAll(LegendBuilder.buildLegendItems(DataCache.getInstance(), true));
    }

    // ---------- 恢复 Intent 状态 ----------
    @SuppressLint("SetTextI18n")
    private void restoreStateFromIntent() {
        startStation = getIntent().getStringExtra("start_station");
        endStation = getIntent().getStringExtra("end_station");
        startEndBar.setStartStation(startStation);
        startEndBar.setEndStation(endStation);
    }

    // ---------- 线路详情跳转 ----------
    private void openLineDetail(String lineId) {
        Intent detailIntent = new Intent(LineInfoActivity.this, DirectionInfoActivity.class);
        detailIntent.putExtra(DirectionInfoActivity.EXTRA_LINE_ID, lineId);
        if (startStation != null) detailIntent.putExtra("start_station", startStation);
        if (endStation != null) detailIntent.putExtra("end_station", endStation);
        startActivityForResult(detailIntent, REQUEST_LINE_DETAIL);
    }

    @SuppressLint("SetTextI18n")
    private void handleLineDetailResult(int resultCode, Intent data) {
        if (resultCode == RESULT_OK && data != null) {
            String start = data.getStringExtra("start_station");
            String end = data.getStringExtra("end_station");
            if (start != null) {
                startStation = start;
                startEndBar.setStartStation(startStation);
            }
            if (end != null) {
                endStation = end;
                startEndBar.setEndStation(endStation);
            }
        }
    }
}