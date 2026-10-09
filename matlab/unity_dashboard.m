%% Unity 数字孪生 · 实时驾驶仪表盘 + MATLAB 控制器
%
% 配套：Assets/Scripts/Learn/MatlabBridge.cs + AutoDrive.cs + CarController.cs
%
% 【这个脚本干什么】
%   上行：实时接收 Unity 的车速 / 油门 / 刹车 / 转向 / 位置 / 前车距离
%         → 画在仪表盘上（车速表、油门条、刹车条、转向条、指示灯、曲线图）
%   下行：把「期望速度 v_des」「刹车 brake」「转向 steer_des」发回 Unity
%         → Unity 的 AutoDrive 收到后就听 MATLAB 的，不再自己全速冲
%
% 【三种运行模式】（仪表盘底部下拉框切换）
%   MATLAB 自动 —— MATLAB 看前车距离决定该跑多快（遇障碍自动减速/刹停）
%   MATLAB 手动 —— 速度由滑块给，转向也由滑块给（真正的遥控）
%   Unity 本地  —— MATLAB 不插手，车按自己那套路径跟踪跑
%
% 【刹车油门是怎么设计的】
%   油门不直接给，而是给「期望速度」。Unity 那边用
%      油门 = 前馈(期望速度/最大速度) + 比例增益 × (期望速度 − 当前速度)
%   换算成油门开度 —— 纯比例会有稳态误差，加前馈才跟得准。
%   刹车是布尔量，一给就按 CarController 的 brakeRate（12 m/s²）减速度刹。
%
% 【跑法】
%   1. 先在 Unity 里按 Play（车的 Inspector 上勾选 MatlabBridge 的 Accept Matlab Command）
%   2. 再在 MATLAB 命令窗口敲  unity_dashboard
%   3. 关掉仪表盘窗口即停止，数据自动存成 CSV
%
% 需要 R2020b 及以上（udpport）。不需要任何工具箱。
%
% 【R2025a 实测要点 2026-10-08】
%   udpport 的属性是 NumDatagramsAvailable（数据报个数），不是 NumBytesAvailable；
%   read(u,1,"uint8") 返回 Datagram 对象，字节内容在它的 .Data 里。

clear; clc; close all;

%% ===== 1. 参数（要和 Unity 侧保持一致）=====
UNITY_IP           = "127.0.0.1";   % 同一台电脑；跨机填 Unity 那台的局域网 IP
MATLAB_LISTEN_PORT = 5005;          % MATLAB 监听（Unity 往这发状态）
UNITY_LISTEN_PORT  = 5006;          % Unity 监听（MATLAB 往这发指令）

RUN_SECONDS = 600;   % 最长跑多久（关窗即停）
PLOT_EVERY  = 2;     % 每收几包刷一次曲线（太频繁会卡）

% ---- MATLAB 自动模式的决策阈值 ----
% 注意：Unity 的 CarController.maxSpeed 是 5，所以 V_MAX 别超过 5，留点余量
V_MAX  = 4.5;    % 畅通时的目标速度 m/s
V_SLOW = 2.0;    % 障碍较近时的目标速度 m/s
D_STOP = 4.0;    % 小于这个距离 → 刹停
D_SLOW = 9.0;    % 小于这个距离 → 减速

% ---- 孪生模型（一阶惯性）时间常数 ----
TAU = 1.2;

%% ===== 2. 建 UDP（先建，端口被占就直接报错退出，别留个空窗口）=====
try
    u = udpport("datagram", "IPV4", "LocalPort", MATLAB_LISTEN_PORT, "Timeout", 0.05);
catch ME
    error(['端口 %d 被占用了 —— 多半是上一次的脚本还在跑。\n' ...
           '先关掉旧的图窗，或者在命令窗口敲 clear all 再试。\n原始错误：%s'], ...
           MATLAB_LISTEN_PORT, ME.message);
end
fprintf("监听 %d，等待 Unity 数据…\n", MATLAB_LISTEN_PORT);

%% ===== 3. 建仪表盘 =====
BG = [0.93 0.94 0.96];
fig = uifigure('Name', 'Unity 数字孪生 · 实时驾驶仪表盘', ...
               'Position', [60 40 1180 780], 'Color', BG);

% ---------- 标题 ----------
uilabel(fig, 'Text', 'UNITY 数字孪生 · 实时驾驶仪表盘', ...
        'Position', [24 728 660 38], 'FontSize', 20, 'FontWeight', 'bold');
