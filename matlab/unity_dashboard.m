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
% 【跑法】★ 顺序很重要，反了就"永远无连接" ★
%   1. 先在 MATLAB 命令窗口敲  unity_dashboard   ← 它先开始监听 5005，然后一直等
%   2. 再回 Unity 按 Play
%   为什么不能反过来：通信是"单向触发"的 ——
%       Unity 发状态包 → MATLAB 收到后才回发指令 → Unity 收到回包才算"已连接"。
%       如果先 Play 了 Unity 又停下，MATLAB 才开始监听，那它就永远等不到包。
%       （先 Play 再开 MATLAB 也不是不行，但必须保证 Unity 一直还在 Play 中。）
%   3. 关掉仪表盘窗口即停止，数据自动存成 CSV
%
% 【★ 常驻模式（2026-10-10 改）★】
%   这个脚本现在**不会自己超时退出**了，行为是：
%     · 开一次就一直挂着，Unity 按多少次 Play 都行；
%     · Unity 停 Play → 界面自动回到"等待 Unity…"，但**累计量不清零**
%       （里程 / 曲线 / 收包数都留着，方便你对比多轮实验）；
%     · 想清零重开，就关掉窗口再跑一次脚本（或按下面的重连计数看第几轮）。
%   好处：不用每次 Play 前都回 MATLAB 敲一遍命令 —— 开机跑一次就扔那儿。
%
% 【为什么不在 Unity 里自动拉起 MATLAB】
%   Unity 侧确实能 Process.Start 拉起 MATLAB，但代价很大：
%     · MATLAB 空载约 1.5 GB 内存 + 启动 20 秒；
%     · 每按一次 Play 就新开一个，连续试几次机器直接卡死；
%     · 自动拉起的那个 MATLAB 是"黑窗口"，你没法在上面拖滑块调参。
%   所以正确做法是反过来：**MATLAB 常驻，Unity 随便连**。
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
VIDEO_PORT         = 5007;          % MATLAB 监听（Unity 往这发摄像头画面）

% ---- 摄像头 / 偏离判定 ----
ENABLE_VIDEO   = true;   % 关掉的话就不收画面、不显示摄像头面板（省点 CPU）
DEV_DIST_LIMIT = 2.0;    % 横向偏离多少米算"压线"
DEV_ANG_LIMIT  = 35;     % 车头方向跟路径方向差多少度算"偏了"

% ---- 航点表（必须和 Unity 场景里的路径一致）----
% 用来算"车离路径多远、车头歪没歪"。
% ★ 这里填的是 mycar.unity 里那 4 个点的世界坐标 (x, z)。
%   以后在 Unity 里改了航点位置，这张表也要跟着改。
WP_POINTS = [  8.49,  -3.10;
              -1.51,   6.90;
             -11.51,  -3.10;
              -1.51, -13.10];

RUN_SECONDS = Inf;   % ★ 常驻：不因超时退出，只有关窗口才停
                     %   （想限时跑就写个秒数，比如 300 = 5 分钟后自动停并存 CSV）
% ★ 也可以用环境变量临时覆盖，方便"录一段固定时长"或自动化测试：
%     set DASH_RUN_SECONDS=30  &&  matlab -batch unity_dashboard
envRun = getenv('DASH_RUN_SECONDS');
if ~isempty(envRun)
    v = str2double(envRun);
    if ~isnan(v) && v > 0
        RUN_SECONDS = v;
        fprintf("（环境变量 DASH_RUN_SECONDS=%s）本次只跑 %.0f 秒\n", envRun, v);
    end
end
PLOT_EVERY  = 2;     % 每收几包刷一次曲线（太频繁会卡）
RECONNECT_GAP = 3;   % 秒。超过这么久没收到包算"Unity 断开"，界面切回等待态
                     %   （Unity 停 Play 后本来就不再发包，用它来判定断开）

% ---- MATLAB 自动模式的决策阈值 ----
% 注意：Unity 的 CarController.maxSpeed 是 5，所以 V_MAX 别超过 5，留点余量
V_MAX  = 4.5;    % 畅通时的目标速度 m/s
V_SLOW = 2.0;    % 障碍较近时的目标速度 m/s
D_STOP = 4.0;    % 小于这个距离 → 刹停
D_SLOW = 9.0;    % 小于这个距离 → 减速

% ---- 孪生模型（一阶惯性）时间常数 ----
TAU = 1.2;

%% ===== 2. 建 UDP（先建，端口被占就直接报错退出，别留个空窗口）=====

