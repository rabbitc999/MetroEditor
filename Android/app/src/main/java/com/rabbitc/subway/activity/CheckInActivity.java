package com.rabbitc.subway.activity;

import android.content.Intent;
import android.graphics.Bitmap;
import android.os.Bundle;
import android.text.Editable;
import android.text.TextUtils;
import android.text.TextWatcher;
import android.view.LayoutInflater;
import android.view.View;
import android.widget.Button;
import android.widget.AutoCompleteTextView;
import android.widget.ImageView;
import android.widget.LinearLayout;
import android.widget.TextView;
import android.widget.Toast;

import androidx.annotation.Nullable;
import androidx.appcompat.app.AlertDialog;
import androidx.appcompat.app.AppCompatActivity;

import com.rabbitc.subway.R;
import com.rabbitc.subway.adapter.FuzzyStationAdapter;
import com.rabbitc.subway.data.CheckInRecord;
import com.rabbitc.subway.data.TripHistory;
import com.rabbitc.subway.data.DataCache;
import com.rabbitc.subway.data.DataCache;
import com.rabbitc.subway.utils.CheckInManager;
import com.rabbitc.subway.utils.StationUtils;
import com.rabbitc.subway.utils.TripHistoryManager;
import com.rabbitc.subway.utils.ImageUtils;

import java.text.SimpleDateFormat;
import java.util.ArrayList;
import java.util.Date;
import java.util.List;
import java.util.Locale;

/**
 * 站点打卡记录列表界面。
 *
 * <p>本界面只负责展示当前城市的打卡记录；新增打卡在 {@link AddCheckInActivity} 中完成。</p>
 */
public class CheckInActivity extends AppCompatActivity {
    private LinearLayout recordContainer;
    private TextView tvRecordTitle;
    private TextView tvEmptyRecord;
    private TextView tvCurrentCity;
    private AutoCompleteTextView searchInput;
    private CheckInManager checkInManager;

    private static final SimpleDateFormat DATE_FORMAT =
            new SimpleDateFormat("yyyy-MM-dd HH:mm:ss", Locale.getDefault());

    @Override
    protected void onCreate(@Nullable Bundle savedInstanceState) {
        // 列表页每次创建和恢复时都会重新读取记录，保证从编辑页返回后内容即时更新。
        super.onCreate(savedInstanceState);
        setContentView(R.layout.activity_check_in);

        checkInManager = new CheckInManager(this);
        initViews();
        refreshRecordList();
    }

    @Override
    protected void onResume() {
        // 其他页面可能新增、编辑或删除记录，因此恢复时不依赖旧的内存列表。
        super.onResume();
        refreshRecordList();
    }

    private void initViews() {
        // 绑定操作按钮和筛选框；筛选文本变化时只重建当前城市的可见列表。
        Button btnBack = findViewById(R.id.btnBack);
        Button btnAdd = findViewById(R.id.btnAdd);
        Button btnClearAll = findViewById(R.id.btnClearAll);
        Button btnPlan = findViewById(R.id.btnPlan);
        tvRecordTitle = findViewById(R.id.tvRecordTitle);
        tvCurrentCity = findViewById(R.id.tvCurrentCity);
        searchInput = findViewById(R.id.searchInput);
        searchInput.setAdapter(new FuzzyStationAdapter(this, StationUtils.getActiveStationList()));
        searchInput.setThreshold(1);
        recordContainer = findViewById(R.id.recordContainer);
        tvEmptyRecord = findViewById(R.id.tvEmptyRecord);

        btnBack.setOnClickListener(v -> finish());
        btnPlan.setOnClickListener(v -> startActivity(new Intent(this, MultiStopPlanActivity.class)));
        btnAdd.setOnClickListener(v ->
                startActivity(new Intent(this, AddCheckInActivity.class)));
        btnClearAll.setOnClickListener(v -> confirmClearAll());

        searchInput.addTextChangedListener(new TextWatcher() {
            @Override
            public void beforeTextChanged(CharSequence s, int start, int count, int after) {
            }

            @Override
            public void onTextChanged(CharSequence s, int start, int before, int count) {
                refreshRecordList();
            }

            @Override
            public void afterTextChanged(Editable s) {
            }
        });
    }

