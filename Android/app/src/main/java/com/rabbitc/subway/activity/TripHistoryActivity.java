package com.rabbitc.subway.activity;

import android.app.Activity;
import android.content.Intent;
import android.os.Bundle;
import android.text.TextUtils;
import android.text.Editable;
import android.text.TextWatcher;
import android.view.LayoutInflater;
import android.view.View;
import android.widget.Button;
import android.widget.EditText;
import android.widget.LinearLayout;
import android.widget.TextView;
import android.widget.Toast;

import androidx.annotation.Nullable;
import androidx.appcompat.app.AlertDialog;
import androidx.appcompat.app.AppCompatActivity;

import com.rabbitc.subway.R;
import com.rabbitc.subway.data.DataCache;
import com.rabbitc.subway.data.TripHistory;
import com.rabbitc.subway.utils.TripHistoryManager;

import java.text.SimpleDateFormat;
import java.util.Date;
import java.util.ArrayList;
import java.util.List;
import java.util.Locale;

/**
 * 历史行程界面 — 展示路线查询的历史记录。
 *
 * <p>功能：</p>
 * <ul>
 *   <li>按时间倒序显示全部历史行程（起终点、时间、里程、耗时、换乘、票价）</li>
 *   <li>点击行程卡片可将起终点回填到主界面快速重新查询</li>
 *   <li>支持单条删除和一键清空</li>
 * </ul>
 *
 * @see TripHistoryManager
 */
public class TripHistoryActivity extends AppCompatActivity {

    /** 回传起点站的 Intent 键 */
    public static final String EXTRA_RETURN_START = "return_start";
    /** 回传终点站的 Intent 键 */
    public static final String EXTRA_RETURN_END = "return_end";
    /** 回传查询模式的 Intent 键 */
    public static final String EXTRA_RETURN_MODE = "return_mode";
    /** 回传完整行程记录的 Intent 键 */
    public static final String EXTRA_RETURN_TRIP = "return_trip";

    // ---------- 视图 ----------
    private TextView tvHistoryTitle;
    private EditText searchInput;
    private LinearLayout historyContainer;
    private TextView tvEmptyHistory;

    // ---------- 数据 ----------
    private TripHistoryManager historyManager;

    // ---------- 时间格式化 ----------
    private static final SimpleDateFormat DATE_FORMAT =
            new SimpleDateFormat("yyyy-MM-dd HH:mm", Locale.getDefault());

    @Override
    protected void onCreate(@Nullable Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        setContentView(R.layout.activity_trip_history);

        historyManager = new TripHistoryManager(this);
        historyManager.removeDuplicateRoutes();

        initViews();
        refreshHistoryList();
    }

    // ======================== 视图初始化 ========================

    private void initViews() {
        Button btnBack = findViewById(R.id.btnBack);
        Button btnClearAll = findViewById(R.id.btnClearAll);
        tvHistoryTitle = findViewById(R.id.tvHistoryTitle);
        searchInput = findViewById(R.id.searchInput);
        historyContainer = findViewById(R.id.historyContainer);
        tvEmptyHistory = findViewById(R.id.tvEmptyHistory);

        btnBack.setOnClickListener(v -> finish());
        btnClearAll.setOnClickListener(v -> confirmClearAll());

        searchInput.addTextChangedListener(new TextWatcher() {
            @Override public void beforeTextChanged(CharSequence s, int start, int count, int after) {}
            @Override public void onTextChanged(CharSequence s, int start, int before, int count) { refreshHistoryList(); }
            @Override public void afterTextChanged(Editable s) {}
        });
    }

    // ======================== 列表刷新 ========================

