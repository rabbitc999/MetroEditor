package com.rabbitc.subway.data;

import android.content.Context;
import android.content.SharedPreferences;
import android.util.Log;

import com.rabbitc.subway.utils.JsonFileUtils;

import java.io.ByteArrayInputStream;
import java.io.ByteArrayOutputStream;
import java.io.File;
import java.io.FileInputStream;
import java.io.FileOutputStream;
import java.io.IOException;
import java.io.InputStream;
import java.io.OutputStream;
import java.nio.charset.StandardCharsets;
import java.util.ArrayList;
import java.util.Collections;
import java.util.Comparator;
import java.util.List;
import java.util.zip.GZIPInputStream;
import java.util.zip.Inflater;
import java.util.zip.InflaterInputStream;

/**
 * 地图数据加载器，负责从 assets 和内部存储中加载地铁数据文件。
 *
 * <p>支持两种文件格式：</p>
 * <ul>
 *   <li><b>.json</b> — 纯文本 JSON 数据文件</li>
 *   <li><b>.bmetro</b> — GZip/deflate 压缩的二进制数据文件</li>
 * </ul>
 *
 * <p>加载策略（按优先级）：</p>
 * <ol>
 *   <li>如果 {@link DataCache} 中已有有效缓存，直接返回</li>
 *   <li>尝试加载上次使用的地图文件（通过 SharedPreferences 记录）</li>
 *   <li>按文件名字典序尝试内部存储中的地图文件</li>
 *   <li>按文件名字典序尝试 assets 中的地图文件</li>
 * </ol>
 *
 * <p>该类的所有方法均为静态方法，不可实例化。</p>
 *
 * @see DataCache
 * @see DataParser
 */
public final class DataLoader {

    private static final String TAG = "DataLoader";
    private static final String PREF_NAME = "subway_weights";
    private static final String KEY_LAST_MAP_FILE = "last_map_file";
    private static final String MAPS_DIR = "maps";

    private DataLoader() {}

    // ======================== 公共入口 ========================

    /**
     * 加载地铁地图数据，是数据加载的主入口。
     *
     * <p>按优先级依次尝试：缓存验证、上次使用的地图文件、内部存储文件（按名字典序）、
     * assets 文件（按名字典序）。任一成功即返回 {@code true}。</p>
     *
     * @param context Android 上下文，用于访问 SharedPreferences 和文件系统
     * @return {@code true} 如果数据加载成功，{@code false} 如果所有来源均加载失败
     */
    public static boolean loadData(Context context) {
        DataCache cache = DataCache.getInstance();
        // 先复用进程内缓存，避免每次进入页面都重复读取和解析地图文件。
        if (cache.isCacheValid()) {
            return true;
        }

        SharedPreferences prefs = context.getSharedPreferences(PREF_NAME, Context.MODE_PRIVATE);
        String lastFile = prefs.getString(KEY_LAST_MAP_FILE, null);
        // 优先恢复用户上次选择的地图；文件损坏时再继续尝试其他来源。
        if (lastFile != null && loadMapByName(context, lastFile)) {
            return true;
        }

        File mapsDir = getMapsDir(context);
        File[] internalFiles = mapsDir.listFiles((dir, name) ->
                name.endsWith(".json") || name.endsWith(".bmetro"));
        if (internalFiles != null && internalFiles.length > 0) {
            List<File> fileList = new ArrayList<>();
            Collections.addAll(fileList, internalFiles);
            fileList.sort(Comparator.comparing(File::getName));
            String first = fileList.get(0).getName();
            // 内部存储文件按稳定顺序选择，保证没有历史记录时加载结果可预测。
            if (loadMapByName(context, first)) {
                return true;
            }
        }

        List<String> assetFiles = getAssetFiles(context);
        if (!assetFiles.isEmpty()) {
            return loadMapByName(context, assetFiles.get(0));
        }

        return false;
    }

    // ======================== 文件操作 ========================

