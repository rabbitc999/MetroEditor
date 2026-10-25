package com.rabbitc.subway.widget;

import android.content.Context;
import android.util.AttributeSet;
import android.widget.ListView;

import com.rabbitc.subway.adapter.LineLegendAdapter;
import com.rabbitc.subway.data.DataCache;
import com.rabbitc.subway.data.LineLegendItem;
import com.rabbitc.subway.utils.LegendBuilder;

import java.util.List;

/**
 * 线路图例列表视图，封装 {@link ListView} 并与 {@link LineLegendAdapter} 配合使用。
 * <p>
 * 支持从 {@link DataCache} 缓存加载图例数据、设置选中线路、以及通过
 * {@link OnItemSelectedListener} 和 {@link OnItemLongClickListener} 回调交互事件。
 * </p>
 *
 * @see LineLegendAdapter
 * @see LineLegendItem
 * @see LegendBuilder
 */
public class LegendListView extends ListView {

    private LineLegendAdapter adapter;
    private OnItemSelectedListener onItemSelectedListener;
    private OnItemLongClickListener onItemLongClickListener;

    /**
     * 通过代码创建 LegendListView 实例。
     *
     * @param context 上下文
     */
    public LegendListView(Context context) {
        super(context);
        init(context);
    }

    /**
     * 从 XML 布局中创建 LegendListView 实例。
     *
     * @param context 上下文
     * @param attrs   属性集
     */
    public LegendListView(Context context, AttributeSet attrs) {
        super(context, attrs);
        init(context);
    }

    /**
     * 从 XML 布局中创建 LegendListView 实例（含默认样式属性）。
     *
     * @param context      上下文
     * @param attrs        属性集
     * @param defStyleAttr 默认样式属性
     */
    public LegendListView(Context context, AttributeSet attrs, int defStyleAttr) {
        super(context, attrs, defStyleAttr);
        init(context);
    }

    private void init(Context context) {
        setDivider(null);
        setDividerHeight(0);
        setVerticalScrollBarEnabled(true);
    }

    /**
     * 设置图例数据列表。首次调用会创建适配器并绑定；后续调用会更新数据。
     *
     * @param items 图例数据列表
     */
    public void setItems(List<LineLegendItem> items) {
        if (adapter == null) {
            adapter = new LineLegendAdapter(getContext(), items);
            // 将已保存的监听传给适配器
            if (onItemSelectedListener != null) {
                adapter.setOnItemSelectedListener(onItemSelectedListener);
            }
            if (onItemLongClickListener != null) {
                adapter.setOnItemLongClickListener(onItemLongClickListener);
            }
            setAdapter(adapter);
        } else {
            adapter.updateItems(items);
            adapter.notifyDataSetChanged();
        }
    }

    /**
     * 从 {@link DataCache} 缓存中加载图例数据。
     *
     * @param cache          数据缓存实例
     * @param existFutureOnly 是否仅显示未来线路
     * @see LegendBuilder#buildLegendItems(DataCache, boolean)
     */
    public void loadFromCache(DataCache cache, boolean existFutureOnly) {
        List<LineLegendItem> items = LegendBuilder.buildLegendItems(cache, existFutureOnly);
        setItems(items);
    }

    /**
     * 设置当前选中的线路名称，列表将高亮对应条目。
     *
     * @param lineName 线路名称
     */
    public void setSelectedLine(String lineName) {
        if (adapter != null) {
            adapter.setSelectedLineName(lineName);
        }
    }

    /**
     * 设置条目选中监听器，在创建适配器时同步传递。
     *
     * @param listener 选中监听器，可为 {@code null}
     */
    public void setOnItemSelectedListener(OnItemSelectedListener listener) {
        this.onItemSelectedListener = listener;
        if (adapter != null) {
            adapter.setOnItemSelectedListener(listener);
        }
    }

    /**
     * 设置条目长按监听器，在创建适配器时同步传递。
     *
     * @param listener 长按监听器，可为 {@code null}
     */
    public void setOnItemLongClickListener(OnItemLongClickListener listener) {
        this.onItemLongClickListener = listener;
        if (adapter != null) {
            adapter.setOnItemLongClickListener(listener);
        }
    }



    // ---------- 回调接口 ----------
    /**
     * 图例条目选中回调接口。
     */
    public interface OnItemSelectedListener {
        /**
         * 当用户点击某条线路图例时回调。
         *
         * @param item 被选中的图例数据
         */
        void onItemSelected(LineLegendItem item);
    }

    /**
     * 图例条目长按回调接口。
     */
    public interface OnItemLongClickListener {
        /**
         * 当用户长按某条线路图例时回调。
         *
         * @param item 被长按的图例数据
         */
        void onItemLongClick(LineLegendItem item);
    }
}