lblClock = uilabel(fig, 'Text', '等待连接…', 'Position', [780 728 376 38], ...
        'FontSize', 15, 'HorizontalAlignment', 'right', 'FontColor', [0.38 0.42 0.48]);

% ---------- 左列：车速表 ----------
gSpeed = uigauge(fig, 'semicircular');
gSpeed.Position = [20 380 340 320];
gSpeed.Limits = [0 60];
gSpeed.MajorTicks = 0:10:60;
gSpeed.MinorTicks = 0:5:60;
gSpeed.ScaleColors = [0.22 0.65 0.36; 0.95 0.76 0.20; 0.90 0.28 0.22];
gSpeed.ScaleColorLimits = [0 35; 35 50; 50 60];
gSpeed.Title = '车速 (km/h)';
gSpeed.FontSize = 11;

lblSpeed = uilabel(fig, 'Text', '0.0', 'Position', [20 308 340 64], ...
        'FontSize', 42, 'FontWeight', 'bold', 'HorizontalAlignment', 'center');
uilabel(fig, 'Text', 'km/h', 'Position', [20 284 340 24], 'FontSize', 13, ...
        'HorizontalAlignment', 'center', 'FontColor', [0.45 0.48 0.53]);

lblMile  = uilabel(fig, 'Text', '里程  0.0 m', 'Position', [20 244 165 32], 'FontSize', 14);
lblLap   = uilabel(fig, 'Text', '圈数  0',     'Position', [195 244 165 32], 'FontSize', 14);
lblWp    = uilabel(fig, 'Text', '目标点  WP-', 'Position', [20 206 340 32], 'FontSize', 14);
lblPos   = uilabel(fig, 'Text', '位置  (-, -)','Position', [20 168 340 32], 'FontSize', 14);
lblState = uilabel(fig, 'Text', '状态机  --',  'Position', [20 130 340 32], 'FontSize', 14);

% ---------- 中列：油门 / 刹车 / 转向 ----------
gThr = uigauge(fig, 'linear');
gThr.Position = [390 590 380 110];
gThr.Limits = [0 100];
gThr.MajorTicks = 0:25:100;
gThr.MajorTickLabels = {'0','25','50','75','100'};
gThr.Orientation = 'horizontal';
gThr.ScaleColors = [0.25 0.60 0.90];
gThr.ScaleColorLimits = [0 100];
gThr.Title = '油门开度 (%)';

gBrk = uigauge(fig, 'linear');
gBrk.Position = [390 470 380 110];
gBrk.Limits = [0 100];
gBrk.MajorTicks = 0:50:100;
gBrk.MajorTickLabels = {'0','50','100'};
gBrk.Orientation = 'horizontal';
gBrk.ScaleColors = [0.90 0.30 0.25];
gBrk.ScaleColorLimits = [0 100];
gBrk.Title = '刹车 (%)';

gStr = uigauge(fig, 'linear');
gStr.Position = [390 350 380 110];
gStr.Limits = [-100 100];
gStr.MajorTicks = -100:50:100;
gStr.MajorTickLabels = {'左满','-50','0','50','右满'};
gStr.Orientation = 'horizontal';
gStr.ScaleColors = [0.38 0.45 0.85];
gStr.ScaleColorLimits = [-100 100];
gStr.Title = '转向 (%)   负 = 左   正 = 右';

% ---------- 中列：指示灯 ----------
GREY = [0.62 0.65 0.69];

lampConn = uilamp(fig, 'Position', [400 296 26 26]);  lampConn.Color = GREY;
lblConn  = uilabel(fig, 'Text', '未连接', 'Position', [436 293 150 30], 'FontSize', 13);

lampCtrl = uilamp(fig, 'Position', [400 256 26 26]);  lampCtrl.Color = GREY;
lblCtrl  = uilabel(fig, 'Text', '本地控制', 'Position', [436 253 150 30], 'FontSize', 13);

lampBrk  = uilamp(fig, 'Position', [600 296 26 26]);  lampBrk.Color = GREY;
lblBrk   = uilabel(fig, 'Text', '刹车', 'Position', [636 293 120 30], 'FontSize', 13);

lampObs  = uilamp(fig, 'Position', [600 256 26 26]);  lampObs.Color = GREY;
lblObs   = uilabel(fig, 'Text', '前方障碍', 'Position', [636 253 120 30], 'FontSize', 13);

lblCtrlBig = uilabel(fig, 'Text', '等待 Unity 数据…', ...
        'Position', [390 190 380 48], 'FontSize', 16, 'FontWeight', 'bold', ...
        'HorizontalAlignment', 'center', 'BackgroundColor', [1 1 1], ...
        'FontColor', [0.35 0.38 0.44]);