    /**
     * 将 JSON 字符串保存为地图文件到内部存储的 maps 目录。
     *
     * @param context  Android 上下文
     * @param fileName 文件名（如 "beijing.json"）
     * @param json     JSON 格式的地图数据字符串
     */
    public static void saveMapToInternalStorage(Context context, String fileName, String json) {
        File mapsDir = getMapsDir(context);
        if (!mapsDir.exists() && !mapsDir.mkdirs()) {
            Log.e(TAG, "无法创建地图目录: " + mapsDir.getAbsolutePath());
            return;
        }
        File destFile = new File(mapsDir, fileName);
        try (FileOutputStream fos = new FileOutputStream(destFile)) {
            fos.write(json.getBytes());
            fos.flush();
            Log.d(TAG, "保存地图成功: " + fileName);
        } catch (Exception e) {
            Log.e(TAG, "保存地图失败: " + fileName, e);
        }
    }

    /**
     * 将原始字节数组保存为地图文件到内部存储的 maps 目录。
     *
     * <p>主要用于保存 .bmetro 格式的压缩数据文件。</p>
     *
     * @param context  Android 上下文
     * @param fileName 文件名（如 "beijing.bmetro"）
     * @param data     原始字节数据
     */
    // 新增：保存字节数组（用于 .bmetro）
    public static void saveMapBytesToInternalStorage(Context context, String fileName, byte[] data) {
        File mapsDir = getMapsDir(context);
        if (!mapsDir.exists() && !mapsDir.mkdirs()) {
            Log.e(TAG, "无法创建地图目录: " + mapsDir.getAbsolutePath());
            return;
        }
        File destFile = new File(mapsDir, fileName);
        try (FileOutputStream fos = new FileOutputStream(destFile)) {
            fos.write(data);
            fos.flush();
            Log.d(TAG, "保存地图（二进制）成功: " + fileName);
        } catch (Exception e) {
            Log.e(TAG, "保存地图（二进制）失败: " + fileName, e);
        }
    }

    /**
     * 将地图文件导出到指定的输出流。
     *
     * <p>可用于将内部存储的地图文件导出到外部存储或通过网络发送。</p>
     *
     * @param context     Android 上下文
     * @param fileName    要导出的文件名
     * @param outputStream 目标输出流（调用方负责关闭）
     * @return {@code true} 如果导出成功，{@code false} 如果文件不存在或读写异常
     */
    public static boolean exportMapToStream(Context context, String fileName, OutputStream outputStream) {
        File srcFile = new File(getMapsDir(context), fileName);
        if (!srcFile.exists()) {
            Log.e(TAG, "导出文件不存在: " + srcFile.getAbsolutePath());
            return false;
        }
        try (FileInputStream fis = new FileInputStream(srcFile)) {
            byte[] buffer = new byte[4096];
            int length;
            while ((length = fis.read(buffer)) > 0) {
                outputStream.write(buffer, 0, length);
            }
            outputStream.flush();
            Log.d(TAG, "导出成功: " + fileName);
            return true;
        } catch (Exception e) {
            Log.e(TAG, "导出异常", e);
            return false;
        }
    }

