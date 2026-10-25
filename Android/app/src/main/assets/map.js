// ============================================================
//  工具函数（排序 & 亮度）
// ============================================================
function extractPrefix(name) {
    const match = name.match(/^\D*/);
    return match ? match[0] : '';
}

function extractLineNumber(name) {
    const match = name.match(/\d+/);
    return match ? parseInt(match[0], 10) : -1;
}

function compareLineName(nameA, nameB) {
    const prefixA = extractPrefix(nameA);
    const prefixB = extractPrefix(nameB);
    const emptyA = prefixA === '';
    const emptyB = prefixB === '';
    if (emptyA !== emptyB) {
        return emptyA ? -1 : 1;
    }
    if (prefixA !== prefixB) {
        return prefixA.localeCompare(prefixB);
    }
    const numA = extractLineNumber(nameA);
    const numB = extractLineNumber(nameB);
    if (numA !== numB) {
        return numA - numB;
    }
    return nameA.localeCompare(nameB);
}

function getContrastTextColor(hexColor) {
    let r, g, b;
    if (hexColor.startsWith('#')) {
        let c = hexColor.substring(1);
        if (c.length === 3) {
            c = c[0] + c[0] + c[1] + c[1] + c[2] + c[2];
        }
        r = parseInt(c.substring(0, 2), 16);
        g = parseInt(c.substring(2, 4), 16);
        b = parseInt(c.substring(4, 6), 16);
    } else {
        return '#000';
    }
    const luminance = 0.299 * r + 0.587 * g + 0.114 * b;
    return luminance > 160 ? '#000' : '#fff';
}

// ============================================================
//  平滑曲线（Catmull-Rom 样条）
// ============================================================
function generateSmoothPoints(points, closed, segmentsPerSegment) {
    // 对每个原始折点补齐邻接控制点，再按 Catmull-Rom 公式插值生成平滑折线。
    if (!points || points.length < 2) return points.slice();
    const pts = points.map(p => (Array.isArray(p) ? { x: p[0], y: p[1] } : p));
    if (pts.length === 2) {
        return pts.map(p => [p.x, p.y]);
    }
    let workPts = pts.slice();
    if (closed) {
        const first = workPts[0];
        const last = workPts[workPts.length - 1];
        if (first.x !== last.x || first.y !== last.y) {
            workPts.push(first);
        }
    }
    if (workPts.length < 4) {
        return workPts.map(p => [p.x, p.y]);
    }
    let expanded = [];
    if (closed) {
        const n = workPts.length;
        expanded.push(workPts[n - 1]);
        expanded.push(...workPts);
        expanded.push(workPts[0]);
    } else {
        const first = workPts[0];
        const second = workPts[1];
        const last = workPts[workPts.length - 1];
        const prevLast = workPts[workPts.length - 2];
        expanded.push({
            x: first.x - (second.x - first.x),
            y: first.y - (second.y - first.y)
        });
        expanded.push(...workPts);
        expanded.push({
            x: last.x + (last.x - prevLast.x),
            y: last.y + (last.y - prevLast.y)
        });
    }
    const result = [];
    const seg = segmentsPerSegment || 10;
    for (let i = 0; i < expanded.length - 3; i++) {
        const p0 = expanded[i];
        const p1 = expanded[i + 1];
        const p2 = expanded[i + 2];
        const p3 = expanded[i + 3];
        for (let j = 0; j < seg; j++) {
            const t = j / seg;
            const t2 = t * t;
            const t3 = t2 * t;
            const x = 0.5 * ((2 * p1.x) +
                (-p0.x + p2.x) * t +
                (2 * p0.x - 5 * p1.x + 4 * p2.x - p3.x) * t2 +
                (-p0.x + 3 * p1.x - 3 * p2.x + p3.x) * t3);
            const y = 0.5 * ((2 * p1.y) +
                (-p0.y + p2.y) * t +
                (2 * p0.y - 5 * p1.y + 4 * p2.y - p3.y) * t2 +
                (-p0.y + 3 * p1.y - 3 * p2.y + p3.y) * t3);
            result.push([x, y]);
        }
    }
    return result;
}

// ============================================================
//  全局状态
// ============================================================
let metroData = null;
const LABEL_BASE_SIZE = 2;
let globalBounds = null;
let currentZoomK = 1;
let currentMode = 'all'; // 'all' | 'highlight' | 'route'
let currentHighlightGroupKey = 'all';
let routeStationNames = [];

// 用于存储渲染上下文
let svg, g, pathGroup, stationGroup, tempLineGroup;
let pathElements, stationCircles, stationLabels;
let platformCircles, expressStopCircles, routingCircles;
let zoomBehavior;
let stationData = [];
let rawDirections = [];
let mergedLines = [];
let dirKeyToGroupKey = new Map();

// 缩放样式更新调度
let updateScheduled = false;
let pendingK = 1;
let styleUpdatePending = false;
let pendingZoomK = 1;

// 已开通站点集合（在 drawMap 中填充）
const activeStationSet = new Set();

