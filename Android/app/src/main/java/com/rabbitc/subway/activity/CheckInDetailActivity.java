package com.rabbitc.subway.activity;

import android.content.ClipData;
import android.content.Intent;
import android.graphics.Bitmap;
import android.text.Html;
import android.net.Uri;
import android.os.Bundle;
import android.text.TextUtils;
import android.view.Gravity;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;
import android.widget.Button;
import android.widget.ImageView;
import android.widget.LinearLayout;
import android.widget.TextView;
import android.widget.Toast;

import androidx.annotation.Nullable;
import androidx.appcompat.app.AppCompatActivity;

import com.rabbitc.subway.R;
import com.rabbitc.subway.data.CheckInRecord;
import com.rabbitc.subway.utils.CheckInManager;
import com.rabbitc.subway.utils.ImageUtils;

import java.text.SimpleDateFormat;
import java.util.ArrayList;
import java.util.Date;
import java.util.List;
import java.util.Locale;

/**
 * 打卡详情页：显示完整备注和全部打卡照片，支持添加、删除照片。
 */
public class CheckInDetailActivity extends AppCompatActivity {

    public static final String EXTRA_RECORD = "checkin_record";

    private static final int REQUEST_PICK_IMAGES = 3001;

    private CheckInRecord currentRecord;
    private CheckInManager checkInManager;
    private TextView tvStation;
    private TextView tvMeta;
    private TextView tvNote;
    private TextView tvEssay;
    private LinearLayout imageContainer;

    private static final SimpleDateFormat DATE_FORMAT =
            new SimpleDateFormat("yyyy-MM-dd HH:mm:ss", Locale.getDefault());

    @Override
    protected void onCreate(@Nullable Bundle savedInstanceState) {
        // 详情页依赖列表页传入的可序列化记录；缺少记录时直接结束，避免空指针渲染。
        super.onCreate(savedInstanceState);
        setContentView(R.layout.activity_check_in_detail);

        currentRecord = (CheckInRecord) getIntent().getSerializableExtra(EXTRA_RECORD);
        if (currentRecord == null) {
            finish();
            return;
        }
        checkInManager = new CheckInManager(this);

        initViews();
        renderRecord();
    }

    @Override
    protected void onResume() {
        // 从编辑页返回后重新读取同一条记录，确保备注或图片变更立即可见。
        super.onResume();
        if (currentRecord != null) {
            CheckInRecord refreshed = findRecord(currentRecord);
            if (refreshed != null) {
                currentRecord = refreshed;
                renderRecord();
            }
        }
    }

    private void initViews() {
        // 详情页只负责绑定展示控件和导航动作，数据填充集中在 renderRecord 中。
        Button btnBack = findViewById(R.id.btnBack);
        Button btnEdit = findViewById(R.id.btnEdit);
        Button btnAddImage = findViewById(R.id.btnAddImage);
        tvStation = findViewById(R.id.tvStation);
        tvMeta = findViewById(R.id.tvMeta);
        tvNote = findViewById(R.id.tvNote);
        tvEssay = findViewById(R.id.tvEssay);
        imageContainer = findViewById(R.id.imageContainer);

        btnBack.setOnClickListener(v -> finish());
        btnEdit.setOnClickListener(v -> {
            Intent intent = new Intent(this, EditCheckInActivity.class);
            intent.putExtra(EditCheckInActivity.EXTRA_RECORD, currentRecord);
            startActivity(intent);
        });
        btnAddImage.setOnClickListener(v -> pickImages());
    }