    private void refreshRecordList() {
        // 先按城市隔离数据，再按站点关键词过滤，避免切换城市后误显示其他城市记录。
        String currentCity = DataCache.getInstance().getKeyCity();
        if (TextUtils.isEmpty(currentCity)) {
            currentCity = "";
        }
        tvCurrentCity.setVisibility(View.VISIBLE);
        tvCurrentCity.setText("🏙️ 当前城市：" + (TextUtils.isEmpty(currentCity) ? "未知" : currentCity));
        String query = searchInput.getText().toString().trim().toLowerCase(Locale.getDefault());
        List<CheckInRecord> records = checkInManager.loadRecords();
        List<CheckInRecord> visible = new ArrayList<>();
        for (CheckInRecord record : records) {
            String recordCity = TextUtils.isEmpty(record.city) ? currentCity : record.city;
            if (!recordCity.equals(currentCity)) {
                continue;
            }
            if (!TextUtils.isEmpty(query)) {
                String station = record.station == null ? "" : record.station.toLowerCase(Locale.getDefault());
                if (!station.contains(query)) {
                    continue;
                }
            }
            visible.add(record);
        }
        recordContainer.removeAllViews();
        tvRecordTitle.setText(String.format(Locale.getDefault(),
                "📋 打卡记录（共 %d 条）", visible.size()));

        if (visible.isEmpty()) {
            tvEmptyRecord.setVisibility(View.VISIBLE);
            return;
        }
        tvEmptyRecord.setVisibility(View.GONE);

        LayoutInflater inflater = LayoutInflater.from(this);
        for (int i = 0; i < visible.size(); i++) {
            CheckInRecord record = visible.get(i);
            View item = inflater.inflate(R.layout.item_checkin_record, recordContainer, false);

            TextView tvCity = item.findViewById(R.id.tvCheckInCity);
            TextView tvName = item.findViewById(R.id.tvStationName);
            TextView tvTime = item.findViewById(R.id.tvCheckInTime);
            TextView tvDate = item.findViewById(R.id.tvCheckInDate);
            TextView tvNote = item.findViewById(R.id.tvCheckInNote);
            ImageView ivImage = item.findViewById(R.id.ivCheckInImage);
            Button btnDelete = item.findViewById(R.id.btnDelete);
            Button btnEdit = item.findViewById(R.id.btnEdit);

            String recordCity = TextUtils.isEmpty(record.city) ? (TextUtils.isEmpty(DataCache.getInstance().getKeyCity()) ? "未知" : DataCache.getInstance().getKeyCity()) : record.city;
            tvCity.setText("🏙️ " + recordCity);
            tvName.setText(record.station);
            tvTime.setText(DATE_FORMAT.format(new Date(record.timestamp)));

            if (!TextUtils.isEmpty(record.date)) {
                tvDate.setVisibility(View.VISIBLE);
                tvDate.setText("📅 " + record.date);
            } else {
                tvDate.setVisibility(View.GONE);
            }

            if (!TextUtils.isEmpty(record.note)) {
                tvNote.setVisibility(View.VISIBLE);
                String shortNote = record.note.length() > 15
                        ? record.note.substring(0, 15) + "..."
                        : record.note;
                tvNote.setText("📝 " + shortNote);
            } else {
                tvNote.setVisibility(View.GONE);
            }

            List<String> images = record.images != null && !record.images.isEmpty()
                    ? record.images
                    : (!TextUtils.isEmpty(record.imageBase64)
                        ? java.util.Collections.singletonList(record.imageBase64)
                        : new ArrayList<>());

            // 兼容旧记录只有 imageBase64 的格式，并将首图作为列表缩略图。
            if (!images.isEmpty()) {
                Bitmap bitmap = ImageUtils.decodeBase64(images.get(0));
                if (bitmap != null) {
                    ivImage.setImageBitmap(bitmap);
                    ivImage.setVisibility(View.VISIBLE);
                }
            } else {
                ivImage.setVisibility(View.GONE);
            }

            // 删除时不能使用过滤列表索引，需通过 sameAs 在全量记录中定位真实条目。
            final CheckInRecord target = record;
            item.setOnClickListener(v -> {
                Intent intent = new Intent(this, CheckInDetailActivity.class);
                intent.putExtra(CheckInDetailActivity.EXTRA_RECORD, target);
                startActivity(intent);
            });
            btnEdit.setOnClickListener(v -> {
                Intent intent = new Intent(this, EditCheckInActivity.class);
                intent.putExtra(EditCheckInActivity.EXTRA_RECORD, target);
                startActivity(intent);
            });
            btnDelete.setOnClickListener(v -> new AlertDialog.Builder(this)
                    .setTitle("删除打卡记录")
                    .setMessage("确定删除这条打卡记录吗？")
                    .setPositiveButton("删除", (dialog, which) -> {
                        List<CheckInRecord> all = checkInManager.loadRecords();
                        int removeIndex = -1;
                        for (int j = 0; j < all.size(); j++) {
                            if (all.get(j).sameAs(target)) {
                                removeIndex = j;
                                break;
                            }
                        }
                        if (removeIndex >= 0) {
                            checkInManager.removeRecord(removeIndex);
                            refreshRecordList();
                        }
                    })
                    .setNegativeButton("取消", null)
                    .show());

            recordContainer.addView(item);
        }
    }

    private void confirmClearAll() {
        // 清空操作不可逆，必须由用户确认后再持久化删除并刷新界面。
        new AlertDialog.Builder(this)
                .setTitle("清空打卡记录")
                .setMessage("确定要删除全部打卡记录吗？此操作不可恢复。")
                .setPositiveButton("清空", (dialog, which) -> {
                    checkInManager.clearAll();
                    refreshRecordList();
                    Toast.makeText(this, "已清空全部记录", Toast.LENGTH_SHORT).show();
                })
                .setNegativeButton("取消", null)
                .show();
    }
}