// ============================================================
//  主绘图函数
// ============================================================
function drawMap(data) {
    metroData = data;
    // drawMap 是渲染入口：先建立可复用索引，再创建 SVG 图层和交互处理器。
    const stations = data.stations;
    const lines = data.lines;
    const keyCity = data.key_city;
    d3.select("#loading").style("display", "none");

    // --- 填充 activeStationSet（已开通站点） ---
    activeStationSet.clear();
    for (const lineInfo of Object.values(lines)) {
        for (const dir of lineInfo.direction) {
            if (dir.is_future !== true) { // 非未来方向
                for (const st of dir.station) {
                    activeStationSet.add(st.name);
                }
            }
        }
    }

    // --- 1. 解析方向：把 JSON 方向转换为绘图和筛选都能复用的轻量对象。 ---
    rawDirections = [];
    const stationToDirKeys = {};
    for (const [lineId, lineInfo] of Object.entries(lines)) {
        lineInfo.direction.forEach((dir, idx) => {
            const dirKey = lineId + '|' + idx;
            const city = dir.city || lineInfo.city || keyCity;
            const baseName = lineInfo.name || lineId;
            const groupKey = (city !== keyCity) ? city + baseName : baseName;
            const displayName = groupKey;
            const color = lineInfo.color || '#888';
            const isLoop = dir.is_loop || false;
            const stationNames = dir.station.map(s => s.name);
            const isFuture = dir.is_future === true;

            rawDirections.push({
                key: dirKey,
                groupKey: groupKey,
                displayName: displayName,
                color: color,
                stationNames: stationNames,
                isLoop: isLoop,
                lineId: lineId,
                isFuture: isFuture,
                dirIndex: idx,
                dirObj: dir,
                segmentTypes: dir.segment_types || [],
                expressStops: dir.express_stops || [],
                routings: dir.routings || []
            });

            stationNames.forEach(name => {
                if (!stationToDirKeys[name]) stationToDirKeys[name] = new Set();
                stationToDirKeys[name].add(dirKey);
            });
        });
    }

    // --- 2. 建立 dirKey → groupKey 映射 ---
    dirKeyToGroupKey = new Map();
    for (const dir of rawDirections) {
        dirKeyToGroupKey.set(dir.key, dir.groupKey);
    }

    // --- 3. 收集快车停靠站及其线路颜色 ---
    const expressColorByStation = {};
    const expressLinesByStation = {};
    rawDirections.forEach(dir => {
        if (!dir.expressStops || dir.expressStops.length === 0) return;
        dir.expressStops.forEach(name => {
            if (!expressColorByStation[name]) expressColorByStation[name] = [];
            if (!expressColorByStation[name].includes(dir.color)) expressColorByStation[name].push(dir.color);
            if (!expressLinesByStation[name]) expressLinesByStation[name] = [];
            if (!expressLinesByStation[name].some(e => e.color === dir.color)) {
                expressLinesByStation[name].push({ name: dir.displayName, color: dir.color });
            }
        });
    });

    // --- 4. 收集交路信息：站点 → 交路名称列表（含线路显示名） ---
    const routingByStation = {};
    rawDirections.forEach(dir => {
        if (!dir.routings || dir.routings.length === 0) return;
        dir.routings.forEach(routing => {
            if (!routing || !routing.name || !Array.isArray(routing.stations)) return;
            routing.stations.forEach(name => {
                if (!routingByStation[name]) routingByStation[name] = [];
                const label = dir.displayName + '·' + routing.name;
                if (!routingByStation[name].includes(label)) {
                    routingByStation[name].push({ text: label, color: dir.color });
                }
            });
        });
    });

    // --- 5. 合并同组线路（同名线路的多个方向），用于图例和高亮状态。 ---
    const groupMap = new Map();
    for (const dir of rawDirections) {
        const key = dir.groupKey;
        if (!groupMap.has(key)) {
            groupMap.set(key, {
                keys: [],
                color: dir.color,
                stationNamesSet: new Set(),
                isLoop: dir.isLoop
            });
        }
        const group = groupMap.get(key);
        group.keys.push(dir.key);
        dir.stationNames.forEach(s => group.stationNamesSet.add(s));
        if (dir.isLoop) group.isLoop = true;
    }

    mergedLines = [];
    for (const [groupKey, group] of groupMap) {
        mergedLines.push({
            groupKey: groupKey,
            displayName: groupKey,
            keys: group.keys,
            color: group.color,
            stationNames: Array.from(group.stationNamesSet),
            isLoop: group.isLoop
        });
    }
    mergedLines.sort((a, b) => compareLineName(a.displayName, b.displayName));

    // --- 6. 坐标转换 ---
    const coords = Object.values(stations).map(s => [s.lon, s.lat]);
    const allLons = coords.map(c => c[0]);
    const allLats = coords.map(c => c[1]);
    const minLon = Math.min(...allLons);
    const maxLon = Math.max(...allLons);
    const minLat = Math.min(...allLats);
    const maxLat = Math.max(...allLats);

    const width = window.innerWidth;
    const height = window.innerHeight;
    const margin = 20;

    const lonRange = maxLon - minLon || 0.01;
    const latRange = maxLat - minLat || 0.01;
    const scaleX = (width - 2 * margin) / lonRange;
    const scaleY = (height - 2 * margin) / latRange;
    const scale = Math.min(scaleX, scaleY) * 0.9;

    const size = Math.min(width, height) * 0.9;

    const xScale = d3.scaleLinear()
        .domain([minLon, maxLon])
        .range([margin, size - margin]);

    const yScale = d3.scaleLinear()
        .domain([minLat, maxLat])
        .range([size - margin, margin]);

    // --- 7. 清空容器 ---
    d3.select("#container").selectAll("*").remove();
    const loading = d3.select("#container").append("div").attr("id", "loading").style("display", "none");

    // --- 8. SVG ---
    svg = d3.select("#container")
        .append("svg")
        .attr("width", width)
        .attr("height", height)
        .style("background", "transparent");

    updateScheduled = true;

    // --- 9. Zoom ---
    zoomBehavior = d3.zoom()
        .scaleExtent([0.001, 100])
        .on("zoom", (event) => {
            const transform = event.transform;
            g.attr("transform", transform);
            const k = transform.k;
            currentZoomK = k;
            pendingK = k;

            // 实时更新位置和大小（这些必须立即变化，否则视觉跳跃）
            stationLabels
                .attr("y", d => d.y - LABEL_BASE_SIZE / k)
                .attr("font-size", Math.min(4, Math.max(1, LABEL_BASE_SIZE / k)) + "px");

            stationCircles
                .attr("r", Math.min(0.3, Math.max(5, 2 / k)));

            // 样式更新（透明度、宽度等）延迟合并
            if (!updateScheduled) {
                updateScheduled = true;
                requestAnimationFrame(() => {
                    updateScheduled = false;
                    applyStylesSmoothly(pendingK);
                });
            }
        });

    svg.call(zoomBehavior);
    g = svg.append("g");

    // --- 10. 构建平台坐标查找表（优先平台坐标绘制线路） ---
    const platformCoordLookup = {};
    for (const [stationName, stationInfo] of Object.entries(stations)) {
        if (stationInfo.lines) {
            for (const lineInfo of stationInfo.lines) {
                if (lineInfo.platform_coord) {
                    if (!platformCoordLookup[stationName]) platformCoordLookup[stationName] = {};
                    platformCoordLookup[stationName][lineInfo.id] = {
                        lon: lineInfo.platform_coord.lon,
                        lat: lineInfo.platform_coord.lat
                    };
                }
            }
        }
    }

    // --- 定义坐标获取函数（优先平台坐标） ---
    function getCoord(stationName, lineId) {
        if (platformCoordLookup[stationName] && platformCoordLookup[stationName][lineId]) {
            const pc = platformCoordLookup[stationName][lineId];
            return { x: xScale(pc.lon), y: yScale(pc.lat) };
        } else {
            const st = stationData.find(d => d.name === stationName);
            return st ? { x: st.x, y: st.y } : null;
        }
    }

    // --- 11. 站点数据（包含换乘/快车/交路/暂缓等判断） ---
    stationData = Object.entries(stations).map(([name, info]) => {
        const dirKeys = Array.from(stationToDirKeys[name] || []);

        // 判断是否属于已开通方向
        const isActive = activeStationSet.has(name);

        // 是否只属于未来方向
        const isFutureOnly = !isActive && dirKeys.length > 0 &&
            dirKeys.every(k => {
                const dir = rawDirections.find(d => d.key === k);
                return dir && dir.isFuture === true;
            });

        // 换乘站判断（基于不同 groupKey）
        const uniqueGroupKeys = new Set();
        dirKeys.forEach(k => {
            const gk = dirKeyToGroupKey.get(k);
            if (gk) uniqueGroupKeys.add(gk);
        });
        const isTransfer = uniqueGroupKeys.size > 1;

        return {
            name,
            lon: info.lon,
            lat: info.lat,
            x: xScale(info.lon),
            y: yScale(info.lat),
            dirKeys: dirKeys,
            isActive: isActive,
            isFutureOnly: isFutureOnly,
            isTransfer: isTransfer,
            expressColors: expressColorByStation[name] || [],
            expressLines: expressLinesByStation[name] || [],
            routingInfos: routingByStation[name] || [],
            otherNames: info.other_names || [],
            pendingLines: (() => {
                const seen = new Set();
                const result = [];
                for (const l of (info.lines || [])) {
                    if (l.status !== 'pending') continue;
                    const dir = rawDirections.find(d => d.lineId === l.id);
                    const lname = dir ? dir.displayName : l.id;
                    if (seen.has(lname)) continue;
                    seen.add(lname);
                    result.push({ name: lname, color: dir ? dir.color : '#888' });
                }
                return result;
            })()
        };
    }).filter(d => !d.isFutureOnly); // 不绘制仅属于未来方向的站点

    // --- 12. 路径数据（跳过未来方向，不绘制未来线路） ---
    const pathData = [];
    for (const dir of rawDirections) {
        if (dir.isFuture) continue; // 不绘制未来线路
        const stationNames = dir.stationNames;
        if (stationNames.length < 2) continue;

        const pts = stationNames
            .map(name => {
                const coord = getCoord(name, dir.lineId);
                return coord ? { x: coord.x, y: coord.y } : null;
            })
            .filter(p => p !== null);

        if (pts.length < 2) continue;

        const isLoop = dir.isLoop;
        const smoothPoints = generateSmoothPoints(pts, isLoop, 10);
        let d = "";
        if (smoothPoints.length > 0) {
            d = "M" + smoothPoints.map(p => p.join(",")).join("L");
            if (isLoop) d += "Z";
        } else {
            d = "M" + pts.map(p => p.x + "," + p.y).join("L");
            if (isLoop) d += "Z";
        }

        pathData.push({
            key: dir.key,
            groupKey: dir.groupKey,
            displayName: dir.displayName,
            color: dir.color,
            d: d,
            isFuture: false,
            strokeDasharray: 'none',
            strokeWidth: 1.2,
            opacity: 1.0,
            segmentTypes: dir.segmentTypes || [],
            effectiveStationNames: stationNames,
            smoothPoints: smoothPoints,
            pts: pts
        });
    }

    // --- 13. 区域图层（已移除，不绘制区域信息） ---

    // --- 14. 背景图片图层（置于最底层，在区域图层之下） ---
    const backgrounds = data.backgrounds || [];
    if (backgrounds.length > 0) {
        const bgGroup = g.insert("g", ":first-child").attr("class", "backgrounds");
        backgrounds.forEach(bg => {
            if (!bg.Src || !bg.Bounds || bg.Bounds.length !== 4) return;
            const bx = xScale(bg.Bounds[0]);
            const by = yScale(bg.Bounds[3]);
            const bw = xScale(bg.Bounds[2]) - xScale(bg.Bounds[0]);
            const bh = yScale(bg.Bounds[1]) - yScale(bg.Bounds[3]);
            const mimeType = bg.Type === 'svg' ? 'image/svg+xml' : 'image/png';
            const href = `data:${mimeType};base64,${bg.Src}`;
            bgGroup.append("image")
                .attr("x", bx)
                .attr("y", by)
                .attr("width", Math.max(bw, 1))
                .attr("height", Math.max(bh, 1))
                .attr("href", href)
                .attr("preserveAspectRatio", "none")
                .attr("opacity", bg.Opacity != null ? bg.Opacity : 1)
                .attr("pointer-events", "none");
        });
    }

    // --- 15. 绘制线路 ---
    pathGroup = g.append("g").attr("class", "paths");
    pathElements = pathGroup.selectAll(".line-path")
        .data(pathData)
        .enter()
        .append("path")
        .attr("class", "line-path")
        .attr("d", d => d.d)
        .attr("stroke", d => d.color)
        .attr("stroke-width", 1.0)
        .attr("opacity", 1)
        .attr("data-group", d => d.groupKey)
        .attr("data-key", d => d.key);

    // --- 16. 路段类型叠加层（高架/地下/地面） ---
    const segmentOverlayGroup = pathGroup.append("g").attr("class", "segment-overlay");
    const segPerSegment = 10;
    pathData.forEach(pd => {
        if (!pd.segmentTypes || pd.segmentTypes.length === 0) return;
        const names = pd.effectiveStationNames;
        const sp = pd.smoothPoints;
        const rawPts = pd.pts;
        if (!names || names.length < 2) return;

        pd.segmentTypes.forEach(seg => {
            const fromIdx = names.indexOf(seg.from);
            const toIdx = names.indexOf(seg.to);
            if (fromIdx < 0 || toIdx < 0) return;

            // 确保顺序正确（from 在 to 之前）
            let idxA = Math.min(fromIdx, toIdx);
            let idxB = Math.max(fromIdx, toIdx);
            if (idxB - idxA !== 1) return; // 只处理相邻站

            // 从平滑点中提取该段的子路径
            let segD = "";
            if (sp && sp.length > 0) {
                const startIdx = idxA * segPerSegment;
                const endIdx = Math.min((idxA + 1) * segPerSegment, sp.length);
                const subPts = sp.slice(startIdx, endIdx + 1);
                if (subPts.length > 0) {
                    segD = "M" + subPts.map(p => p.join(",")).join("L");
                }
            }
            if (!segD && rawPts && rawPts.length > idxA) {
                const p1 = rawPts[idxA];
                const p2 = rawPts[idxB] || rawPts[idxA];
                segD = `M${p1.x},${p1.y}L${p2.x},${p2.y}`;
            }
            if (!segD) return;

            const segType = (seg.type || '').toLowerCase();
            let dashArray = 'none';
            let strokeWidth = 0.45;
            if (segType === 'underground') {
                dashArray = '1.2,0.6';
            } else if (segType === 'ground') {
                dashArray = '0.3,0.45';
                strokeWidth = 0.35;
            } else if (segType === 'elevated') {
                dashArray = '1.8,0.3,0.3,0.3';
            }

            segmentOverlayGroup.append("path")
                .attr("class", "segment-overlay-path")
                .attr("d", segD)
                .attr("stroke", pd.color)
                .attr("stroke-width", strokeWidth)
                .attr("stroke-dasharray", dashArray)
                .attr("fill", "none")
                .attr("opacity", pd.isFuture ? 0.5 : 0.85)
                .attr("stroke-linecap", "round")
                .attr("stroke-linejoin", "round")
                .attr("pointer-events", "none");
        });
    });

    // --- 17. 站点（圆 + 标签） ---
    stationGroup = g.append("g").attr("class", "stations");

    stationCircles = stationGroup.selectAll(".station")
        .data(stationData)
        .enter()
        .append("circle")
        .attr("class", "station")
        .attr("cx", d => d.x)
        .attr("cy", d => d.y)
        .attr("r", 1.5)
        .attr("fill", d => {
            if (d.isTransfer) return "#FF45BB";
            if (d.dirKeys.length > 1) return "#FF4500";
            else if (d.dirKeys.length === 1) {
                const key = d.dirKeys[0];
                const dir = rawDirections.find(di => di.key === key);
                return dir ? dir.color : '#888';
            }
            return '#888';
        })
        .attr("stroke", "none")
        .attr("stroke-width", 0)
        .attr("opacity", 1.0)
        .style("cursor", "pointer")
        .on("mouseover", (event, d) => {
            if (!shouldRespondToEvent(d)) return;
            const nameSet = new Map();
            d.dirKeys.forEach(k => {
                const dir = rawDirections.find(di => di.key === k);
                if (dir) {
                    if (!nameSet.has(dir.displayName)) {
                        nameSet.set(dir.displayName, dir.color);
                    }
                }
            });
            const sortedEntries = Array.from(nameSet.entries()).sort((a, b) => compareLineName(a[0], b[0]));

            let cardsHtml = '';
            sortedEntries.forEach(([displayName, color]) => {
                const textColor = getContrastTextColor(color);
                cardsHtml += `<span class="line-tag" style="background-color:${color}; color:${textColor};">${displayName}</span>`;
            });

            // 交路信息：显示该站属于哪些交路
            let routingHtml = '';
            if (d.routingInfos && d.routingInfos.length > 0) {
                d.routingInfos.forEach(ri => {
                    routingHtml += `<span class="routing-tag" style="color:${ri.color}; border-color:${ri.color};">⟳ ${ri.text}</span>`;
                });
            }

            const tooltip = d3.select("#tooltip");
            tooltip.select(".station-name").text(d.name);
            tooltip.select(".line-tag-container").html(cardsHtml);

            // 快车停靠：显示该站作为快车停靠站的线路（带线路色）
            let expressHtml = '';
            if (d.expressLines && d.expressLines.length > 0) {
                expressHtml = d.expressLines.map(e => {
                    const tc = getContrastTextColor(e.color);
                    return `<span class="express-tag" style="background-color:${e.color}; color:${tc};">快车 ${e.name}</span>`;
                }).join('');
            }
            tooltip.select(".express-container").html(expressHtml);

            // 暂缓开通：显示该站在某些线路上的暂缓状态
            let pendingHtml = '';
            if (d.pendingLines && d.pendingLines.length > 0) {
                pendingHtml = d.pendingLines.map(p => {
                    return `<span class="pending-tag">暂缓 ${p.name}</span>`;
                }).join('');
            }
            tooltip.select(".pending-container").html(pendingHtml);

            tooltip.select(".routing-container").html(routingHtml);

            // 其他语言名称（翻译名）：显示为灰色标签
            let otherNamesHtml = '';
            if (d.otherNames && d.otherNames.length > 0) {
                otherNamesHtml = d.otherNames
                    .map(n => `<span class="other-name-tag">${n}</span>`)
                    .join('');
            }
            tooltip.select(".other-names-container").html(otherNamesHtml);

            tooltip
                .style("opacity", 1)
                .style("left", (event.pageX + 14) + "px")
                .style("top", (event.pageY - 10) + "px");
        })
        .on("mouseout", () => {
            d3.select("#tooltip").style("opacity", 0);
        })
        .on("click", (event, d) => {
            if (d.isFutureOnly) return;
            if (!shouldRespondToEvent(d)) return;
            showStationInfo(d.name, d.dirKeys);
        });

    // 长按（触屏）
    let longPressTimer = null;
    stationCircles.on("touchstart", function(event) {
        const d = d3.select(this).datum();
        if (!shouldRespondToEvent(d)) {
            clearTimeout(longPressTimer);
            return;
        }
        longPressTimer = setTimeout(() => {
            showStationInfo(d.name, d.dirKeys);
            if (window.Android && window.Android.onStationLongClick) {
                window.Android.onStationLongClick(d.name);
            }
            event.preventDefault();
        }, 500);
    }).on("touchend", function() {
        clearTimeout(longPressTimer);
    }).on("touchmove", function() {
        clearTimeout(longPressTimer);
    });

    // 站点标签
    stationLabels = stationGroup.selectAll(".station-label")
        .data(stationData)
        .enter()
        .append("text")
        .attr("class", "station-label")
        .attr("x", d => d.x)
        .attr("y", d => d.y - LABEL_BASE_SIZE)
        .attr("text-anchor", "middle")
        .attr("font-size", LABEL_BASE_SIZE + "px")
        .attr("fill", "#222")
        .attr("opacity", 1.0)
        .text(d => d.name)
        .style("font-family", "Microsoft YaHei, PingFang SC, sans-serif")
        .style("text-shadow", "0 0 4px #fff, 0 0 4px #fff, 0 0 8px #fff")
        .style("user-select", "none")
        .style("font-weight", "500");

    // --- 18. 绘制平台坐标（虚线空心圆） ---
    const platformCoords = [];
    for (const [stationName, lineMap] of Object.entries(platformCoordLookup)) {
        for (const [lineId, coord] of Object.entries(lineMap)) {
            const px = xScale(coord.lon);
            const py = yScale(coord.lat);
            platformCoords.push({ x: px, y: py, stationName, lineId });
        }
    }
    platformCircles = stationGroup.selectAll(".platform-coord")
        .data(platformCoords)
        .enter()
        .append("circle")
        .attr("class", "platform-coord")
        .attr("cx", d => d.x)
        .attr("cy", d => d.y)
        .attr("r", 0.2)
        .attr("fill", "none")
        .attr("stroke", "#999")
        .attr("stroke-width", 0.06)
        .style("pointer-events", "none");

    // --- 19. 快车停靠站标记（空心环，置于站点圆之上） ---
    expressStopCircles = stationGroup.selectAll(".express-stop")
        .data(stationData.filter(d => d.expressColors.length > 0))
        .enter()
        .append("circle")
        .attr("class", "express-stop")
        .attr("cx", d => d.x)
        .attr("cy", d => d.y)
        .attr("r", 0.25)
        .attr("fill", "#fff")
        .attr("fill-opacity", 0.9)
        .attr("stroke", d => d.expressColors[0])
        .attr("stroke-width", 0.08)
        .attr("data-station", d => d.name)
        .style("pointer-events", "none");

    // --- 20. 交路站点标记（虚线圆环，表示该站属于某交路） ---
    routingCircles = stationGroup.selectAll(".routing-stop")
        .data(stationData.filter(d => d.routingInfos.length > 0))
        .enter()
        .append("circle")
        .attr("class", "routing-stop")
        .attr("cx", d => d.x)
        .attr("cy", d => d.y)
        .attr("r", 0.25)
        .attr("fill", "none")
        .attr("stroke", d => d.routingInfos[0].color)
        .attr("stroke-width", 0.06)
        .attr("stroke-dasharray", "0.45,0.3")
        .attr("data-station", d => d.name)
        .style("pointer-events", "none");

    // --- 21. 临时图层（路径/高亮） ---
    tempLineGroup = g.insert('g', '.stations').attr("class", "temp-layer");

    // --- 22. 全局边界 ---
    if (stationData.length > 0) {
        const xs = stationData.map(d => d.x);
        const ys = stationData.map(d => d.y);
        globalBounds = {
            minX: Math.min(...xs),
            maxX: Math.max(...xs),
            minY: Math.min(...ys),
            maxY: Math.max(...ys)
        };
    } else {
        globalBounds = { minX: 0, maxX: 1, minY: 0, maxY: 1 };
    }

    // --- 23. 初始状态（默认只显示换乘站） ---
    setAllMode();

    // --- 暴露全局对象供 Android 调用 ---
    window.__stationData = stationData;
    window.__mergedLines = mergedLines;
    window.__rawDirections = rawDirections;
    window.__highlightLine = highlightLine;
    window.__zoomToPoints = zoomToPoints;
    window.__drawTempLine = drawTempLine;
    window.__clearTempMode = clearTempMode;
    window.__setAllMode = setAllMode;
    window.__setRouteMode = setRouteMode;
    window.__tempLineGroup = tempLineGroup;
    window.__svg = svg;
    window.__zoom = zoomBehavior;
    window.__globalBounds = globalBounds;
    window.__metroData = data;
}

