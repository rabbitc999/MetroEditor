package com.rabbitc.subway.activity;

import android.Manifest;
import android.content.ClipData;
import android.content.Intent;
import android.content.pm.PackageManager;
import android.graphics.Bitmap;
import android.net.Uri;
import android.os.Bundle;
import android.text.TextUtils;
import android.view.Gravity;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;
import com.rabbitc.subway.adapter.FuzzyStationAdapter;
import android.widget.AutoCompleteTextView;
import android.widget.Button;
import android.widget.EditText;
import android.widget.ImageView;
import android.widget.LinearLayout;
import android.widget.TextView;
import android.widget.Toast;

import androidx.annotation.NonNull;
import androidx.annotation.Nullable;
import androidx.appcompat.app.AlertDialog;
import androidx.appcompat.app.AppCompatActivity;
import androidx.core.app.ActivityCompat;
import androidx.core.content.FileProvider;

import com.rabbitc.subway.R;
import com.rabbitc.subway.data.CheckInRecord;
import com.rabbitc.subway.data.DataCache;
import com.rabbitc.subway.utils.CheckInManager;
import com.rabbitc.subway.utils.CoordinateUtils;
import com.rabbitc.subway.utils.ImageUtils;
import com.rabbitc.subway.utils.LocationHelper;

import java.io.File;
import java.text.SimpleDateFormat;
import java.util.ArrayList;
import java.util.Calendar;
import java.util.Date;
import java.util.List;
import java.util.Locale;
import java.util.Map;
import java.util.TreeSet;

/**
 * 新增站点打卡界面。
 *
 * <p>负责定位、展示附近站点、支持手动输入站名，以及图片、备注、日期的打卡表单。</p>
 */
public class AddCheckInActivity extends AppCompatActivity {
    /** 从站点列表直接进入打卡表单时携带的站点名称。 */
    public static final String EXTRA_STATION_NAME = "station_name";
    /** 从旧入口进入编辑流程时携带的记录对象。 */
    public static final String EXTRA_EDIT_RECORD = "edit_record";

    private static final int REQUEST_LOCATION_PERMISSION = 1001;
    private static final int REQUEST_PICK_IMAGE = 2001;
    private static final int REQUEST_TAKE_PHOTO = 2002;

    private TextView tvLocationStatus;
    private TextView tvNearbyTitle;
    private LinearLayout nearbyStationContainer;
    private AutoCompleteTextView manualStationInput;

    private CheckInManager checkInManager;
    private double currentLon = Double.NaN;
    private double currentLat = Double.NaN;

    private String pendingStation;
    private double pendingDistance = -1;
    private boolean pendingHasOnSiteProof;
    private List<String> pendingImages = new ArrayList<>();
    private CheckInRecord editingRecord;
    private String pendingDate;
    private File cameraTmpFile;
    private AlertDialog checkInDialog;
    private ImageView ivPreview;
    private TextView imageCountText;
    private TextView proofStatusText;

    private static final SimpleDateFormat DAY_FORMAT =
            new SimpleDateFormat("yyyy-MM-dd", Locale.getDefault());

    @Override
    protected void onCreate(@Nullable Bundle savedInstanceState) {
        // 入口可能来自附近站点、编辑记录或普通新增页面，按 Intent 优先级选择初始化分支。
        super.onCreate(savedInstanceState);
        setContentView(R.layout.activity_add_check_in);

        checkInManager = new CheckInManager(this);
        initViews();

        String stationName = getIntent().getStringExtra(EXTRA_STATION_NAME);
        if (!TextUtils.isEmpty(stationName)) {
            startCheckInForm(stationName);
            return;
        }

        CheckInRecord editRecord = (CheckInRecord) getIntent().getSerializableExtra(EXTRA_EDIT_RECORD);
        if (editRecord != null) {
            Intent editIntent = new Intent(this, EditCheckInActivity.class);
            editIntent.putExtra(EditCheckInActivity.EXTRA_RECORD, editRecord);
            startActivity(editIntent);
            return;
        }

        if (LocationHelper.hasLocationPermission(this)) {
            startLocate();
        } else {
            ActivityCompat.requestPermissions(this,
                    new String[]{
                            Manifest.permission.ACCESS_FINE_LOCATION,
                            Manifest.permission.ACCESS_COARSE_LOCATION},
                    REQUEST_LOCATION_PERMISSION);
        }
    }

