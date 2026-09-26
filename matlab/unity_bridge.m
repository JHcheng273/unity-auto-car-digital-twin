%% Unity <-> MATLAB 数字孪生：双向 UDP 联动
% 配套：Assets/Scripts/Shared/MatlabUdpBridge.cs
%
% 【数字孪生的三个要素在这里分别是什么】
%   物理实体 = Unity 里的那辆车（真实的运动、有传感器）
%   虚拟模型 = 本脚本里的"一阶惯性模型"，独立推演车速和里程
%   数据连接 = UDP 双向：实体发状态上来，孪生体发指令回去
%
% 【跑法】
%   1. 先运行本脚本（会开出图窗并等待数据）
%   2. 再在 Unity 里按 Play
%   3. 关掉图窗即停止，数据自动存成 CSV
%
% 需要 R2020b 及以上（用到了 udpport）。不需要任何工具箱。

clear; clc; close all;

%% ===== 1. 参数（要和 Unity 侧保持一致）=====
% 同一台电脑：UNITY_IP 填 "127.0.0.1"
% 两台电脑（无线）：UNITY_IP 填 Unity 那台机器的局域网 IP
%   在 Unity 机器上开 cmd 跑 ipconfig，找"无线局域网适配器 WLAN"下的 IPv4 地址
UNITY_IP           = "127.0.0.1";
MATLAB_LISTEN_PORT = 5005;   % MATLAB 监听（Unity 往这发状态）
UNITY_LISTEN_PORT  = 5006;   % Unity 监听（MATLAB 往这发指令）
% 跨机时还要做两件事：
%   1. 本机 Windows 防火墙放行 UDP 5005 / 5006（入站）
%   2. Unity 的 Inspector 里把 MatlabUdpBridge 的 matlabHost 改成这台机器的 IP

RUN_SECONDS = 120;   % 跑多久，调试时改小
PLOT_EVERY  = 3;     % 每收几包刷新一次图（刷新太频繁会卡）

%% ===== 2. 孪生体（虚拟模型）参数 =====
% 一阶惯性模型：车不会瞬间达到目标速度，中间有个时间常数
% 这个 tau 就是"你的数字模型认为车有多迟钝"，和实体对比能看出模型准不准
TAU     = 1.2;   % 时间常数（秒），越大越迟钝
V_MAX   = 8.0;   % 畅通时的目标速度
V_SLOW  = 3.0;   % 较近时的目标速度
D_STOP  = 5.0;   % 小于这个距离就停车（米）
D_SLOW  = 10.0;  % 小于这个距离就减速（米）

%% ===== 3. 建 UDP =====
u = udpport("datagram", "IPV4", "LocalPort", MATLAB_LISTEN_PORT, "Timeout", 0.05);
fprintf("监听 %d，等待 Unity 数据...\n", MATLAB_LISTEN_PORT);

%% ===== 4. 图窗：三张实时曲线（答辩时这图很加分）=====
fig = figure('Name','数字孪生实时监控','NumberTitle','off','Position',[80 60 900 720]);

ax1 = subplot(3,1,1);
h_v = animatedline('Color','b','LineWidth',1.5);
hold on; h_vdes = animatedline('Color','r','LineStyle','--','LineWidth',1.2);
h_vmod = animatedline('Color',[0 0.6 0],'LineStyle','-.','LineWidth',1.3);
grid on; ylabel('速度 m/s');
legend('实体实际速度','孪生体目标速度','孪生模型速度','Location','northwest');

ax2 = subplot(3,1,2);
h_d = animatedline('Color','m','LineWidth',1.5);
hold on; grid on; ylabel('前车距离 m');

ax3 = subplot(3,1,3);
h_sa = animatedline('Color','b','LineWidth',1.5);
hold on; h_sm = animatedline('Color',[0 0.6 0],'LineStyle','-.','LineWidth',1.3);
grid on; ylabel('累计里程 m'); xlabel('仿真时间 s');
legend('实体实际里程','孪生模型里程','Location','northwest');

%% ===== 5. 状态初始化 =====
v_model  = 0;    % 孪生模型算出的速度
s_model  = 0;    % 孪生模型算出的累计里程
s_actual = 0;    % 实体实际累计里程（位置差分累加）
t_prev   = [];
pos_prev = [];

t0 = tic;
seq = 0;
count = 0;
log_t = []; log_v = []; log_vdes = []; log_vmod = [];
log_d = []; log_wp = []; log_sa = []; log_sm = [];

