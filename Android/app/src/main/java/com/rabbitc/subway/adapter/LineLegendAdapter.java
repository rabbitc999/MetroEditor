package com.rabbitc.subway.adapter;

import android.annotation.SuppressLint;
import android.content.Context;
import android.graphics.Color;
import android.graphics.drawable.GradientDrawable;
import android.os.Handler;
import android.text.TextUtils;
import android.view.LayoutInflater;
import android.view.MotionEvent;
import android.view.View;
import android.view.ViewConfiguration;
import android.view.ViewGroup;
import android.widget.BaseAdapter;
import android.widget.HorizontalScrollView;
import android.widget.LinearLayout;
import android.widget.TextView;

import androidx.core.content.ContextCompat;

import com.rabbitc.subway.R;
import com.rabbitc.subway.data.LineLegendItem;
import com.rabbitc.subway.widget.LegendListView;

import java.util.List;

/**
 * 线路图例列表适配器，用于展示地铁线路的颜色块、名称、标签和长度信息。
 * <p>
 * 支持单条线路选中高亮、标签着色（单独计费/分段运营/有快车/环线）等功能。
 * 通过 {@link LegendListView.OnItemSelectedListener} 和 {@link LegendListView.OnItemLongClickListener} 回调交互事件。
 * </p>
 *
 * @see LegendListView
 * @see LineLegendItem
 */
public class LineLegendAdapter extends BaseAdapter {

    private final Context context;
    private List<LineLegendItem> items;
    private String selectedLineName;
    private LegendListView.OnItemSelectedListener itemSelectedListener;
    private LegendListView.OnItemLongClickListener itemLongClickListener;

    // ---------- 构造方法 ----------
    /**
     * 构造线路图例列表适配器。
     *
     * @param context 上下文
     * @param items   线路图例数据列表
     */
    public LineLegendAdapter(Context context, List<LineLegendItem> items) {
        this.context = context;
        this.items = items;
    }

    // ---------- 设置监听器 ----------
    /**
     * 设置条目选中监听器，当用户点击某条线路图例时回调。
     *
     * @param listener 选中监听器，可为 {@code null}
     */
    public void setOnItemSelectedListener(LegendListView.OnItemSelectedListener listener) {
        this.itemSelectedListener = listener;
    }

    /**
     * 设置条目长按监听器，当用户长按某条线路图例时回调。
     *
     * @param listener 长按监听器，可为 {@code null}
     */
    public void setOnItemLongClickListener(LegendListView.OnItemLongClickListener listener) {
        this.itemLongClickListener = listener;
    }

    /**
     * 设置当前选中的线路名称，触发列表刷新以高亮对应条目。
     *
     * @param lineName 线路名称
     */
    public void setSelectedLineName(String lineName) {
        this.selectedLineName = lineName;
        notifyDataSetChanged();
    }

    /**
     * 更新线路图例数据列表并刷新视图。
     *
     * @param newItems 新的图例数据列表
     */
    public void updateItems(List<LineLegendItem> newItems) {
        this.items = newItems;
        notifyDataSetChanged();
    }

    // ---------- BaseAdapter 实现 ----------
    /**
     * 返回图例列表的条目数。
     *
     * @return 条目数量，若数据为空则返回 0
     */
    @Override
    public int getCount() {
        return items == null ? 0 : items.size();
    }

    /**
     * 获取指定位置的图例数据对象。
     *
     * @param position 列表位置索引
     * @return 对应位置的 {@link LineLegendItem} 对象
     */
    @Override
    public LineLegendItem getItem(int position) {
        return items.get(position);
    }

    /**
     * 获取指定位置的条目 ID。
     *
     * @param position 列表位置索引
     * @return 位置索引作为条目 ID
     */
    @Override
    public long getItemId(int position) {
        return position;
    }