    private void initViews() {
        // 只在这里绑定布局控件，业务方法通过成员变量更新状态，避免重复查找视图。
        Button btnBack = findViewById(R.id.btnBack);
        tvLocationStatus = findViewById(R.id.tvLocationStatus);
        tvNearbyTitle = findViewById(R.id.tvNearbyTitle);
        nearbyStationContainer = findViewById(R.id.nearbyStationContainer);
        manualStationInput = findViewById(R.id.manualStationInput);
        Button btnManualCheckIn = findViewById(R.id.btnManualCheckIn);

        btnBack.setOnClickListener(v -> finish());
        setupManualInput();

        btnManualCheckIn.setText("手动补卡");
        btnManualCheckIn.setOnClickListener(v -> {
            String name = manualStationInput.getText().toString().trim();
            if (TextUtils.isEmpty(name)) {
                Toast.makeText(this, "请输入站点名称", Toast.LENGTH_SHORT).show();
                return;
            }
            startCheckInForm(name);
        });
    }

    private void setupManualInput() {
        // 坐标表的键名是当前地图的完整站点集合，排序后交给模糊适配器提供补全。
        List<String> stations = new ArrayList<>();
        Map<String, double[]> coords = DataCache.getInstance().getStationCoords();
        if (coords != null) {
            stations.addAll(new TreeSet<>(coords.keySet()));
        }
        FuzzyStationAdapter adapter = new FuzzyStationAdapter(this, stations);
        manualStationInput.setAdapter(adapter);
        manualStationInput.setThreshold(1);
    }

    @Override
    public void onRequestPermissionsResult(int requestCode, @NonNull String[] permissions,
                                           @NonNull int[] grantResults) {
        super.onRequestPermissionsResult(requestCode, permissions, grantResults);
        if (requestCode == REQUEST_LOCATION_PERMISSION) {
            if (grantResults.length > 0 && grantResults[0] == PackageManager.PERMISSION_GRANTED) {
                startLocate();
            } else {
                tvLocationStatus.setText("未授予定位权限，无法获取附近站点\n（可在系统设置中开启定位权限）");
            }
        }
    }

    private void startLocate() {
        // 定位回调可能来自后台线程，UI 更新统一切回主线程后再刷新附近站点。
        tvLocationStatus.setText("正在获取位置...");
        LocationHelper.getCurrentLocation(this, LocationHelper.LOCATION_TIMEOUT_MS,
                new LocationHelper.OnLocationResult() {
                    @Override
                    public void onLocation(double longitude, double latitude) {
                        currentLon = longitude;
                        currentLat = latitude;
                        runOnUiThread(() -> {
                            tvLocationStatus.setText(String.format(Locale.getDefault(),
                                    "当前位置: %.5f, %.5f", latitude, longitude));
                            refreshNearbyStations();
                        });
                    }

                    @Override
                    public void onError(String reason) {
                        runOnUiThread(() ->
                                tvLocationStatus.setText("定位失败: " + reason));
                    }
                });
    }

    private void refreshNearbyStations() {
        // 没有有效坐标时保留当前界面，避免把“定位失败”误显示成“附近无站点”。
        if (Double.isNaN(currentLon) || Double.isNaN(currentLat)) {
            return;
        }

        List<LocationHelper.NearbyStation> nearby = LocationHelper.findNearbyStations(
                currentLon, currentLat, LocationHelper.DEFAULT_RADIUS_METERS, 15);
        nearbyStationContainer.removeAllViews();

        if (nearby.isEmpty()) {
            tvNearbyTitle.setVisibility(View.VISIBLE);
            tvNearbyTitle.setText("附近站点\n附近 5 公里内暂无地铁站");
            return;
        }

        tvNearbyTitle.setVisibility(View.VISIBLE);
        tvNearbyTitle.setText(String.format(Locale.getDefault(),
                "附近站点（共 %d 个）", nearby.size()));

        LayoutInflater inflater = LayoutInflater.from(this);
        for (LocationHelper.NearbyStation station : nearby) {
            View item = inflater.inflate(R.layout.item_nearby_station,
                    nearbyStationContainer, false);

            TextView tvName = item.findViewById(R.id.tvNearbyStationName);
            TextView tvDistance = item.findViewById(R.id.tvNearbyStationDistance);
            Button btnCheckIn = item.findViewById(R.id.btnCheckIn);

            tvName.setText(station.name);
            tvDistance.setText("距离 " + station.getDistanceText());
            btnCheckIn.setOnClickListener(v -> startCheckInForm(station.name));
            nearbyStationContainer.addView(item);
        }
    }

