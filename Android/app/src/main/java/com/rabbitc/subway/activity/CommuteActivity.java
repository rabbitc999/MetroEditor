package com.rabbitc.subway.activity;

import android.annotation.SuppressLint;
import android.content.Intent;
import android.content.pm.PackageManager;
import android.net.Uri;
import android.os.Bundle;
import android.text.TextUtils;
import android.widget.AutoCompleteTextView;
import android.widget.Button;
import android.widget.ListView;
import android.widget.TextView;
import android.widget.Toast;

import androidx.activity.result.ActivityResultLauncher;
import androidx.activity.result.contract.ActivityResultContracts;
import androidx.annotation.NonNull;

import com.rabbitc.subway.data.PathResult;
import com.rabbitc.subway.data.json.MetroData;
import com.rabbitc.subway.utils.GraphicsViewUtils;
import com.rabbitc.subway.adapter.FuzzyStationAdapter;
import com.rabbitc.subway.data.SearchWeight;
import com.rabbitc.subway.algroithm.TimeCalculator;
import com.rabbitc.subway.data.DataCache;
import com.rabbitc.subway.data.DataLoader;
import com.rabbitc.subway.algroithm.MetroGraph;
import com.rabbitc.subway.R;
import com.rabbitc.subway.adapter.StationListAdapter;
import com.rabbitc.subway.data.StationValue;
import com.rabbitc.subway.utils.StationUtils;
import com.rabbitc.subway.utils.TextMatchUtils;

import java.io.IOException;
import java.io.OutputStream;
import java.util.ArrayList;
import java.util.Collections;
import java.util.Comparator;
import java.util.List;
import java.util.Map;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;
import java.util.concurrent.Future;
import java.util.concurrent.atomic.AtomicInteger;

/**
 * 批量通勤分析页面。
 *
 * <p>以一个起点并行计算到其他站点的距离、耗时、票价和换乘信息，
 * 并支持恢复缓存结果及导出 CSV。</p>
 */
public class CommuteActivity extends BaseActivity {
    private static final int REQUEST_PERMISSION_WRITE = 100;
    private AutoCompleteTextView startInput;
    private Button batchBtn, exportCsvBtn, backBtn;
    private TextView statusText, csvCount;
    private ListView listView;
    private List<String> allStations;
    private List<StationValue> lastBatchData = new ArrayList<>();
    boolean isItemReturn = false;
    private final ActivityResultLauncher<Intent> createDocumentLauncher = registerForActivityResult(
            new ActivityResultContracts.StartActivityForResult(),
            result -> {
                if (result.getResultCode() == RESULT_OK && result.getData() != null) {
                    Uri uri = result.getData().getData();
                    if (uri != null) {
                        writeCsvToUri(uri);
                    }
                }
            });

    private void showCachedData() {
        StationListAdapter adapter = new StationListAdapter(lastBatchData);
        listView.setAdapter(adapter);
    }

    /**
     * 在基础数据就绪后初始化批量查询界面和历史缓存状态。
     *
     * @param savedInstanceState 系统保存的 Activity 状态
     */
    @SuppressLint("SetTextI18n")
    @Override
    protected void onDataReady(Bundle savedInstanceState) {
        setContentView(R.layout.activity_commute);

        initViews();
        setupListeners();

        // 加载数据
        boolean loaded = DataLoader.loadData(this);
        if (!loaded) {
            statusText.setText("❌ 数据加载失败");
            statusText.setTextColor(getColor(R.color.red));
            return;
        }
        DataCache cache = DataCache.getInstance();
        allStations = StationUtils.getActiveStationList();
        setupAutoComplete();
        // 从Intent获取起点
        String start = getIntent().getStringExtra("start_station");
        if (!TextUtils.isEmpty(start) && allStations.contains(start)) {
            startInput.setText(start);
        }
        enableUI();
        statusText.setText("就绪，输入起点后点击批量计算");
        statusText.setTextColor(getColor(R.color.blue));

        // 在 onCreate 中，加载数据之后，设置完 startInput 之后
        if (!TextUtils.isEmpty(start) && allStations.contains(start)) {
            startInput.setText(start);
        }

        // 检查缓存
        String cachedStart = cache.getCachedStartStation();
        List<StationValue> cachedData = cache.getCachedBatchData();
        if (cachedStart != null && cachedData != null && !cachedData.isEmpty()
                && cachedStart.equals(startInput.getText().toString().trim())) {
            // 复用缓存
            lastBatchData = cachedData;
            showCachedData();
            statusText.setText("✅ 已恢复上次计算结果，共 " + cachedData.size() + " 个站点");
            statusText.setTextColor(getColor(R.color.green));
            exportCsvBtn.setEnabled(true);
            csvCount.setText("共 " + cachedData.size() + " 条记录");
        } else {
            // 清空旧缓存
            cache.setCachedBatchData(null);
            cache.setCachedStartStation(null);
            statusText.setText("就绪，输入起点后点击批量计算");
            statusText.setTextColor(getColor(R.color.blue));
        }

        isItemReturn = false;
    }

