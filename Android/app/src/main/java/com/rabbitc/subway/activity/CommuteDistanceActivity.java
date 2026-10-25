package com.rabbitc.subway.activity;

import android.Manifest;
import android.annotation.SuppressLint;
import android.content.Intent;
import android.content.pm.PackageManager;
import android.content.SharedPreferences;
import android.os.Bundle;
import android.text.Editable;
import android.text.TextUtils;
import android.text.TextWatcher;
import android.widget.AutoCompleteTextView;
import android.widget.Button;
import android.widget.EditText;
import android.widget.ListView;
import android.widget.TextView;
import android.widget.Toast;

import androidx.annotation.NonNull;
import androidx.appcompat.app.AppCompatActivity;
import androidx.core.app.ActivityCompat;

import com.rabbitc.subway.R;
import com.rabbitc.subway.algroithm.MetroGraph;
import com.rabbitc.subway.algroithm.TimeCalculator;
import com.rabbitc.subway.adapter.FuzzyStationAdapter;
import com.rabbitc.subway.adapter.RouteSegmentAdapter;
import com.rabbitc.subway.data.DataCache;
import com.rabbitc.subway.data.PathResult;
import com.rabbitc.subway.data.RouteSegment;
import com.rabbitc.subway.data.SearchWeight;
import com.rabbitc.subway.data.TripHistory;
import com.rabbitc.subway.utils.CoordinateUtils;
import com.rabbitc.subway.utils.LocationHelper;
import com.rabbitc.subway.utils.StationUtils;
import com.rabbitc.subway.utils.TripHistoryManager;
import com.rabbitc.subway.utils.WidgetUtils;

import org.json.JSONArray;
import org.json.JSONObject;

import java.io.BufferedReader;
import java.io.InputStreamReader;
import java.net.HttpURLConnection;
import java.net.URL;
import java.net.URLEncoder;
import java.util.ArrayList;
import java.util.List;
import java.util.Locale;
import java.util.Map;

/**
 * 单次通勤距离计算页面。
 *
 * <p>把家庭位置和公司地址转换为最近地铁站后，计算地铁距离、耗时、
 * 换乘次数及票价。</p>
 */
public class CommuteDistanceActivity extends AppCompatActivity {
    private static final int REQUEST_LOCATION = 6001;

    private AutoCompleteTextView homeStationInput;
    private EditText homeDistanceInput;
    private EditText companyAddressInput;
    private EditText amapApiKeyInput;
    private TextView tvResult;
    private ListView commuteRouteList;
    private TextView tvCommuteDistance;
    private TextView tvCommuteTime;
    private TextView tvCommuteTransfers;
    private TextView tvCommuteFare;

    private double homeLon = Double.NaN;
    private double homeLat = Double.NaN;

    /** 初始化地址、定位、计算按钮和结果列表。 */
    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        setContentView(R.layout.activity_commute_distance);

        Button btnBack = findViewById(R.id.btnBack);
        btnBack.setOnClickListener(v -> finish());

        homeStationInput = findViewById(R.id.homeStationInput);
        homeDistanceInput = findViewById(R.id.homeDistanceInput);
        companyAddressInput = findViewById(R.id.companyAddressInput);
        amapApiKeyInput = findViewById(R.id.amapApiKeyInput);
        SharedPreferences prefs = getSharedPreferences("amap_settings", MODE_PRIVATE);
        amapApiKeyInput.setText(prefs.getString("amap_api_key", ""));
        amapApiKeyInput.addTextChangedListener(new TextWatcher() {
            @Override public void beforeTextChanged(CharSequence s, int start, int count, int after) {}
            @Override public void onTextChanged(CharSequence s, int start, int before, int count) {
                getSharedPreferences("amap_settings", MODE_PRIVATE).edit().putString("amap_api_key", s.toString().trim()).apply();
            }
            @Override public void afterTextChanged(Editable s) {}
        });
        tvResult = findViewById(R.id.tvResult);
        commuteRouteList = findViewById(R.id.commuteRouteList);
        tvCommuteDistance = findViewById(R.id.tvCommuteDistance);
        tvCommuteTime = findViewById(R.id.tvCommuteTime);
        tvCommuteTransfers = findViewById(R.id.tvCommuteTransfers);
        tvCommuteFare = findViewById(R.id.tvCommuteFare);