    private void startCheckInForm(String stationName) {
        Intent intent = new Intent(this, CheckInFormActivity.class);
        intent.putExtra(CheckInFormActivity.EXTRA_STATION_NAME, stationName);
        startActivity(intent);
    }

    private void showImagePickMenu() {
        // 相册支持多选，拍照用于现场凭证，第三项清空当前表单中的待提交图片。
        String[] options = {"📷 从相册选择（可多选）", "📸 拍照", "🗑 移除全部图片"};
        new AlertDialog.Builder(this)
                .setTitle("打卡图片")
                .setItems(options, (dialog, which) -> {
                    if (which == 0) {
                        pickFromGallery();
                    } else if (which == 1) {
                        takePhoto();
                    } else {
                        pendingImages.clear();
                        updateImagePreview();
                    }
                })
                .setNegativeButton("取消", null)
                .show();
    }

    private void pickFromGallery() {
        Intent intent = new Intent(Intent.ACTION_GET_CONTENT);
        intent.addCategory(Intent.CATEGORY_OPENABLE);
        intent.setType("image/*");
        intent.putExtra(Intent.EXTRA_ALLOW_MULTIPLE, true);
        startActivityForResult(Intent.createChooser(intent, "选择打卡图片"), REQUEST_PICK_IMAGE);
    }

    private void takePhoto() {
        // 只有在站点附近完成定位时才允许拍照，生成的临时文件由 FileProvider 暴露给相机。
        if (pendingDistance > 1000 || pendingDistance < 0) {
            Toast.makeText(this, "当前不在站点 1000 米范围内，无法作为现场凭证拍照", Toast.LENGTH_SHORT).show();
            return;
        }
        try {
            File dir = new File(getCacheDir(), "checkin_images");
            if (!dir.exists()) dir.mkdirs();
            cameraTmpFile = new File(dir, "photo_" + System.currentTimeMillis() + ".jpg");

            Uri photoUri = FileProvider.getUriForFile(this,
                    "com.rabbitc.subway.fileprovider", cameraTmpFile);

            Intent intent = new Intent(android.provider.MediaStore.ACTION_IMAGE_CAPTURE);
            intent.putExtra(android.provider.MediaStore.EXTRA_OUTPUT, photoUri);
            intent.addFlags(Intent.FLAG_GRANT_WRITE_URI_PERMISSION
                    | Intent.FLAG_GRANT_READ_URI_PERMISSION);
            startActivityForResult(intent, REQUEST_TAKE_PHOTO);
        } catch (Exception e) {
            Toast.makeText(this, "无法启动相机: " + e.getMessage(), Toast.LENGTH_SHORT).show();
        }
    }

    private void showDatePicker(TextView tvDate) {
        Calendar cal = Calendar.getInstance();
        try {
            Date d = DAY_FORMAT.parse(pendingDate);
            if (d != null) cal.setTime(d);
        } catch (Exception ignored) {
        }
        new android.app.DatePickerDialog(this, (view, year, month, dayOfMonth) -> {
            Calendar picked = Calendar.getInstance();
            picked.set(year, month, dayOfMonth);
            pendingDate = DAY_FORMAT.format(picked.getTime());
            tvDate.setText(pendingDate);
        }, cal.get(Calendar.YEAR), cal.get(Calendar.MONTH), cal.get(Calendar.DAY_OF_MONTH)).show();
    }