% 先清理同一个 MATLAB 会话里可能残留的 udpport。
% 为什么会残留：脚本跑到一半按 Ctrl+C，收尾的 clear u 就不会执行，
%   于是那个 udpport 对象一直占着 5005，下次再跑就报"端口被占用"。
try
    stale = udpportfind;
    for k = 1:numel(stale)
        try
            if double(stale(k).LocalPort) == MATLAB_LISTEN_PORT
                fprintf("发现残留的 udpport（正占用 %d），先清理掉…\n", MATLAB_LISTEN_PORT);
                delete(stale(k));
            end
        catch
        end
    end
    pause(0.2);
catch
    % udpportfind 不可用就算了，不影响主流程
end

try
    u = udpport("datagram", "IPV4", "LocalPort", MATLAB_LISTEN_PORT, "Timeout", 0.05);
catch ME
    error(['端口 %d 被占用了。按顺序排查：\n' ...
           '  ① 旧的仪表盘图窗还开着吗？关掉它。\n' ...
           '  ② 命令窗口敲 clear all，再重跑本脚本。\n' ...
           '  ③ 还不行就关掉 MATLAB 重开 —— 僵尸端口只能这么清。\n' ...
           '  （想快速诊断，在 MATLAB 里跑 check_link）\n' ...
           '原始错误：%s'], ...
           MATLAB_LISTEN_PORT, ME.message);
end
fprintf("监听 %d，等待 Unity 数据…\n", MATLAB_LISTEN_PORT);

% ---- 视频接收端（独立端口 5007，和状态包互不干扰）----
% ★ 必须单独 try 包起来：unity_dashboard 的两个头等大事是
%   "收状态 + 画仪表盘"。万一 5007 起不来（被占/防火墙），
%   也不该把整个仪表盘拖下水 —— 大不了这次没有画面。
videoRx = [];
if ENABLE_VIDEO
    try
        videoRx = VideoReceiver(VIDEO_PORT);
        fprintf("监听 %d，等待摄像头画面…\n", VIDEO_PORT);
    catch ME
        fprintf("⚠ 视频端口 %d 起不来（%s）\n", VIDEO_PORT, ME.message);
        fprintf("  仪表盘照常运行，只是没有实时画面。\n");
        fprintf("  排查：netstat -ano | findstr %d\n", VIDEO_PORT);
        videoRx = [];
    end
end

%% ===== 3. 建仪表盘 =====
BG = [0.93 0.94 0.96];
% ★ 窗口比原来宽了 400px：右边新开一列放摄像头。
%   原来三列是 [16..] [388..] [784..]，各宽 380、右边界 1164；
%   现在再加一列 [1180..]，宽 380、右边界 1560，窗口留到 1580。
FIG_W = 1560;
fig = uifigure('Name', 'Unity 数字孪生 · 实时驾驶仪表盘', ...
               'Position', [40 40 FIG_W 780], 'Color', BG);

% ---------- 标题 ----------
uilabel(fig, 'Text', 'UNITY 数字孪生 · 实时驾驶仪表盘', ...
        'Position', [24 728 660 38], 'FontSize', 20, 'FontWeight', 'bold');
lblClock = uilabel(fig, 'Text', '等待连接…', 'Position', [FIG_W-400 728 376 38], ...
        'FontSize', 15, 'HorizontalAlignment', 'right', 'FontColor', [0.38 0.42 0.48]);

% ---------- 左列：车速表 ----------
% 【R2025a 实测】uigauge 既没有 Title 也没有 Label 属性（只有 Limits / MajorTicks /
%   MinorTicks / ScaleColors / ScaleColorLimits / FontSize / Value / Orientation），
%   所以标题只能用外部的 uilabel 或 uipanel 的 Title 来做。
%
% 【布局思路】用 uipanel 把左列切成上下两块，标题嵌在边框上 ——
%   比"外置 uilabel 摆在表上方"节省纵向空间，也不会跟表盘刻度贴在一起。
%   左列总高：从 y=176 到 y=712（536px），让出顶部标题栏和底部控制台。
pnlSpeed = uipanel(fig, 'Title', '车速', 'Position', [16 300 356 412], ...
                   'FontSize', 13, 'FontWeight', 'bold', ...
                   'BackgroundColor', [1 1 1]);

gSpeed = uigauge(pnlSpeed, 'semicircular');
gSpeed.Position = [8 46 340 330];
gSpeed.Limits = [0 60];
gSpeed.MajorTicks = 0:10:60;
gSpeed.MinorTicks = 0:5:60;
gSpeed.ScaleColors = [0.22 0.65 0.36; 0.95 0.76 0.20; 0.90 0.28 0.22];
gSpeed.ScaleColorLimits = [0 35; 35 50; 50 60];
gSpeed.FontSize = 11;

% 大号数字摆在表盘中心偏下（表盘是半圆，中间是空的，正好塞进去）
lblSpeed = uilabel(pnlSpeed, 'Text', '0.0', 'Position', [8 118 340 62], ...
        'FontSize', 40, 'FontWeight', 'bold', 'HorizontalAlignment', 'center');