        homeStationInput.setAdapter(new FuzzyStationAdapter(this, StationUtils.getActiveStationList()));
        homeStationInput.setThreshold(1);

        Button btnGpsHome = findViewById(R.id.btnGpsHome);
        btnGpsHome.setOnClickListener(v -> requestHomeLocation());

        Button btnCalculate = findViewById(R.id.btnCalculate);
        btnCalculate.setOnClickListener(v -> calculate());

        findViewById(R.id.btnBatchCalc).setOnClickListener(v -> {
            startActivity(new Intent(this, CommuteActivity.class));
        });
    }

    /** 请求 GPS 并将最近站点及步行距离回填到家庭位置输入框。 */
    @SuppressLint("SetTextI18n")
    private void requestHomeLocation() {
        if (!LocationHelper.hasLocationPermission(this)) {
            ActivityCompat.requestPermissions(this,
                    new String[]{Manifest.permission.ACCESS_FINE_LOCATION, Manifest.permission.ACCESS_COARSE_LOCATION},
                    REQUEST_LOCATION);
            return;
        }
        tvResult.setText("正在获取 GPS...");
        LocationHelper.getCurrentLocation(this, LocationHelper.LOCATION_TIMEOUT_MS,
                new LocationHelper.OnLocationResult() {
                    @Override
                    public void onLocation(double longitude, double latitude) {
                        homeLon = longitude;
                        homeLat = latitude;
                        LocationHelper.NearbyStation nearest = LocationHelper.findNearestStation(longitude, latitude);
                        runOnUiThread(() -> {
                            if (nearest != null) {
                                homeStationInput.setText(nearest.name);
                                homeDistanceInput.setText(String.valueOf(Math.round(nearest.distanceMeters)));
                                tvResult.setText("已获取家附近最近站：" + nearest.name);
                            } else {
                                tvResult.setText("GPS 已获取，但未找到附近站点");
                            }
                        });
                    }

                    @Override
                    public void onError(String reason) {
                        runOnUiThread(() -> tvResult.setText("定位失败：" + reason));
                    }
                });
    }

    @Override
    public void onRequestPermissionsResult(int requestCode, @NonNull String[] permissions, @NonNull int[] grantResults) {
        super.onRequestPermissionsResult(requestCode, permissions, grantResults);
        if (requestCode == REQUEST_LOCATION && grantResults.length > 0
                && grantResults[0] == PackageManager.PERMISSION_GRANTED) {
            requestHomeLocation();
        }
    }

    /** 校验输入、调用地理编码服务并异步计算通勤路线。 */
    @SuppressLint("SetTextI18n")
    private void calculate() {
        String homeStation = homeStationInput.getText().toString().trim();
        String homeDistanceText = homeDistanceInput.getText().toString().trim();
        String companyAddress = companyAddressInput.getText().toString().trim();

        if (TextUtils.isEmpty(homeStation) && (Double.isNaN(homeLon) || Double.isNaN(homeLat))) {
            Toast.makeText(this, "请先 GPS 获取或输入家最近地铁站", Toast.LENGTH_SHORT).show();
            return;
        }
        String apiKey = amapApiKeyInput.getText().toString().trim();
        if (TextUtils.isEmpty(apiKey)) {
            Toast.makeText(this, "请输入高德 API Key", Toast.LENGTH_SHORT).show();
            return;
        }
        if (TextUtils.isEmpty(companyAddress)) {
            Toast.makeText(this, "请输入公司详细地址", Toast.LENGTH_SHORT).show();
            return;
        }

        double homeWalking = 0;
        if (!TextUtils.isEmpty(homeDistanceText)) {
            try { homeWalking = Double.parseDouble(homeDistanceText); } catch (NumberFormatException ignored) {}
        }

        tvResult.setText("正在解析公司地址...");
        final String finalApiKey = amapApiKeyInput.getText().toString().trim();
        final double finalHomeLon = homeLon;
        final double finalHomeLat = homeLat;
        final double finalHomeWalking = homeWalking;

        new Thread(() -> {
            double[] companyCoord = geocodeAmap(companyAddress, DataCache.getInstance().getKeyCity(), finalApiKey);
            if (companyCoord == null) {
                runOnUiThread(() -> tvResult.setText("公司地址解析失败"));
                return;
            }

            LocationHelper.NearbyStation homeNearest;
            if (!TextUtils.isEmpty(homeStation)) {
                Map<String, double[]> coords = DataCache.getInstance().getStationCoords();
                double[] stationCoord = coords != null ? coords.get(homeStation) : null;
                double homeDist = finalHomeWalking;
                if (stationCoord == null) {
                    runOnUiThread(() -> tvResult.setText("找不到家附近站点：" + homeStation));
                    return;
                }
                homeNearest = new LocationHelper.NearbyStation(homeStation, homeDist);
            } else {
                homeNearest = LocationHelper.findNearestStation(finalHomeLon, finalHomeLat);
            }

            LocationHelper.NearbyStation companyNearest = LocationHelper.findNearestStation(companyCoord[0], companyCoord[1]);
            if (homeNearest == null || companyNearest == null) {
                runOnUiThread(() -> tvResult.setText("未找到附近地铁站"));
                return;
            }

            Map<String, double[]> coords = DataCache.getInstance().getStationCoords();
            double[] homeCoord = coords != null ? coords.get(homeNearest.name) : null;
            double[] companyStationCoord = coords != null ? coords.get(companyNearest.name) : null;
            double stationDistance = (homeCoord != null && companyStationCoord != null)
                    ? CoordinateUtils.haversineDistance(homeCoord[0], homeCoord[1],
                        companyStationCoord[0], companyStationCoord[1])
                    : 0;

            String result;
            if (homeNearest.name.equals(companyNearest.name) || stationDistance <= 0) {
                runOnUiThread(() -> {
                    tvCommuteDistance.setText("0千米");
                    tvCommuteTime.setText("0分钟");
                    tvCommuteTransfers.setText("0次换乘");
                    tvCommuteFare.setText("0元");
                });
                result = "🏠 " + homeNearest.name + " → 🏢 " + companyNearest.name
                        + "\n无需乘坐地铁（请参考步行方案）";
                TripHistory sameStation = new TripHistory(homeNearest.name, companyNearest.name, System.currentTimeMillis());
                sameStation.city = DataCache.getInstance().getKeyCity();
                sameStation.distanceKm = 0;
                sameStation.transfers = 0;
                sameStation.fare = 0;
                sameStation.timeMinutes = 0;
                sameStation.mode = "通勤查询";
                sameStation.stations = new ArrayList<>();
                sameStation.pathNodes = new ArrayList<>();
                sameStation.segments = new ArrayList<>();
                runOnUiThread(() -> commuteRouteList.setAdapter(null));
                new TripHistoryManager(this).addRecord(sameStation);
            } else {
                DataCache cache = DataCache.getInstance();
                MetroGraph graph = new MetroGraph();
                graph.buildGraphs(cache.getMetroData(), cache.getKeyCity());
                SearchWeight weight = new SearchWeight(0, 0, 1000, 1, 0);
                PathResult path = graph.findTransferPath(homeNearest.name, companyNearest.name, weight);
                if (path == null || path.transfers == -1) {
                    result = "两个地铁站之间未找到可用地铁线路";
                    runOnUiThread(() -> commuteRouteList.setAdapter(null));
                } else {
                    List<RouteSegment> segments = graph.getRouteSegments(path.path);
                    double metroDistance = graph.computePathDistance(path.path);
                    double timeMinutes = TimeCalculator.computeTotalTime(path.path, graph, weight, System.currentTimeMillis()) / 60.0;
                    int stationCount = graph.getUniqueStationCount(path.path) - 1;

                    result = "🏠 " + homeNearest.name + "（步行 " + CoordinateUtils.formatDistance(homeNearest.distanceMeters) + "）"
                            + " → 🏢 " + companyNearest.name + "（步行 " + CoordinateUtils.formatDistance(companyNearest.distanceMeters) + "）";
                    final List<RouteSegment> finalSegments = segments;
                    runOnUiThread(() -> {
                        commuteRouteList.setAdapter(new RouteSegmentAdapter(this, finalSegments));
                        tvCommuteDistance.setText(String.format(Locale.US, "%.1f千米", metroDistance));
                        tvCommuteTime.setText(String.format(Locale.US, "%.1f分钟", timeMinutes));
                        tvCommuteTransfers.setText(path.transfers + "次换乘");
                        tvCommuteFare.setText(String.format(Locale.US, "%.0f元", path.totalMoney));
                        WidgetUtils.setListViewHeightBasedOnChildren(commuteRouteList);
                    });

                    TripHistory record = new TripHistory(homeNearest.name, companyNearest.name, System.currentTimeMillis());
                    record.city = cache.getKeyCity();
                    record.distanceKm = metroDistance;
                    record.transfers = path.transfers;
                    record.fare = path.totalMoney;
                    record.timeMinutes = timeMinutes;
                    record.specialMoney = path.specialMoney;
                    record.stationCount = stationCount;
                    record.mode = "通勤查询";
                    record.stations = new ArrayList<>();
                    for (String node : path.path) {
                        String[] parts = node.split("\\|");
                        if (parts.length >= 2) record.stations.add(parts[1]);
                    }
                    record.pathNodes = new ArrayList<>(path.path);
                    record.segments = new ArrayList<>(segments);
                    new TripHistoryManager(this).addRecord(record);
                }
            }

            final String finalResult = result;
            runOnUiThread(() -> tvResult.setText(finalResult));
        }).start();
    }

    private double[] geocodeAmap(String address, String city, String apiKey) {
        HttpURLConnection conn = null;
        try {
            StringBuilder url = new StringBuilder("https://restapi.amap.com/v3/geocode/geo?key=")
                    .append(apiKey)
                    .append("&address=").append(URLEncoder.encode(address, "UTF-8"))
                    .append("&output=JSON");
            if (!TextUtils.isEmpty(city)) url.append("&city=").append(URLEncoder.encode(city, "UTF-8"));

            conn = (HttpURLConnection) new URL(url.toString()).openConnection();
            conn.setConnectTimeout(10000);
            conn.setReadTimeout(10000);
            BufferedReader reader = new BufferedReader(new InputStreamReader(conn.getInputStream(), "UTF-8"));
            StringBuilder response = new StringBuilder();
            String line;
            while ((line = reader.readLine()) != null) response.append(line);

            JSONObject root = new JSONObject(response.toString());
            if (!"1".equals(root.optString("status"))) return null;
            JSONArray geocodes = root.optJSONArray("geocodes");
            if (geocodes == null || geocodes.length() == 0) return null;
            String location = geocodes.getJSONObject(0).optString("location");
            if (TextUtils.isEmpty(location)) return null;
            String[] parts = location.split(",");
            if (parts.length < 2) return null;
            return new double[]{Double.parseDouble(parts[0]), Double.parseDouble(parts[1])};
        } catch (Exception e) {
            return null;
        } finally {
            if (conn != null) conn.disconnect();
        }
    }
}
