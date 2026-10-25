package com.rabbitc.subway.widget;

import android.content.Context;
import android.util.AttributeSet;
import android.view.View;
import android.view.ViewGroup;

/**
 * 自动换行布局，支持 margin，无第三方依赖。
 */
public class FlowLayout extends ViewGroup {

    private int lineSpacing = 8; // 行间距（dp），默认 8dp
    private int horizontalSpacing = 8; // 水平间距（dp），默认 8dp

    public FlowLayout(Context context) {
        super(context);
        init(context);
    }

    public FlowLayout(Context context, AttributeSet attrs) {
        super(context, attrs);
        init(context);
    }

    public FlowLayout(Context context, AttributeSet attrs, int defStyleAttr) {
        super(context, attrs, defStyleAttr);
        init(context);
    }

    private void init(Context context) {
        float density = context.getResources().getDisplayMetrics().density;
        lineSpacing = (int) (lineSpacing * density + 0.5f);
        horizontalSpacing = (int) (horizontalSpacing * density + 0.5f);
    }

    /**
     * 测量布局尺寸，根据子 View 的测量结果和行宽约束自动换行计算总宽高。
     * <p>
     * 当子 View 超出可用宽度时自动换行，行高取该行最高子 View 的高度。
     * EXACTLY 模式下直接使用父容器指定的尺寸。
     * </p>
     *
     * @param widthMeasureSpec  宽度测量规格
     * @param heightMeasureSpec 高度测量规格
     */
    @Override
    protected void onMeasure(int widthMeasureSpec, int heightMeasureSpec) {
        int widthMode = MeasureSpec.getMode(widthMeasureSpec);
        int widthSize = MeasureSpec.getSize(widthMeasureSpec);
        int heightMode = MeasureSpec.getMode(heightMeasureSpec);
        int heightSize = MeasureSpec.getSize(heightMeasureSpec);

        int paddingLeft = getPaddingLeft();
        int paddingRight = getPaddingRight();
        int paddingTop = getPaddingTop();
        int paddingBottom = getPaddingBottom();

        int childCount = getChildCount();
        int totalWidth = 0;
        int totalHeight = 0;

        // 每行的宽度和高度（宽度不含 padding，高度不含 padding，但包含行间距）
        int lineWidth = 0;
        int lineHeight = 0;

        // 遍历所有子 View
        for (int i = 0; i < childCount; i++) {
            View child = getChildAt(i);
            if (child.getVisibility() == GONE) continue;

            // 测量子 View
            measureChildWithMargins(child, widthMeasureSpec, 0, heightMeasureSpec, 0);

            // 获取子 View 的 MarginLayoutParams
            MarginLayoutParams lp = (MarginLayoutParams) child.getLayoutParams();
            int childWidth = child.getMeasuredWidth() + lp.leftMargin + lp.rightMargin;
            int childHeight = child.getMeasuredHeight() + lp.topMargin + lp.bottomMargin;

            // 先把当前子项加入本行试算，超出可用宽度时再提交上一行并换行。
            // 判断是否需要换行（加上水平间距）
            int totalChildWidth = lineWidth + childWidth + (lineWidth > 0 ? horizontalSpacing : 0);
            if (lineWidth > 0 && totalChildWidth > widthSize - paddingLeft - paddingRight) {
                // 换行
                totalWidth = Math.max(totalWidth, lineWidth);
                totalHeight += lineHeight + lineSpacing;
                lineWidth = childWidth;
                lineHeight = childHeight;
            } else {
                lineWidth += childWidth + (lineWidth > 0 ? horizontalSpacing : 0);
                lineHeight = Math.max(lineHeight, childHeight);
            }
        }

        // 处理最后一行
        totalWidth = Math.max(totalWidth, lineWidth);
        totalHeight += lineHeight + paddingTop + paddingBottom;

        // 如果宽度是 EXACTLY，直接使用指定宽度；否则使用计算宽度
        int finalWidth = widthMode == MeasureSpec.EXACTLY ? widthSize : totalWidth + paddingLeft + paddingRight;
        int finalHeight = heightMode == MeasureSpec.EXACTLY ? heightSize : totalHeight + paddingBottom;

        setMeasuredDimension(finalWidth, finalHeight);
    }

    /**
     * 布局子 View，按从左到右、自动换行的方式排列。
     * <p>
     * 当子 View 无法在剩余宽度内放置时自动换到下一行。每个子 View 的位置由其 margin 值调整。
     * </p>
     *
     * @param changed 布局是否发生变化
     * @param l       左边界
     * @param t       上边界
     * @param r       右边界
     * @param b       下边界
     */
    @Override
    protected void onLayout(boolean changed, int l, int t, int r, int b) {
        int childCount = getChildCount();
        int paddingLeft = getPaddingLeft();
        int paddingTop = getPaddingTop();
        int paddingRight = getPaddingRight();

        int width = getMeasuredWidth();
        int left = paddingLeft;
        int top = paddingTop;
        int lineHeight = 0;

        for (int i = 0; i < childCount; i++) {
            View child = getChildAt(i);
            if (child.getVisibility() == GONE) continue;

            MarginLayoutParams lp = (MarginLayoutParams) child.getLayoutParams();
            int childWidth = child.getMeasuredWidth();
            int childHeight = child.getMeasuredHeight();

            int childLeft = left + lp.leftMargin;
            int childTop = top + lp.topMargin;
            int childRight = childLeft + childWidth;
            int childBottom = childTop + childHeight;

            // 测量阶段和布局阶段使用相同的换行条件，避免子项在最终布局时跳动。
            // 判断是否需要换行
            if (left + lp.leftMargin + childWidth + lp.rightMargin > width - paddingRight) {
                // 换行
                left = paddingLeft;
                top += lineHeight + lineSpacing;
                lineHeight = 0;

                // 重新计算位置
                childLeft = left + lp.leftMargin;
                childTop = top + lp.topMargin;
                childRight = childLeft + childWidth;
                childBottom = childTop + childHeight;
            }

            child.layout(childLeft, childTop, childRight, childBottom);

            // 更新 left 和 lineHeight
            left += childWidth + lp.leftMargin + lp.rightMargin + horizontalSpacing;
            lineHeight = Math.max(lineHeight, childHeight + lp.topMargin + lp.bottomMargin);
        }
    }

    @Override
    protected ViewGroup.LayoutParams generateDefaultLayoutParams() {
        return new MarginLayoutParams(ViewGroup.LayoutParams.WRAP_CONTENT, ViewGroup.LayoutParams.WRAP_CONTENT);
    }

    @Override
    protected ViewGroup.LayoutParams generateLayoutParams(ViewGroup.LayoutParams p) {
        return new MarginLayoutParams(p);
    }

    @Override
    public ViewGroup.LayoutParams generateLayoutParams(AttributeSet attrs) {
        return new MarginLayoutParams(getContext(), attrs);
    }
}
