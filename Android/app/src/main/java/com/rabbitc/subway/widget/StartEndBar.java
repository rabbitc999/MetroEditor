// com.rabbitc.subway.widget.StartEndBar.java
package com.rabbitc.subway.widget;

import android.content.Context;
import android.content.res.TypedArray;
import android.util.AttributeSet;
import android.view.LayoutInflater;
import android.view.View;
import android.widget.Button;
import android.widget.LinearLayout;
import android.widget.TextView;

import androidx.annotation.Nullable;

import com.rabbitc.subway.R;

/**
 * 起终点站点栏控件，用于显示和切换当前查询的起点和终点站点。
 * <p>
 * 支持从 XML 属性设置起终点文本、提示文本和返回按钮可见性。
 * 通过 {@link OnStartEndClickListener} 回调返回按钮点击事件。
 * </p>
 */
public class StartEndBar extends LinearLayout {

    private TextView tvStart;
    private TextView tvEnd;
    private TextView tvHint;
    private Button btnBack;

    private OnStartEndClickListener listener;

    /**
     * 起终点栏回调接口。
     */
    public interface OnStartEndClickListener {
        /**
         * 当用户点击返回按钮时回调。
         */
        void onBackClick();
    }

    /**
     * 通过代码创建 StartEndBar 实例。
     *
     * @param context 上下文
     */
    public StartEndBar(Context context) {
        this(context, null);
    }

    /**
     * 从 XML 布局中创建 StartEndBar 实例。
     *
     * @param context 上下文
     * @param attrs   属性集，可包含 {@code R.styleable.StartEndBar} 中定义的属性
     */
    public StartEndBar(Context context, @Nullable AttributeSet attrs) {
        this(context, attrs, 0);
    }

    /**
     * 从 XML 布局中创建 StartEndBar 实例（含默认样式属性）。
     *
     * @param context      上下文
     * @param attrs        属性集
     * @param defStyleAttr 默认样式属性
     */
    public StartEndBar(Context context, @Nullable AttributeSet attrs, int defStyleAttr) {
        super(context, attrs, defStyleAttr);
        init(context, attrs);
    }

    private void init(Context context, AttributeSet attrs) {
        LayoutInflater.from(context).inflate(R.layout.view_start_end_bar, this, true);

        tvStart = findViewById(R.id.tv_start);
        tvEnd = findViewById(R.id.tv_end);
        tvHint = findViewById(R.id.tv_hint);
        btnBack = findViewById(R.id.btn_back);

        btnBack.setOnClickListener(v -> {
            if (listener != null) listener.onBackClick();
        });

        // 从 XML 读取自定义属性
        if (attrs != null) {
            TypedArray a = context.obtainStyledAttributes(attrs, R.styleable.StartEndBar);
            String startText = a.getString(R.styleable.StartEndBar_startText);
            String endText = a.getString(R.styleable.StartEndBar_endText);
            String hintText = a.getString(R.styleable.StartEndBar_hintText);
            boolean showBack = a.getBoolean(R.styleable.StartEndBar_showBackButton, false);
            boolean showHint = a.getBoolean(R.styleable.StartEndBar_showHint, false);
            a.recycle();

            if (startText != null) tvStart.setText(startText);
            if (endText != null) tvEnd.setText(endText);
            if (hintText != null) tvHint.setText(hintText);
            btnBack.setVisibility(showBack ? View.VISIBLE : View.GONE);
            tvHint.setVisibility(showHint ? View.VISIBLE : View.GONE);
        }
    }

    // ----- 公共方法 -----
    /**
     * 设置起点站点名称。
     *
     * @param station 站点名称，{@code null} 时显示"未选择"
     */
    public void setStartStation(String station) {
        tvStart.setText(station != null ? station : "未选择");
    }

    /**
     * 设置终点���点名称。
     *
     * @param station 站点名称，{@code null} 时显示"未选择"
     */
    public void setEndStation(String station) {
        tvEnd.setText(station != null ? station : "未选择");
    }

    /**
     * 同时设置起点和终点站点名称。
     *
     * @param start 起点站名
     * @param end   终点站名
     */
    public void setStartEnd(String start, String end) {
        setStartStation(start);
        setEndStation(end);
    }

    /**
     * 设置返回按钮的可见性。
     *
     * @param show {@code true} 显示返回按钮，{@code false} 隐藏
     */
    public void showBackButton(boolean show) {
        btnBack.setVisibility(show ? View.VISIBLE : View.GONE);
    }

    /**
     * 设置提示文本内容。
     *
     * @param hint 提示文字
     */
    public void setHintText(String hint) {
        tvHint.setText(hint);
    }

    /**
     * 设置提示文本的可见性。
     *
     * @param show {@code true} 显示提示，{@code false} 隐藏
     */
    public void showHint(boolean show) {
        tvHint.setVisibility(show ? View.VISIBLE : View.GONE);
    }

    /**
     * 设置起终点栏点击监听器（返回按钮）。
     *
     * @param listener 点击监听器，可为 {@code null}
     */
    public void setOnStartEndClickListener(OnStartEndClickListener listener) {
        this.listener = listener;
    }

    /**
     * 获取起点站点名称。
     *
     * @return 站点名称，若为"未选择"则返回 {@code null}
     */
    public String getStartStation() {
        String text = tvStart.getText().toString();
        return "未选择".equals(text) ? null : text;
    }

    /**
     * 获取终点站点名称。
     *
     * @return 站点名称，若为"未选择"则返回 {@code null}
     */
    public String getEndStation() {
        String text = tvEnd.getText().toString();
        return "未选择".equals(text) ? null : text;
    }
}