%% ===== 6. 主循环 =====
while toc(t0) < RUN_SECONDS && ishandle(fig)
    n = u.NumBytesAvailable;

    if n > 0
        raw = read(u, n, "uint8");
        s   = string(char(raw(:)'));
        try
            st = jsondecode(s);
        catch
            continue;   % 解析失败（半包 / 脏数据）就跳过，别崩
        end

        count = count + 1;

        %% --- 6.1 实体走了多远（用位置差分算，不依赖 Unity 的速度字段）---
        if isempty(pos_prev)
            ds = 0;
            dt = 0;
        else
            dt = st.t - t_prev;
            ds = hypot(st.x - pos_prev(1), st.z - pos_prev(2));
        end
        pos_prev = [st.x, st.z];
        t_prev   = st.t;
        s_actual = s_actual + ds;

        %% --- 6.2 决策：按前车距离定目标速度 ---
        if st.has_front && st.front_dist < D_STOP
            v_des = 0.0;                 % 太近，停
        elseif st.has_front && st.front_dist < D_SLOW
            v_des = V_SLOW;              % 较近，慢
        else
            v_des = V_MAX;               % 畅通，正常跑
        end

        %% --- 6.3 孪生模型推演（数字孪生的核心）---
        % 一阶惯性：模型速度按时间常数逼近目标速度
        if dt > 0
            v_model = v_model + (v_des - v_model) * dt / TAU;
            s_model = s_model + v_model * dt;
        end

        %% --- 6.4 回传指令给 Unity（反向控制）---
        % steer_des = 999 表示"转向交给 Unity 的 WaypointFollower 自己算"
        cmd = struct('v_des', v_des, 'steer_des', 999, 'brake', v_des == 0, 'seq', seq);
        seq = seq + 1;
        write(u, uint8(jsonencode(cmd)), "uint8", UNITY_IP, UNITY_LISTEN_PORT);

        %% --- 6.5 记录 ---
        log_t(end+1,1)    = st.t;
        log_v(end+1,1)    = st.speed;
        log_vdes(end+1,1) = v_des;
        log_vmod(end+1,1) = v_model;
        log_d(end+1,1)    = st.front_dist;
        log_wp(end+1,1)   = st.wp;
        log_sa(end+1,1)   = s_actual;
        log_sm(end+1,1)   = s_model;

        %% --- 6.6 刷新图 ---
        if mod(count, PLOT_EVERY) == 0
            addpoints(h_v,    st.t, st.speed);
            addpoints(h_vdes, st.t, v_des);
            addpoints(h_vmod, st.t, v_model);
            addpoints(h_d,    st.t, st.front_dist);
            addpoints(h_sa,   st.t, s_actual);
            addpoints(h_sm,   st.t, s_model);
            drawnow limitrate;
        end
    else
        pause(0.005);   % 没数据就让出 CPU，别空转烧满一个核
    end
end

%% ===== 7. 收尾：存 CSV + 打印孪生精度指标 =====
clear u;
if ~isempty(log_t)
    T = table(log_t, log_v, log_vdes, log_vmod, log_d, log_wp, log_sa, log_sm, ...
        'VariableNames', {'t','speed','v_des','v_model','front_dist','wp','s_actual','s_model'});
    fname = "twin_log_" + datestr(now,'yyyymmdd_HHMMSS') + ".csv";
    writetable(T, fname);

    err = log_v - log_vmod;          % 实体速度 - 模型速度

    fprintf("\n===== 数字孪生结果 =====\n");
    fprintf("收到 %d 包，已保存 %s\n", count, fname);
    fprintf("实体平均速度 %.2f m/s | 孪生模型平均速度 %.2f m/s\n", ...
        mean(log_v), mean(log_vmod));
    fprintf("速度偏差：均值 %.3f m/s，RMS %.3f m/s，最大 %.3f m/s\n", ...
        mean(err), sqrt(mean(err.^2)), max(abs(err)));
    fprintf("累计里程：实体 %.2f m | 模型 %.2f m | 差异 %.2f%%\n", ...
        s_actual, s_model, 100*abs(s_actual-s_model)/max(s_actual,0.001));
    fprintf("最小前车距离 %.2f m\n", min(log_d));
    fprintf("提示：偏差 RMS 越小，说明 tau 这个模型参数标定得越准。\n");
else
    fprintf("一个包都没收到。检查：Unity 开了没？防火墙？端口对不对？\n");
end
