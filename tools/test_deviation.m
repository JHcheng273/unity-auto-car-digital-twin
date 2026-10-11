%% check_deviation 逻辑验证
%
% 用项目真实航点坐标，把车放在各种位置/朝向上，
% 检查判定是否符合直觉。这几条是最容易搞错的：
%   · 车在线段中点正上方 1 米 → 不该算偏离（阈值 2 米）
%   · 车偏离 3 米 → 该算偏离
%   · 车在路径上但车头转了 90 度 → 该算偏离（朝向）
%   · 车跑过目标点之后 → 距离不该无限增长（投影要夹紧）
%   · Unity 的 yaw 方向和数学 yaw 不同，方向别搞反

clear; clc;

% ★ 本脚本在 tools/ 目录下跑，而 check_deviation.m 在 matlab/ 目录。
%   不把 matlab/ 加进搜索路径，MATLAB 就找不到那个函数。
%   这里用脚本自身的位置反推出 matlab 目录，免得写死绝对路径。
here    = fileparts(mfilename('fullpath'));   % ...\tools
root    = fileparts(here);                    % 项目根
matlabDir = fullfile(root, 'matlab');
if exist(matlabDir, 'dir') && ~contains(path, matlabDir)
    addpath(matlabDir);
    fprintf("已把 matlab 目录加入搜索路径：\n  %s\n\n", matlabDir);
end

% 项目真实航点（从 mycar.unity 解出来的）
WP = [  8.49,  -3.10;
       -1.51,   6.90;
      -11.51,  -3.10;
       -1.51, -13.10];
nWP = size(WP, 1);

DIST_LIMIT = 2.0;
ANG_LIMIT  = 35;

fprintf("===== check_deviation 验证 =====\n");
fprintf("阈值：横向 %.1f m，朝向 %.0f°\n\n", DIST_LIMIT, ANG_LIMIT);

% 场景定义：{说明, x, z, yaw, wpIdx, 期望是否偏离}
% ★ wpIdx 是 **0 基**（和 Unity 的 CurrentWaypointIndex 一致）：
%     wpIdx=1 表示"正在追 WP1"，此时参考线段是 WP0 → WP1
% 注意 Unity 的 yaw：0=朝 +Z，90=朝 +X，180=朝 -Z，270=朝 -X
cases = {
    % --- 第1段：WP0(8.49,-3.10) → WP1(-1.51,6.90)，wpIdx=1 ---
    % 这段方向是 (-10, +10)，归一化约 (-0.707, +0.707)
    % Unity yaw = atan2(fx, fz) = atan2(-0.707, 0.707) = -45° → 315°
    '段1 起点上，朝向正确',        8.49, -3.10, 315, 1, false
    '段1 中点，几乎在线上',        3.49,  1.90, 315, 1, false
    '段1 偏离 1.5 米（未超阈值）',  3.49 + 1.06, 1.90 + 1.06, 315, 1, false
    '段1 偏离 3 米（该报警）',     3.49 + 2.12, 1.90 + 2.12, 315, 1, true
    '段1 线上但车头转了 90°',      3.49,  1.90, 315 + 90, 1, true
    % --- 第2段：WP1(-1.51,6.90) → WP2(-11.51,-3.10)，wpIdx=2 ---
    % 方向 (-10,-10) → (-0.707,-0.707)，yaw = atan2(-0.707,-0.707) = -135° → 225°
    '段2 起点，朝向正确',         -1.51,  6.90, 225, 2, false
    '段2 中点，在线上',           -6.51,  1.90, 225, 2, false
    '段2 线上但车头反了 180°',    -6.51,  1.90, 225 + 180, 2, true
    % --- 投影夹紧：车跑过 WP1 之后，应该算到端点的距离而不是无限增长 ---
    % WP1 是 (-1.51, 6.90)，沿着段1 方向 (-0.707,+0.707) 再往前 2.5 米。
    % ★ 这里不能用 2.0 米 —— 正好压在阈值上，判 ">" 时会算成"正常"，
    %   测试用例自己踩边界，看不出 bug 也看不出正确。
    '跑过 WP1 之后 2.5 米（投影夹紧）', -1.51 - 1.768, 6.90 + 1.768, 315, 1, true
};

pass = 0; fail = 0;
for k = 1:size(cases, 1)
    c = cases(k, :);
    name = c{1}; x = c{2}; z = c{3}; yaw = c{4}; wpIdx = c{5}; expect = c{6};

    [dev, dist, ang, info] = check_deviation(x, z, yaw, wpIdx, WP, nWP, ...
                                             DIST_LIMIT, ANG_LIMIT);
    ok = (dev == expect);
    if ok, pass = pass + 1; else, fail = fail + 1; end

    if ok, mark = "[OK]  "; else, mark = "[FAIL]"; end
    fprintf("%s %s\n", mark, name);
    fprintf("       偏离=%d(期望%d)  横向=%.2f m  夹角=%.0f°\n", dev, expect, dist, ang);
    fprintf("       %s\n", info);
end

fprintf("\n===== 结果：%d 通过，%d 失败 =====\n", pass, fail);

% ---- 额外：扫一遍"偏离阈值"的数值曲线，看边界是否平滑 ----
fprintf("\n----- 距离扫描（段1 中点垂直偏离，固定朝向）-----\n");
base = [3.49, 1.90];
perp = [0.7071, 0.7071];       % 段1 的垂直方向
fprintf("%8s  %10s  %8s  %s\n", "偏离量", "算出的距离", "夹角", "判定");
for d = 0:0.5:3.5
    px = base(1) + perp(1) * d;
    pz = base(2) + perp(2) * d;
    [dev, dd, ang, ~] = check_deviation(px, pz, 315, 1, WP, nWP, DIST_LIMIT, ANG_LIMIT);
    if dev, verdict = "偏离"; else, verdict = "正常"; end
    fprintf("%6.1f m  %8.2f m  %6.0f°  %s\n", d, dd, ang, verdict);
end
