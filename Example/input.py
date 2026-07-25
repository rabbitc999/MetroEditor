import csv
import json
import re
import struct
from collections import OrderedDict
from pypinyin import pinyin, Style

def get_pinyin_initial(name):
    initials = []
    for char in name:
        if not (char.isascii() and char.isalpha()):
            py = pinyin(char, style=Style.FIRST_LETTER)[0][0]
            initials.append(py.upper())
        else:
            initials.append(char.upper())
    return ''.join(initials)

def extract_segments(guide, start_station):
    pattern = r'开往(.*?)方向的(.*?)，坐(\d+)站.*?【(.*?)】'
    matches = re.findall(pattern, guide)
    segments = []
    current = start_station
    for dir_name, line_name, stop_count, end_station in matches:
        segments.append((
            current,
            dir_name.strip(),
            line_name.strip(),
            int(stop_count),
            end_station.strip()
        ))
        current = end_station
    return segments

def convert_csv_json_to_bin(csv_path, json_path, bin_path):
    # 1. 读取 JSON
    with open(json_path, 'r', encoding='utf-8') as f:
        data = json.load(f)

    key_city = data.get('key_city', '广州')
    stations_info = data['stations']
    lines_info = data['lines']

    station_names = list(stations_info.keys())
    line_ids = list(lines_info.keys())

    # 2. 读取 CSV
    rows = []
    with open(csv_path, 'r', encoding='utf-8') as f:
        reader = csv.DictReader(f)
        for row in reader:
            if not row.get('起点站') or not row.get('终点站'):
                continue
            rows.append(row)

    direction_names = set()
    for row in rows:
        segs = extract_segments(row['乘车指引'], row['起点站'])
        for _, dir_name, _, _, _ in segs:
            direction_names.add(dir_name.strip())

    # 3. 建立 ID 映射
    station_name_to_id = {name: idx + 1 for idx, name in enumerate(station_names)}
    line_name_to_id = {}
    for idx, lid in enumerate(line_ids):
        line_name_to_id[lines_info[lid]['name']] = idx + 1
    dir_names = sorted(direction_names)
    dir_name_to_id = {name: idx + 1 for idx, name in enumerate(dir_names)}

    # 4. 构建站点表（版本5：加拼音）
    station_table = bytearray()
    for name in station_names:
        info = stations_info.get(name)
        if info is None:
            lon, lat = 0.0, 0.0
            line_ids_for_station = []
        else:
            lon, lat = info['lon'], info['lat']
            line_ids_for_station = list({line['id'] for line in info['lines']})

        pinyin_str = get_pinyin_initial(name)
        pinyin_bytes = pinyin_str.encode('ascii')
        name_bytes = name.encode('gbk', errors='replace')

        station_table.extend(struct.pack('<H', len(name_bytes)))
        station_table.extend(name_bytes)
        station_table.extend(struct.pack('<H', len(pinyin_bytes)))
        station_table.extend(pinyin_bytes)
        station_table.extend(struct.pack('<f', lon))
        station_table.extend(struct.pack('<f', lat))
        station_table.extend(struct.pack('<H', len(line_ids_for_station)))
        for lid in line_ids_for_station:
            if lid in lines_info:
                lname = lines_info[lid]['name']
                lid_new = line_name_to_id.get(lname, 0)
                station_table.extend(struct.pack('<H', lid_new))
            else:
                station_table.extend(struct.pack('<H', 0))

    # 5. 构建线路表（增加城市字段）
    line_table = bytearray()
    for lid in line_ids:
        linfo = lines_info[lid]
        name = linfo['name']
        city = linfo.get('city', '广州')
        color_str = linfo.get('color', '#000000')
        rgb = int(color_str[1:], 16) if color_str.startswith('#') else 0
        name_bytes = name.encode('gbk', errors='replace')
        city_bytes = city.encode('gbk', errors='replace')
        line_table.extend(struct.pack('<H', len(name_bytes)))
        line_table.extend(name_bytes)
        line_table.extend(struct.pack('<I', rgb))
        line_table.extend(struct.pack('<H', len(city_bytes)))
        line_table.extend(city_bytes)

    # 6. 构建方向表（不变）
    dir_table = bytearray()
    for name in dir_names:
        name_bytes = name.encode('gbk', errors='replace')
        dir_table.extend(struct.pack('<H', len(name_bytes)))
        dir_table.extend(name_bytes)

    # 7. 构建记录表（不变）
    records = []
    for row in rows:
        start_name = row['起点站']
        end_name = row['终点站']
        start_id = station_name_to_id.get(start_name, 0)
        end_id = station_name_to_id.get(end_name, 0)
        dist = int(float(row['距离(km)']) * 1000)
        time_min = int(float(row['时间(分钟)']) * 100)
        fare = int(float(row['票价(元)']) * 100)
        transfers = int(row['换乘次数'])
        total_stops = int(row['站数'])

        segs = extract_segments(row['乘车指引'], start_name)
        seg_count = len(segs)
        segs_new = []
        for seg_start, dir_name, line_name, stops, seg_end in segs:
            seg_start_id = station_name_to_id.get(seg_start, 0)
            line_id = line_name_to_id.get(line_name, 0)
            dir_id = dir_name_to_id.get(dir_name, 0)
            seg_end_id = station_name_to_id.get(seg_end, 0)
            segs_new.append((seg_start_id, line_id, dir_id, stops, seg_end_id))

        records.append({
            'start': start_id,
            'end': end_id,
            'dist': dist,
            'time': time_min,
            'fare': fare,
            'transfers': transfers,
            'total_stops': total_stops,
            'seg_count': seg_count,
            'segs': segs_new
        })

    record_table = bytearray()
    for rec in records:
        record_table.extend(struct.pack('<HHIHHHHH',
            rec['start'], rec['end'], rec['dist'],
            rec['time'], rec['fare'],
            rec['transfers'], rec['total_stops'], rec['seg_count']))
        for seg in rec['segs']:
            record_table.extend(struct.pack('<HHHHH', *seg))

    # ===== 新增：构建线路顺序表（版本5，存储所有方向） =====
    order_table = bytearray()
    order_table.extend(struct.pack('<H', len(line_ids)))  # 线路数
    for lid in line_ids:
        linfo = lines_info[lid]
        dirs = linfo.get('direction', [])
        order_table.extend(struct.pack('<B', len(dirs)))  # 方向数
        for dir_obj in dirs:
            # 方向名称：开往{end}方向
            dir_name = f"开往{dir_obj['end']}方向"
            dir_name_bytes = dir_name.encode('gbk', errors='replace')
            order_table.extend(struct.pack('<H', len(dir_name_bytes)))
            order_table.extend(dir_name_bytes)

            station_list = dir_obj.get('station', [])
            station_count = len(station_list)
            order_table.extend(struct.pack('<H', station_count))
            for st in station_list:
                sname = st['name']
                sid = station_name_to_id.get(sname, 0)
                order_table.extend(struct.pack('<H', sid))
            # 状态
            for st in station_list:
                sname = st['name']
                status = 0
                if sname in stations_info:
                    for line_entry in stations_info[sname]['lines']:
                        if line_entry['id'] == lid:
                            if line_entry.get('status') == 'pending':
                                status = 1
                            break
                order_table.extend(struct.pack('<B', status))

    # 8. 计算偏移，写入文件（版本号5）
    header_size = 6 + 2 + len(key_city.encode('gbk'))  # 魔数+版本+key_city长度+内容
    # 先写key_city到文件头后面
    key_city_bytes = key_city.encode('gbk', errors='replace')
    # 偏移表在key_city之后
    section_count = 5  # 站点、线路、方向、记录、顺序
    offset_table_size = 2 + section_count * (2 + 8 + 8)
    base_offset = header_size + offset_table_size

    station_len = len(station_table)
    line_len = len(line_table)
    dir_len = len(dir_table)
    record_len = len(record_table)
    order_len = len(order_table)

    TYPE_STATION = 1
    TYPE_LINE = 2
    TYPE_DIR = 3
    TYPE_RECORD = 4
    TYPE_ORDER = 5

    sections = [
        (TYPE_STATION, base_offset, station_len),
        (TYPE_LINE, base_offset + station_len, line_len),
        (TYPE_DIR, base_offset + station_len + line_len, dir_len),
        (TYPE_RECORD, base_offset + station_len + line_len + dir_len, record_len),
        (TYPE_ORDER, base_offset + station_len + line_len + dir_len + record_len, order_len),
    ]

    with open(bin_path, 'wb') as f:
        f.write(b'ROUT')
        f.write(struct.pack('<H', 5))          # 版本5
        f.write(struct.pack('<H', len(key_city_bytes)))
        f.write(key_city_bytes)
        f.write(struct.pack('<H', section_count))
        for typ, off, length in sections:
            f.write(struct.pack('<H', typ))
            f.write(struct.pack('<Q', off))
            f.write(struct.pack('<Q', length))
        f.write(station_table)
        f.write(line_table)
        f.write(dir_table)
        f.write(record_table)
        f.write(order_table)

    print(f"转换完成！共 {len(records)} 条记录。")
    print(f"站点数: {len(station_names)}, 线路数: {len(line_ids)}, 方向数: {len(dir_names)}")

if __name__ == '__main__':
    convert_csv_json_to_bin('input.csv', 'route_data.json', 'routes.bin')