    private void initViews() {
        startInput = findViewById(R.id.commuteStartInput);
        batchBtn = findViewById(R.id.commuteBatchBtn);
        exportCsvBtn = findViewById(R.id.exportCsvBtn);
        statusText = findViewById(R.id.commuteStatus);
        listView = findViewById(R.id.commuteListView);
        csvCount = findViewById(R.id.csvCount);
        backBtn = findViewById(R.id.backToMainBtn);
    }

    private void setupListeners() {
        backBtn.setOnClickListener(v -> finish());

        batchBtn.setOnClickListener(v -> performBatchQuery());

        exportCsvBtn.setOnClickListener(v -> exportCsv());

        listView.setOnItemClickListener((parent, view, position, id) -> {
            StationValue item = (StationValue) parent.getItemAtPosition(position);
            if (item == null) return;
            // 回传终点
            Intent result = new Intent();
            String start = startInput.getText().toString().trim();
            if (!TextUtils.isEmpty(start)) result.putExtra("start_station", start);
            result.putExtra("end_station", item.station);  // 新增终点
            setResult(RESULT_OK, result);
            isItemReturn = true;
            finish();
        });
    }

    private void setupAutoComplete() {
        FuzzyStationAdapter adapter = new FuzzyStationAdapter(this, allStations);
        startInput.setAdapter(adapter);

        // 输入一个字即触发过滤
        startInput.setThreshold(1);
    }

    /** 使用起点站并行计算其余站点，并在所有任务完成后刷新列表。 */
    @SuppressLint("SetTextI18n")
    private void performBatchQuery() {
        String start = startInput.getText().toString().trim();
        if (TextUtils.isEmpty(start)) {
            statusText.setText("请先输入起点站");
            return;
        }

        GraphicsViewUtils.hideKeyboard(this, getCurrentFocus());
        allStations = StationUtils.getActiveStationList();
        String matchedStart = TextMatchUtils.findBestMatch(start, allStations);
        if (!matchedStart.equals(start)) {
            startInput.setText(matchedStart);
            start = matchedStart;
        }
        if (!allStations.contains(start)) {
            statusText.setText("起点不存在");
            return;
        }

        batchBtn.setEnabled(false);
        batchBtn.setText("计算中...");
        statusText.setText("正在并行计算所有站点...");
        statusText.setTextColor(getColor(R.color.blue));

        String finalStart = start;
        // 移除起点
        List<String> targetStations = new ArrayList<>(allStations);
        targetStations.remove(finalStart);

        int total = targetStations.size();
        AtomicInteger processed = new AtomicInteger(0);
        // 线程安全的列表，或使用 ConcurrentLinkedQueue 最后再转 ArrayList
        List<StationValue> rows = Collections.synchronizedList(new ArrayList<>());

        // 线程池大小：可用处理器核心数，但不大于站点数
        int poolSize = Math.min(Runtime.getRuntime().availableProcessors(), total);
        ExecutorService executor = Executors.newFixedThreadPool(poolSize);

        MetroGraph metroGraph = new MetroGraph();
        DataCache cache = DataCache.getInstance();
        MetroData metroData = cache.getMetroData();
        String keyCity = cache.getKeyCity();
        metroGraph.buildGraphs(metroData, keyCity);
        SearchWeight weight = SearchWeight.fromContext(this);

        List<Future<Void>> futures = new ArrayList<>();
        for (String station : targetStations) {
            Future<Void> future = executor.submit(() -> {
                PathResult result = metroGraph.findTransferPath(finalStart, station, weight);
                String routeDesc = metroGraph.formatRouteWithDirection(result.path);
                double distance = metroGraph.computePathDistance(result.path);
                int stationCount = metroGraph.getUniqueStationCount(result.path) - 1;
                double time = TimeCalculator.computeTotalTime(result.path, metroGraph, weight, 1783296000);
                double value = result.cost;

                StationValue sv = new StationValue(station, distance, result.transfers, value,
                        routeDesc, result.totalMoney, result.specialMoney, time / 60.0, stationCount);
                rows.add(sv);

                // 进度更新（降低频率）
                int current = processed.incrementAndGet();
                int percent = (int) (current * 100.0 / total);
                runOnUiThread(() -> statusText.setText("进度 " + percent + "% （" + current + "/" + total + "）"));
                return null;
            });
            futures.add(future);
        }

        // 所有任务完成后排序并刷新 UI
        executor.execute(() -> {
            try {
                for (Future<Void> f : futures) {
                    f.get(); // 等待全部结束
                }
            } catch (Exception e) {
                e.printStackTrace();
            }
            rows.sort(Comparator.comparingDouble(a -> a.value));

            runOnUiThread(() -> {
                batchBtn.setEnabled(true);
                batchBtn.setText("批量计算");
                lastBatchData = rows;
                statusText.setText("✅ 共 " + rows.size() + " 个可达站点（并行计算）");
                statusText.setTextColor(getColor(R.color.green));
                exportCsvBtn.setEnabled(true);
                csvCount.setText("共 " + rows.size() + " 条记录");
                StationListAdapter adapter = new StationListAdapter(rows);
                listView.setAdapter(adapter);
            });
            executor.shutdown();
        });
    }