    /**
     * 从输入流导入地图文件到内部存储。
     *
     * <p>自动检测数据格式：</p>
     * <ul>
     *   <li>GZip 压缩格式（文件头为 {@code 0x1F 0x8B}）→ 保存为 .bmetro 格式</li>
     *   <li>纯 JSON 文本 → 保存为 .json 格式</li>
     * </ul>
     * <p>若文件名后缀与实际内容格式不符，会自动修正文件名。</p>
     *
     * @param context     Android 上下文
     * @param inputStream 输入流（调用方负责关闭）
     * @param fileName    目标文件名
     * @return {@code true} 如果导入成功，{@code false} 如果数据为空或解析失败
     */
    public static boolean importMapFromStream(Context context, InputStream inputStream, String fileName) {
        try {
            // 读取全部字节
            byte[] allBytes = readBytesFromStream(inputStream);
            if (allBytes == null || allBytes.length == 0) {
                Log.e(TAG, "导入文件为空");
                return false;
            }

            // 判断是否为 GZip 压缩格式
            boolean isGzip = allBytes.length >= 2 &&
                    allBytes[0] == (byte) 0x1F && allBytes[1] == (byte) 0x8B;

            // 文件名只用于展示，实际格式以文件头为准，避免后缀错误导致无法加载。
            // 如果文件名后缀与内容不符，自动修正（可选）
            String actualFileName = fileName;
            if (isGzip && !fileName.endsWith(".bmetro")) {
                // 若内容是压缩但后缀不是 .bmetro，可强制改为 .bmetro
                // 此处保留原文件名，但为了统一，建议改为 .bmetro
                actualFileName = fileName.replaceAll("\\.json$", "") + ".bmetro";
                Log.d(TAG, "检测到压缩内容，文件名自动修正为: " + actualFileName);
            } else if (!isGzip && fileName.endsWith(".bmetro")) {
                // 若内容是纯 JSON 但后缀是 .bmetro，则修正为 .json
                actualFileName = fileName.replaceAll("\\.bmetro$", "") + ".json";
                Log.d(TAG, "检测到纯 JSON，文件名自动修正为: " + actualFileName);
            } else {
                actualFileName = fileName;
            }

            if (isGzip) {
                // 保存为二进制（保持压缩）
                saveMapBytesToInternalStorage(context, actualFileName, allBytes);
            } else {
                // 保存为文本 JSON（先做基本校验）
                String json = new String(allBytes, "UTF-8");
                new org.json.JSONObject(json); // 简单校验
                saveMapToInternalStorage(context, actualFileName, json);
            }
            Log.d(TAG, "导入成功: " + actualFileName);
            return true;
        } catch (Exception e) {
            Log.e(TAG, "导入失败", e);
            return false;
        }
    }

    /**
     * 删除内部存储中的指定地图文件。
     *
     * @param context  Android 上下文
     * @param fileName 要删除的文件名
     * @return {@code true} 如果文件存在且删除成功，{@code false} 否则
     */
    public static boolean deleteMapFile(Context context, String fileName) {
        File file = new File(getMapsDir(context), fileName);
        if (file.exists()) {
            boolean deleted = file.delete();
            if (deleted) {
                Log.d(TAG, "删除成功: " + fileName);
            } else {
                Log.e(TAG, "删除失败: " + fileName);
            }
            return deleted;
        }
        return false;
    }

    // ======================== 查询 ========================

    /**
     * 获取内部存储中存放地图文件的 maps 目录。
     *
     * @param context Android 上下文
     * @return maps 目录的 File 对象
     */
    public static File getMapsDir(Context context) {
        return new File(context.getFilesDir(), MAPS_DIR);
    }

    /**
     * 获取所有可用的地图文件列表，包括内部存储文件和 assets 中文件。
     *
     * <p>���果按文件名字典序排列，内部存储文件优先于同名的 assets 文件。</p>
     *
     * @param context Android 上下文
     * @return 可用地图文件信息列表
     * @see MapFileInfo
     */
    public static List<MapFileInfo> getAvailableMaps(Context context) {
        List<MapFileInfo> result = new ArrayList<>();

        // 内部存储文件（.json 和 .bmetro）
        File mapsDir = getMapsDir(context);
        File[] internalFiles = mapsDir.listFiles((dir, name) ->
                name.endsWith(".json") || name.endsWith(".bmetro"));
        if (internalFiles != null) {
            for (File f : internalFiles) {
                result.add(new MapFileInfo(f.getName(), true, f.length()));
            }
        }

        // assets 文件（仅 .json，可扩展）
        List<String> assetFiles = getAssetFiles(context);
        for (String name : assetFiles) {
            boolean exists = false;
            for (MapFileInfo info : result) {
                if (info.fileName.equals(name)) {
                    exists = true;
                    break;
                }
            }
            if (!exists) {
                result.add(new MapFileInfo(name, false, 0));
            }
        }

        result.sort(Comparator.comparing(a -> a.fileName));
        return result;
    }

    // ======================== 核心加载 ========================

