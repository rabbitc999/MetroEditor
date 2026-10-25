package com.rabbitc.subway.activity;

import android.graphics.Color;
import android.os.Bundle;
import android.text.TextUtils;
import android.view.Gravity;
import android.view.View;
import android.widget.Button;
import android.widget.AutoCompleteTextView;
import android.widget.EditText;
import android.widget.LinearLayout;
import android.widget.TextView;
import android.widget.Toast;

import androidx.appcompat.app.AlertDialog;
import androidx.appcompat.app.AppCompatActivity;

import com.rabbitc.subway.R;
import com.rabbitc.subway.adapter.FuzzyStationAdapter;
import com.rabbitc.subway.data.DataCache;
import com.rabbitc.subway.data.FavoriteRoute;
import com.rabbitc.subway.utils.FavoriteRouteManager;
import com.rabbitc.subway.utils.StationUtils;

import java.util.ArrayList;
import java.util.List;

/**
 * 收藏路线管理页面。
 *
 * <p>仅显示当前城市的收藏路线，并提供查看、编辑、删除和排序入口。</p>
 */
public class FavoriteRoutesActivity extends AppCompatActivity {

    private FavoriteRouteManager manager;
    private LinearLayout container;
    private String currentCity;
    private List<FavoriteRoute> routes = new ArrayList<>();

    /** 初始化收藏路线管理器并加载当前城市的路线列表。 */
    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        setContentView(R.layout.activity_favorite_routes);
        manager = new FavoriteRouteManager(this);
        currentCity = DataCache.getInstance().getKeyCity();

        Button btnBack = findViewById(R.id.btnBack);
        btnBack.setOnClickListener(v -> finish());
        container = findViewById(R.id.favoriteListContainer);

        refreshList();
    }

    private void refreshList() {
        routes.clear();
        for (FavoriteRoute route : manager.loadRoutes()) {
            if (!TextUtils.isEmpty(currentCity) && !currentCity.equals(route.keyCity)) continue;
            routes.add(route);
        }

        container.removeAllViews();
        if (routes.isEmpty()) {
            TextView empty = new TextView(this);
            empty.setText("暂无当前城市的收藏路线");
            empty.setGravity(Gravity.CENTER);
            empty.setTextColor(Color.parseColor("#999999"));
            empty.setPadding(0, dp(30), 0, dp(30));
            container.addView(empty);
            return;
        }

        for (int i = 0; i < routes.size(); i++) {
            final int index = i;
            FavoriteRoute route = routes.get(i);

            TextView item = new TextView(this);
            String label = TextUtils.isEmpty(route.label) ? "" : route.label;
            item.setText(label.isEmpty()
                    ? (route.start + " → " + route.end)
                    : label + "（" + route.start + " → " + route.end + "）");
            item.setTextSize(15);
            item.setTextColor(Color.parseColor("#333333"));
            item.setGravity(Gravity.CENTER_VERTICAL);
            item.setPadding(dp(12), dp(14), dp(12), dp(14));
            item.setBackgroundResource(R.drawable.bg_favorite_route);
            LinearLayout.LayoutParams lp = new LinearLayout.LayoutParams(LinearLayout.LayoutParams.MATCH_PARENT, LinearLayout.LayoutParams.WRAP_CONTENT);
            lp.bottomMargin = dp(8);
            item.setLayoutParams(lp);

            item.setOnClickListener(v -> showRouteDialog(route, index));
            item.setOnLongClickListener(v -> {
                showManageMenu(route, index);
                return true;
            });
            container.addView(item);
        }
    }

    private void showRouteDialog(FavoriteRoute route, int index) {
        String[] options = {"修改", "删除", "取消"};
        new AlertDialog.Builder(this)
                .setTitle(route.start + " → " + route.end)
                .setItems(options, (dialog, which) -> {
                    if (which == 0) showEditDialog(route, index);
                    else if (which == 1) deleteRoute(index);
                })
                .show();
    }

    private void showManageMenu(FavoriteRoute route, int index) {
        String[] options = {"上移", "下移", "修改", "删除"};
        new AlertDialog.Builder(this)
                .setTitle("管理收藏路线")
                .setItems(options, (dialog, which) -> {
                    if (which == 0) moveRoute(index, -1);
                    else if (which == 1) moveRoute(index, 1);
                    else if (which == 2) showEditDialog(route, index);
                    else if (which == 3) deleteRoute(index);
                })
                .show();
    }

    private void showEditDialog(FavoriteRoute route, int index) {
        LinearLayout layout = new LinearLayout(this);
        layout.setOrientation(LinearLayout.VERTICAL);
        layout.setPadding(dp(16), dp(16), dp(16), dp(8));

        AutoCompleteTextView etStart = new AutoCompleteTextView(this);
        etStart.setAdapter(new FuzzyStationAdapter(this, StationUtils.getActiveStationList()));
        etStart.setThreshold(1);
        etStart.setHint("起点站");
        etStart.setText(route.start);
        layout.addView(etStart);

        AutoCompleteTextView etEnd = new AutoCompleteTextView(this);
        etEnd.setAdapter(new FuzzyStationAdapter(this, StationUtils.getActiveStationList()));
        etEnd.setThreshold(1);
        etEnd.setHint("终点站");
        etEnd.setText(route.end);
        layout.addView(etEnd);

        EditText etLabel = new EditText(this);
        etLabel.setHint("标签（可选）");
        etLabel.setText(route.label);
        layout.addView(etLabel);

        new AlertDialog.Builder(this)
                .setTitle("修改收藏路线")
                .setView(layout)
                .setPositiveButton("保存", (dialog, which) -> {
                    String start = etStart.getText().toString().trim();
                    String end = etEnd.getText().toString().trim();
                    String label = etLabel.getText().toString().trim();
                    if (start.isEmpty() || end.isEmpty()) {
                        Toast.makeText(this, "起点和终点不能为空", Toast.LENGTH_SHORT).show();
                        return;
                    }
                    route.start = start;
                    route.end = end;
                    route.label = label;
                    routes.set(index, route);
                    saveRoutes();
                    refreshList();
                })
                .setNegativeButton("取消", null)
                .show();
    }

    private void moveRoute(int index, int delta) {
        int target = index + delta;
        if (index < 0 || target < 0 || target >= routes.size()) return;
        FavoriteRoute route = routes.remove(index);
        routes.add(target, route);
        saveRoutes();
        refreshList();
    }

    private void deleteRoute(int index) {
        new AlertDialog.Builder(this)
                .setTitle("删除收藏路线")
                .setMessage("确定删除吗？")
                .setPositiveButton("删除", (dialog, which) -> {
                    routes.remove(index);
                    saveRoutes();
                    refreshList();
                })
                .setNegativeButton("取消", null)
                .show();
    }

    private void saveRoutes() {
        List<FavoriteRoute> all = manager.loadRoutes();
        // 先移除当前城市的所有记录，再按当前排序写入
        all.removeIf(r -> !TextUtils.isEmpty(currentCity) && currentCity.equals(r.keyCity));
        all.addAll(routes);
        manager.saveRoutes(all);
    }

    private int dp(int value) {
        return Math.round(value * getResources().getDisplayMetrics().density);
    }
}