    private void renderRecord() {
        // 先刷新文本元数据，再重建图片容器，保证删除或新增图片后不会残留旧视图。
        tvStation.setText(currentRecord.station);
        StringBuilder meta = new StringBuilder();
        if (!TextUtils.isEmpty(currentRecord.city)) meta.append("🏙️ ").append(currentRecord.city).append("  ");
        meta.append("🕒 ").append(DATE_FORMAT.format(new Date(currentRecord.timestamp)));
        if (!TextUtils.isEmpty(currentRecord.date)) meta.append("  📅 ").append(currentRecord.date);
        tvMeta.setText(meta.toString());
        tvNote.setText(TextUtils.isEmpty(currentRecord.note) ? "📝 无备注" : "📝 " + currentRecord.note);
        if (TextUtils.isEmpty(currentRecord.essay)) {
            tvEssay.setVisibility(View.GONE);
        } else {
            tvEssay.setVisibility(View.VISIBLE);
            tvEssay.setText(Html.fromHtml(currentRecord.essay, Html.FROM_HTML_MODE_LEGACY));
        }

        imageContainer.removeAllViews();
        if (currentRecord.images == null || currentRecord.images.isEmpty()) {
            TextView empty = new TextView(this);
            empty.setText("暂无照片");
            empty.setGravity(Gravity.CENTER);
            empty.setTextColor(0xFF999999);
            empty.setPadding(0, dp(20), 0, dp(20));
            imageContainer.addView(empty);
            return;
        }

        for (int i = 0; i < currentRecord.images.size(); i++) {
            // 每张图片独立创建一行，后续可按索引扩展删除或预览操作。
            final int index = i;
            String base64 = currentRecord.images.get(i);
            View row = LayoutInflater.from(this).inflate(R.layout.item_checkin_image, imageContainer, false);
            ImageView iv = row.findViewById(R.id.ivPhoto);
            Button btnDelete = row.findViewById(R.id.btnDeletePhoto);

            Bitmap bitmap = ImageUtils.decodeBase64(base64);
            if (bitmap != null) {
                iv.setImageBitmap(bitmap);

                // 1. 宽度填满父容器，高度自适应（依赖图片比例）
                ViewGroup.LayoutParams params = iv.getLayoutParams();
                params.width = ViewGroup.LayoutParams.MATCH_PARENT;
                params.height = ViewGroup.LayoutParams.WRAP_CONTENT;
                iv.setLayoutParams(params);

                // 2. 【核心】开启自适应边界，让高度随宽度等比缩放
                iv.setAdjustViewBounds(true);

                // 3. 设置缩放类型（保持比例，居中显示，不裁剪）
                iv.setScaleType(ImageView.ScaleType.FIT_CENTER);

            } else {
                iv.setImageResource(android.R.drawable.ic_menu_gallery);
                // 占位图可以沿用布局默认尺寸，或者也设置为 MATCH_PARENT
            }

            btnDelete.setVisibility(View.GONE);
            imageContainer.addView(row);
        }
    }

    private void pickImages() {
        // 使用系统选择器并允许多选，实际压缩和去重在 onActivityResult 中完成。
        Intent intent = new Intent(Intent.ACTION_GET_CONTENT);
        intent.addCategory(Intent.CATEGORY_OPENABLE);
        intent.setType("image/*");
        intent.putExtra(Intent.EXTRA_ALLOW_MULTIPLE, true);
        startActivityForResult(Intent.createChooser(intent, "选择打卡照片"), REQUEST_PICK_IMAGES);
    }

    @Override
    protected void onActivityResult(int requestCode, int resultCode, @Nullable Intent data) {
        // 将系统返回的 ClipData 或单个 Uri 统一收集后追加到当前记录。
        super.onActivityResult(requestCode, resultCode, data);
        if (requestCode != REQUEST_PICK_IMAGES || resultCode != RESULT_OK || data == null) return;

        List<Uri> uris = new ArrayList<>();
        if (data.getClipData() != null) {
            ClipData clipData = data.getClipData();
            for (int i = 0; i < clipData.getItemCount(); i++) {
                Uri uri = clipData.getItemAt(i).getUri();
                if (uri != null) uris.add(uri);
            }
        } else if (data.getData() != null) {
            uris.add(data.getData());
        }

        if (currentRecord.images == null) currentRecord.images = new ArrayList<>();
        int added = 0;
        for (Uri uri : uris) {
            String base64 = ImageUtils.compressToBase64(this, uri);
            if (TextUtils.isEmpty(base64) || currentRecord.images.contains(base64)) continue;
            currentRecord.images.add(base64);
            added++;
        }
        if (added > 0) {
            currentRecord.imageBase64 = currentRecord.images.get(0);
            checkInManager.updateRecord(currentRecord, currentRecord);
            renderRecord();
        } else {
            Toast.makeText(this, "未添加有效照片", Toast.LENGTH_SHORT).show();
        }
    }

    private CheckInRecord findRecord(CheckInRecord target) {
        // 通过城市、站点和时间戳匹配记录，而不是依赖详情页打开时的列表位置。
        List<CheckInRecord> records = checkInManager.loadRecords();
        for (CheckInRecord record : records) {
            if (record.sameAs(target)) return record;
        }
        return null;
    }

    private int dp(int value) {
        return Math.round(value * getResources().getDisplayMetrics().density);
    }
}
