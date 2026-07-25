import sys
import struct
import os

class RouteFinder:
    def __init__(self, bin_path):
        self.stations = {}         # id -> name
        self.station_to_id = {}    # name -> id
        self.station_coords = {}   # id -> (lon, lat)
        self.station_lines = {}    # id -> list of line ids
        self.lines = {}            # id -> name
        self.line_colors = {}      # id -> rgb (int)
        self.directions = {}       # id -> name
        self.records = []          # 所有原始记录（解码后）
        self.index = {}            # (start_id, end_id) -> record 索引

        self._load_bin(bin_path)
        self._build_index()

    def _load_bin(self, bin_path):
        if not os.path.exists(bin_path):
            raise FileNotFoundError(f"二进制文件 {bin_path} 不存在！请先运行 converter.py")

        with open(bin_path, 'rb') as f:
            # 1. 文件头
            magic = f.read(4)
            if magic != b'ROUT':
                raise ValueError("无效的二进制文件格式")
            version = struct.unpack('<H', f.read(2))[0]
            if version != 2:
                raise ValueError(f"不支持的版本: {version}，仅支持版本 2")

            # 2. 偏移表
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

            # 3. 读取站点表
            station_off, station_len = sections[TYPE_STATION]
            f.seek(station_off)
            station_data = f.read(station_len)
            pos = 0
            sid = 1
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

                self.stations[sid] = name
                self.station_to_id[name] = sid
                self.station_coords[sid] = (lon, lat)
                self.station_lines[sid] = line_ids
                sid += 1

            # 4. 读取线路表
            line_off, line_len = sections[TYPE_LINE]
            f.seek(line_off)
            line_data = f.read(line_len)
            pos = 0
            lid = 1
            while pos < len(line_data):
                name_len = struct.unpack('<H', line_data[pos:pos+2])[0]
                pos += 2
                name = line_data[pos:pos+name_len].decode('gb2312')
                pos += name_len
                color = struct.unpack('<I', line_data[pos:pos+4])[0]
                pos += 4

                self.lines[lid] = name
                self.line_colors[lid] = color
                lid += 1

            # 5. 读取方向表
            dir_off, dir_len = sections[TYPE_DIR]
            f.seek(dir_off)
            dir_data = f.read(dir_len)
            pos = 0
            did = 1
            while pos < len(dir_data):
                name_len = struct.unpack('<H', dir_data[pos:pos+2])[0]
                pos += 2
                name = dir_data[pos:pos+name_len].decode('gb2312')
                pos += name_len
                self.directions[did] = name
                did += 1

            # 6. 读取记录表
            record_off, record_len = sections[TYPE_RECORD]
            f.seek(record_off)
            record_data = f.read(record_len)
            pos = 0
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
                    segs.append({
                        'start': s_start,
                        'line': s_line,
                        'dir': s_dir,
                        'stops': s_stops,
                        'end': s_end
                    })
                    pos += 10

                record = {
                    'start': start,
                    'end': end,
                    'dist': dist,
                    'time': time_min,
                    'fare': fare,
                    'transfers': transfers,
                    'total_stops': total_stops,
                    'seg_count': seg_count,
                    'segs': segs
                }
                self.records.append(record)

    def _build_index(self):
        """构建 (起点ID, 终点ID) -> 记录 的哈希索引"""
        for rec in self.records:
            key = (rec['start'], rec['end'])
            self.index[key] = rec

    def find_by_id(self, start_id, end_id):
        """通过站点ID查找"""
        key = (start_id, end_id)
        return self.index.get(key)

    def find_by_name(self, start_name, end_name):
        """通过站点名称查找"""
        if start_name not in self.station_to_id:
            raise ValueError(f"未知起点站: {start_name}")
        if end_name not in self.station_to_id:
            raise ValueError(f"未知终点站: {end_name}")
        start_id = self.station_to_id[start_name]
        end_id = self.station_to_id[end_name]
        return self.find_by_id(start_id, end_id)

    def get_station_info(self, station_name):
        """获取站点的详细信息（经纬度、途经线路）"""
        if station_name not in self.station_to_id:
            return None
        sid = self.station_to_id[station_name]
        lon, lat = self.station_coords[sid]
        line_ids = self.station_lines[sid]
        line_names = [self.lines[lid] for lid in line_ids if lid in self.lines]
        return {
            'name': station_name,
            'lon': lon,
            'lat': lat,
            'lines': line_names
        }

    def format_route(self, rec, show_extra=False):
        """格式化输出路线详情，可额外显示站点坐标和线路颜色"""
        if rec is None:
            return "未找到该路线！"

        start_name = self.stations[rec['start']]
        end_name = self.stations[rec['end']]

        lines = []
        lines.append(f"📍 {start_name} -> {end_name}")
        if show_extra:
            # 显示起点和终点坐标
            s_lon, s_lat = self.station_coords[rec['start']]
            e_lon, e_lat = self.station_coords[rec['end']]
            lines.append(f"   起点坐标: ({s_lon:.6f}, {s_lat:.6f})")
            lines.append(f"   终点坐标: ({e_lon:.6f}, {e_lat:.6f})")
        lines.append(f"   距离: {rec['dist']/1000:.3f} km")
        lines.append(f"   时间: {rec['time']/100:.1f} 分钟")
        lines.append(f"   票价: {rec['fare']/100:.2f} 元")
        lines.append(f"   换乘: {rec['transfers']} 次 | 总站数: {rec['total_stops']} 站")
        lines.append(f"   分段数: {rec['seg_count']}")

        for idx, seg in enumerate(rec['segs'], 1):
            s_name = self.stations[seg['start']]
            line_name = self.lines[seg['line']]
            dir_name = self.directions[seg['dir']]
            e_name = self.stations[seg['end']]
            if show_extra:
                color = self.line_colors.get(seg['line'], 0)
                hex_color = f"#{color:06X}" if color != 0 else "#000000"
                line_name = f"{line_name} [{hex_color}]"
            lines.append(f"   ➜ 第{idx}段: {s_name} → 坐 {line_name}(开往{dir_name}方向) {seg['stops']} 站 → {e_name}")

        return "\n".join(lines)