// ============================================================
//  交互函数（与 Android 共用）
// ============================================================
function shouldRespondToEvent(station) {
    // 交互命中测试与当前显示模式保持一致，避免隐藏线路仍响应点击。
    if (currentMode === 'all') {
        return true;
    }
    if (currentMode === 'highlight') {
        return station.dirKeys.some(k => {
            const gk = dirKeyToGroupKey.get(k);
            return gk === currentHighlightGroupKey;
        });
    }
    if (currentMode === 'route') {
        return routeStationNames.includes(station.name);
    }
    return true;
}

function updateStyles(k) {
    // 所有尺寸都按缩放比例反向调整，使线宽、站点和标记在屏幕上保持可读。
    const baseWidth = 1.8;
    const baseRadius = 0.1;
    const highlightFactor = 0.5;
    const dimFactor = 0.4;

    const scaledWidth = Math.min(Math.max(baseWidth / k, 0.8), 1);
    const scaledRadius = Math.min(Math.max(baseRadius / k, 0.1), 0.0);

    const isHighlight = (currentMode === 'highlight');
    const isRoute = (currentMode === 'route');

    // 线路样式
    pathElements
        .attr("stroke-width", d => {
            if (isHighlight && d.groupKey === currentHighlightGroupKey) {
                return scaledWidth * highlightFactor;
            } else if (isHighlight) {
                return scaledWidth * dimFactor;
            } else {
                return scaledWidth;
            }
        })
        .attr("opacity", d => {
            if (isHighlight && d.groupKey === currentHighlightGroupKey) {
                return 1;
            } else if (isHighlight) {
                return 0.15;
            } else if (isRoute) {
                return 0.15;
            } else {
                return 1;
            }
        });

    // 站点圆
    stationCircles.attr("r", scaledRadius);
    stationCircles.attr("opacity", d => {
        if (currentMode === 'all') {
            return d.isTransfer ? 1 : 0.5;
        }
        if (isHighlight) {
            const belongs = d.dirKeys.some(k => {
                const gk = dirKeyToGroupKey.get(k);
                return gk === currentHighlightGroupKey;
            });
            return belongs ? 1 : 0;
        }
        if (isRoute) {
            return routeStationNames.includes(d.name) ? 1 : 0;
        }
        return 1;
    });

    // 站点标签
    stationLabels.attr("opacity", d => {
        if (currentMode === 'all') {
            return d.isTransfer ? 1 : 0.2;
        }
        if (isRoute) {
            return routeStationNames.includes(d.name) ? 1 : 0;
        }
        if (isHighlight) {
            const belongs = d.dirKeys.some(k => {
                const gk = dirKeyToGroupKey.get(k);
                return gk === currentHighlightGroupKey;
            });
            return belongs ? 1 : 0;
        }
        return 1;
    });

    // 平台坐标圆圈随缩放调整大小
    if (platformCircles) {
        platformCircles
            .attr("r", Math.min(Math.max(0.2 / k, 0.05), 0.3))
            .attr("stroke-width", Math.min(Math.max(0.06 / k, 0.02), 0.1));
    }

    // 快车停靠站标记（空心环）随缩放调整，保持屏幕尺寸稳定
    if (expressStopCircles) {
        expressStopCircles
            .attr("r", Math.min(Math.max(0.25 / k, 0.05), 0.4))
            .attr("stroke-width", Math.min(Math.max(0.08 / k, 0.02), 0.15))
            .attr("opacity", d => {
                if (currentMode === 'all') return 1;
                if (isHighlight) {
                    const belongs = d.dirKeys.some(k => {
                        const gk = dirKeyToGroupKey.get(k);
                        return gk === currentHighlightGroupKey;
                    });
                    return belongs ? 1 : 0;
                }
                if (isRoute) {
                    return routeStationNames.includes(d.name) ? 1 : 0;
                }
                return 1;
            });
    }

    // 交路站点标记（虚线圆环）随缩放调整，保持屏幕尺寸稳定
    if (routingCircles) {
        routingCircles
            .attr("r", Math.min(Math.max(0.25 / k, 0.05), 0.4))
            .attr("stroke-width", Math.min(Math.max(0.06 / k, 0.02), 0.12))
            .attr("opacity", d => {
                if (currentMode === 'all') return 1;
                if (isHighlight) {
                    const belongs = d.dirKeys.some(k => {
                        const gk = dirKeyToGroupKey.get(k);
                        return gk === currentHighlightGroupKey;
                    });
                    return belongs ? 1 : 0;
                }
                if (isRoute) {
                    return routeStationNames.includes(d.name) ? 1 : 0;
                }
                return 1;
            });
    }

    // 临时图层
    tempLineGroup.selectAll(".temp-line")
        .attr("stroke-width", Math.min(Math.max(3 / k, 0.2), 0.03));
    tempLineGroup.selectAll(".temp-marker")
        .attr("r", Math.min(Math.max(0.2 / k, 0.2), 0.1));

    g.selectAll(".line-path")
        .attr("stroke-width", function(d) {
            let w = 1.8 / currentZoomK;
            w = Math.min(Math.max(w, 0.2), 0.5);
            return w;
        });
}

