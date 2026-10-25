package com.rabbitc.subway.activity;

import android.annotation.SuppressLint;
import android.content.Context;
import android.content.Intent;
import android.database.Cursor;
import android.graphics.Color;
import android.net.Uri;
import android.os.Bundle;
import android.provider.OpenableColumns;
import android.text.TextUtils;
import android.view.Gravity;
import android.view.View;
import android.view.ViewGroup;
import android.widget.ArrayAdapter;
import android.widget.EditText;
import android.widget.LinearLayout;
import android.widget.ListView;
import android.widget.TextView;
import android.widget.Toast;

import androidx.annotation.NonNull;
import androidx.annotation.Nullable;
import androidx.appcompat.app.AlertDialog;
import androidx.appcompat.app.AppCompatActivity;

import com.rabbitc.subway.R;
import com.rabbitc.subway.data.DataCache;
import com.rabbitc.subway.data.DataLoader;
import com.rabbitc.subway.data.MapFileInfo;
import com.rabbitc.subway.network.SyncCallback;
import com.rabbitc.subway.network.TcpSyncClient;
import com.rabbitc.subway.network.UdpDiscoveryResponder;

import java.io.File;
import java.io.InputStream;
import java.io.OutputStream;
import java.util.List;

/**
 * 地图文件管理：列出可用地图、导入、导出、删除、重命名（内部文件）。
 */
public class MapManageActivity extends AppCompatActivity {

    // ---------- 常量 ----------
    private static final int REQUEST_IMPORT = 1001;
    private static final int REQUEST_EXPORT = 1002;

    // ---------- 视图 ----------
    private ListView listView;
    private MapAdapter adapter;

    // ---------- 数据 ----------
    private List<MapFileInfo> mapList;
    private int selectedPosition = -1; // -1 表示未选中
    private TcpSyncClient tcpSyncClient;
    private UdpDiscoveryResponder udpDiscoveryResponder;

    // ---------- 生命周期 ----------
    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        setContentView(R.layout.activity_map_manage);

