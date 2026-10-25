package com.rabbitc.subway.utils;

import android.content.Context;
import android.graphics.Bitmap;
import android.graphics.BitmapFactory;
import android.media.ExifInterface;
import android.net.Uri;
import android.util.Base64;

import java.io.ByteArrayOutputStream;
import java.io.InputStream;

/**
 * 图片工具类 — 将相册/拍照得到的图片压缩并编码为 Base64。
 *
 * <p>用于站点打卡的图片存储与 TCP 同步。图片按最长边
 * {@link #MAX_DIMENSION} 像素等比缩放，JPEG 质量 {@link #JPEG_QUALITY}，
 * 压缩后通常在 50-200KB 之间，可安全存入 SharedPreferences 并经 TCP 传输。</p>
 */
public final class ImageUtils {

    /** 压缩后最长边像素数 */
    private static final int MAX_DIMENSION = 1024;
    /** JPEG 压缩质量（0-100） */
    private static final int JPEG_QUALITY = 75;

    private ImageUtils() {}

    /**
     * 从 Uri 读取图片，压缩后编码为 Base64 JPEG 字符串。
     *
     * @param context 上下文
     * @param uri     图片 Uri（相册或拍照结果）
     * @return Base64 字符串（不含 data: 前缀），失败返回 null
     */
    public static String compressToBase64(Context context, Uri uri) {
        if (uri == null) return null;
        try {
            InputStream input = context.getContentResolver().openInputStream(uri);
            if (input == null) return null;

            // 第一次解码只取尺寸
            BitmapFactory.Options opts = new BitmapFactory.Options();
            opts.inJustDecodeBounds = true;
            BitmapFactory.decodeStream(input, null, opts);
            try { input.close(); } catch (Exception ignored) {}

            if (opts.outWidth <= 0 || opts.outHeight <= 0) return null;

            // 计算采样率
            opts.inSampleSize = calculateSampleSize(opts.outWidth, opts.outHeight);
            opts.inJustDecodeBounds = false;

            // 第二次解码为位图
            input = context.getContentResolver().openInputStream(uri);
            if (input == null) return null;
            Bitmap bitmap = BitmapFactory.decodeStream(input, null, opts);
            try { input.close(); } catch (Exception ignored) {}
            if (bitmap == null) return null;

            // 尺寸仍超限时等比缩放
            int maxSide = Math.max(bitmap.getWidth(), bitmap.getHeight());
            if (maxSide > MAX_DIMENSION) {
                float scale = (float) MAX_DIMENSION / maxSide;
                int w = Math.round(bitmap.getWidth() * scale);
                int h = Math.round(bitmap.getHeight() * scale);
                Bitmap scaled = Bitmap.createScaledBitmap(bitmap, w, h, true);
                bitmap.recycle();
                bitmap = scaled;
            }

            // 压缩为 JPEG 并编码 Base64
            ByteArrayOutputStream bos = new ByteArrayOutputStream();
            bitmap.compress(Bitmap.CompressFormat.JPEG, JPEG_QUALITY, bos);
            bitmap.recycle();
            return Base64.encodeToString(bos.toByteArray(), Base64.NO_WRAP);
        } catch (Exception e) {
            e.printStackTrace();
            return null;
        }
    }

    /**
     * 将 Base64 字符串解码为位图（用于显示缩略图）。
     *
     * @param base64 图片 Base64 字符串
     * @return 位图，失败返回 null
     */
    public static Bitmap decodeBase64(String base64) {
        if (base64 == null || base64.isEmpty()) return null;
        try {
            byte[] bytes = Base64.decode(base64, Base64.NO_WRAP);
            return BitmapFactory.decodeByteArray(bytes, 0, bytes.length);
        } catch (Exception e) {
            e.printStackTrace();
            return null;
        }
    }

    /**
     * 计算采样率（2 的幂次），使解码后尺寸不超过目标上限。
     */
    private static int calculateSampleSize(int width, int height) {
        int sampleSize = 1;
        while (width / (sampleSize * 2) >= MAX_DIMENSION
                || height / (sampleSize * 2) >= MAX_DIMENSION) {
            sampleSize *= 2;
        }
        return sampleSize;
    }
}
