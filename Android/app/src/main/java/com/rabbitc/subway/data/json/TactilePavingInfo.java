package com.rabbitc.subway.data.json;

/**
 * 盲道铺设信息模型，记录站点内盲道的铺设状态。
 * 用于无障碍出行指引。
 */
public class TactilePavingInfo {

    /** 盲道铺设状态，取值如 "available"（已铺设）、"partial"（部分铺设）、"none"（未铺设） */
    public String status;
}