uilabel(pnlSpeed, 'Text', 'km/h', 'Position', [8 96 340 22], 'FontSize', 12, ...
        'HorizontalAlignment', 'center', 'FontColor', [0.45 0.48 0.53]);

% ---------- 左列下半：运行信息 ----------
pnlInfo = uipanel(fig, 'Title', '运行信息', 'Position', [16 172 356 120], ...
                  'FontSize', 13, 'FontWeight', 'bold', ...
                  'BackgroundColor', [1 1 1]);

% 两列排布：左边 3 行，右边 3 行，每行 28px 高、间隔 2px
lblMile  = uilabel(pnlInfo, 'Text', '里程   0.0 m',  'Position', [14 74 160 26], 'FontSize', 13);
lblLap   = uilabel(pnlInfo, 'Text', '圈数   0',      'Position', [186 74 156 26], 'FontSize', 13);
lblWp    = uilabel(pnlInfo, 'Text', '目标点   WP-',  'Position', [14 44 160 26], 'FontSize', 13);
lblPos   = uilabel(pnlInfo, 'Text', '位置   (-,-)',  'Position', [186 44 156 26], 'FontSize', 13);
lblState = uilabel(pnlInfo, 'Text', '状态机   --',   'Position', [14 14 328 26], 'FontSize', 13);

% ---------- 中列：油门 / 刹车 / 转向 ----------
% 同样用 uipanel 分组：标题嵌在边框上，表盘放在面板内。
% 中列 x 从 388 到 768（宽 380）。
pnlThr = uipanel(fig, 'Title', '油门开度 (%)', 'Position', [388 578 380 96], ...
                 'FontSize', 13, 'FontWeight', 'bold', 'BackgroundColor', [1 1 1]);
gThr = uigauge(pnlThr, 'linear');
gThr.Position = [14 12 352 60];
gThr.Limits = [0 100];
gThr.MajorTicks = 0:25:100;
gThr.MajorTickLabels = {'0','25','50','75','100'};
gThr.Orientation = 'horizontal';
gThr.ScaleColors = [0.25 0.60 0.90];
gThr.ScaleColorLimits = [0 100];

pnlBrk = uipanel(fig, 'Title', '刹车 (%)', 'Position', [388 476 380 96], ...
                 'FontSize', 13, 'FontWeight', 'bold', 'BackgroundColor', [1 1 1]);
gBrk = uigauge(pnlBrk, 'linear');
gBrk.Position = [14 12 352 60];
gBrk.Limits = [0 100];
gBrk.MajorTicks = 0:50:100;
gBrk.MajorTickLabels = {'0','50','100'};
gBrk.Orientation = 'horizontal';
gBrk.ScaleColors = [0.90 0.30 0.25];
gBrk.ScaleColorLimits = [0 100];

pnlStr = uipanel(fig, 'Title', '转向 (%)    负 = 左    正 = 右', ...
                 'Position', [388 374 380 96], ...
                 'FontSize', 13, 'FontWeight', 'bold', 'BackgroundColor', [1 1 1]);
gStr = uigauge(pnlStr, 'linear');
gStr.Position = [14 12 352 60];
gStr.Limits = [-100 100];
gStr.MajorTicks = -100:50:100;
gStr.MajorTickLabels = {'左满','-50','0','50','右满'};
gStr.Orientation = 'horizontal';
gStr.ScaleColors = [0.38 0.45 0.85];
gStr.ScaleColorLimits = [-100 100];

% ---------- 中列：指示灯（独立面板，四盏灯两行两列）----------
GREY = [0.62 0.65 0.69];

pnlLamp = uipanel(fig, 'Title', '状态指示灯', 'Position', [388 240 380 128], ...
                  'FontSize', 13, 'FontWeight', 'bold', 'BackgroundColor', [1 1 1]);

lampConn = uilamp(pnlLamp, 'Position', [22 68 24 24]);  lampConn.Color = GREY;
lblConn  = uilabel(pnlLamp, 'Text', '未连接', 'Position', [56 66 130 26], 'FontSize', 12.5);

lampCtrl = uilamp(pnlLamp, 'Position', [22 28 24 24]);  lampCtrl.Color = GREY;
lblCtrl  = uilabel(pnlLamp, 'Text', '本地控制', 'Position', [56 26 130 26], 'FontSize', 12.5);

lampBrk  = uilamp(pnlLamp, 'Position', [210 68 24 24]); lampBrk.Color = GREY;
lblBrk   = uilabel(pnlLamp, 'Text', '刹车中', 'Position', [244 66 120 26], 'FontSize', 12.5);

