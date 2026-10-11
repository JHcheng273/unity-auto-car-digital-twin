function [deviated, dist, angleDeg, info] = check_deviation(x, z, yawDeg, wpIdx, WP, wpCount, distLimit, angLimit)
%CHECK_DEVIATION 判断小车是否偏离了既定路径。
%
% 判定考虑两个条件（任一满足就算偏离）：
%   ① 横向距离：车离"当前航点连线"的垂直距离 > distLimit
%   ② 朝向夹角：车头方向与"应该前进的方向"夹角 > angLimit
% 两个都要看，是因为它们分别抓不同的情况：
%   · 只看距离：车斜着往路边冲、但还没走远时抓不到
%   · 只看角度：车原地打转时距离没变，但已经在偏离了
%
% 输入：
%   x, z      车当前位置（Unity 的 XZ 平面）
%   yawDeg    车头朝向（角度，Unity 的 eulerAngles.y；0=+Z，顺时针为正）
%   wpIdx     当前正在追的航点下标（0 起）
%   WP        N×2 的航点坐标矩阵，WP(i,:) = [x z]
%   wpCount   航点总数
%   distLimit 横向距离阈值（米）
%   angLimit  朝向夹角阈值（度）
%
% 输出：
%   deviated  true/false
%   dist      横向距离（米）
%   angleDeg  朝向夹角（度）
%   info      文字说明，直接显示在面板上

if nargin < 7 || isempty(distLimit), distLimit = 2.0; end
if nargin < 8 || isempty(angLimit),  angLimit  = 35;  end

deviated = false;
dist     = 0;
angleDeg = 0;
info     = '路径跟踪正常';

% ★ 这两个方向分量要**先给默认值**。
%   虽然下面两个分支都会赋值，但 MATLAB 的静态检查看不穿这一点，
%   会在"角度计算"那行报"变量可能未定义/未使用"。
%   显式初始化一下就干净了。
segDirX = 0;
segDirZ = 1;

if isempty(WP) || wpCount < 2
    info = '没有路径数据';
    return;
end

% ---- 当前航点和上一个航点（构成"应该走的那条线段"）----
iNow  = mod(wpIdx, wpCount) + 1;
iPrev = mod(wpIdx - 1, wpCount) + 1;
if iPrev < 1 || iPrev > size(WP, 1), iPrev = iNow; end
if iNow  < 1 || iNow  > size(WP, 1), iNow  = 1;    end

A = WP(iPrev, :);   % 起点
B = WP(iNow,  :);   % 目标点

seg = B - A;
segLen = norm(seg);
if segLen < 1e-6
    % 两点重合，退化成"离这个点的距离"；方向沿用上面给的默认值 (0,1)，
    % 反正这时角度判定没有意义
    dist = hypot(x - B(1), z - B(2));
else
    segDirX = seg(1) / segLen;
    segDirZ = seg(2) / segLen;

    % ---- 点到线段的垂直距离 ----
    % 把车投影到线段上，投影参数 t 夹到 [0,1] —— 这样线段外的点
    % 算的是到端点的距离，而不是到无限长直线的距离（否则车跑过目标点
    % 之后距离会莫名其妙地一直变大）
    vx = x - A(1);  vz = z - A(2);
    t  = (vx * segDirX + vz * segDirZ) / segLen;
    t  = max(0, min(1, t));
    projX = A(1) + t * segLen * segDirX;
    projZ = A(2) + t * segLen * segDirZ;
    dist = hypot(x - projX, z - projZ);
end
% ---- 车头方向 vs 应该前进的方向 ----
% Unity 的 yaw：0 度朝 +Z，顺时针为正。
% 换成方向向量：forward = (sin(yaw), cos(yaw))
yawRad = deg2rad(yawDeg);
fwdX = sin(yawRad);
fwdZ = cos(yawRad);

dotp = fwdX * segDirX + fwdZ * segDirZ;
dotp = max(-1, min(1, dotp));
angleDeg = rad2deg(acos(dotp));

% ---- 判定 ----
overDist  = dist     > distLimit;
overAngle = angleDeg > angLimit;

if overDist && overAngle
    deviated = true;
    info = sprintf('偏离！横向 %.1f m + 朝向 %.0f°', dist, angleDeg);
elseif overDist
    deviated = true;
    info = sprintf('偏离！横向偏离 %.1f m', dist);
elseif overAngle
    deviated = true;
    info = sprintf('偏离！朝向偏了 %.0f°', angleDeg);
else
    info = sprintf('正常  横向 %.2f m  夹角 %.0f°', dist, angleDeg);
end

end