        initViews();
        setupListeners();
        refreshList();
    }

    @Override
    protected void onActivityResult(int requestCode, int resultCode, Intent data) {
        super.onActivityResult(requestCode, resultCode, data);
        if (resultCode == RESULT_OK && data != null) {
            Uri uri = data.getData();
            if (requestCode == REQUEST_IMPORT) {
                handleImport(uri);
            } else if (requestCode == REQUEST_EXPORT) {
                handleExport(uri);
            }
        }
    }

    // ---------- 初始化 ----------
    private void initViews() {
        listView = findViewById(R.id.listView);
        findViewById(R.id.backMainBtn).setOnClickListener(v -> finish());
        findViewById(R.id.btn_import).setOnClickListener(v -> importMap());
        findViewById(R.id.btn_export).setOnClickListener(v -> exportSelected());
        findViewById(R.id.btn_refresh).setOnClickListener(v -> refreshList());
    }

    private void setupListeners() {
        // 单击：选中/取消选中
        listView.setOnItemClickListener((parent, view, position, id) -> {
            if (selectedPosition == position) {
                selectedPosition = -1;
            } else {
                selectedPosition = position;
            }
            adapter.setSelectedPosition(selectedPosition);
            adapter.notifyDataSetChanged();
        });

        // 长按：弹出操作菜单（重命名 或 删除）
        listView.setOnItemLongClickListener((parent, view, position, id) -> {
            MapFileInfo info = mapList.get(position);
            if (!info.isInternal) {
                Toast.makeText(this, "内置地图不可操作", Toast.LENGTH_SHORT).show();
                return true;
            }
            showOperationDialog(info);
            return true;
        });

        findViewById(R.id.syncBtn).setOnClickListener(v -> showSyncDialog());
    }

    // ---------- 操作对话框：重命名 / 删除 ----------
    private void showOperationDialog(MapFileInfo info) {
        String[] options = {"重命名", "删除"};
        new AlertDialog.Builder(this)
                .setTitle("操作文件: " + info.fileName)
                .setItems(options, (dialog, which) -> {
                    if (which == 0) {
                        showRenameDialog(info);
                    } else {
                        showDeleteConfirmDialog(info);
                    }
                })
                .setNegativeButton("取消", null)
                .show();
    }

    // ---------- 重命名对话框 ----------
    private void showRenameDialog(MapFileInfo info) {
        File oldFile = new File(DataLoader.getMapsDir(this), info.fileName);
        if (!oldFile.exists()) {
            Toast.makeText(this, "文件不存在，请刷新", Toast.LENGTH_SHORT).show();
            refreshList();
            return;
        }

        // 输入框
        EditText input = new EditText(this);
        input.setText(info.fileName);
        input.setSelection(info.fileName.lastIndexOf('.')); // 光标定位到扩展名前

        new AlertDialog.Builder(this)
                .setTitle("重命名")
                .setView(input)
                .setPositiveButton("确定", (dialog, which) -> {
                    String newName = input.getText().toString().trim();
                    if (TextUtils.isEmpty(newName)) {
                        Toast.makeText(this, "文件名不能为空", Toast.LENGTH_SHORT).show();
                        return;
                    }
                    // 检查扩展名合法性（必须 .json 或 .bmetro）
                    if (!newName.endsWith(".json") && !newName.endsWith(".bmetro")) {
                        Toast.makeText(this, "文件名必须以 .json 或 .bmetro 结尾", Toast.LENGTH_SHORT).show();
                        return;
                    }
                    // 检查是否与现有文件重名（排除自身）
                    File newFile = new File(DataLoader.getMapsDir(this), newName);
                    if (newFile.exists()) {
                        Toast.makeText(this, "文件已存在，请换一个名称", Toast.LENGTH_SHORT).show();
                        return;
                    }
                    // 执行重命名
                    boolean success = oldFile.renameTo(newFile);
                    if (success) {
                        Toast.makeText(this, "重命名成功", Toast.LENGTH_SHORT).show();
                        // 如果当前缓存的地图正好是这个文件，需要清除缓存并重新加载
                        DataCache cache = DataCache.getInstance();
                        if (cache.getJsonData() != null && cache.getJsonData().contains(oldFile.getName())) {
                            // 简单处理：重新加载数据（可选择加载新文件）
                            cache.clear();
                            DataLoader.loadMapByName(this, newName);
                        }
                        refreshList();
                    } else {
                        Toast.makeText(this, "重命名失败", Toast.LENGTH_SHORT).show();
                    }
                })
                .setNegativeButton("取消", null)
                .show();
    }

    // ---------- 列表刷新 ----------
    private void refreshList() {
        mapList = DataLoader.getAvailableMaps(this);
        if (adapter == null) {
            adapter = new MapAdapter(mapList);
            listView.setAdapter(adapter);
        } else {
            adapter.updateData(mapList);
        }
        // 检查选中位置是否有效
        if (selectedPosition >= mapList.size()) {
            selectedPosition = -1;
        }
        adapter.setSelectedPosition(selectedPosition);
        adapter.notifyDataSetChanged();
    }

    // ---------- 导入导出 ----------
    private void importMap() {
        Intent intent = new Intent(Intent.ACTION_OPEN_DOCUMENT);
        intent.addCategory(Intent.CATEGORY_OPENABLE);
        intent.setType("*/*");
        intent.putExtra(Intent.EXTRA_MIME_TYPES, new String[]{"application/json", "application/octet-stream"});
        startActivityForResult(intent, REQUEST_IMPORT);
    }

    private void exportSelected() {
        if (selectedPosition == -1 || selectedPosition >= mapList.size()) {
            Toast.makeText(this, "请先单击选中一个内部地图文件", Toast.LENGTH_SHORT).show();
            return;
        }
        MapFileInfo info = mapList.get(selectedPosition);
        if (!info.isInternal) {
            Toast.makeText(this, "只能导出内部保存的地图", Toast.LENGTH_SHORT).show();
            return;
        }
        File file = new File(DataLoader.getMapsDir(this), info.fileName);
        if (!file.exists()) {
            Toast.makeText(this, "文件已被删除，请刷新列表", Toast.LENGTH_SHORT).show();
            return;
        }
        Intent intent = new Intent(Intent.ACTION_CREATE_DOCUMENT);
        intent.addCategory(Intent.CATEGORY_OPENABLE);
        // 根据扩展名设置 MIME
        String mime = info.fileName.endsWith(".bmetro") ? "application/octet-stream" : "application/json";
        intent.setType(mime);
        intent.putExtra(Intent.EXTRA_TITLE, info.fileName);
        startActivityForResult(intent, REQUEST_EXPORT);
    }

    // ---------- 处理导入导出结果 ----------
    private void handleImport(Uri uri) {
        if (uri == null) return;
        try (InputStream is = getContentResolver().openInputStream(uri)) {
            String fileName = getFileNameFromUri(this, uri);
            if (fileName == null || !fileName.endsWith(".json")) {
                fileName = "imported_" + System.currentTimeMillis() + ".json";
            }
            boolean success = DataLoader.importMapFromStream(this, is, fileName);
            if (success) {
                Toast.makeText(this, "导入成功，正在加载...", Toast.LENGTH_SHORT).show();
                DataLoader.loadMapByName(this, fileName);
            } else {
                Toast.makeText(this, "导入失败，格式错误", Toast.LENGTH_SHORT).show();
            }
            refreshList();
        } catch (Exception e) {
            e.printStackTrace();
            Toast.makeText(this, "导入异常: " + e.getMessage(), Toast.LENGTH_SHORT).show();
        }
    }

    private void handleExport(Uri uri) {
        if (selectedPosition == -1 || selectedPosition >= mapList.size()) {
            Toast.makeText(this, "未选中有效文件", Toast.LENGTH_SHORT).show();
            return;
        }
        MapFileInfo info = mapList.get(selectedPosition);
        if (info == null || !info.isInternal) {
            Toast.makeText(this, "未选中有效文件", Toast.LENGTH_SHORT).show();
            return;
        }
        if (uri == null) {
            Toast.makeText(this, "保存位置无效", Toast.LENGTH_SHORT).show();
            return;
        }
        try (OutputStream os = getContentResolver().openOutputStream(uri)) {
            if (os == null) {
                Toast.makeText(this, "无法创建输出流，请检查存储权限", Toast.LENGTH_SHORT).show();
                return;
            }
            boolean success = DataLoader.exportMapToStream(this, info.fileName, os);
            if (success) {
                Toast.makeText(this, "导出成功", Toast.LENGTH_SHORT).show();
            } else {
                Toast.makeText(this, "导出失败，文件可能损坏", Toast.LENGTH_SHORT).show();
            }
        } catch (Exception e) {
            e.printStackTrace();
            Toast.makeText(this, "导出异常: " + e.getMessage(), Toast.LENGTH_SHORT).show();
        }
    }

    // ---------- 删除确认对话框 ----------
    private void showDeleteConfirmDialog(MapFileInfo info) {
        new AlertDialog.Builder(this)
                .setTitle("删除地图")
                .setMessage("确定要删除地图文件 \"" + info.fileName + "\" 吗？")
                .setPositiveButton("确定", (dialog, which) -> {
                    boolean deleted = DataLoader.deleteMapFile(this, info.fileName);
                    if (deleted) {
                        Toast.makeText(this, "已删除: " + info.fileName, Toast.LENGTH_SHORT).show();
                        // 如果删除的是当前地图，清除缓存并重新加载
                        DataCache cache = DataCache.getInstance();
                        cache.clear();
                        if (!DataLoader.loadData(this)) {
                            Toast.makeText(this, "无可用地图，请导入", Toast.LENGTH_LONG).show();
                        }
                        refreshList();
                    } else {
                        Toast.makeText(this, "删除失败", Toast.LENGTH_SHORT).show();
                    }
                })
                .setNegativeButton("取消", null)
                .show();
    }

    // ---------- 工具方法 ----------

    /**
     * 从 Uri 中提取文件名。
     *
     * <p>优先通过 {@link android.provider.OpenableColumns#DISPLAY_NAME} 查询内容提供者
     * 获取显示名称；如果查询失败，则从 Uri 的路径中提取最后一段作为文件名。
     *
     * @param context 上下文
     * @param uri     文件 URI
     * @return 提取到的文件名，如果无法获取则返回 {@code null}
     */
    public static String getFileNameFromUri(Context context, Uri uri) {
        String fileName = null;
        if (uri.getScheme().equals("content")) {
            try (Cursor cursor = context.getContentResolver().query(uri, null, null, null, null)) {
                if (cursor != null && cursor.moveToFirst()) {
                    int nameIndex = cursor.getColumnIndex(OpenableColumns.DISPLAY_NAME);
                    if (nameIndex != -1) {
                        fileName = cursor.getString(nameIndex);
                    }
                }
            } catch (Exception e) {
                e.printStackTrace();
            }
        }
        if (fileName == null) {
            String path = uri.getPath();
            if (path != null) {
                int lastSlash = path.lastIndexOf('/');
                if (lastSlash >= 0) {
                    fileName = path.substring(lastSlash + 1);
                }
            }
        }
        return fileName;
    }

    // ---------- 内部适配器 ----------
    private class MapAdapter extends ArrayAdapter<MapFileInfo> {
        private int selectedPos = -1;

        public MapAdapter(List<MapFileInfo> data) {
            super(MapManageActivity.this, android.R.layout.simple_list_item_1, data);
        }

        public void setSelectedPosition(int pos) {
            selectedPos = pos;
        }

        public void updateData(List<MapFileInfo> newData) {
            clear();
            addAll(newData);
            notifyDataSetChanged();
        }

        @SuppressLint("SetTextI18n")
        @NonNull
        @Override
        public View getView(int position, @Nullable View convertView, @NonNull ViewGroup parent) {
            View view = super.getView(position, convertView, parent);
            TextView textView = view.findViewById(android.R.id.text1);
            MapFileInfo info = getItem(position);
            String prefix = info.isInternal ? "[已保存] " : "[内置] ";
            String indicator = (selectedPos == position && info.isInternal) ? " ✓" : "";
            textView.setText(prefix + info.fileName + indicator);

            // 选中高亮
            if (selectedPos == position && info.isInternal) {
                view.setBackgroundColor(0xFFCCCCCC);
            } else {
                view.setBackgroundColor(0x00000000);
            }
            return view;
        }
    }


    // ======================== TCP 设备同步 ========================

    /**
     * 显示设备同步对话框，输入服务端 IP 和端口后连接。
     */
    @SuppressLint("SetTextI18n")
    private void showSyncDialog() {
        LinearLayout layout = new LinearLayout(this);
        layout.setOrientation(LinearLayout.VERTICAL);
        layout.setPadding(dpToPx(20), dpToPx(16), dpToPx(20), dpToPx(8));

        // 状态提示
        TextView tvStatus = new TextView(this);
        tvStatus.setTextSize(12);
        tvStatus.setPadding(0, 0, 0, dpToPx(12));

        if (tcpSyncClient != null && tcpSyncClient.isConnected()) {
            tvStatus.setText("已连接: " + tcpSyncClient.getServerName());
            tvStatus.setTextColor(Color.parseColor("#2E8B57"));
        } else {
            tvStatus.setText("未连接，请输入 MetroEditor 的 IP 和端口");
            tvStatus.setTextColor(Color.parseColor("#666666"));
        }
        layout.addView(tvStatus);

        // IP 输入
        LinearLayout ipRow = createLabeledInput("IP地址", "192.168.1.100");
        layout.addView(ipRow);

        // 端口输入
        LinearLayout portRow = createLabeledInput("端口", "28080");
        layout.addView(portRow);

        // 日志区
        TextView tvLog = new TextView(this);
        tvLog.setTextSize(12);
        tvLog.setTextColor(Color.parseColor("#999999"));
        tvLog.setPadding(0, dpToPx(12), 0, 0);
        tvLog.setVisibility(View.GONE);
        layout.addView(tvLog);

        AlertDialog.Builder builder = new AlertDialog.Builder(this);
        builder.setTitle("设备同步");

        // 根据连接状态显示不同按钮
        if (tcpSyncClient != null && tcpSyncClient.isConnected()) {
            builder.setPositiveButton("断开", (dialog, which) -> {
                if (tcpSyncClient != null) {
                    tcpSyncClient.disconnect();
                    Toast.makeText(this, "已断开连接", Toast.LENGTH_SHORT).show();
                }
            });
            builder.setNegativeButton("关闭", null);
        } else {
            builder.setPositiveButton("连接", (dialog, which) -> {
                EditText etIp = (EditText) ipRow.getChildAt(1);
                EditText etPort = (EditText) portRow.getChildAt(1);
                String host = etIp.getText().toString().trim();
                String portStr = etPort.getText().toString().trim();

                if (TextUtils.isEmpty(host)) {
                    Toast.makeText(this, "请输入 IP 地址", Toast.LENGTH_SHORT).show();
                    return;
                }

                int port;
                try {
                    port = Integer.parseInt(portStr);
                    if (port <= 0 || port > 65535) throw new NumberFormatException();
                } catch (NumberFormatException e) {
                    Toast.makeText(this, "端口无效", Toast.LENGTH_SHORT).show();
                    return;
                }

                startTcpSync(host, port);
            });
            builder.setNegativeButton("取消", null);
        }

        builder.setView(layout);
        builder.show();
    }

    /**
     * 启动 UDP 设备发现监听 — 让电脑端（MetroEditor）自动搜索到本机，
     * 收到电脑端连接指令后自动建立 TCP 连接。
     */
    private void startUdpDiscovery() {
        udpDiscoveryResponder = new UdpDiscoveryResponder((serverIp, serverPort) -> {
            if (tcpSyncClient != null && tcpSyncClient.isConnected()) {
                Toast.makeText(MapManageActivity.this,
                        "已连接到电脑端，无需重复连接", Toast.LENGTH_SHORT).show();
                return;
            }
            Toast.makeText(MapManageActivity.this,
                    "电脑端 (" + serverIp + ") 发起连接...", Toast.LENGTH_SHORT).show();
            startTcpSync(serverIp, serverPort);
        });
        udpDiscoveryResponder.start();
    }

    /**
     * 启动 TCP 同步客户端并连接到服务端。
     */
    private void startTcpSync(String host, int port) {
        if (tcpSyncClient != null) {
            tcpSyncClient.disconnect();
        }

        tcpSyncClient = new TcpSyncClient(this, new SyncCallback() {
            @Override
            public void onConnected(String serverName) {
                Toast.makeText(MapManageActivity.this,
                        "已连接到电脑端: " + serverName, Toast.LENGTH_LONG).show();
            }

            @Override
            public void onDisconnected(String reason) {
                Toast.makeText(MapManageActivity.this, "与电脑端断开连接: " + reason, Toast.LENGTH_SHORT).show();
            }

            @Override
            public void onDataSynced(String fileName, boolean success, String message) {
                if (success) {
                    Toast.makeText(MapManageActivity.this, "同步成功: " + fileName, Toast.LENGTH_LONG).show();
                    // 重新加载地图数据
                    DataLoader.loadMapByName(MapManageActivity.this, fileName);
                    rebuildAfterSync();
                } else {
                    Toast.makeText(MapManageActivity.this, "同步失败: " + message, Toast.LENGTH_LONG).show();
                }
            }

            @Override
            public void onLog(String message) {
                // 可选：记录日志，此处不显示
            }
        });

        tcpSyncClient.connect(host, port);
        Toast.makeText(this, "正在连接 " + host + ":" + port + "...", Toast.LENGTH_SHORT).show();
    }

    /**
     * 数据同步后重新构建地铁图和界面。
     */
    private void rebuildAfterSync() {
        DataCache cache = DataCache.getInstance();
        Toast.makeText(this, "已重新加载" + cache.getKeyCity() + "地铁", Toast.LENGTH_LONG).show();
    }

    /**
     * 页面销毁时清理线程池资源。
     */
    @Override
    protected void onDestroy() {
        super.onDestroy();

        // 停止 UDP 设备发现监听
        if (udpDiscoveryResponder != null) {
            udpDiscoveryResponder.stop();
            udpDiscoveryResponder = null;
        }
        // 断开 TCP 同步客户端
        if (tcpSyncClient != null) {
            tcpSyncClient.disconnect();
            tcpSyncClient = null;
        }
    }

    /**
     * 创建一个水平布局：左侧固定宽度标签 + 右侧输入框
     */
    @SuppressLint("SetTextI18n")
    private LinearLayout createLabeledInput(String labelText, String defaultValue) {
        LinearLayout row = new LinearLayout(this);
        row.setOrientation(LinearLayout.HORIZONTAL);
        row.setPadding(0, dpToPx(4), 0, dpToPx(4));
        row.setGravity(Gravity.CENTER_VERTICAL);

        // 左侧标签（固定宽度，右对齐）
        TextView label = new TextView(this);
        label.setText(labelText + "：");
        label.setTextSize(14);
        label.setTextColor(Color.parseColor("#333333"));
        label.setWidth(dpToPx(60));  // 固定宽度，保证对齐
        label.setGravity(Gravity.END | Gravity.CENTER_VERTICAL);
        label.setPadding(0, 0, dpToPx(8), 0);
        row.addView(label);

        // 右侧输入框
        EditText editText = new EditText(this);
        editText.setText(defaultValue != null ? defaultValue : "");
        editText.setHint("请输入" + labelText);
        editText.setSingleLine(true);
        editText.setLayoutParams(new LinearLayout.LayoutParams(
                0,
                ViewGroup.LayoutParams.WRAP_CONTENT,
                1.0f
        ));
        row.addView(editText);

        return row;
    }

    private int dpToPx(int dp) {
        return (int) (dp * getResources().getDisplayMetrics().density + 0.5f);
    }
}