function scheduleStyleUpdate(k) {
    // 缩放事件频率很高，合并到下一帧只执行一次样式更新。
    pendingZoomK = k;
    if (!styleUpdatePending) {
        styleUpdatePending = true;
        requestAnimationFrame(() => {
            styleUpdatePending = false;
            applyStylesSmoothly(pendingZoomK);
        });
    }
}

function applyStylesSmoothly(k) {
    // ---- 以下所有计算与 updateStyles 完全一致 ----
    const baseWidth = 1.8;
    const baseRadius = 0.1;
    const highlightFactor = 0.5;
    const dimFactor = 0.4;

    const scaledWidth = Math.min(Math.max(baseWidth / k, 0.8), 1);
    const scaledRadius = Math.min(Math.max(baseRadius / k, 0.1), 0.0);

    const isHighlight = (currentMode === 'highlight');
    const isRoute = (currentMode === 'route');

    // 线路样式（与原来完全一样）
    pathElements
        .attr("stroke-width", d => {
            if (isHighlight && d.groupKey === currentHighlightGroupKey) {
                return scaledWidth * highlightFactor;
            } else if (isHighlight) {
                return scaledWidth * dimFactor;
            } else {
                return scaledWidth;
            }
        })
        .attr("opacity", d => {
            if (isHighlight && d.groupKey === currentHighlightGroupKey) {
                return 1;
            } else if (isHighlight) {
                return 0.15;
            } else if (isRoute) {
                return 0.15;
            } else {
                return 1;
            }
        });

    // 站点圆
    stationCircles.attr("r", scaledRadius);
    stationCircles.attr("opacity", d => {
        if (currentMode === 'all') {
            return d.isTransfer ? 1 : 0.5;
        }
        if (isHighlight) {
            const belongs = d.dirKeys.some(k => {
                const gk = dirKeyToGroupKey.get(k);
                return gk === currentHighlightGroupKey;
            });
            return belongs ? 1 : 0;
        }
        if (isRoute) {
            return routeStationNames.includes(d.name) ? 1 : 0;
        }
        return 1;
    });

    // 站点标签
    stationLabels.attr("opacity", d => {
        if (currentMode === 'all') {
            return d.isTransfer ? 1 : 0.2;
        }
        if (isRoute) {
            return routeStationNames.includes(d.name) ? 1 : 0;
        }
        if (isHighlight) {
            const belongs = d.dirKeys.some(k => {
                const gk = dirKeyToGroupKey.get(k);
                return gk === currentHighlightGroupKey;
            });
            return belongs ? 1 : 0;
        }
        return 1;
    });

    // 平台坐标圆圈随缩放调整大小
    if (platformCircles) {
        platformCircles
            .attr("r", Math.min(Math.max(0.2 / k, 0.05), 0.3))
            .attr("stroke-width", Math.min(Math.max(0.06 / k, 0.02), 0.1));
    }

    // 快车停靠站标记（空心环）随缩放调整，保持屏幕尺寸稳定
    if (expressStopCircles) {
        expressStopCircles
            .attr("r", Math.min(Math.max(0.25 / k, 0.05), 0.4))
            .attr("stroke-width", Math.min(Math.max(0.08 / k, 0.02), 0.15))
            .attr("opacity", d => {
                if (currentMode === 'all') return 1;
                if (isHighlight) {
                    const belongs = d.dirKeys.some(k => {
                        const gk = dirKeyToGroupKey.get(k);
                        return gk === currentHighlightGroupKey;
                    });
                    return belongs ? 1 : 0;
                }
                if (isRoute) {
                    return routeStationNames.includes(d.name) ? 1 : 0;
                }
                return 1;
            });
    }

    // 交路站点标记（虚线圆环）随缩放调整，保持屏幕尺寸稳定
    if (routingCircles) {
        routingCircles
            .attr("r", Math.min(Math.max(0.25 / k, 0.05), 0.4))
            .attr("stroke-width", Math.min(Math.max(0.06 / k, 0.02), 0.12))
            .attr("opacity", d => {
                if (currentMode === 'all') return 1;
                if (isHighlight) {
                    const belongs = d.dirKeys.some(k => {
                        const gk = dirKeyToGroupKey.get(k);
                        return gk === currentHighlightGroupKey;
                    });
                    return belongs ? 1 : 0;
                }
                if (isRoute) {
                    return routeStationNames.includes(d.name) ? 1 : 0;
                }
                return 1;
            });
    }

    // 临时图层
    tempLineGroup.selectAll(".temp-line")
        .attr("stroke-width", Math.min(Math.max(3 / k, 0.2), 0.1));
    tempLineGroup.selectAll(".temp-marker")
        .attr("r", Math.min(Math.max(0.2 / k, 0.2), 0.1));
}

