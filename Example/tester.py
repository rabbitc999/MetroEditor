import struct

def read_bin_and_test(bin_path):
    with open(bin_path, 'rb') as f:
        # --- 读文件头 ---
        magic = f.read(4)
        if magic != b'ROUT':
            print("无效的二进制文件格式！")
            return
        version = struct.unpack('<H', f.read(2))[0]
        print(f"魔数: {magic}, 版本: {version}")

        # --- 读偏移表 ---
        section_count = struct.unpack('<H', f.read(2))[0]
        sections = {}
        for _ in range(section_count):
            typ = struct.unpack('<H', f.read(2))[0]
            offset = struct.unpack('<Q', f.read(8))[0]
            length = struct.unpack('<Q', f.read(8))[0]
            sections[typ] = (offset, length)

        TYPE_STATION = 1
        TYPE_LINE = 2
        TYPE_DIR = 3
        TYPE_RECORD = 4

        # --- 读取站点表 ---
        station_off, station_len = sections[TYPE_STATION]
        f.seek(station_off)
        station_data = f.read(station_len)
        pos = 0
        station_id_to_name = {}   # id -> 名称
        station_name_to_id = {}   # 名称 -> id
        station_coords = {}       # id -> (lon, lat)
        station_line_ids = {}     # id -> 线路id列表
        idx = 1
        while pos < len(station_data):
            name_len = struct.unpack('<H', station_data[pos:pos+2])[0]
            pos += 2
            name = station_data[pos:pos+name_len].decode('gb2312')
            pos += name_len
            lon = struct.unpack('<f', station_data[pos:pos+4])[0]
            pos += 4
            lat = struct.unpack('<f', station_data[pos:pos+4])[0]
            pos += 4
            line_count = struct.unpack('<H', station_data[pos:pos+2])[0]
            pos += 2
            line_ids = []
            for _ in range(line_count):
                lid = struct.unpack('<H', station_data[pos:pos+2])[0]
                line_ids.append(lid)
                pos += 2
            station_id_to_name[idx] = name
            station_name_to_id[name] = idx
            station_coords[idx] = (lon, lat)
            station_line_ids[idx] = line_ids
            idx += 1
        total_stations = idx - 1
        print(f"读取站点数: {total_stations}")

        # --- 读取线路表 ---
        line_off, line_len = sections[TYPE_LINE]
        f.seek(line_off)
        line_data = f.read(line_len)
        pos = 0
        line_id_to_name = {}
        line_id_to_color = {}
        idx = 1
        while pos < len(line_data):
            name_len = struct.unpack('<H', line_data[pos:pos+2])[0]
            pos += 2
            name = line_data[pos:pos+name_len].decode('gb2312')
            pos += name_len
            color = struct.unpack('<I', line_data[pos:pos+4])[0]
            pos += 4
            line_id_to_name[idx] = name
            line_id_to_color[idx] = color
            idx += 1
        total_lines = idx - 1
        print(f"读取线路数: {total_lines}")

        # --- 读取方向表 ---
        dir_off, dir_len = sections[TYPE_DIR]
        f.seek(dir_off)
        dir_data = f.read(dir_len)
        pos = 0
        dir_id_to_name = {}
        idx = 1
        while pos < len(dir_data):
            name_len = struct.unpack('<H', dir_data[pos:pos+2])[0]
            pos += 2
            name = dir_data[pos:pos+name_len].decode('gb2312')
            pos += name_len
            dir_id_to_name[idx] = name
            idx += 1
        total_dirs = idx - 1
        print(f"读取方向数: {total_dirs}")

        # --- 读取记录表 ---
        record_off, record_len = sections[TYPE_RECORD]
        f.seek(record_off)
        record_data = f.read(record_len)
        pos = 0
        record_count = 0
        records = []
        while pos < len(record_data):
            # 固定头 18 字节
            header = record_data[pos:pos+18]
            if len(header) < 18:
                break
            start, end, dist, time_min, fare, transfers, total_stops, seg_count = struct.unpack('<HHIHHHHH', header)
            pos += 18
            segs = []
            for _ in range(seg_count):
                seg_raw = record_data[pos:pos+10]
                if len(seg_raw) < 10:
                    break
                s_start, s_line, s_dir, s_stops, s_end = struct.unpack('<HHHHH', seg_raw)
                segs.append((s_start, s_line, s_dir, s_stops, s_end))
                pos += 10
            records.append({
                'start': start,
                'end': end,
                'dist': dist,
                'time': time_min,
                'fare': fare,
                'transfers': transfers,
                'total_stops': total_stops,
                'seg_count': seg_count,
                'segs': segs
            })
            record_count += 1

        print(f"总记录数: {record_count}\n")

        # 打印前5条记录
        for i in range(min(5, record_count)):
            rec = records[i]
            start_name = station_id_to_name.get(rec['start'], '未知')
            end_name = station_id_to_name.get(rec['end'], '未知')
            print(f"--- 记录 {i+1} ---")
            print(f"起点: {start_name}, 终点: {end_name}")
            print(f"距离: {rec['dist']/1000:.3f} km, 时间: {rec['time']/100:.1f} min, 票价: {rec['fare']/100:.2f} 元")
            print(f"换乘: {rec['transfers']} 次, 总站数: {rec['total_stops']}, 段数: {rec['seg_count']}")
            for idx, seg in enumerate(rec['segs'], 1):
                s_start_id, s_line_id, s_dir_id, s_stops, s_end_id = seg
                s_start = station_id_to_name.get(s_start_id, '未知')
                s_end = station_id_to_name.get(s_end_id, '未知')
                line = line_id_to_name.get(s_line_id, '未知')
                direction = dir_id_to_name.get(s_dir_id, '未知')
                print(f"  第{idx}段: {s_start} -> 坐{line}(开往{direction}方向) {s_stops}站 -> {s_end}")
            print()

        # 打印几个站点的信息（示例）
        print("--- 站点信息示例（前3个） ---")
        for sid in list(station_id_to_name.keys())[:3]:
            name = station_id_to_name[sid]
            lon, lat = station_coords[sid]
            lines = [line_id_to_name.get(lid, '未知') for lid in station_line_ids[sid]]
            print(f"站点: {name}, 经度: {lon:.6f}, 纬度: {lat:.6f}, 线路: {', '.join(lines)}")
        print()

        # 打印线路颜色示例
        print("--- 线路颜色示例（前3个） ---")
        for lid in list(line_id_to_name.keys())[:3]:
            name = line_id_to_name[lid]
            color = line_id_to_color[lid]
            # 将颜色整数转换为十六进制字符串（不带#）
            hex_color = f"#{color:06X}" if color != 0 else "#000000"
            print(f"线路: {name}, 颜色: {hex_color}")

        print("\n✅ 全部数据读取完整，格式验证通过！")

if __name__ == '__main__':
    read_bin_and_test('routes_with_info.bin')