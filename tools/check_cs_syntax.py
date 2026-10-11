"""
C# 静态自检（沙箱不给跑 csc.exe，只能用这种"笨办法"）
=====================================================
检查项：
  1. 括号 / 花括号 / 方括号是否平衡
  2. 字符串和注释里的括号不参与计数（简单状态机）
  3. 每个 class 是否只有一个匹配的收尾
  4. 报告所有 public 字段（方便和场景 YAML 对照）
"""
import re
import sys
import os

FILES = [
    r"E:\My project\Assets\Scripts\Learn\MyFrontDetector.cs",
    r"E:\My project\Assets\Scripts\Learn\ObstacleManager.cs",
    r"E:\My project\Assets\Scripts\Learn\AvoidanceDriver.cs",
    r"E:\My project\Assets\Scripts\Learn\RoadNetwork.cs",
    r"E:\My project\Assets\Scripts\Learn\MouseTargetSelector.cs",
    r"E:\My project\Assets\Scripts\Learn\AutoDrive.cs",
    r"E:\My project\Assets\Editor\RoadNetworkSetup.cs",
    r"E:\My project\Assets\Scripts\Learn\CameraStreamer.cs",
    r"E:\My project\Assets\Scripts\Learn\ProjectionBeam.cs",
    r"E:\My project\Assets\Editor\ProjectionSetup.cs",
]


def strip_code(src):
    """去掉字符串字面量和注释，只留结构性字符"""
    out = []
    i = 0
    n = len(src)
    while i < n:
        c = src[i]
        # 行注释
        if c == '/' and i + 1 < n and src[i + 1] == '/':
            while i < n and src[i] != '\n':
                i += 1
            continue
        # 块注释
        if c == '/' and i + 1 < n and src[i + 1] == '*':
            i += 2
            while i + 1 < n and not (src[i] == '*' and src[i + 1] == '/'):
                i += 1
            i += 2
            continue
        # 逐字字符串 @"..."
        if c == '@' and i + 1 < n and src[i + 1] == '"':
            i += 2
            while i < n:
                if src[i] == '"':
                    if i + 1 < n and src[i + 1] == '"':
                        i += 2
                        continue
                    i += 1
                    break
                i += 1
            continue
        # 普通字符串 "..."
        if c == '"':
            i += 1
            while i < n:
                if src[i] == '\\':
                    i += 2
                    continue
                if src[i] == '"':
                    i += 1
                    break
                i += 1
            continue
        # 字符字面量 '...'
        if c == "'":
            i += 1
            while i < n:
                if src[i] == '\\':
                    i += 2
                    continue
                if src[i] == "'":
                    i += 1
                    break
                i += 1
            continue
        out.append(c)
        i += 1
    return ''.join(out)


def check(path):
    print("=" * 70)
    print(os.path.basename(path))
    print("=" * 70)
    if not os.path.exists(path):
        print("  ✘ 文件不存在")
        return False

    src = open(path, encoding='utf-8').read()
    code = strip_code(src)

    pairs = {'{': '}', '(': ')', '[': ']'}
    closes = {'}': '{', ')': '(', ']': '['}
    stack = []
    line = 1
    ok = True

    for ch in code:
        if ch == '\n':
            line += 1
        if ch in pairs:
            stack.append((ch, line))
        elif ch in closes:
            if not stack:
                print(f"  ✘ 第 {line} 行：多了一个 '{ch}'")
                ok = False
            else:
                op, ol = stack.pop()
                if op != closes[ch]:
                    print(f"  ✘ 第 {line} 行：'{ch}' 与第 {ol} 行的 '{op}' 不匹配")
                    ok = False

    if stack:
        for op, ol in stack:
            print(f"  ✘ 第 {ol} 行的 '{op}' 没有闭合")
        ok = False

    if ok:
        # 数 class / namespace
        nclass = len(re.findall(r'\bclass\s+\w+', code))
        print(f"  ✔ 括号平衡   class 数={nclass}")

    # 公共字段（给场景 YAML 对照用）
    print("  --- public 字段 ---")
    for m in re.finditer(r'public\s+(?:readonly\s+)?([\w<>\[\],\s]+?)\s+(\w+)\s*(?:=|;|\{)', code):
        t = ' '.join(m.group(1).split())
        if t in ('class', 'void', 'enum'):
            continue
        print(f"    {t}  {m.group(2)}")

    return ok


allok = True
for f in FILES:
    if not check(f):
        allok = False
    print()

print("=" * 70)
print("✔ 全部通过" if allok else "✘ 有问题，见上面")
print("=" * 70)