    @Override
    protected void onActivityResult(int requestCode, int resultCode, @Nullable Intent data) {
        // 将相册 ClipData、单张 Uri 和相机临时文件统一转换成 Uri 列表处理。
        super.onActivityResult(requestCode, resultCode, data);
        if (resultCode != RESULT_OK) return;

        if (requestCode == REQUEST_PICK_IMAGE && data != null) {
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
            applyPickedImages(uris, "图片加载失败");
        } else if (requestCode == REQUEST_TAKE_PHOTO && cameraTmpFile != null) {
            Uri uri = Uri.fromFile(cameraTmpFile);
            applyPickedImages(List.of(uri), "拍照图片处理失败");
        }
    }

    private void applyPickedImages(List<Uri> uris, String errorMsg) {
        // Base64 内容既用于持久化也用于去重，因此在写入待提交列表前先压缩并检查重复值。
        int added = 0;
        for (Uri uri : uris) {
            String base64 = ImageUtils.compressToBase64(this, uri);
            if (TextUtils.isEmpty(base64) || pendingImages.contains(base64)) continue;
            pendingImages.add(base64);
            added++;
        }
        if (added == 0 && !uris.isEmpty()) {
            Toast.makeText(this, errorMsg, Toast.LENGTH_SHORT).show();
        }
        updateImagePreview();
    }

    private void updateImagePreview() {
        // 预览只展示第一张图片，但计数始终反映全部待提交图片。
        if (ivPreview == null) return;
        if (pendingImages.isEmpty()) {
            ivPreview.setBackgroundColor(0xFFEEF2F7);
            ivPreview.setImageResource(android.R.drawable.ic_menu_gallery);
        } else {
            Bitmap bitmap = ImageUtils.decodeBase64(pendingImages.get(0));
            if (bitmap != null) {
                ivPreview.setBackgroundColor(0x00000000);
                ivPreview.setImageBitmap(bitmap);
            }
        }
        if (imageCountText != null) {
            imageCountText.setText("已选择 " + pendingImages.size() + " 张图片");
        }
    }

    private void doCheckIn(String stationName, double distanceMeters, String note,
                           String date, String essay, CheckInRecord editRecord) {
        // 编辑时保留原时间、坐标和城市；新增时使用本次定位结果，并统一写入图片兼容字段。
        CheckInRecord record = new CheckInRecord(
                stationName,
                editRecord != null ? editRecord.timestamp : System.currentTimeMillis(),
                editRecord != null ? editRecord.longitude : (Double.isNaN(currentLon) ? 0 : currentLon),
                editRecord != null ? editRecord.latitude : (Double.isNaN(currentLat) ? 0 : currentLat),
                distanceMeters);
        record.city = editRecord != null ? editRecord.city : DataCache.getInstance().getKeyCity();
        record.note = note;
        record.essay = essay;
        record.date = date;
        record.images = new ArrayList<>(pendingImages);
        record.imageBase64 = record.images.isEmpty() ? null : record.images.get(0);

        if (editRecord != null) {
            checkInManager.updateRecord(editRecord, record);
            Toast.makeText(this, "打卡信息已更新", Toast.LENGTH_SHORT).show();
        } else {
            checkInManager.addRecord(record);
            Toast.makeText(this, "打卡成功: " + stationName, Toast.LENGTH_SHORT).show();
        }
        setResult(RESULT_OK);
        finish();
    }

    private double[] getStationCoordinates(String stationName) {
        // 坐标缺失返回 null，由调用方决定是否允许继续执行手动打卡流程。
        if (TextUtils.isEmpty(stationName)) return null;
        java.util.Map<String, double[]> coords = DataCache.getInstance().getStationCoords();
        return coords != null ? coords.get(stationName) : null;
    }
    private void applyEssayFormat(EditText editText, String prefix, String suffix) {
        // 仅包裹当前选区，未选中文本时等价于在光标处插入一对格式标记。
        int start = editText.getSelectionStart();
        int end = editText.getSelectionEnd();
        String selected = editText.getText().subSequence(start, end).toString();
        editText.getText().replace(start, end, prefix + selected + suffix);
    }

    private void insertEssayText(EditText editText, String text) {
        int start = Math.max(editText.getSelectionStart(), 0);
        editText.getText().insert(start, text);
    }

    private int dp(int value) {
        return Math.round(value * getResources().getDisplayMetrics().density);
    }
}