    @SuppressLint("DefaultLocale")
    private String buildCsvContent() {
        if (lastBatchData.isEmpty()) {
            Toast.makeText(this, "没有数据可导出", Toast.LENGTH_SHORT).show();
            return "";
        }

        StringBuilder content = new StringBuilder();
        String start = startInput.getText().toString().trim();
        content.append("站点,距离(km),换乘次数,时间(分钟),站数,痛苦度,票价(元),特殊票价(元),乘车指引\n");
        content.append(start).append(",0,0,0,0,0,0,0,无需乘坐地铁\n");
        for (StationValue row : lastBatchData) {
            content.append("\"")
                    .append(row.station).append("\",")
                    .append(String.format("%.3f", row.distance)).append(",")
                    .append(row.transfers).append(",")
                    .append(String.format("%.1f", row.time)).append(",")
                    .append(row.stationCount).append(",")
                    .append(String.format("%.3f", row.value)).append(",")
                    .append(String.format("%.2f", row.fare >= 0 ? row.fare : 0)).append(",")
                    .append(String.format("%.2f", row.specialFare >= 0 ? row.specialFare : 0)).append(",\"")
                    .append(row.routeDesc.replace("\"", "\"\"")).append("\"\n");
        }

        return content.toString();
    }

    private void writeCsvToUri(Uri uri) {
        try (OutputStream os = getContentResolver().openOutputStream(uri)) {
            if (os != null) {
                os.write(buildCsvContent().getBytes());
                Toast.makeText(this, "已保存到 " + uri.getPath(), Toast.LENGTH_LONG).show();
            } else {
                Toast.makeText(this, "保存失败", Toast.LENGTH_SHORT).show();
            }
        } catch (IOException e) {
            Toast.makeText(this, "保存失败: " + e.getMessage(), Toast.LENGTH_SHORT).show();
        }
    }

    public void exportCsv() {
        if (lastBatchData.isEmpty()) {
            Toast.makeText(this, "没有数据可导出", Toast.LENGTH_SHORT).show();
            return;
        }

        String startStation = startInput.getText().toString().trim();
        String city = DataCache.getInstance().getKeyCity();
        String fileName = "从" + startStation + "站的通勤分析（" + city + "地铁）.csv";

        Intent intent = new Intent(Intent.ACTION_CREATE_DOCUMENT);
        intent.addCategory(Intent.CATEGORY_OPENABLE);
        intent.setType("text/csv");
        intent.putExtra(Intent.EXTRA_TITLE, fileName);

        createDocumentLauncher.launch(intent);
    }

    private void enableUI() {
        startInput.setEnabled(true);
        batchBtn.setEnabled(true);
    }

    @Override
    public void onRequestPermissionsResult(int requestCode, @NonNull String[] permissions, @NonNull int[] grantResults) {
        super.onRequestPermissionsResult(requestCode, permissions, grantResults);
        if (requestCode == REQUEST_PERMISSION_WRITE) {
            if (grantResults.length > 0 && grantResults[0] == PackageManager.PERMISSION_GRANTED) {
                exportCsv();
            } else {
                Toast.makeText(this, "需要写入权限才能导出 CSV", Toast.LENGTH_SHORT).show();
            }
        }
    }

    @Override
    public void finish() {
        if (!isItemReturn) {
            Intent result = new Intent();
            String start = startInput.getText().toString().trim();
            if (!TextUtils.isEmpty(start)) result.putExtra("start_station", start);
            setResult(RESULT_OK, result);
        }
        super.finish();
    }

    @Override
    public void onDestroy() {
        super.onDestroy();
        DataCache cache = DataCache.getInstance();

        // 保存当前计算结果到缓存
        if (lastBatchData != null && !lastBatchData.isEmpty()) {
            cache.setCachedBatchData(new ArrayList<>(lastBatchData));
            String start = startInput.getText().toString().trim();
            cache.setCachedStartStation(start);
        } else {
            // 若结果为空，清空缓存
            cache.setCachedBatchData(null);
            cache.setCachedStartStation(null);
        }
    }
}
