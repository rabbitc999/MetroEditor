package com.rabbitc.subway.data.json;

import com.google.gson.annotations.SerializedName;

import java.util.List;

/**
 * 站点设施数据模型，汇总一个地铁站内各类无障碍和便民设施信息。
 * 包括卫生间、无障碍卫生间、电梯、宽闸机、自动扶梯和盲道等设施。
 */
public class StationFacilities {

    /** 普通卫生间信息 */
    public ToiletInfo toilet;

    /** 无障碍卫生间信息（JSON: "accessible_toilet"） */
    @SerializedName("accessible_toilet")
    public ToiletInfo accessibleToilet;

    /** 电梯信息列表 */
    public List<ElevatorInfo> elevators;

    /** 宽闸机/大件行李通道信息（JSON: "wide_gate"） */
    @SerializedName("wide_gate")
    public WideGateInfo wideGate;

    /** 自动扶梯信息列表 */
    public List<EscalatorInfo> escalators;

    /** 盲道铺设信息（JSON: "tactile_paving"） */
    @SerializedName("tactile_paving")
    public TactilePavingInfo tactilePaving;
}