    /**
     * 按文件名加载地图数据并进行解析缓存。
     *
     * <p>按优先级尝试：</p>
     * <ol>
     *   <li>从内部存储加载（.bmetro → 解压 → 解析；.json → 直接解析）</li>
     *   <li>从 assets 加载（.bmetro → 解压 → 解析后自动保存到内部存储；.json → 直接解析后自动保存）</li>
     * </ol>
     * <p>从 assets 加载成功时会自动保存到内部存储并记录为上次使用文件。</p>
     *
     * @param context  Android 上下文
     * @param fileName 地图文件名
     * @return {@code true} 如果加载并解析成功，{@code false} 否则
     * @see DataParser#parse()
     */
    public static boolean loadMapByName(Context context, String fileName) {
        File internalFile = new File(getMapsDir(context), fileName);
        String json = null;

        // 先查内部存储；用户导入的数据应覆盖同名的内置地图。
        // 1. 尝试从内部存储加载
        if (internalFile.exists()) {
            try {
                if (fileName.endsWith(".bmetro")) {
                    // 压缩格式：读取字节并解压
                    byte[] compressed = readBytesFromFile(internalFile);
                    json = decompressGzip(compressed);
                } else {
                    // JSON 文本直接读取
                    json = JsonFileUtils.readStringFromFile(internalFile);
                }
                if (parseAndCache(json, context, fileName)) {
                    return true;
                }
            } catch (Exception e) {
            }
        }

        // 内部文件不存在或解析失败时，再从 assets 读取并缓存一份副本。
        // 2. 尝试从 assets 加载（只有 .json）
        if (fileName.endsWith(".json")) {
            try {
                InputStream is = context.getAssets().open("map/" + fileName);
                json = JsonFileUtils.readStringFromInputStream(is);
                if (parseAndCache(json, context, fileName)) {
                    // 自动保存到内部存储（保持原格式）
                    saveMapToInternalStorage(context, fileName, json);
                    saveLastMapFile(context, fileName);
                    return true;
                }
            } catch (Exception e) {
                Log.e(TAG, "从 assets 加载失败", e);
            }
        } else if (fileName.endsWith(".bmetro")) {
            try {
                InputStream is = context.getAssets().open("map/" + fileName);
                byte[] jsonBytes = JsonFileUtils.readBytesFromInputStream(is);
                json = decompressGzip(jsonBytes);
                if (parseAndCache(json, context, fileName)) {
                    // 自动保存到内部存储（保持原格式）
                    saveMapToInternalStorage(context, fileName, json);
                    saveLastMapFile(context, fileName);
                    return true;
                }
            } catch (Exception e) {
                Log.e(TAG, "从 assets 加载失败", e);
            }
        }

        return false;
    }

    // ======================== 私有辅助 ========================

    /**
     * 解析 JSON 数据并填充缓存，同时记录使用的文件名到 SharedPreferences。
     *
     * @param json     JSON 格式的地图数据字符串
     * @param context  Android 上下文
     * @param fileName 当前使用的文件名
     * @return {@code true} 如果解析成功，{@code false} 否则
     */
    private static boolean parseAndCache(String json, Context context, String fileName) {
        try {
            DataCache cache = DataCache.getInstance();
            // 解析前清理旧地图，防止新旧城市的索引混在一起。
            cache.clear();
            cache.setJsonData(json);
            DataParser.parse();
            // 只有完整解析成功后才记录“上次使用文件”。
            saveLastMapFile(context, fileName);
            return true;
        } catch (Exception e) {
            return false;
        }
    }

    /**
     * 保存最近一次成功加载的地图文件名到 SharedPreferences。
     *
     * @param context  Android 上下文
     * @param fileName 文件名
     */
    private static void saveLastMapFile(Context context, String fileName) {
        SharedPreferences prefs = context.getSharedPreferences(PREF_NAME, Context.MODE_PRIVATE);
        prefs.edit().putString(KEY_LAST_MAP_FILE, fileName).apply();
    }

    /**
     * 获取 assets/map 目录下所有 .json 和 .bmetro 文件列表��按文件名字典序排列。
     *
     * @param context Android 上下文
     * @return 文件名列表（仅文件名，不含路径）
     */
    private static List<String> getAssetFiles(Context context) {
        try {
            String[] assetsFiles = context.getAssets().list("map");
            if (assetsFiles == null) return Collections.emptyList();
            List<String> jsonFiles = new ArrayList<>();
            for (String name : assetsFiles) {
                if (name.endsWith(".json") || name.endsWith(".bmetro")) {
                    jsonFiles.add(name);
                }
            }
            Collections.sort(jsonFiles);
            return jsonFiles;
        } catch (Exception e) {
            Log.e(TAG, "读取 assets 目录失败", e);
            return Collections.emptyList();
        }
    }