function highlightLine(groupKey) {
    if (currentMode === 'highlight' && currentHighlightGroupKey === groupKey) {
        if (routeStationNames.length > 0) {
            setRouteMode();
        } else {
            setAllMode();
        }
        return;
    }

    currentMode = 'highlight';
    currentHighlightGroupKey = groupKey;
    tempLineGroup.selectAll("*").remove();

    const merged = mergedLines.find(m => m.displayName === groupKey);
    if (merged) {
        const points = merged.stationNames
            .map(name => {
                const found = stationData.find(d => d.name === name);
                return found ? { x: found.x, y: found.y } : null;
            })
            .filter(p => p !== null);
        if (points.length > 0) {
            zoomToPoints(points);
        }
    }

    updateStyles(currentZoomK);
}

function setAllMode() {
    currentMode = 'all';
    currentHighlightGroupKey = 'all';
    tempLineGroup.selectAll("*").remove();
    updateStyles(currentZoomK);
    zoomToPoints(null);
}

function setRouteMode() {
    if (routeStationNames.length > 0) {
        currentMode = 'route';
        currentHighlightGroupKey = 'all';
        drawTempLine(routeStationNames, true);
        updateStyles(currentZoomK);
    } else {
        setAllMode();
    }
}

function zoomToPoints(points) {
    let targetBounds;
    if (!points || points.length === 0) {
        targetBounds = globalBounds;
    } else {
        const xs = points.map(p => p.x);
        const ys = points.map(p => p.y);
        targetBounds = {
            minX: Math.min(...xs),
            maxX: Math.max(...xs),
            minY: Math.min(...ys),
            maxY: Math.max(...ys)
        };
    }
    if (!targetBounds) return;

    const { minX, maxX, minY, maxY } = targetBounds;
    const rangeX = maxX - minX || 1;
    const rangeY = maxY - minY || 1;
    const centerX = (minX + maxX) / 2;
    const centerY = (minY + maxY) / 2;

    const margin = 10;
    const w = window.innerWidth;
    const h = window.innerHeight;
    const scaleX = (w - 2 * margin) / rangeX;
    const scaleY = (h - 2 * margin) / rangeY;
    const fitScale = Math.min(scaleX, scaleY, 10);

    const transform = d3.zoomIdentity
        .translate(w / 2 - centerX * fitScale, h / 2 - centerY * fitScale)
        .scale(fitScale);

    svg.transition().duration(500).call(zoomBehavior.transform, transform);
}

