package com.rabbitc.subway.data.json;

import com.google.gson.annotations.SerializedName;

/**
 * 换乘优惠规则模型，定义地铁换乘时的票价优惠政策。
 * 支持设置是否启用换乘优惠、优惠金额及时间限制。
 */
public class TransferDiscount {

    /** 是否启用换乘优惠 */
    public boolean enabled;

    /** 换乘优惠金额（JSON: "discount_amount"） */
    @SerializedName("discount_amount")
    public double discountAmount;

    /** 换乘时间限制（分钟），超过该时间则不再享受优惠（JSON: "time_limit_minutes"） */
    @SerializedName("time_limit_minutes")
    public int timeLimitMinutes;
}
