package com.rabbitc.subway.data.parse;

import java.util.List;

/**
 * 运营时间规则，由 {@code DataParser} 从原始 JSON 解析得到，表示某条线路或全局的运营时间。
 * 存储于 {@code DataCache} 中供运行时运营状态判断使用。
 */
public class OpenTimeRule {
    /** 线路标识，可为 null 表示全局规则 */
    public String lineId;
    /** 运营开始时间，格式为 "HH:mm" */
    public String startTime;
    /** 运营结束时间，格式为 "HH:mm" */
    public String endTime;
    /** 适用的星期列表 */
    public List<String> days;
}
