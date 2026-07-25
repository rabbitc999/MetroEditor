import csv
import sys

def find_route(csv_filepath, start_station, end_station):
    """
    在CSV文件中查找指定起点和终点的乘车路径
    """
    try:
        with open(csv_filepath, mode='r', encoding='utf-8-sig') as f:
            reader = csv.DictReader(f)
            # 遍历每一行进行匹配
            for row in reader:
                if row['起点站'] == start_station and row['终点站'] == end_station:
                    return row
            return None
    except FileNotFoundError:
        print(f"错误：找不到文件 {csv_filepath}")
        sys.exit(1)
    except Exception as e:
        print(f"读取文件时发生错误：{e}")
        sys.exit(1)

if __name__ == "__main__":
    # 修改为你的CSV文件路径
    CSV_PATH = "全站点距离矩阵_20260724_155024.csv"
    
    print("=== 地铁乘车路径查询系统 ===")
    start = input("请输入起点站：").strip()
    end = input("请输入终点站：").strip()
    
    result = find_route(CSV_PATH, start, end)
    
    if result:
        print("\n--- 查询结果 ---")
        print(f"起点：{result['起点站']}")
        print(f"终点：{result['终点站']}")
        print(f"距离：{result['距离(km)']} km")
        print(f"预计时间：{result['时间(分钟)']} 分钟")
        print(f"换乘次数：{result['换乘次数']} 次")
        print(f"票价：{result['票价(元)']} 元")
        print(f"乘车指引：{result['乘车指引']}")
    else:
        print(f"\n未找到从【{start}】到【{end}】的直达路径记录，请检查站名是否正确。")