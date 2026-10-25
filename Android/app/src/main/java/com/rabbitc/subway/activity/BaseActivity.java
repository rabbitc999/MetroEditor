package com.rabbitc.subway.activity;

import android.content.Intent;
import android.os.Bundle;
import android.widget.Toast;

import androidx.annotation.Nullable;
import androidx.appcompat.app.AppCompatActivity;

import com.rabbitc.subway.data.DataCache;

/**
 * 所有 Activity 的抽象基类，使用模板方法模式确保数据缓存有效性。
 *
 * <p>在 {@link #onCreate(Bundle)} 中自动检查 {@link DataCache#getInstance()} 是否已
 * 加载有效数据。如果缓存无效，则提示用户并跳转回 {@link MainActivity} 重新加载；
 * 如果缓存有效，则调用子类的 {@link #onDataReady(Bundle)} 继续执行原有初始化逻辑。
 *
 * <p>子类只需实现 {@link #onDataReady(Bundle)} 方法，在其中执行布局设置、视图初始化
 * 等操作，无需关心数据缓存的验证。
 *
 * @see DataCache
 * @see MainActivity
 */
public abstract class BaseActivity extends AppCompatActivity {
    @Override
    protected void onCreate(@Nullable Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        // 检查数据缓存是否有效
        DataCache cache = DataCache.getInstance();
        // 子页面不自行加载地图，统一依赖主页面建立好的全局缓存。
        if (!cache.isCacheValid()) {
            Toast.makeText(this, "数据未加载，请返回主界面重新加载地图", Toast.LENGTH_LONG).show();
            // 跳转到 MainActivity
            Intent intent = new Intent(this, MainActivity.class);
            intent.setFlags(Intent.FLAG_ACTIVITY_CLEAR_TOP | Intent.FLAG_ACTIVITY_SINGLE_TOP);
            startActivity(intent);
            finish();
            return;
        }
        // 缓存就绪后才交给子类，保证所有页面看到的是同一份地图索引。
        onDataReady(savedInstanceState);
    }

    /**
     * 子类在此方法中执行原本 onCreate 的逻辑，此时数据已确保有效
     */
    protected abstract void onDataReady(@Nullable Bundle savedInstanceState);
}
