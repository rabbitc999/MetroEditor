package com.rabbitc.subway.utils;

import org.json.JSONArray;
import org.json.JSONException;
import org.json.JSONObject;

import java.io.BufferedReader;
import java.io.ByteArrayOutputStream;
import java.io.File;
import java.io.FileInputStream;
import java.io.IOException;
import java.io.InputStream;
import java.io.InputStreamReader;
import java.nio.charset.StandardCharsets;
import java.util.ArrayList;
import java.util.HashMap;
import java.util.Iterator;
import java.util.List;
import java.util.Map;
import java.util.regex.Pattern;

/**
 * JSON 文件读写工具类。
 * <p>
 * 提供从 {@link InputStream} 或 {@link File} 中读取文本与字节数据，
 * 以及从 {@link JSONObject} 中安全提取 int、double、boolean 等类型值的辅助方法。
 * 所有类型提取方法在解析失败时均返回调用方指定的默认值，不会抛出异常。
 * </p>
 */
public class JsonFileUtils {
    /**
     * 从 InputStream 中读取全部文本内容（UTF-8 编码）。
     *
     * @param is 输入流
     * @return 字符串内容
     * @throws IOException IO 异常
     */
    public static String readStringFromInputStream(InputStream is) throws IOException {
        StringBuilder sb = new StringBuilder();
        try (BufferedReader reader = new BufferedReader(new InputStreamReader(is, StandardCharsets.UTF_8))) {
            String line;
            while ((line = reader.readLine()) != null) {
                sb.append(line);
            }
        }
        return sb.toString();
    }

    /**
     * 从 InputStream 中读取全部字节数据。
     *
     * @param is 输入流
     * @return 字节数组
     * @throws IOException IO 异常
     */
    public static byte[] readBytesFromInputStream(InputStream is) throws IOException {
        ByteArrayOutputStream baos = new ByteArrayOutputStream();
        byte[] buffer = new byte[4096];
        int len;
        while ((len = is.read(buffer)) != -1) {
            baos.write(buffer, 0, len);
        }
        return baos.toByteArray();
    }

    /**
     * 从 File 中读取全部文本内容。
     *
     * @param file 文件对象
     * @return 字符串内容
     * @throws IOException IO 异常
     */
    public static String readStringFromFile(File file) throws IOException {
        try (FileInputStream fis = new FileInputStream(file)) {
            return readStringFromInputStream(fis);
        }
    }

    /**
     * 安全解析浮点数，失败时返回默认值。
     *
     * @param text         待解析的字符串
     * @param defaultValue 默认值
     * @return 浮点数结果
     */
    public static float parseFloatOrDefault(String text, float defaultValue) {
        try {
            return Float.parseFloat(text);
        } catch (NumberFormatException e) {
            return defaultValue;
        }
    }

    /**
     * 从 JSONObject 中安全获取 int 值，支持数字、字符串提取。
     *
     * @param obj          JSONObject
     * @param key          键名
     * @param defaultValue 默认值
     * @return int 值
     */
    public static int optIntSafe(JSONObject obj, String key, int defaultValue) {
        Object value = obj.opt(key);
        if (value == null) return defaultValue;
        if (value instanceof Number) {
            return ((Number) value).intValue();
        }
        if (value instanceof String) {
            try {
                String str = ((String) value).trim();
                Pattern p = Pattern.compile("[-+]?\\d+(\\.\\d+)?");
                java.util.regex.Matcher m = p.matcher(str);
                if (m.find()) {
                    return (int) Double.parseDouble(m.group());
                }
                return defaultValue;
            } catch (NumberFormatException e) {
                return defaultValue;
            }
        }
        return defaultValue;
    }

    /**
     * 从 JSONObject 中安全获取 double 值。
     *
     * @param obj          JSONObject
     * @param key          键名
     * @param defaultValue 默认值
     * @return double 值
     */
    public static double optDoubleSafe(JSONObject obj, String key, double defaultValue) {
        Object value = obj.opt(key);
        if (value == null) return defaultValue;
        if (value instanceof Number) {
            return ((Number) value).doubleValue();
        }
        if (value instanceof String) {
            try {
                String str = ((String) value).trim();
                Pattern p = Pattern.compile("[-+]?\\d+(\\.\\d+)?");
                java.util.regex.Matcher m = p.matcher(str);
                if (m.find()) {
                    return Double.parseDouble(m.group());
                }
                return defaultValue;
            } catch (NumberFormatException e) {
                return defaultValue;
            }
        }
        return defaultValue;
    }

    /**
     * 从 JSONObject 中安全获取 boolean 值。
     *
     * @param obj          JSONObject
     * @param key          键名
     * @param defaultValue 默认值
     * @return boolean 值
     */
    public static boolean optBooleanSafe(JSONObject obj, String key, boolean defaultValue) {
        Object value = obj.opt(key);
        if (value == null) return defaultValue;
        if (value instanceof Boolean) {
            return (Boolean) value;
        }
        if (value instanceof String) {
            String str = ((String) value).trim().toLowerCase();
            return "true".equals(str) || "1".equals(str);
        }
        if (value instanceof Number) {
            return ((Number) value).intValue() != 0;
        }
        return defaultValue;
    }
}