// ---------- 路径绘制（Android 调用） ----------
function drawTempLine(stationNames, isRestore = false) {
    // 临时路线独立于原始线路图层，清除时只需移除 tempLineGroup。
    if (!isRestore) {
        routeStationNames = stationNames.slice();
        currentMode = 'route';
        currentHighlightGroupKey = 'all';
        tempLineGroup.selectAll("*").remove();
    }

    const points = stationNames
        .map(name => {
            const found = stationData.find(d => d.name === name);
            if (!found) {
                console.warn(`站点 "${name}" 未找到`);
                return null;
            }
            return { x: found.x, y: found.y, name: found.name, dirKeys: found.dirKeys };
        })
        .filter(p => p !== null);

    if (points.length < 2) {
        console.warn('路径站点不足');
        if (!isRestore) {
            if (routeStationNames.length > 0) {
                routeStationNames = [];
                setAllMode();
            }
        }
        return;
    }

    if (!isRestore) {
        zoomToPoints(points.map(p => ({ x: p.x, y: p.y })));
    }

    const lineD = "M" + points.map(p => p.x + "," + p.y).join("L");
    tempLineGroup.append("path")
        .attr("d", lineD)
        .attr("class", "temp-line")
        .style("stroke", "#FF0000")
        .style("stroke-width", 0.1)
        .style("stroke-dasharray", "2,2")
        .style("fill", "none")
        .style("opacity", 0.9)
        .style("pointer-events", "none");

    points.forEach(p => {
        const isTransfer = p.dirKeys.length > 1;
        if (isTransfer) {
            tempLineGroup.append("circle")
                .attr("cx", p.x)
                .attr("cy", p.y)
                .attr("r", 0.2)
                .attr("class", "temp-marker")
                .style("fill", "#FF0000")
                .style("stroke", "#fff")
                .style("stroke-width", 0.1)
                .style("pointer-events", "none");
        }
    });

    updateStyles(currentZoomK);
}