% ---------- 右列：实时曲线 ----------
ax = uiaxes(fig, 'Position', [790 330 370 370]);
ax.Title.String = '速度对比 (m/s)';
ax.XLabel.String = '时间 (s)';
ax.YLabel.String = '速度';
grid(ax, 'on'); hold(ax, 'on');

hV    = animatedline(ax, 'Color', [0.16 0.42 0.86], 'LineWidth', 1.6);
hVdes = animatedline(ax, 'Color', [0.92 0.36 0.16], 'LineStyle', '--', 'LineWidth', 1.3);
hVmod = animatedline(ax, 'Color', [0.20 0.62 0.30], 'LineStyle', '-.', 'LineWidth', 1.3);
legend(ax, {'实体速度','MATLAB 期望','孪生模型'}, 'Location', 'northwest', 'FontSize', 9);

% ---------- 右列：数据明细 ----------
lblMsg = uilabel(fig, 'Text', {'等待 Unity 数据…'}, ...
        'Position', [790 180 370 140], 'FontSize', 12.5, ...
        'VerticalAlignment', 'top', 'BackgroundColor', [1 1 1], ...
        'FontColor', [0.22 0.25 0.31]);

% ---------- 底部：控制台 ----------
pnl = uipanel(fig, 'Title', '控制台', 'Position', [20 20 1140 145], 'FontSize', 13);

uilabel(pnl, 'Text', '运行模式', 'Position', [20 100 120 22], 'FontSize', 12);
ddMode = uidropdown(pnl, 'Items', {'MATLAB 自动','MATLAB 手动','Unity 本地'}, ...
        'Value', 'MATLAB 自动', 'Position', [20 62 190 30]);

uilabel(pnl, 'Text', '目标速度 (m/s)', 'Position', [240 100 200 22], 'FontSize', 12);
sldV = uislider(pnl, 'Limits', [0 8], 'Value', V_MAX, 'MajorTicks', 0:2:8, ...
        'Position', [240 78 300 3]);
lblV = uilabel(pnl, 'Text', sprintf('%.1f', V_MAX), 'Position', [556 62 70 26], ...
        'FontSize', 15, 'FontWeight', 'bold');

uilabel(pnl, 'Text', '手动转向（负 = 左，正 = 右）', 'Position', [240 40 280 22], 'FontSize', 12);
sldS = uislider(pnl, 'Limits', [-1 1], 'Value', 0, 'MajorTicks', -1:0.5:1, ...
        'Position', [240 18 300 3]);
lblS = uilabel(pnl, 'Text', '0.00', 'Position', [556 2 70 26], ...
        'FontSize', 15, 'FontWeight', 'bold');

swBrk = uiswitch(pnl, 'slider', 'Position', [660 66 50 22]);
uilabel(pnl, 'Text', '刹车', 'Position', [720 66 60 22], 'FontSize', 12);

swStop = uiswitch(pnl, 'toggle', 'Position', [660 20 60 26]);
uilabel(pnl, 'Text', '急停', 'Position', [730 22 60 22], 'FontSize', 12, ...
        'FontColor', [0.85 0.25 0.18]);

lblSend = uilabel(pnl, 'Text', '已下发 0 条指令', 'Position', [820 70 300 28], 'FontSize', 13);
lblStat = uilabel(pnl, 'Text', '收包 0', 'Position', [820 36 300 28], ...
        'FontSize', 13, 'FontColor', [0.45 0.48 0.53]);

% 滑块实时显示数字
sldV.ValueChangedFcn = @(s,e) set(lblV, 'Text', sprintf('%.1f', s.Value));
sldS.ValueChangedFcn = @(s,e) set(lblS, 'Text', sprintf('%.2f', s.Value));

%% ===== 4. 主循环 =====
t0 = tic;
seq = 0; count = 0;
s_actual = 0; v_model = 0; s_model = 0;
t_prev = []; pos_prev = [];
v_des = 0; brake = false; steer_des = 999;

% 预分配日志数组（循环里用 end+1 增长数组每次都要重新分配内存，很慢，MATLAB 也会警告）
MAXLOG = 50000;
log_t    = zeros(MAXLOG,1);  log_v    = zeros(MAXLOG,1);
log_vdes = zeros(MAXLOG,1);  log_vmod = zeros(MAXLOG,1);
log_d    = zeros(MAXLOG,1);  log_wp   = zeros(MAXLOG,1);
nlog = 0;