def main():
    # 默认二进制文件名（新格式）
    BIN_PATH = 'routes_with_info.bin'

    try:
        finder = RouteFinder(BIN_PATH)
        print(f"✅ 加载成功！共 {len(finder.records)} 条路线，{len(finder.stations)} 个站点，{len(finder.lines)} 条线路。")
        print("=" * 50)
    except Exception as e:
        print(f"❌ 加载失败: {e}")
        return

    # 支持两种模式：
    # 1. 命令行参数: python finder.py "白江" "白云大道北" [--extra]
    # 2. 交互式输入
    show_extra = '--extra' in sys.argv
    if len(sys.argv) >= 3:
        # 移除可能的 --extra
        args = [arg for arg in sys.argv[1:] if not arg.startswith('--')]
        if len(args) >= 2:
            start_name = args[0]
            end_name = args[1]
            try:
                rec = finder.find_by_name(start_name, end_name)
                print(finder.format_route(rec, show_extra))
            except ValueError as e:
                print(f"❌ {e}")
            return

    # 交互式模式
    print("🔍 路线查询工具 (输入 'quit' 退出，使用 --extra 查看额外信息)")
    while True:
        try:
            start = input("\n请输入起点站名: ").strip()
            if start.lower() == 'quit':
                break
            end = input("请输入终点站名: ").strip()
            if end.lower() == 'quit':
                break
            if not start or not end:
                print("❌ 站点名不能为空！")
                continue
            rec = finder.find_by_name(start, end)
            print(finder.format_route(rec, show_extra))
        except KeyboardInterrupt:
            print("\n退出程序")
            break
        except Exception as e:
            print(f"❌ 查询出错: {e}")

if __name__ == '__main__':
    main()