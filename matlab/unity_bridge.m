%% Unity <-> MATLAB 数字孪生 UDP 桥接（最小可跑示例）
% 配套：Assets/Scripts/Shared/MatlabUdpBridge.cs
%
% 跑法：
%   1. 先运行本脚本（会开出图窗并等待数据）
%   2. 再在 Unity 里按 Play
%   3. 关掉图窗即停止，数据自动存成 CSV
%
% 需要 R2020b 及以上（用到了 udpport）。老版本用 udp() 对象，语法不一样。
% 工具箱：不需要任何额外工具箱，纯 MATLAB 基础环境就能跑。

clear; clc; close all;

%% ===== 参数（和 Unity 侧保持一致）=====
UNITY_IP          = "127.0.0.1";
MATLAB_LISTEN_PORT = 5005;   % MATLAB 监听（Unity 往这发状态）
UNITY_LISTEN_PORT  = 5006;   % Unity 监听（MATLAB 往这发指令）
RUN_SECONDS        = 120;    % 跑多久，改小方便调试
PLOT_EVERY         = 5;      % 每收几包刷新一次图（刷新太频繁会卡）

%% ===== 建 UDP =====
u = udpport("datagram", "IPV4", "LocalPort", MATLAB_LISTEN_PORT, "Timeout", 0.05);
fprintf("监听 %d，等待 Unity 数据...\n", MATLAB_LISTEN_PORT);

%% ===== 图窗：实时曲线（答辩时这图很加分）=====
fig = figure('Name','数字孪生实时监控','NumberTitle','off');
ax1 = subplot(2,1,1); h_v = animatedline('Color','b','LineWidth',1.5);
     hold on; h_vdes = animatedline('Color','r','LineStyle','--','LineWidth',1.2);
     ylabel('速度 m/s'); legend('实际速度','目标速度','Location','northwest'); grid on;
ax2 = subplot(2,1,2); h_d = animatedline('Color','m','LineWidth',1.5);
     ylabel('前车距离 m'); xlabel('仿真时间 s'); grid on;

%% ===== 主循环 =====
t0 = tic;
seq = 0;
count = 0;
log_t = []; log_v = []; log_vdes = []; log_d = []; log_wp = [];

while toc(t0) < RUN_SECONDS && ishandle(fig)
    % --- 收一包状态 ---
    n = u.NumBytesAvailable;
    if n > 0
        raw = read(u, n, "uint8");
        s   = string(char(raw(:)'));
        try
            st = jsondecode(s);
        catch
            continue;   % 解析失败（半包/脏数据）就跳过，别崩
        end

        count = count + 1;

        % --- 算法：这里就是你的"虚拟模型" ---
        % 最简单的纵向决策：按前车距离给目标速度
        if st.has_front && st.front_dist < 5
            v_des = 0.0;        % 太近，停
        elseif st.has_front && st.front_dist < 10
            v_des = 3.0;        % 较近，慢
        else
            v_des = 8.0;        % 畅通，正常跑
        end

        % 转向交给 Unity 的 WaypointFollower（999 表示"我不管"）
        cmd = struct('v_des', v_des, 'steer_des', 999, 'brake', v_des == 0, 'seq', seq);
        seq = seq + 1;

        % --- 回传指令 ---
        write(u, uint8(jsonencode(cmd)), "uint8", UNITY_IP, UNITY_LISTEN_PORT);

        % --- 存日志 ---
        log_t(end+1,1)    = st.t;
        log_v(end+1,1)    = st.speed;
        log_vdes(end+1,1) = v_des;
        log_d(end+1,1)    = st.front_dist;
        log_wp(end+1,1)   = st.wp;

        % --- 刷新图 ---
        if mod(count, PLOT_EVERY) == 0
            addpoints(h_v,   st.t, st.speed);
            addpoints(h_vdes, st.t, v_des);
            addpoints(h_d,   st.t, st.front_dist);
            drawnow limitrate;
        end
    else
        pause(0.005);   % 没数据就让出 CPU，别空转烧满一个核
    end
end

%% ===== 收尾：存 CSV，直接拿去写报告 =====
clear u;
if ~isempty(log_t)
    T = table(log_t, log_v, log_vdes, log_d, log_wp, ...
        'VariableNames', {'t','speed','v_des','front_dist','wp'});
    fname = "twin_log_" + datestr(now,'yyyymmdd_HHMMSS') + ".csv";
    writetable(T, fname);
    fprintf("收到 %d 包，已保存 %s\n", count, fname);

    fprintf("平均速度 %.2f m/s，最小前车距离 %.2f m\n", ...
        mean(log_v), min(log_d));
else
    fprintf("一个包都没收到。检查：Unity 开了没？防火墙？端口对不对？\n");
end