lampObs  = uilamp(pnlLamp, 'Position', [210 28 24 24]); lampObs.Color = GREY;
lblObs   = uilabel(pnlLamp, 'Text', '前方有障碍', 'Position', [244 26 120 26], 'FontSize', 12.5);

% ---------- 中列底部：谁在开车（大字提示条）----------
lblCtrlBig = uilabel(fig, 'Text', '等待 Unity 数据…', ...
        'Position', [388 176 380 56], 'FontSize', 15, 'FontWeight', 'bold', ...
        'HorizontalAlignment', 'center', 'VerticalAlignment', 'center', ...
        'BackgroundColor', [1 1 1], 'FontColor', [0.35 0.38 0.44]);

% ---------- 右列：实时曲线 ----------
% 右列 x 从 784 到 1164（宽 380）。
% 曲线占上半，明细占下半，两块高度加起来 = 顶栏到控制台之间的距离。
pnlChart = uipanel(fig, 'Title', '速度对比 (m/s)', 'Position', [784 464 380 248], ...
                   'FontSize', 13, 'FontWeight', 'bold', 'BackgroundColor', [1 1 1]);

ax = uiaxes(pnlChart, 'Position', [12 10 356 200]);
ax.XLabel.String = '时间 (s)';
ax.YLabel.String = '速度';
grid(ax, 'on'); hold(ax, 'on');

hV    = animatedline(ax, 'Color', [0.16 0.42 0.86], 'LineWidth', 1.6);
hVdes = animatedline(ax, 'Color', [0.92 0.36 0.16], 'LineStyle', '--', 'LineWidth', 1.3);
hVmod = animatedline(ax, 'Color', [0.20 0.62 0.30], 'LineStyle', '-.', 'LineWidth', 1.3);
% 图例挪到东北角：原来放西北角会压住曲线起点的上升段
legend(ax, {'实体速度','MATLAB 期望','孪生模型'}, 'Location', 'northeast', 'FontSize', 9);

% ---------- 右列：数据明细 ----------
pnlDetail = uipanel(fig, 'Title', '数据明细', 'Position', [784 240 380 216], ...
                    'FontSize', 13, 'FontWeight', 'bold', 'BackgroundColor', [1 1 1]);
lblMsg = uilabel(pnlDetail, 'Text', {'等待 Unity 数据…'}, ...
        'Position', [12 10 356 178], 'FontSize', 12, ...
        'VerticalAlignment', 'top', 'WordWrap', 'on', 'FontColor', [0.22 0.25 0.31]);

% ---------- 第四列：车载摄像头（实时路况 + 偏离告警）----------
% 这一列是新加的，所以窗口宽度也跟着加宽了（见上面 FIG_W）。
% 面板高度占满右列：从 y=240 到 y=712。
camPanel = [];
if ENABLE_VIDEO
    camPanel = CameraPanel(fig, [1180 240 380 472]);
    camPanel.setLimits(DEV_DIST_LIMIT, DEV_ANG_LIMIT);
end

% ---------- 底部：控制台 ----------
% ★ 宽度要跟着窗口走：原来是写死的 1148（窗口 1180 减左右边距 32），
%   窗口加宽后如果不改，控制台右边会空出一大块。
%   干脆用 FIG_W 算，以后改窗口宽度就不用再回来动这里。
pnl = uipanel(fig, 'Title', '控制台', 'Position', [16 16 FIG_W-32 148], 'FontSize', 13);

% 第 1 组：运行模式
uilabel(pnl, 'Text', '运行模式', 'Position', [18 106 120 22], 'FontSize', 12, ...
        'FontWeight', 'bold');
ddMode = uidropdown(pnl, 'Items', {'MATLAB 自动','MATLAB 手动','Unity 本地'}, ...
        'Value', 'MATLAB 自动', 'Position', [18 68 190 30]);

% 第 2 组：目标速度滑块
uilabel(pnl, 'Text', '目标速度 (m/s)', 'Position', [232 106 200 22], 'FontSize', 12, ...
        'FontWeight', 'bold');
sldV = uislider(pnl, 'Limits', [0 8], 'Value', V_MAX, 'MajorTicks', 0:2:8, ...
        'Position', [232 84 280 3]);
lblV = uilabel(pnl, 'Text', sprintf('%.1f', V_MAX), 'Position', [524 74 60 28], ...
        'FontSize', 15, 'FontWeight', 'bold');

% 第 3 组：手动转向滑块
uilabel(pnl, 'Text', '手动转向（负 = 左，正 = 右）', 'Position', [232 46 280 22], 'FontSize', 12, ...
        'FontWeight', 'bold');
sldS = uislider(pnl, 'Limits', [-1 1], 'Value', 0, 'MajorTicks', -1:0.5:1, ...
        'Position', [232 24 280 3]);
