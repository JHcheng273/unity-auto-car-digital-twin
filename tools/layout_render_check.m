function layout_render_check()
%LAYOUT_RENDER_CHECK 真实起一次 uifigure，把布局截图存盘，人工核对。
%
% 【为什么不能只看算式】
%   算式对了不代表看着对 —— 字体宽度、面板标题栏占位、uigauge 内部留白
%   这些都是"算不出来"的。唯一可信的证据是**真渲染一张图看一眼**。
%
% 用法（在 tools/ 目录下）：
%   matlab -batch layout_render_check

outPng = fullfile(fileparts(mfilename('fullpath')), 'layout_render_check.png');

% ★ 把 matlab/ 加进搜索路径 —— 脚本可能是从 tools/ 目录跑的，
%   那时 CameraPanel / VideoReceiver / check_deviation 都找不到。
%   用 mfilename('fullpath') 反推，不写死绝对路径（换机器也能跑）。
thisDir = fileparts(mfilename('fullpath'));
mlDir   = fullfile(thisDir, '..', 'matlab');
addpath(mlDir);

% ---- 直接复用仪表盘的布局段，但把主循环跳过 ----
% 做法：把 unity_dashboard.m 读进来，截到"===== 4. 主循环 =====" 之前，
%       然后假造几个数据把界面填满，再截图。
src = fileread(fullfile(fileparts(mfilename('fullpath')), '..', 'matlab', 'unity_dashboard.m'));
marker = '%% ===== 4. 主循环 =====';
idx = strfind(src, marker);
if isempty(idx), error('找不到主循环标记'); end
head = src(1:idx(1)-1);
head = strrep(head, 'clear; clc; close all;', '');   % 别把当前环境清了

% ★ 布局验证**不需要真的开端口** —— 开了反而会和正在运行的仪表盘抢 5005，
%   或者被上次 Ctrl+C 留下的僵尸端口卡住。
%   做法：把建 UDP 的那两段整段替换成"什么都不做"。
%   （从 "%% ===== 2. 建 UDP" 到 "%% ===== 3. 建仪表盘" 之间全丢掉）
i2 = strfind(head, '%% ===== 2. 建 UDP');
i3 = strfind(head, '%% ===== 3. 建仪表盘');
if ~isempty(i2) && ~isempty(i3) && i3(1) > i2(1)
    head = [head(1:i2(1)-1), ...
            'u = []; videoRx = [];   % 布局验证：不建 UDP，不占端口', newline, ...
            head(i3(1):end)];
    % ENABLE_VIDEO 保持 true，这样摄像头面板照样建出来（我们要验的就是它）
end

eval(head);   % 这一步会把图窗、所有控件、变量都建出来

% ---- 灌一批假数据，把文字/表盘填满（这样才能看出有没有重叠/截断）----
gSpeed.Value = 32;  lblSpeed.Text = '32.0';
gThr.Value = 45;  gBrk.Value = 0;  gStr.Value = -18;

lblMile.Text  = sprintf('里程  %.1f m', 138.6);
lblLap.Text   = sprintf('圈数  %d', 1);
lblWp.Text    = sprintf('目标点  WP%d', 2);
lblPos.Text   = sprintf('位置  (%.1f, %.1f)', -1.5, 6.9);
lblState.Text = '状态机  Driving';

lampConn.Color = [0.20 0.75 0.35];  lblConn.Text = '已连接 · 已授权';
lampCtrl.Color = [0.20 0.60 0.92];  lblCtrl.Text = 'MATLAB 接管';
lampBrk.Color  = GREY;              lblBrk.Text  = '刹车中';
lampObs.Color  = [0.95 0.45 0.15];  lblObs.Text  = '前方障碍';

lblCtrlBig.Text = '控制来源：MATLAB · MATLAB 自动';
lblCtrlBig.FontColor = [0.10 0.42 0.80];

lblMsg.Text = {
    '运行模式    MATLAB 自动'
    '期望速度    4.50 m/s'
    '实际速度    3.16 m/s  (11.4 km/h)'
    '油门/刹车   45%  /  false'
    '前方障碍    有！ 3.7 m'
    '孪生模型    3.31 m/s'
    '实体里程    138.6 m      孪生里程  135.2 m'
    '收包 / 下发 875  /  875'
};
lblClock.Text = '10:42:07';
lblSend.Text  = '已下发 875 条指令';
lblStat.Text  = '收包 875 条   第 1 轮连接';
lblRate.Text  = '速率 20.0 包/秒   已运行 44 秒';

% ---- 摄像头面板：塞一张假画面 + 偏离状态 ----
% ★ 用 Unity 那边真实的尺寸 240×180，这样能看出宽高比对不对。
if ~isempty(camPanel)
    [X, Y] = meshgrid(1:240, 1:180);
    fake = uint8(zeros(180, 240, 3));
    fake(:,:,1) = uint8(60 + 120 * X / 240);          % 横向渐变（红）
    fake(:,:,2) = uint8(40 + 150 * Y / 180);          % 纵向渐变（绿）
    fake(1:40, 1:40, :) = 255;                        % 左上白方块
    fake(60:90, 180:230, 1) = 235;                    % 右下品红块
    fake(60:90, 180:230, 3) = 235;
    fake(120:170, 60:150, :) = 30;                    % 中下深色块
    camPanel.Update(fake, true, '偏离！横向 3.2 m + 夹角 42°');
    camPanel.SetFps(11.6, '');
end

% ---- 曲线填点 ----
t = (0:0.05:20)';
vv = 4.5 * (1 - exp(-t/1.2));
vd = 4.5 * ones(size(t));
vm = 4.5 * (1 - exp(-t/1.35));
addpoints(hV, t, vv);  addpoints(hVdes, t, vd);  addpoints(hVmod, t, vm);

drawnow; pause(1.0); drawnow;      % 等窗口画完

% ---- 截图 ----
% ★ exportapp 是 R2020b 起可用的"整窗截图"，比自己 getframe 靠谱
%   （getframe 抓不到 uifigure 里的控件）。
try
    exportapp(fig, outPng);
    fprintf('OK 截图已保存：%s\n', outPng);
catch ME
    fprintf('exportapp 失败（%s），改用 getframe\n', ME.message);
    fr = getframe(fig); imwrite(fr.cdata, outPng);
    fprintf('OK 截图已保存：%s\n', outPng);
end

info = imfinfo(outPng);
fprintf('尺寸 %d x %d\n', info(1).Width, info(1).Height);

close(fig);
end
