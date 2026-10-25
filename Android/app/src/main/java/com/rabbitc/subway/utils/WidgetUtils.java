package com.rabbitc.subway.utils;

import android.view.View;
import android.view.ViewGroup;
import android.widget.ListAdapter;
import android.widget.ListView;

/**
 * UI 组件工具类。
 * <p>
 * 提供 {@link ListView} 在 {@link android.widget.ScrollView} 中
 * 动态计算总高度并完全展开的辅助方法，避免 ListView 嵌套时只显示部分条目的问题。
 * </p>
 */
public class WidgetUtils {

    /**
     * 动态计算 ListView 的高度，使其在 ScrollView 中完全展开
     * 注意：此方法应在 ListView 已测量至少一次后调用，否则宽度可能为0导致高度计算错误。
     * 建议通过 {@link #setListViewHeight(ListView)} 延迟执行。
     */
    public static void setListViewHeightBasedOnChildren(ListView listView) {
        ListAdapter listAdapter = listView.getAdapter();
        if (listAdapter == null) {
            return;
        }

        // 确保宽度非零
        int width = listView.getWidth();
        if (width <= 0) {
            // 若宽度为0，尝试通过 post 延迟执行
            listView.post(() -> setListViewHeightBasedOnChildren(listView));
            return;
        }

        // 使用 EXACTLY 模式，保证子项宽度正确
        int desiredWidth = View.MeasureSpec.makeMeasureSpec(width, View.MeasureSpec.EXACTLY);

        int totalHeight = 0;
        for (int i = 0; i < listAdapter.getCount(); i++) {
            View listItem = listAdapter.getView(i, null, listView);
            listItem.measure(desiredWidth, View.MeasureSpec.UNSPECIFIED);
            totalHeight += listItem.getMeasuredHeight();
        }

        // 加上分隔线高度
        int dividerHeight = listView.getDividerHeight();
        if (dividerHeight > 0) {
            totalHeight += dividerHeight * (listAdapter.getCount() - 1);
        }

        // 加上 padding
        totalHeight += listView.getPaddingTop() + listView.getPaddingBottom();

        ViewGroup.LayoutParams params = listView.getLayoutParams();
        params.height = totalHeight;
        listView.setLayoutParams(params);
        listView.requestLayout();
    }

    /**
     * 延迟执行高度计算，确保 ListView 已测量
     */
    public static void setListViewHeight(ListView listView) {
        listView.post(() -> setListViewHeightBasedOnChildren(listView));
    }
}