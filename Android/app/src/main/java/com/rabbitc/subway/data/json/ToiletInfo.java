package com.rabbitc.subway.data.json;

/**
 * 卫生间信息模型，表示站点内卫生间的位置和设施类型。
 * 包括卫生间所在区域（付费区内/外）、无障碍支持及性别类型。
 */
public class ToiletInfo {

    /** 卫生间位置，取值如 "paid_area"（付费区内）、"unpaid_area"（非付费区） */
    public String position;

    /** 是否为无障碍卫生间 */
    public boolean accessible;

    /** 卫生间性别类型，取值如 "mixed"（男女通用）、"male"（男）、"female"（女） */
    public String gender;
}