lblS = uilabel(pnl, 'Text', '0.00', 'Position', [524 14 60 28], ...
        'FontSize', 15, 'FontWeight', 'bold');

% 第 4 组：两个开关（分开摆，各自带标签，不再挤在一起）
% 【R2025a 实测】uiswitch 的 Value 不是 true/false，而是 Items 里的某个字符串。
%   直接写 sw.Value = true 会报「'Value' 必须为 'Items' 中的某个元素」。
uilabel(pnl, 'Text', '强制刹车', 'Position', [620 106 80 22], 'FontSize', 12, ...
        'FontWeight', 'bold');
swBrk = uiswitch(pnl, 'slider', 'Position', [620 66 50 22]);
swBrk.Items = {'关', '开'};
swBrk.Value = '关';

uilabel(pnl, 'Text', '急停', 'Position', [620 46 80 22], 'FontSize', 12, ...
        'FontWeight', 'bold', 'FontColor', [0.85 0.25 0.18]);
swStop = uiswitch(pnl, 'toggle', 'Position', [620 16 60 26]);
swStop.Items = {'关', '开'};
swStop.Value = '关';

% 第 5 组：收发统计
uilabel(pnl, 'Text', '通信统计', 'Position', [740 106 200 22], 'FontSize', 12, ...
        'FontWeight', 'bold');
lblSend = uilabel(pnl, 'Text', '已下发 0 条指令', 'Position', [740 74 380 26], 'FontSize', 12.5);
lblStat = uilabel(pnl, 'Text', '收包 0 条', 'Position', [740 44 380 26], ...
        'FontSize', 12.5, 'FontColor', [0.45 0.48 0.53]);
lblRate = uilabel(pnl, 'Text', '速率 --', 'Position', [740 14 380 26], ...
        'FontSize', 12.5, 'FontColor', [0.45 0.48 0.53]);

% 滑块实时显示数字
sldV.ValueChangedFcn = @(s,e) set(lblV, 'Text', sprintf('%.1f', s.Value));
sldS.ValueChangedFcn = @(s,e) set(lblS, 'Text', sprintf('%.2f', s.Value));

%% ===== 4. 主循环 =====
t0 = tic;
seq = 0; count = 0;
s_actual = 0; v_model = 0; s_model = 0;
% t_prev 先留空：它是"位置差分"的基准，每轮重连都必须清掉，
% 否则拿上一轮末尾的位置减这一轮开头的位置，会算出几百米的假里程。
t_prev = []; pos_prev = [];
v_des = 0; brake = false; steer_des = 999;

% ---- 常驻/重连相关 ----
last_rx   = NaN;   % 最后一次收到包的时刻（toc 值）；NaN = 还没收到过
sessCount = 0;     % 这是第几轮连接（Unity 每 Play 一次算一轮）
connected = false; % 当前是否处于"已连上"状态
nWaiting  = 0;     % 累计"没人发包"的循环次数，用来控制界面提示刷新频率
prevCount = 0;     % 上一秒的收包数，用来算**瞬时**速率（不是累计平均）
rateT     = 0;     % 上次刷新速率的时刻（MATLAB 变量名不能以下划线开头，所以叫 rateT）
v_model_sess = 0;  % 本轮开始时的孪生转速（重连后从当前车速接上去，不跳变）

% ---- 摄像头 / 偏离判定相关 ----
nWpVid    = size(WP_POINTS, 1);   % 航点个数
vidFramesDrawn = 0;               % 画过多少帧（拿来算显示帧率）
vidFpsT   = 0;                    % 上次算显示帧率的时刻
vidFps    = 0;                    % 显示帧率（显示在面板提示里）
lastDevInfo = '路径跟踪正常';      % 上一次的偏离说明
lastDevState = false;             % 上一次的偏离状态（用来判断"状态变了没"，变了才打日志）

% 预分配日志数组（循环里用 end+1 增长数组每次都要重新分配内存，很慢，MATLAB 也会警告）
MAXLOG = 50000;
log_t    = zeros(MAXLOG,1);  log_v    = zeros(MAXLOG,1);
log_vdes = zeros(MAXLOG,1);  log_vmod = zeros(MAXLOG,1);
log_d    = zeros(MAXLOG,1);  log_wp   = zeros(MAXLOG,1);
nlog = 0;

stateNames = {'Driving','Blocked','Finished'};