    /**
     * 创建或复用图例列表项视图，设置线路颜色块、名称、标签和长度信息。
     * <p>
     * 选中的条目会显示高亮背景；支持多色线路的颜色块拼接显示。
     * 条目绑定单击和长按监听器，通过 {@link LegendListView.OnItemSelectedListener} 和
     * {@link LegendListView.OnItemLongClickListener} 回调。
     * </p>
     *
     * @param position    列表位置索引
     * @param convertView 可复用的旧视图，可能为 {@code null}
     * @param parent      父视图容器
     * @return 填充了图例数据的列表项视图
     */
    @SuppressLint("ClickableViewAccessibility")
    @Override
    public View getView(int position, View convertView, ViewGroup parent) {
        ViewHolder holder;
        if (convertView == null) {
            convertView = LayoutInflater.from(context).inflate(R.layout.item_line_legend, parent, false);
            holder = new ViewHolder();
            holder.scrollView = convertView.findViewById(R.id.itemScrollView);
            holder.content = convertView.findViewById(R.id.itemContent);
            holder.colorContainer = convertView.findViewById(R.id.colorContainer);
            holder.nameView = convertView.findViewById(R.id.legend_name);
            holder.tagsContainer = convertView.findViewById(R.id.legend_tags_container);
            holder.lengthView = convertView.findViewById(R.id.legend_length);
            convertView.setTag(holder);
        } else {
            holder = (ViewHolder) convertView.getTag();
        }

        final LineLegendItem item = getItem(position);

        // ---------- 选中高亮 ----------
        boolean isSelected = item.lineName.equals(selectedLineName);
        if (isSelected) {
            GradientDrawable selectedBg = new GradientDrawable();
            selectedBg.setShape(GradientDrawable.RECTANGLE);
            selectedBg.setCornerRadius(8);
            selectedBg.setColor(ContextCompat.getColor(context, R.color.selected_item_bg));
            holder.content.setBackground(selectedBg);
        } else {
            holder.content.setBackgroundColor(Color.TRANSPARENT);
        }

        // ---------- 设置名称和长度 ----------
        holder.nameView.setText(item.lineName);
        holder.lengthView.setText(item.lengthStr);

        // ---------- 颜色块 ----------
        holder.colorContainer.removeAllViews();
        if (item.colors != null && !item.colors.isEmpty()) {
            for (int color : item.colors) {
                View colorView = createColorView(color);
                holder.colorContainer.addView(colorView);
            }
        } else {
            // 默认灰色占位
            View placeholder = createColorView(Color.GRAY);
            holder.colorContainer.addView(placeholder);
        }

        // ---------- 标签 ----------
        holder.tagsContainer.removeAllViews();
        if (item.tags != null) {
            for (String tag : item.tags) {
                if (TextUtils.isEmpty(tag)) continue;
                TextView tagView = createTagView(tag);
                holder.tagsContainer.addView(tagView);
            }
        }

        // ---------- 点击和长按监听 ----------
        holder.content.setOnClickListener(v -> {
            if (itemSelectedListener != null) {
                itemSelectedListener.onItemSelected(item);
            }
        });

        holder.content.setOnLongClickListener(v -> {
            if (itemLongClickListener != null) {
                itemLongClickListener.onItemLongClick(item);
                return true;
            }
            return false;
        });

        return convertView;
    }

    // ---------- 辅助方法 ----------
    /**
     * 创建一个颜色块 View。
     *
     * @param color 颜色 int 值
     * @return 48×48 像素的颜色块 View
     */
    private View createColorView(int color) {
        View view = new View(context);
        LinearLayout.LayoutParams params = new LinearLayout.LayoutParams(48, 48);
        params.setMargins(0, 0, 8, 0);
        view.setLayoutParams(params);
        view.setBackgroundColor(color);
        return view;
    }

    /**
     * 创建一个标签 TextView，具有圆角背景和白色文字。
     *
     * @param tagText 标签文本，如"单独计费"、"环线"等
     * @return 设置了样式的标签 TextView
     */
    private TextView createTagView(String tagText) {
        TextView tagView = new TextView(context);
        tagView.setText(tagText);
        tagView.setTextSize(10);
        tagView.setTextColor(Color.WHITE);
        tagView.setPadding(6, 2, 6, 2);

        GradientDrawable drawable = new GradientDrawable();
        drawable.setShape(GradientDrawable.RECTANGLE);
        drawable.setCornerRadius(4);
        drawable.setColor(getTagColor(tagText));
        tagView.setBackground(drawable);

        LinearLayout.LayoutParams params = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.WRAP_CONTENT,
                ViewGroup.LayoutParams.WRAP_CONTENT);
        params.setMarginEnd(4);
        tagView.setLayoutParams(params);
        return tagView;
    }

    /**
     * 根据标签文本返回对应颜色。
     *
     * @param tag 标签文本
     * @return 标签背景颜色 int 值，未知标签返回灰色
     */
    private int getTagColor(String tag) {
        switch (tag) {
            case "单独计费": return Color.parseColor("#D32F2F");
            case "分段运营": return Color.parseColor("#FF9800");
            case "有快车":   return Color.parseColor("#2196F3");
            case "环线":     return Color.parseColor("#4CAF50");
            case "有交路":   return Color.parseColor("#7B1FA2");
            default:        return Color.parseColor("#757575");
        }
    }


    /**
     * 列表项视图持有者，缓存子视图引用以提升滚动性能。
     */
    static class ViewHolder {
        HorizontalScrollView scrollView;
        LinearLayout content;          // 内部容器
        LinearLayout colorContainer, tagsContainer;
        TextView nameView, lengthView;
    }
}