function clearTempMode() {
    setAllMode();
}

// ---------- Android 注入接口 ----------
function loadDataFromAndroid(jsonData) {
    // AndroidBridge 传入的值可能是对象，也可能是转义后的 JSON 字符串。
    console.log('接收到数据:', jsonData);
    if (jsonData) {
        try {
            let data = jsonData;
            if (typeof jsonData === 'string') {
                data = JSON.parse(jsonData);
            }
            console.log('数据解析成功，站点数:', Object.keys(data.stations).length);
            drawMap(data);
            if (window.Android && window.Android.onMapReady) {
                window.Android.onMapReady();
            }
        } catch (e) {
            console.error('加载失败:', e);
        }
    } else {
        console.warn('数据为空');
    }
}

// ---------- Android 调用的其他接口 ----------
function drawPathFromAndroid(stationNames) {
    // Android 已完成路径搜索，这里只负责将站点名映射到坐标并绘制红色覆盖线。
    console.log("drawPathFromAndroid 接收到:", stationNames);
    if (!stationNames || stationNames.length < 2) {
        console.warn('路径站点不足');
        return;
    }
    const points = stationNames
        .map(name => stationData.find(d => d.name.trim() === name.trim()))
        .filter(p => p !== undefined);

    if (points.length < 2) {
        console.warn('部分站点未找到，实际找到:', points.length);
        return;
    }

    currentMode = 'route';
    currentHighlightGroupKey = 'all';
    routeStationNames = stationNames.slice();

    if (tempLineGroup) tempLineGroup.selectAll("*").remove();

    let d = "M" + points.map(p => p.x + "," + p.y).join("L");

    tempLineGroup.append("path")
        .attr("d", d)
        .attr("class", "temp-line")
        .style("stroke", "#FF0000")
        .style("stroke-width", 0.5)
        .style("fill", "none")
        .style("opacity", 0.9)
        .style("pointer-events", "none");

    points.forEach(p => {
        const isTransfer = p.dirKeys && p.dirKeys.length > 1;
        tempLineGroup.append("circle")
            .attr("cx", p.x)
            .attr("cy", p.y)
            .attr("r", isTransfer ? 0.3 : 0.1)
            .attr("class", "temp-marker")
            .style("fill", isTransfer ? "#FF4500" : "#FF0000")
            .style("stroke", "#fff")
            .style("stroke-width", 0.3)
            .style("pointer-events", "none");
    });

    updateStyles(currentZoomK);
    window.__zoomToPoints(points);
}

