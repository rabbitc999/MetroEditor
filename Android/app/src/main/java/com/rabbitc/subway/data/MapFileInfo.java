package com.rabbitc.subway.data;

/**
 * 地图文件信息，用于地图管理界面。
 * <p>
 * 区分内置地图（assets）和用户导入的地图（内部存储）。
 * </p>
 */
public class MapFileInfo {

    /** 文件名（如 "guangzhou.json"） */
    public String fileName;

    /**
     * 是否为内部存储文件。
     * <ul>
     *     <li>true - 保存在应用内部存储（用户导入）</li>
     *     <li>false - 保存在 assets 目录（内置）</li>
     * </ul>
     */
    public boolean isInternal;

    /** 文件大小（字节），内置地图可设为 0 */
    public long fileSize;

    public MapFileInfo(String fileName, boolean isInternal, long fileSize) {
        this.fileName = fileName;
        this.isInternal = isInternal;
        this.fileSize = fileSize;
    }
}