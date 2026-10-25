package com.rabbitc.subway.activity;

import android.content.ClipData;
import android.content.Intent;
import android.graphics.Bitmap;
import android.net.Uri;
import android.os.Bundle;
import android.text.TextUtils;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;
import android.widget.Button;
import android.widget.EditText;
import android.widget.ImageView;
import android.widget.LinearLayout;
import android.widget.TextView;
import android.widget.Toast;

import androidx.annotation.Nullable;
import androidx.appcompat.app.AppCompatActivity;
import androidx.core.content.FileProvider;

import com.rabbitc.subway.R;
import com.rabbitc.subway.data.CheckInRecord;
import com.rabbitc.subway.utils.CheckInManager;
import com.rabbitc.subway.utils.CoordinateUtils;
import com.rabbitc.subway.utils.ImageUtils;

import java.io.File;
import java.text.SimpleDateFormat;
import java.util.ArrayList;
import java.util.Calendar;
import java.util.Date;
import java.util.List;
import java.util.Locale;

/**
 * 编辑已有站点打卡记录的表单页面。
 *
 * <p>进入页面时恢复原记录及图片，保存时将修改后的内容写回本地记录管理器。</p>
 */
public class EditCheckInActivity extends AppCompatActivity {

    public static final String EXTRA_RECORD = "checkin_record";

    private static final int REQUEST_PICK_IMAGES = 4001;
    private static final int REQUEST_TAKE_PHOTO = 4002;

    private CheckInRecord currentRecord;
    private CheckInManager checkInManager;
    private EditText etNote;
    private EditText etEssay;
    private TextView tvDate;
    private TextView tvStation;
    private LinearLayout imageListContainer;
    private List<String> pendingImages = new ArrayList<>();
    private String pendingDate;
    private File cameraTmpFile;

    private static final SimpleDateFormat DAY_FORMAT = new SimpleDateFormat("yyyy-MM-dd", Locale.getDefault());

    /** 读取待编辑记录，初始化表单字段和图片预览。 */
    @Override
    protected void onCreate(@Nullable Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        setContentView(R.layout.activity_edit_check_in);

        currentRecord = (CheckInRecord) getIntent().getSerializableExtra(EXTRA_RECORD);
        if (currentRecord == null) {
            finish();
            return;
        }
        checkInManager = new CheckInManager(this);
        pendingDate = TextUtils.isEmpty(currentRecord.date) ? DAY_FORMAT.format(new Date()) : currentRecord.date;
        if (currentRecord.images != null) pendingImages.addAll(currentRecord.images);
        if (pendingImages.isEmpty() && !TextUtils.isEmpty(currentRecord.imageBase64)) pendingImages.add(currentRecord.imageBase64);

        initViews();
        renderImagePreview();
    }

    private void initViews() {
        Button btnBack = findViewById(R.id.btnBack);
        Button btnAddImages = findViewById(R.id.btnAddImages);
        Button btnTakePhoto = findViewById(R.id.btnTakePhoto);
        Button btnClearImages = findViewById(R.id.btnClearImages);
        Button btnSave = findViewById(R.id.btnSave);

        tvStation = findViewById(R.id.tvStation);
        tvDate = findViewById(R.id.tvDate);
        etNote = findViewById(R.id.etNote);
        etEssay = findViewById(R.id.etEssay);
        imageListContainer = findViewById(R.id.imageListContainer);

        tvStation.setText("📍 " + currentRecord.station + (TextUtils.isEmpty(currentRecord.city) ? "" : "（" + currentRecord.city + "）"));
        tvDate.setText(pendingDate);
        etNote.setText(currentRecord.note);
        etEssay.setText(currentRecord.essay);

        btnBack.setOnClickListener(v -> finish());
        tvDate.setOnClickListener(v -> showDatePicker());
        btnAddImages.setOnClickListener(v -> pickImages());
        btnTakePhoto.setOnClickListener(v -> takePhoto());
        btnClearImages.setOnClickListener(v -> {
            pendingImages.clear();
            renderImagePreview();
        });
        Button btnBold = findViewById(R.id.btnBold);
        Button btnItalic = findViewById(R.id.btnItalic);
        Button btnHeading = findViewById(R.id.btnHeading);

        btnBold.setOnClickListener(v -> applyEssayFormat(etEssay, "<b>", "</b>"));
        btnItalic.setOnClickListener(v -> applyEssayFormat(etEssay, "<i>", "</i>"));
        btnHeading.setOnClickListener(v -> applyEssayFormat(etEssay, "<h3>", "</h3>"));

        btnSave.setOnClickListener(v -> saveChanges());
    }