function highlightLinesFromAndroid(lineIds) {
    // Java 侧传入线路 ID，前端转换成当前城市使用的 groupKey 后复用既有高亮逻辑。
    console.log("highlightLinesFromAndroid 接收到:", lineIds);
    if (!lineIds || lineIds.length === 0) {
        console.log("清除高亮");
        if (window.__setAllMode) {
            window.__setAllMode();
        }
        return;
    }
    if (!metroData) {
        console.warn("metroData 未初始化");
        return;
    }
    const lines = metroData.lines;
    const keyCity = metroData.key_city;
    const groupKeys = [];
    for (const id of lineIds) {
        const lineInfo = lines[id];
        if (!lineInfo) continue;
        const city = lineInfo.city || keyCity;
        const baseName = lineInfo.name || id;
        const groupKey = (city !== keyCity) ? city + baseName : baseName;
        groupKeys.push(groupKey);
        console.log("生成 groupKey:", groupKey);
    }
    if (groupKeys.length === 0) return;
    const firstGroupKey = groupKeys[0];
    console.log("调用 __highlightLine，参数:", firstGroupKey);
    if (window.__highlightLine) {
        window.__highlightLine(firstGroupKey);
    }
}

function clearRouteFromAndroid() {
    window.__clearTempMode();
}

function zoomToStationsFromAndroid(stationNames) {
    const points = stationNames
        .map(name => stationData.find(d => d.name === name))
        .filter(p => p !== undefined)
        .map(p => ({ x: p.x, y: p.y }));
    if (points.length > 0) {
        window.__zoomToPoints(points);
    }
}

function zoomToLinesFromAndroid(lineIds) {
    // 线路缩放先收集该线路所有方向的站点，再交给统一的站点缩放函数计算边界。
    if (!metroData) return;
    const lines = metroData.lines;
    const allStations = new Set();
    for (const id of lineIds) {
        const lineInfo = lines[id];
        if (!lineInfo) continue;
        for (const dir of lineInfo.direction) {
            for (const st of dir.station) {
                allStations.add(st.name);
            }
        }
    }
    if (allStations.size > 0) {
        zoomToStationsFromAndroid(Array.from(allStations));
    }
}

function showErrorFromAndroid(msg) {
    alert('错误: ' + msg);
}

// ============================================================
//  信息面板（站点详情）
// ============================================================
function showStationInfo(name, dirKeys) {
    const panel = d3.select("#info-panel");
    const content = d3.select("#info-content");

    const lineMap = new Map();
    dirKeys.forEach(k => {
        const dir = rawDirections.find(d => d.key === k);
        if (dir) {
            if (!lineMap.has(dir.displayName)) {
                lineMap.set(dir.displayName, dir.color);
            }
        }
    });

    const sortedEntries = Array.from(lineMap.entries()).sort((a, b) => compareLineName(a[0], b[0]));

    let cardsHtml = '';
    sortedEntries.forEach(([displayName, color]) => {
        const textColor = getContrastTextColor(color);
        cardsHtml += `<span class="line-tag" style="background-color:${color}; color:${textColor};">${displayName}</span>`;
    });

    const count = sortedEntries.length;
    content.html(`
        <div><strong>${name}</strong></div>
        <div style="color:#666;font-size:12px;">途经线路 ${count} 条</div>
        <div class="line-tag-container">${cardsHtml}</div>
    `);
    panel.style("display", "block");
}

d3.select("#info-close").on("click", function() {
    d3.select("#info-panel").style("display", "none");
});

// ============================================================
//  窗口自适应
// ============================================================
let resizeTimer;
window.addEventListener('resize', () => {
    clearTimeout(resizeTimer);
    resizeTimer = setTimeout(() => {
        if (metroData) drawMap(metroData);
    }, 250);
});

// ============================================================
//  暴露全局函数供 Android 调用
// ============================================================
window.loadDataFromAndroid = loadDataFromAndroid;
window.drawPathFromAndroid = drawPathFromAndroid;
window.highlightLinesFromAndroid = highlightLinesFromAndroid;
window.clearRouteFromAndroid = clearRouteFromAndroid;
window.zoomToStationsFromAndroid = zoomToStationsFromAndroid;
window.zoomToLinesFromAndroid = zoomToLinesFromAndroid;
window.showErrorFromAndroid = showErrorFromAndroid;

console.log('✅ 地图 HTML 已加载，等待 Android 注入数据');
