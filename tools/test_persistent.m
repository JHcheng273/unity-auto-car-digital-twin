%% 常驻 / 自动重连 —— 状态机验证
%
% 为什么不用"真开一个仪表盘 + 真的发 UDP"来测：
%   unity_dashboard 是**脚本**（不是函数），没法在同一个 MATLAB 会话里
%   一边跑它一边发 UDP —— 脚本会占住主线程，你那边的 timer 回调根本轮不上。
%   要真端到端测就得开两个 MATLAB 进程，太重、而且两个进程抢 5005 端口。
%
% 所以这里测**逻辑本身**：
%   把 unity_dashboard.m 里的「会话检测 + 断开判定」状态机原样抄一遍
%   （对应主脚本的 4.0 段和 else 分支），用压缩的时间轴把
%   "连上 → 断开 → 再连上"跑一遍，检查三件事：
%     ① 两轮连接都识别出来
%     ② 断开被检测到并回到等待态
%     ③ 重连时位置差分基准清干净了 —— 累计里程不虚增
%   这三条对了，主脚本里的那段逻辑就是对的（代码逐行一致）。

clear; clc;

%% ---- 跟主脚本保持一致的常量 ----
RECONNECT_GAP = 0.30;   % 主脚本里是 3 秒；这里按 10 倍速压缩
STEP          = 0.05;   % 模拟步长
T_END         = 12.0;

%% ---- 状态（名字与主脚本一致）----
last_rx   = NaN;
sessCount = 0;
connected = false;
s_actual  = 0;
pos_prev  = [];
count     = 0;
eventLog  = strings(0);

fprintf("===== 常驻/重连 状态机验证（时间轴 ×10 速）=====\n");
fprintf("时间线：0~2.4s 发包 | 2.4~4.4s 停 | 4.4~8.0s 发包 | 8.0~12s 停\n\n");

%% ---- 主循环（逻辑照抄 unity_dashboard.m）----
for k = 0:round(T_END/STEP)
    tNow = k * STEP;

    % 模拟 Unity 是否在 Play
    active = (tNow < 2.4) || (tNow >= 4.4 && tNow < 8.0);

    if active
        count = count + 1;

        % ===== 4.0 新会话检测 =====
        if isnan(last_rx) || (tNow - last_rx) > RECONNECT_GAP
            sessCount = sessCount + 1;
            % ★ 关键：pos_prev 也要清（曾经漏过，导致里程虚增）
            t_prev   = [];
            pos_prev = [];
            eventLog(end+1) = sprintf("[第 %d 轮] 链路已建立  (t=%.2f)", sessCount, tNow);
        end
        last_rx   = tNow;
        connected = true;

        % ===== 4.1 位置差分算里程 =====
        x = tNow;  z = 0;                   % 假车：x 每秒走 1 米
        if isempty(pos_prev)
            ds = 0;
        else
            ds = hypot(x - pos_prev(1), z - pos_prev(2));
        end
        pos_prev = [x z];
        s_actual = s_actual + ds;

    else
        % ===== 断开判定（主脚本 else 分支）=====
        if ~isnan(last_rx) && (tNow - last_rx) > RECONNECT_GAP && connected
            connected = false;
            eventLog(end+1) = sprintf( ...
                "Unity 已断开，回到等待状态  (t=%.2f, 累计 %d 包, 里程 %.2f m)", ...
                tNow, count, s_actual);
        end
    end
end

%% ---- 结果 ----
fprintf("----- 事件日志 -----\n");
for k = 1:numel(eventLog)
    fprintf("  %s\n", eventLog(k));
end

nRound  = sum(contains(eventLog, "链路已建立"));
nDiscon = sum(contains(eventLog, "断开"));

fprintf("\n----- 判定 -----\n");
ok = true;

if nRound == 2
    fprintf("  [OK]   识别出 2 轮连接\n");
else
    fprintf("  [FAIL] 识别出 %d 轮，期望 2\n", nRound);  ok = false;
end

% 时间线里有 2 段静默（2.4~4.4s、8.0~12s），所以会有 2 次断开 —— 这是对的
if nDiscon == 2
    fprintf("  [OK]   检测到 2 次断开（对应 2 段静默，正确）\n");
else
    fprintf("  [FAIL] 检测到 %d 次断开，期望 2\n", nDiscon);  ok = false;
end

% 假车只在发包时段前进：2.4 + 3.6 = 6.0 米
if s_actual > 5.5 && s_actual < 6.5
    fprintf("  [OK]   累计里程 %.2f m，没被重连污染\n", s_actual);
else
    fprintf("  [FAIL] 累计里程 %.2f m，期望 5.5~6.5 —— 重连时基准没清干净\n", s_actual);
    ok = false;
end

if ok
    fprintf("\n===== 全部通过：常驻 / 自动重连状态机正确 =====\n");
else
    fprintf("\n===== 有失败项 =====\n");
end