    /**
     * 刷新历史行程列表。
     */
    private void refreshHistoryList() {
        List<TripHistory> allRecords = historyManager.loadRecords();
        String currentCity = DataCache.getInstance().getKeyCity();
        String query = searchInput.getText().toString().trim().toLowerCase(Locale.getDefault());
        List<TripHistory> records = new ArrayList<>();
        for (TripHistory record : allRecords) {
            if (!TextUtils.isEmpty(currentCity) && !TextUtils.isEmpty(record.city)
                    && !record.city.equals(currentCity)) {
                continue;
            }
            if (TextUtils.isEmpty(query)) {
                records.add(record);
            } else {
                String text = (record.getRouteText() + " " + record.mode).toLowerCase(Locale.getDefault());
                if (text.contains(query)) records.add(record);
            }
        }
        historyContainer.removeAllViews();

        tvHistoryTitle.setText(String.format(Locale.getDefault(),
                "共 %d 条行程", records.size()));

        if (records.isEmpty()) {
            tvEmptyHistory.setVisibility(View.VISIBLE);
            return;
        }
        tvEmptyHistory.setVisibility(View.GONE);

        LayoutInflater inflater = LayoutInflater.from(this);
        for (int i = 0; i < records.size(); i++) {
            TripHistory record = records.get(i);
            View item = inflater.inflate(R.layout.item_trip_history, historyContainer, false);

            TextView tvRoute = item.findViewById(R.id.tvRoute);
            TextView tvTime = item.findViewById(R.id.tvTime);
            TextView tvDistance = item.findViewById(R.id.tvDistance);
            TextView tvTime2 = item.findViewById(R.id.tvTime2);
            TextView tvTransfers = item.findViewById(R.id.tvTransfers);
            TextView tvFare = item.findViewById(R.id.tvFare);
            Button btnDelete = item.findViewById(R.id.btnDelete);

            tvRoute.setText(record.getRouteText());
            tvTime.setText(DATE_FORMAT.format(new Date(record.timestamp))
                    + (record.mode != null && !record.mode.isEmpty() ? " · " + record.mode : ""));
            tvDistance.setText(String.format(Locale.getDefault(), "%.1f公里", record.distanceKm));
            tvTime2.setText(record.getTimeText());
            tvTransfers.setText(String.format(Locale.getDefault(),
                    "%d次换乘", record.transfers));
            tvFare.setText(record.getFareText());

            // 点击卡片回传起终点到主界面
            item.setOnClickListener(v -> {
                Intent result = new Intent();
                result.putExtra(EXTRA_RETURN_TRIP, record);
                result.putExtra(EXTRA_RETURN_START, record.startStation);
                result.putExtra(EXTRA_RETURN_END, record.endStation);
                result.putExtra(EXTRA_RETURN_MODE, record.mode);
                setResult(Activity.RESULT_OK, result);
                finish();
            });

            final int index = i;
            final TripHistory target = record;
            btnDelete.setOnClickListener(v -> new AlertDialog.Builder(this)
                    .setTitle("删除历史行程")
                    .setMessage("确定删除这条历史行程吗？")
                    .setPositiveButton("删除", (dialog, which) -> {
                        List<TripHistory> all = historyManager.loadRecords();
                        int removeIndex = -1;
                        for (int j = 0; j < all.size(); j++) {
                            if (all.get(j).sameAs(target)) {
                                removeIndex = j;
                                break;
                            }
                        }
                        if (removeIndex >= 0) {
                            historyManager.removeRecord(removeIndex);
                            refreshHistoryList();
                        }
                    })
                    .setNegativeButton("取消", null)
                    .show());

            historyContainer.addView(item);
        }
    }

    // ======================== 清空记录 ========================

    /**
     * 确认清空全部历史行程。
     */
    private void confirmClearAll() {
        new AlertDialog.Builder(this)
                .setTitle("清空历史行程")
                .setMessage("确定要删除全部历史行程吗？此操作不可恢复。")
                .setPositiveButton("清空", (dialog, which) -> {
                    historyManager.clearAll();
                    refreshHistoryList();
                    Toast.makeText(this, "已清空全部行程", Toast.LENGTH_SHORT).show();
                })
                .setNegativeButton("取消", null)
                .show();
    }
}