    /**
     * 解压压缩数据，自动试探 GZip 和 deflate (zlib) 两种格式。
     *
     * <p>GZip 格式的文件头以 {@code 0x1F 0x8B} 开头。若 GZip 解压失败，则回退到
     * deflate (zlib) 解压方式。</p>
     *
     * @param compressed 压缩后的字节数组
     * @return 解压后的 JSON 字符串
     * @throws Exception 解压失败时抛出
     */
    // ---------- 新增 GZip 解压 ----------
    private static String decompressGzip(byte[] compressed) throws Exception {
        Log.d(TAG, "文件头: " + String.format("%02X %02X", compressed[0], compressed[1]));
        // 部分编辑器输出的是标准 GZip，另一部分输出 zlib/deflate；按顺序尝试兼容两者。
        // 先尝试 GZip
        try {
            return decompressGZipInternal(compressed);
        } catch (IOException e) {
            try {
            Log.w(TAG, "不是 GZip 格式，尝试 Deflate (zlib)");
            // 尝试 Deflate (zlib)
            return decompressZlib(compressed);
            } catch (Exception ee)
            { Log.w(TAG, ee); return ""; }
        }
    }

    /**
     * 使用 GZip 流解压字节数组。
     *
     * @param compressed 经过 GZip 压缩的字节数组
     * @return 解压后的 UTF-8 字符串
     * @throws IOException 解压失败时抛出
     */
    private static String decompressGZipInternal(byte[] compressed) throws IOException {
        try (ByteArrayOutputStream baos = new ByteArrayOutputStream();
             GZIPInputStream gzip = new GZIPInputStream(new ByteArrayInputStream(compressed))) {
            byte[] buffer = new byte[4096];
            int len;
            while ((len = gzip.read(buffer)) > 0) {
                baos.write(buffer, 0, len);
            }
            return new String(baos.toByteArray(), StandardCharsets.UTF_8);
        }
    }

    /**
     * 使用 deflate (zlib) 算法解压字节数组。
     *
     * <p>使用 {@link Inflater} 且传入 {@code true} 表示期望 zlib 包装头。</p>
     *
     * @param compressed 经过 deflate 压缩的字节数组
     * @return 解压后的 UTF-8 字符串
     * @throws Exception 解压失败时抛出
     */
    private static String decompressZlib(byte[] compressed) throws Exception {
        Inflater inflater = new Inflater(true); // true 表示 zlib 头
        inflater.setInput(compressed);
        ByteArrayOutputStream baos = new ByteArrayOutputStream();
        byte[] buffer = new byte[8192];
        while (!inflater.finished()) {
            int count = inflater.inflate(buffer);
            baos.write(buffer, 0, count);
        }
        inflater.end();
        return new String(baos.toByteArray(), StandardCharsets.UTF_8);
    }

    /**
     * 读取文件全部字节。
     *
     * @param file 目标文件
     * @return 文件的完整字节数组
     * @throws IOException 读取失败时抛出
     */
    // 读取文件全部字节
    private static byte[] readBytesFromFile(File file) throws IOException {
        try (FileInputStream fis = new FileInputStream(file)) {
            return readBytesFromStream(fis);
        }
    }

    /**
     * 将输入流中的所有字节读入字节数组。
     *
     * @param is 输入流
     * @return 包含流中所有字节的数组
     * @throws IOException 读取失败时抛出
     */
    private static byte[] readBytesFromStream(InputStream is) throws IOException {
        ByteArrayOutputStream baos = new ByteArrayOutputStream();
        byte[] buffer = new byte[4096];
        int len;
        while ((len = is.read(buffer)) > 0) {
            baos.write(buffer, 0, len);
        }
        return baos.toByteArray();
    }
}
