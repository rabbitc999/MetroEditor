// com.rabbitc.subway.adapter.RouteSegmentAdapter.java
package com.rabbitc.subway.adapter;

import android.annotation.SuppressLint;
import android.content.Context;
import android.graphics.Color;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;
import android.widget.BaseAdapter;
import android.widget.TextView;

import com.rabbitc.subway.R;
import com.rabbitc.subway.data.RouteSegment;
import com.rabbitc.subway.data.DataCache;
import com.rabbitc.subway.data.json.Line;
import com.rabbitc.subway.data.json.MetroData;

import java.util.List;

/**
 * 路线分段列表适配器，用于展示换乘计算结果的每一个乘车段。
 * <p>
 * 每个分段显示：线路颜色、线路名称、站数、方向、起终点和换乘提示。
 * 最后一段显示"行程完成"，中间段显示换乘类型（站内换乘/出站换乘）。
 * </p>
 */
public class RouteSegmentAdapter extends BaseAdapter {

    private final Context context;
    private final List<RouteSegment> segments;
    private final LayoutInflater inflater;

    /**
     * 构造路线分段列表适配器。
     *
     * @param context  上下文，用于创建 LayoutInflater
     * @param segments 路线分段数据列表
     */
    public RouteSegmentAdapter(Context context, List<RouteSegment> segments) {
        this.context = context;
        this.segments = segments;
        this.inflater = LayoutInflater.from(context);
    }

    /**
     * 返回路线分段列表的条目数。
     *
     * @return 分段数量
     */
    @Override
    public int getCount() {
        return segments.size();
    }

    /**
     * 获取指定位置的路线分段对象。
     *
     * @param position 列表位置索引
     * @return 对应位置的 {@link RouteSegment} 对象
     */
    @Override
    public Object getItem(int position) {
        return segments.get(position);
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
     * 创建或复用分段列表项视图，设置线路颜色、站数、方向、起终点和换乘提示。
     * <p>
     * 最后一个分段显示"行程完成"，其他分段根据下一段是否为出站换乘显示对应提示。
     * </p>
     *
     * @param position    列表位置索引
     * @param convertView 可复用的旧视图，可能为 {@code null}
     * @param parent      父视图容器
     * @return 填充了路线分段数据的列表项视图
     */
    @SuppressLint("SetTextI18n")
    @Override
    public View getView(int position, View convertView, ViewGroup parent) {
        if (convertView == null) {
            convertView = inflater.inflate(R.layout.item_route_segment, parent, false);
        }

        RouteSegment seg = segments.get(position);

        View colorView = convertView.findViewById(R.id.segment_color);
        TextView lineName = convertView.findViewById(R.id.segment_line_name);
        TextView stationCount = convertView.findViewById(R.id.segment_station_count);
        TextView direction = convertView.findViewById(R.id.segment_direction);
        TextView start = convertView.findViewById(R.id.segment_start);
        TextView end = convertView.findViewById(R.id.segment_end);
        TextView transferHint = convertView.findViewById(R.id.segment_transfer_hint);

        // 设置线路颜色
        int color = getLineColor(seg.lineId);
        colorView.setBackgroundColor(color);

        // 线路名称
        lineName.setText(seg.lineName);

        // 站数（区间数）
        stationCount.setText(seg.stationCount + "站");

        // 方向显示（优先使用 direction 字段，若为空则用终点站）
        String dirText = (seg.direction != null && !seg.direction.isEmpty())
                ? "开往" + seg.direction + "方向"
                : "开往" + seg.endStation + "方向";
        direction.setText(dirText);

        // 起终点
        start.setText(seg.startStation);
        end.setText(seg.endStation);

        // ========== 换乘提示（基于下一个分段） ==========
        if (position == segments.size() - 1) {
            // 最后一段：行程完成
            transferHint.setText("🏁 行程完成");
            transferHint.setVisibility(View.VISIBLE);
        } else {
            // 获取下一个分段
            RouteSegment nextSeg = segments.get(position + 1);
            String transferType = nextSeg.isOutStationTransfer ? "出站换乘" : "站内换乘";
            // 显示换乘目标线路
            transferHint.setText("🔄 " + transferType + nextSeg.lineName);
            transferHint.setVisibility(View.VISIBLE);
        }

        return convertView;
    }

    /**
     * 根据线路 ID 从缓存数据中查询线路颜色值。
     * 若找不到对应线路，则返回默认灰色 {@code #999999}。
     *
     * @param lineId 线路标识符
     * @return 线路颜色 int 值
     */
    private int getLineColor(String lineId) {
        DataCache cache = DataCache.getInstance();
        MetroData metroData = cache.getMetroData();
        if (metroData != null && metroData.lines != null) {
            Line line = metroData.lines.get(lineId);
            if (line != null && line.color != null) {
                try {
                    return Color.parseColor(line.color);
                } catch (IllegalArgumentException ignored) {}
            }
        }
        return Color.parseColor("#999999");
    }
}