stateNames = {'Driving','Blocked','Finished'};

while toc(t0) < RUN_SECONDS && isvalid(fig)

    n = u.NumDatagramsAvailable;

    if n > 0
        d   = read(u, 1, "uint8");          % 读 1 个数据报 → Datagram 对象
        raw = uint8(d(1).Data);             % 字节内容在 .Data 里
        s   = string(char(raw(:)'));
        try
            st = jsondecode(s);
        catch
            continue;                       % 半包/脏数据，跳过别崩
        end

        count = count + 1;

        %% --- 4.1 实体走了多远（位置差分，不依赖 Unity 的 speed 字段）---
        if isempty(pos_prev)
            ds = 0; dt = 0;
        else
            dt = st.t - t_prev;
            ds = hypot(st.x - pos_prev(1), st.z - pos_prev(2));
        end
        pos_prev = [st.x, st.z];
        t_prev   = st.t;
        s_actual = s_actual + ds;

        %% --- 4.2 按模式决策：给多快的速度、要不要刹车 ---
        mode = ddMode.Value;

        if swStop.Value                          % 急停优先级最高
            v_des = 0;  brake = true;

        elseif strcmp(mode, 'MATLAB 自动')
            if st.has_front && st.front_dist < D_STOP
                v_des = 0;       brake = true;    % 太近 → 刹停
            elseif st.has_front && st.front_dist < D_SLOW
                v_des = V_SLOW;  brake = false;   % 较近 → 减速
            else
                v_des = V_MAX;   brake = false;   % 畅通 → 正常跑
            end

        elseif strcmp(mode, 'MATLAB 手动')
            v_des = sldV.Value;
            brake = swBrk.Value;

        else                                     % Unity 本地
            v_des = -1;  brake = false;           % -1 = 不接管速度
        end

        % 手动刹车开关：只要不是"Unity 本地"模式，拨了就刹
        if ~strcmp(mode, 'Unity 本地') && swBrk.Value
            brake = true;
        end

        %% --- 4.3 孪生模型推演（数字孪生的核心）---
        vRef = max(v_des, 0);
        if dt > 0
            v_model = v_model + (vRef - v_model) * dt / TAU;
            s_model = s_model + v_model * dt;
        end

        %% --- 4.4 转向指令 ---
        if strcmp(mode, 'MATLAB 手动')
            steer_des = sldS.Value;              % 手动模式：滑块说了算
        else
            steer_des = 999;                     % 其他模式：交给 Unity 的路径跟踪
        end

        %% --- 4.5 回传指令给 Unity ---
        cmd = struct('v_des', v_des, 'steer_des', steer_des, ...
                     'brake', brake, 'seq', seq);
        seq = seq + 1;
        write(u, uint8(jsonencode(cmd)), "uint8", UNITY_IP, UNITY_LISTEN_PORT);

        %% --- 4.6 刷新仪表盘 ---
        kmh = st.speed * 3.6;
        gSpeed.Value  = min(kmh, 60);
        lblSpeed.Text = sprintf('%.1f', kmh);
        gThr.Value    = st.motor * 100;
        gBrk.Value    = double(st.braking) * 100;
        gStr.Value    = st.steer * 100;

        lblMile.Text = sprintf('里程  %.1f m', s_actual);
        lblLap.Text  = sprintf('圈数  %d', st.lap);
        lblWp.Text   = sprintf('目标点  WP%d', st.wp);
        lblPos.Text  = sprintf('位置  (%.1f, %.1f)', st.x, st.z);

        si = max(min(st.state, 2), 0) + 1;       % 防越界
        lblState.Text = sprintf('状态机  %s', stateNames{si});

        % 连接灯
        hasAccept = isfield(st, 'accept') && st.accept;
        if hasAccept
            lampConn.Color = [0.20 0.75 0.35];
            lblConn.Text   = '已连接 · 已授权';
        else
            lampConn.Color = [0.93 0.72 0.20];
            lblConn.Text   = '已连接 · 未授权';
        end

        % 控制权灯
        if strcmp(mode, 'Unity 本地')
            lampCtrl.Color = GREY;
            lblCtrl.Text   = '本地控制';
        else
            lampCtrl.Color = [0.20 0.60 0.92];
            lblCtrl.Text   = 'MATLAB 接管';
        end

        % 刹车 / 障碍灯
        if st.braking
            lampBrk.Color = [0.92 0.28 0.22];
        else
            lampBrk.Color = GREY;
        end
        if st.has_front
            lampObs.Color = [0.95 0.45 0.15];
        else
            lampObs.Color = GREY;
        end

        % 顶部大字：现在到底谁在开车
        if strcmp(mode, 'Unity 本地')
            lblCtrlBig.Text = '控制来源：Unity 本地';
            lblCtrlBig.FontColor = [0.35 0.38 0.44];
        elseif ~hasAccept
            lblCtrlBig.Text = 'Unity 未勾选 Accept Matlab Command';
            lblCtrlBig.FontColor = [0.85 0.30 0.20];
        else
            lblCtrlBig.Text = sprintf('控制来源：MATLAB · %s', mode);
            lblCtrlBig.FontColor = [0.10 0.42 0.80];
        end

        % 数据明细
        if v_des < 0
            vDesText = '不接管（交给 Unity）';
        else
            vDesText = sprintf('%.2f m/s', v_des);
        end
        if st.has_front
            frontText = sprintf('有！%.1f m', st.front_dist);
        else
            frontText = '无';
        end

        lblMsg.Text = {
            sprintf('运行模式    %s', mode)
            sprintf('期望速度    %s', vDesText)
            sprintf('实际速度    %.2f m/s  (%.1f km/h)', st.speed, kmh)
            sprintf('油门/刹车   %.0f%%  /  %s', st.motor*100, string(st.braking))
            sprintf('前方障碍    %s', frontText)
            sprintf('孪生模型    %.2f m/s', v_model)
            sprintf('实体里程    %.1f m      孪生里程  %.1f m', s_actual, s_model)
            sprintf('收包 / 下发 %d  /  %d', count, seq)
        };

        lblClock.Text = char(datetime('now', 'Format', 'HH:mm:ss'));
        lblSend.Text  = sprintf('已下发 %d 条指令', seq);
        lblStat.Text  = sprintf('收包 %d 条', count);

        %% --- 4.7 记录 ---
        if nlog < MAXLOG
            nlog = nlog + 1;
            log_t(nlog)    = st.t;
            log_v(nlog)    = st.speed;
            log_vdes(nlog) = max(v_des, 0);
            log_vmod(nlog) = v_model;
            log_d(nlog)    = st.front_dist;
            log_wp(nlog)   = st.wp;
        end

        %% --- 4.8 刷新曲线 ---
        if mod(count, PLOT_EVERY) == 0
            addpoints(hV,    st.t, st.speed);
            addpoints(hVdes, st.t, max(v_des, 0));
            addpoints(hVmod, st.t, v_model);
            drawnow limitrate;
        end

    else
        pause(0.005);   % 没数据就让出 CPU，别空转烧满一个核
    end
end

%% ===== 5. 收尾：存 CSV + 打印统计 =====
clear u;

if nlog > 0
    % 把预分配数组截到实际长度
    lg_t  = log_t(1:nlog);    lg_v  = log_v(1:nlog);
    lg_vd = log_vdes(1:nlog); lg_vm = log_vmod(1:nlog);
    lg_d  = log_d(1:nlog);    lg_wp = log_wp(1:nlog);

    T = table(lg_t, lg_v, lg_vd, lg_vm, lg_d, lg_wp, ...
        'VariableNames', {'t','speed','v_des','v_model','front_dist','wp'});
    fname = "dash_log_" + string(datetime('now'), 'yyyyMMdd_HHmmss') + ".csv";
    writetable(T, fname);

    err = lg_v - lg_vm;

    fprintf("\n===== 运行结果 =====\n");
    fprintf("收到 %d 包，已保存 %s\n", count, fname);
    fprintf("实体平均速度 %.2f m/s | 孪生模型平均速度 %.2f m/s\n", mean(lg_v), mean(lg_vm));
    fprintf("速度偏差：均值 %.3f，RMS %.3f，最大 %.3f m/s\n", ...
        mean(err), sqrt(mean(err.^2)), max(abs(err)));
    fprintf("实体累计里程 %.2f m | 孪生里程 %.2f m\n", s_actual, s_model);
    fprintf("提示：偏差 RMS 越小，说明 TAU 这个模型参数标定得越准。\n");
else
    fprintf("\n一个包都没收到。按顺序检查：\n");
    fprintf("  1. Unity 按 Play 了吗？\n");
    fprintf("  2. 车的 Inspector 上 MatlabBridge 的 Enable Network 勾了吗？\n");
    fprintf("  3. 端口被占？cmd 里跑：netstat -ano | findstr 5005\n");
end