while toc(t0) < RUN_SECONDS && isvalid(fig)

    % ★ 摄像头画面和状态包是**两条独立的流**，走不同端口。
    %   所以收画面这件事必须放在 `if 有状态包` 之外 ——
    %   否则 Unity 一停发状态（比如按了暂停），画面就跟着冻住了。
    camImg = [];
    if ~isempty(videoRx)
        camImg = videoRx.Poll();     % 没新帧就返回 []，不是错误
    end

    n = u.NumDatagramsAvailable;

    if n > 0
        % ★ 一圈把积压的状态包**全收掉**，只把最后一个拿去刷界面。
        %   原来这里一圈只 read 一个包，后果是：
        %     Unity 发 20 Hz，而主循环因为要刷界面/解图只能跑到 12~15 Hz，
        %     收包速率就被循环速率卡住了 —— 界面看到的永远是旧数据，
        %     而且这些没读的包会一直堆在队列里，越堆越滞后。
        %   全收掉之后，界面反映的永远是"最新一刻"的状态。
        %   上限 50 是防呆：万一 Unity 疯狂刷包，也别让这一圈卡太久。
        nTake = min(n, 50);
        st = [];
        for kRead = 1:nTake
            d = read(u, 1, "uint8");
            if isempty(d), break; end
            raw = uint8(d(1).Data);
            s   = string(char(raw(:)'));
            try
                cand = jsondecode(s);
            catch
                continue;                   % 半包/脏数据，跳过别崩
            end
            st = cand;                      % 只留最后一个，前面的直接丢掉
        end

        if isempty(st)
            continue;                       % 这一圈全是脏包，跳过
        end

        count = count + 1;

        %% --- 4.0 新会话检测（常驻模式的核心）---
        % 情形 A：第一次收包            → 开一轮
        % 情形 B：隔了 RECONNECT_GAP 秒以上才又来包 → 说明 Unity 中途停过 Play，开新一轮
        %
        % ★ 这里有个坑，专门防一下：**启动后的头几秒不能算"断开"**。
        %   原因是第一帧 JPEG 解码要走一次冷启动（imread 首次调用要加载编解码器），
        %   实测能卡住 1~3 秒。这几秒里主循环根本没在 read，包全积在队列里，
        %   等它回来时 `toc - last_rx` 就超了 3 秒 → 被误判成"Unity 重连"，
        %   日志里莫名其妙多出"第 2 轮"，曲线也被白切一刀。
        %   办法：只有在**真正收到过一段数据之后**（startupGrace 之后）才允许判断开。
        STARTUP_GRACE = 8;      % 秒。启动后这段时间内不判"断开"
        firstEver     = isnan(last_rx);
        oldEnough     = toc(t0) > STARTUP_GRACE;
        gap           = 0;
        if ~firstEver, gap = toc(t0) - last_rx; end

        if firstEver || (gap > RECONNECT_GAP && oldEnough)
            sessCount = sessCount + 1;
            % ★ 位置差分基准要连 pos_prev 一起清！
            %   只清 t_prev 的话，重连后仍是拿「停 Play 前那个位置」和「新 Play 的当前位置」
            %   相减 —— 会把中间静止的那几秒也算成里程，凭空多出好几米。
            t_prev   = [];
            pos_prev = [];
            v_model  = v_model_sess;   % 孪生转速接着上次，别跳回 0
            clearpoints(hV);  clearpoints(hVdes);  clearpoints(hVmod);   % 曲线另起一段
            if firstEver
                fprintf("[第 %d 轮] 收到 Unity 数据，链路已建立\n", sessCount);
            else
                fprintf("[第 %d 轮] 数据中断 %.1f 秒后恢复，判定为 Unity 重新 Play\n", ...
                        sessCount, gap);
            end
            % 重连时把视频拼接状态和积压包也清掉：
            % 不清的话，队列里上一轮的残片会跟新一轮的帧号混在一起
            if ~isempty(videoRx)
                videoRx.Flush();
            end
        end
        last_rx   = toc(t0);
        connected = true;

        %% --- 4.1 实体走了多远（位置差分，不依赖 Unity 的 speed 字段）---
        if isempty(pos_prev)
            ds = 0; dt = 0;
        else
            dt = st.t - t_prev;
            ds = hypot(st.x - pos_prev(1), st.z - pos_prev(2));
        end
        pos_prev = [st.x, st.z];
        t_prev   = st.t;

        % ★ dt 必须夹住上限。
        %   孪生模型是"一阶惯性"：v_model += (vRef - v_model) * dt / TAU。
        %   只要 dt 比 TAU（1.2 秒）大几倍，这一步就会**过冲** ——
        %   实测主循环卡一下、dt 变成 4 秒时，v_model 直接冲到 39 m/s，
        %   然后开始在正负之间来回振荡，曲线彻底失控。
        %   MATLAB 主循环偶尔卡一下是常态（窗口重绘、GC、解码冷启动），
        %   所以这里把 dt 限制在 0.5 秒以内：宁可让模型走慢一点，
        %   也不要让它一步跨过头。
        DT_MAX = 0.5;
        if dt > DT_MAX
            dt = DT_MAX;
        end

        s_actual = s_actual + ds;

        %% --- 4.2 按模式决策：给多快的速度、要不要刹车 ---
        mode = ddMode.Value;

        % uiswitch 的 Value 是字符串（'关' / '开'），不是 true/false
        brakeSwitch = strcmp(swBrk.Value,  '开');
        stopSwitch  = strcmp(swStop.Value, '开');

        if stopSwitch                            % 急停优先级最高
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
            brake = brakeSwitch;

        else                                     % Unity 本地
            v_des = -1;  brake = false;           % -1 = 不接管速度
        end

        % 手动刹车开关：只要不是"Unity 本地"模式，拨了就刹
        if ~strcmp(mode, 'Unity 本地') && brakeSwitch
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
        lblStat.Text  = sprintf('收包 %d 条   第 %d 轮连接', count, sessCount);

        %% --- 4.6b 偏离判定（驱动摄像头面板变红）---
        % 用 Unity 报上来的 (x, z, yaw, wp) 判断车现在有没有压线/歪头。
        % 判定逻辑在 check_deviation.m 里，这里只负责把结果变成界面状态。
        hasYaw = isfield(st, 'yaw');
        if hasYaw
            try
                [devNow, devDist, devAng, devInfo] = check_deviation( ...
                    st.x, st.z, st.yaw, st.wp, WP_POINTS, nWpVid, ...
                    DEV_DIST_LIMIT, DEV_ANG_LIMIT);
            catch ME
                % 判定失败不该拖垮仪表盘：退回"正常"，并把原因写到状态条
                devNow = false;  devDist = 0;  devAng = 0;
                devInfo = sprintf('偏离判定失败：%s', ME.message);
            end
            % ★ 先比后赋值 —— 顺序反了的话，下面那句"翻转时打日志"永远不成立
            %   （lastDevState 已经等于 devNow 了，比出来永远是"没变"）。
            if devNow && ~lastDevState
                fprintf("[偏离] t=%.1fs  %s  (x=%.1f, z=%.1f, yaw=%.0f°)\n", ...
                        st.t, devInfo, st.x, st.z, st.yaw);
            end

            lastDevInfo  = devInfo;
            lastDevState = devNow;
        end

        % 收包速率：用"最近 1 秒新增多少包"算，才是真实速率。
        % （原来用 累计包数/总秒数，一旦断开重连，这个数会被历史拖住，看不出当前状态）
        elapsed = toc(t0);
        if elapsed - rateT > 1
            lblRate.Text = sprintf('速率 %.1f 包/秒   已运行 %.0f 秒', ...
                                   (count - prevCount) / (elapsed - rateT), elapsed);
            prevCount = count;
            rateT     = elapsed;
        end

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
        nWaiting = nWaiting + 1;

        % 没数据时的界面提示（每约 0.5 秒刷一次，别每帧都刷）
        if mod(nWaiting, 100) == 0
            waited = toc(t0);

            if isnan(last_rx)
                %% 从头到尾一个包都没收到
                if waited > 6
                    lblCtrlBig.Text = {
                        sprintf('⚠ 已等 %.0f 秒，仍未收到 Unity 任何数据', waited)
                        '请检查：① Unity 是否已按 Play（不是暂停）'
                        '        ② 车的 Matlab Bridge 是否勾了 Enable Network'
                        '        ③ Unity 的 Console 里有没有「已发 N 包」在涨'
                    };
                    lblCtrlBig.FontColor = [0.85 0.30 0.20];
                    lampConn.Color = [0.85 0.30 0.20];
                    lblConn.Text   = '未收到数据';
                    lblClock.Text  = sprintf('等待 Unity… %.0f 秒', waited);
                end

            elseif (waited - last_rx) > RECONNECT_GAP && connected
                %% ★ 曾经连上过，现在断了 —— 切回等待态，但累计量保留
                connected = false;
                lampConn.Color = [0.93 0.72 0.20];
                lblConn.Text   = 'Unity 已断开';
                lampCtrl.Color = GREY;
                lblCtrl.Text   = '待连接';
                lblCtrlBig.Text = sprintf('Unity 已停止（第 %d 轮已结束），仍在监听 %d 端口…', ...
                                          sessCount, MATLAB_LISTEN_PORT);
                lblCtrlBig.FontColor = [0.75 0.55 0.10];
                fprintf("[第 %d 轮] Unity 已断开，回到等待状态（累计 %d 包，数据保留）\n", ...
                        sessCount, count);
                v_model_sess = v_model;      % 记下当前孪生转速，下一轮从这儿接着算
            end

            % 等待中也要让时钟走，不然看起来像死了
            if ~connected
                lblClock.Text = sprintf('等待 Unity…（已 %d 轮）', sessCount);
            end
        end
    end

    %% --- 4.9 刷新摄像头面板 ---
    % ★ 放在循环末尾、两种分支之外 —— 不管这轮有没有状态包，画面都要刷。
    %   要是塞进 `if n > 0` 里，Unity 一不发状态包画面就冻住，看着像卡死。
    if ~isempty(camPanel)
        % 状态条的措辞要区分"没画面"和"有画面但没偏离"
        if ~isempty(camImg)
            vidFramesDrawn = vidFramesDrawn + 1;
            camPanel.Update(camImg, lastDevState, lastDevInfo);
        elseif camPanel.FramesShown == 0
            % 一次都没收到过画面
            if ~isempty(videoRx) && videoRx.FramesBad > 0
                camPanel.SetStatus(sprintf('画面丢帧 %d 次（检查 Unity 的 CameraStreamer）', ...
                                    videoRx.FramesBad), [0.80 0.45 0.10]);
            else
                camPanel.SetStatus('等待画面…（Unity 侧 Camera Streamer 勾了吗？）', ...
                                   [0.50 0.53 0.58]);
            end
        else
            % 有画面，但这轮没新帧 —— 只更新偏离状态，不重传图像
            camPanel.Update([], lastDevState, lastDevInfo);
        end
    end

    % 画面显示帧率：拿"最近这段时间画了多少帧"算。
    % 和 Unity 的发送帧率一比，就知道 MATLAB 有没有消费不过来
    % （跟不上就会表现为画面越来越滞后）。
    if ~isempty(camPanel) && camPanel.FramesShown > 0
        elapsedNow = toc(t0);
        if elapsedNow - vidFpsT > 2
            vidFps         = vidFramesDrawn / max(0.001, elapsedNow - vidFpsT);
            vidFramesDrawn = 0;
            vidFpsT        = elapsedNow;

            % 顺便把丢帧数写上去：丢帧一直涨说明网络或 MATLAB 跟不上
            if ~isempty(videoRx) && videoRx.FramesBad > 0
                rxInfo = sprintf('   ·   丢帧 %d', videoRx.FramesBad);
            else
                rxInfo = '';
            end
            camPanel.SetFps(vidFps, rxInfo);
        end
    end
end

%% ===== 5. 收尾：存 CSV + 打印统计 =====
% 常驻模式（RUN_SECONDS = Inf）下，走到这里只有一种可能：**你关掉了窗口**。
% 所以这里可以放心当作"实验结束"，存盘 + 打统计。
clear u;                       % 关掉状态端口 5005

% ★ 视频统计要在 delete 之前取出来（对象删了就读不到了）
if ~isempty(videoRx)
    camOk  = videoRx.FramesOk;
    camBad = videoRx.FramesBad;
    camBytes = videoRx.BytesRx;
    camSkip  = videoRx.Skipped;
    delete(videoRx);           % ★ 视频端口 5007 也必须一起关，
                               %   不关的话下次重跑会报"端口被占用"，
                               %   而且只能重启 MATLAB 才能解开。
    fprintf("\n摄像头：解码 %d 帧，丢弃 %d 帧，共收 %.1f MB\n", ...
            camOk, camBad, camBytes / 1048576);
    if camOk + camBad > 0
        fprintf("        丢帧率 %.1f%%（>5%% 就该调低 Unity 的分辨率或帧率）\n", ...
                100 * camBad / (camOk + camBad));
    end
    if camSkip > 0
        fprintf("        为避免卡主循环，跳过了 %d 帧没解码（正常，不用管）\n", camSkip);
    end
end
clear videoRx camPanel;

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
    fprintf("共 %d 轮连接，收到 %d 包，已保存 %s\n", sessCount, count, fname);
    fprintf("实体平均速度 %.2f m/s | 孪生模型平均速度 %.2f m/s\n", mean(lg_v), mean(lg_vm));
    fprintf("速度偏差：均值 %.3f，RMS %.3f，最大 %.3f m/s\n", ...
        mean(err), sqrt(mean(err.^2)), max(abs(err)));
    fprintf("实体累计里程 %.2f m | 孪生里程 %.2f m\n", s_actual, s_model);
    fprintf("提示：偏差 RMS 越小，说明 TAU 这个模型参数标定得越准。\n");
    fprintf("注：曲线在每次重连时会另起一段，但 CSV 里的 t 是连续的，画图不受影响。\n");
else
    fprintf("\n一个包都没收到。按顺序检查：\n");
    fprintf("  1. Unity 按 Play 了吗？\n");
    fprintf("  2. 车的 Inspector 上 MatlabBridge 的 Enable Network 勾了吗？\n");
    fprintf("  3. 端口被占？cmd 里跑：netstat -ano | findstr 5005\n");
end
