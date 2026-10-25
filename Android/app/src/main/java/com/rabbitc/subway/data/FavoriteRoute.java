package com.rabbitc.subway.data;

/**
 * 用户收藏的路线，包含城市、起点、终点和自定义标签。
 * <p>
 * 通过 {@link com.rabbitc.subway.utils.FavoriteRouteManager} 进行持久化存储。
 * </p>
 */
public class FavoriteRoute {

    /** 城市名称（用于区分不同城市的地图） */
    public String keyCity;

    /** 起点站名称 */
    public String start;

    /** 终点站名称 */
    public String end;

    /** 用户自定义标签（如 "去公司"、"回家"），可为空 */
    public String label;

    /** 创建一条收藏路线记录。 */
    public FavoriteRoute(String keyCity, String start, String end, String label) {
        this.keyCity = keyCity;
        this.start = start;
        this.end = end;
        this.label = label;
    }
}