    private void renderImagePreview() {
        imageListContainer.removeAllViews();

        if (pendingImages.isEmpty()) {
            return; // 无图片时不显示任何项
        }

        LayoutInflater inflater = LayoutInflater.from(this);
        for (int i = 0; i < pendingImages.size(); i++) {
            final int index = i;
            View row = inflater.inflate(R.layout.item_checkin_image, imageListContainer, false);
            ImageView iv = row.findViewById(R.id.ivPhoto);
            Button btnDelete = row.findViewById(R.id.btnDeletePhoto);

            Bitmap bmp = ImageUtils.decodeBase64(pendingImages.get(index));
            if (bmp != null) {
                iv.setImageBitmap(bmp);

                // ---- 核心修改：动态调整宽高 ----
                ViewGroup.LayoutParams params = iv.getLayoutParams();
                params.width = ViewGroup.LayoutParams.MATCH_PARENT;
                params.height = ViewGroup.LayoutParams.WRAP_CONTENT;
                iv.setLayoutParams(params);
                iv.setAdjustViewBounds(true);
                iv.setScaleType(ImageView.ScaleType.FIT_CENTER);
            } else {
                iv.setImageResource(android.R.drawable.ic_menu_gallery);
                // 占位图也可按需设置相同参数（但通常占位图是固定尺寸，可保留原样）
            }

            btnDelete.setOnClickListener(v -> {
                pendingImages.remove(index);
                renderImagePreview();
            });
            imageListContainer.addView(row);
        }
    }

    private void pickImages() {
        Intent intent = new Intent(Intent.ACTION_GET_CONTENT);
        intent.addCategory(Intent.CATEGORY_OPENABLE);
        intent.setType("image/*");
        intent.putExtra(Intent.EXTRA_ALLOW_MULTIPLE, true);
        startActivityForResult(Intent.createChooser(intent, "选择打卡照片"), REQUEST_PICK_IMAGES);
    }

    private void takePhoto() {
        if (currentRecord.distance > 1000 || currentRecord.distance < 0) {
            Toast.makeText(this, "当前不在站点 1000 米范围内，无法拍照作为现场凭证", Toast.LENGTH_SHORT).show();
            return;
        }
        try {
            File dir = new File(getCacheDir(), "checkin_images");
            if (!dir.exists()) dir.mkdirs();
            cameraTmpFile = new File(dir, "edit_photo_" + System.currentTimeMillis() + ".jpg");
            Uri uri = FileProvider.getUriForFile(this, "com.rabbitc.subway.fileprovider", cameraTmpFile);
            Intent intent = new Intent(android.provider.MediaStore.ACTION_IMAGE_CAPTURE);
            intent.putExtra(android.provider.MediaStore.EXTRA_OUTPUT, uri);
            intent.addFlags(Intent.FLAG_GRANT_WRITE_URI_PERMISSION | Intent.FLAG_GRANT_READ_URI_PERMISSION);
            startActivityForResult(intent, REQUEST_TAKE_PHOTO);
        } catch (Exception e) {
            Toast.makeText(this, "无法启动相机", Toast.LENGTH_SHORT).show();
        }
    }

    private void showDatePicker() {
        Calendar cal = Calendar.getInstance();
        try {
            Date d = DAY_FORMAT.parse(pendingDate);
            if (d != null) cal.setTime(d);
        } catch (Exception ignored) {}
        new android.app.DatePickerDialog(this, (view, year, month, dayOfMonth) -> {
            Calendar picked = Calendar.getInstance();
            picked.set(year, month, dayOfMonth);
            pendingDate = DAY_FORMAT.format(picked.getTime());
            tvDate.setText(pendingDate);
        }, cal.get(Calendar.YEAR), cal.get(Calendar.MONTH), cal.get(Calendar.DAY_OF_MONTH)).show();
    }

    @Override
    protected void onActivityResult(int requestCode, int resultCode, @Nullable Intent data) {
        super.onActivityResult(requestCode, resultCode, data);
        if (resultCode != RESULT_OK) return;
        List<Uri> uris = new ArrayList<>();
        if (requestCode == REQUEST_PICK_IMAGES && data != null) {
            if (data.getClipData() != null) {
                ClipData clipData = data.getClipData();
                for (int i = 0; i < clipData.getItemCount(); i++) {
                    Uri uri = clipData.getItemAt(i).getUri();
                    if (uri != null) uris.add(uri);
                }
            } else if (data.getData() != null) {
                uris.add(data.getData());
            }
        } else if (requestCode == REQUEST_TAKE_PHOTO && cameraTmpFile != null) {
            insertEssayText(etEssay, "\n【距离水印：" + CoordinateUtils.formatDistance(currentRecord.distance) + "】\n");
            uris.add(Uri.fromFile(cameraTmpFile));
        }
        for (Uri uri : uris) {
            String base64 = ImageUtils.compressToBase64(this, uri);
            if (!TextUtils.isEmpty(base64) && !pendingImages.contains(base64)) pendingImages.add(base64);
        }
        renderImagePreview();
    }

    private void applyEssayFormat(EditText editText, String prefix, String suffix) {
        int start = editText.getSelectionStart();
        int end = editText.getSelectionEnd();
        String selected = editText.getText().subSequence(start, end).toString();
        editText.getText().replace(start, end, prefix + selected + suffix);
    }

    private void insertEssayText(EditText editText, String text) {
        int start = Math.max(editText.getSelectionStart(), 0);
        editText.getText().insert(start, text);
    }

    private void saveChanges() {
        currentRecord.note = etNote.getText().toString().trim();
        currentRecord.essay = etEssay.getText().toString().trim();
        currentRecord.date = pendingDate;
        currentRecord.images = new ArrayList<>(pendingImages);
        currentRecord.imageBase64 = pendingImages.isEmpty() ? null : pendingImages.get(0);
        checkInManager.updateRecord(currentRecord, currentRecord);
        Toast.makeText(this, "打卡信息已更新", Toast.LENGTH_SHORT).show();
        setResult(RESULT_OK);
        finish();
    }
}
