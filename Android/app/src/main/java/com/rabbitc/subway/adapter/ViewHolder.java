package com.rabbitc.subway.adapter;

import android.widget.HorizontalScrollView;
import android.widget.LinearLayout;
import android.widget.TextView;

/**
 * ListView 或 RecyclerView 的视图持有者，用于缓存图例列表中的子视图。
 * <p>
 * 配合 {@link com.rabbitc.subway.adapter.LineLegendAdapter} 使用。
 * </p>
 */
public class ViewHolder {

    /** 线路名称文本视图 */
    public TextView nameView;

    /** 标签容器（用于显示 "环线"、"有快车" 等标签） */
    public LinearLayout tagsContainer;

    /** 线路长度文本视图 */
    public TextView lengthView;

    /** 标签较多时承载横向滚动内容的滚动框。 */
    HorizontalScrollView scrollView;
}
