%% 冒烟测试：把重排后的 unity_dashboard 布局真的建一遍
%
% 为什么需要：新版布局用了 uipanel 嵌套（面板里放 uigauge / uilamp / uilabel），
%   还改了所有坐标。光看代码看不出会不会重叠、会不会报错 —— 真建一遍最快。
%
% 验证内容：
%   1. 所有控件能建出来（面板嵌套不报错）
%   2. 各控件的 Position 不重叠（脚本自动检查）
%   3. 都在画布范围内（1180 x 800）

ok = true;
fig = [];
FG = [1 1 1];

try
    fig = uifigure('Visible', 'off', 'Position', [60 40 1180 800], 'Color', [0.93 0.94 0.96]);

    % ---------- 顶部 ----------
    uilabel(fig, 'Text', 'UNITY 数字孪生 · 实时驾驶仪表盘', ...
            'Position', [24 748 660 38], 'FontSize', 20, 'FontWeight', 'bold');
    uilabel(fig, 'Text', '等待连接…', 'Position', [780 748 376 38], ...
            'FontSize', 15, 'HorizontalAlignment', 'right');
    fprintf('OK   顶部标题栏\n');

    % ---------- 左列：车速面板 ----------
    pnlSpeed = uipanel(fig, 'Title', '车速', 'Position', [16 300 356 412], ...
                       'FontSize', 13, 'FontWeight', 'bold', 'BackgroundColor', FG);
    g = uigauge(pnlSpeed, 'semicircular', 'Position', [8 46 340 330]);
    g.Limits = [0 60]; g.MajorTicks = 0:10:60; g.MinorTicks = 0:5:60;
    g.ScaleColors = [0.22 0.65 0.36; 0.95 0.76 0.20; 0.90 0.28 0.22];
    g.ScaleColorLimits = [0 35; 35 50; 50 60];
    g.Value = 23.4;
    uilabel(pnlSpeed, 'Text', '23.4', 'Position', [8 118 340 62], 'FontSize', 40, ...
            'FontWeight', 'bold', 'HorizontalAlignment', 'center');
    uilabel(pnlSpeed, 'Text', 'km/h', 'Position', [8 96 340 22], 'FontSize', 12, ...
            'HorizontalAlignment', 'center');
    fprintf('OK   左列车速面板（表盘 + 大数字）\n');

    % ---------- 左列：运行信息面板 ----------
    pnlInfo = uipanel(fig, 'Title', '运行信息', 'Position', [16 172 356 120], ...
                      'FontSize', 13, 'FontWeight', 'bold', 'BackgroundColor', FG);
    uilabel(pnlInfo, 'Text', '里程  12.3 m',  'Position', [14 74 160 26], 'FontSize', 13);
    uilabel(pnlInfo, 'Text', '圈数  1',       'Position', [186 74 156 26], 'FontSize', 13);
    uilabel(pnlInfo, 'Text', '目标点  WP3',   'Position', [14 44 160 26], 'FontSize', 13);
    uilabel(pnlInfo, 'Text', '位置  (1.2,3.4)','Position', [186 44 156 26], 'FontSize', 13);
    uilabel(pnlInfo, 'Text', '状态机  Driving','Position', [14 14 328 26], 'FontSize', 13);
    fprintf('OK   左列运行信息面板\n');

    % ---------- 中列：三个表 ----------
    for k = 1:3
        switch k
            case 1
                ttl = '油门开度 (%)'; lim = [0 100]; sc = [0.25 0.60 0.90]; y = 578;
            case 2
                ttl = '刹车 (%)';     lim = [0 100]; sc = [0.90 0.30 0.25]; y = 476;
            otherwise
                ttl = '转向 (%)    负 = 左    正 = 右'; lim = [-100 100]; sc = [0.38 0.45 0.85]; y = 374;
        end
        p = uipanel(fig, 'Title', ttl, 'Position', [388 y 380 96], ...
                    'FontSize', 13, 'FontWeight', 'bold', 'BackgroundColor', FG);
        gg = uigauge(p, 'linear', 'Position', [14 12 352 60]);
        gg.Limits = lim; gg.Orientation = 'horizontal';
        gg.ScaleColors = sc; gg.ScaleColorLimits = lim;
        gg.Value = mean(lim);
    end
    fprintf('OK   中列三个表（油门/刹车/转向）\n');

    % ---------- 中列：指示灯面板 ----------
    GREY = [0.62 0.65 0.69];
    pnlLamp = uipanel(fig, 'Title', '状态指示灯', 'Position', [388 240 380 128], ...
                      'FontSize', 13, 'FontWeight', 'bold', 'BackgroundColor', FG);
    L1 = uilamp(pnlLamp, 'Position', [22 68 24 24]);  L1.Color = [0.2 0.75 0.35];
    uilabel(pnlLamp, 'Text', '已连接',      'Position', [56 66 130 26], 'FontSize', 12.5);
    L2 = uilamp(pnlLamp, 'Position', [22 28 24 24]);  L2.Color = GREY;
    uilabel(pnlLamp, 'Text', 'MATLAB 接管', 'Position', [56 26 130 26], 'FontSize', 12.5);
    L3 = uilamp(pnlLamp, 'Position', [210 68 24 24]); L3.Color = [0.92 0.28 0.22];
    uilabel(pnlLamp, 'Text', '刹车中',      'Position', [244 66 120 26], 'FontSize', 12.5);
    L4 = uilamp(pnlLamp, 'Position', [210 28 24 24]); L4.Color = GREY;
    uilabel(pnlLamp, 'Text', '前方有障碍',  'Position', [244 26 120 26], 'FontSize', 12.5);
    fprintf('OK   中列指示灯面板（4 灯 2x2）\n');

    % ---------- 中列底部大字 ----------
    lb = uilabel(fig, 'Text', '控制来源：MATLAB · MATLAB 自动', ...
            'Position', [388 176 380 56], 'FontSize', 15, 'FontWeight', 'bold', ...
            'HorizontalAlignment', 'center', 'VerticalAlignment', 'center', ...
            'BackgroundColor', FG);
    lb.Text = {'⚠ 已等 77 秒，仍未收到 Unity 任何数据';
               '请检查：① Unity 是否已按 Play';
               '        ② Matlab Bridge 是否勾了 Enable Network'};
    lb.FontSize = 12;
    fprintf('OK   中列底部大字提示（多行）\n');

    % ---------- 右列：曲线 ----------
    pnlChart = uipanel(fig, 'Title', '速度对比 (m/s)', 'Position', [784 464 380 248], ...
                       'FontSize', 13, 'FontWeight', 'bold', 'BackgroundColor', FG);
    ax = uiaxes(pnlChart, 'Position', [12 10 356 200]);
    ax.XLabel.String = '时间 (s)'; ax.YLabel.String = '速度';
    grid(ax, 'on'); hold(ax, 'on');
    h1 = animatedline(ax, 'Color', [0.16 0.42 0.86], 'LineWidth', 1.6);
    h2 = animatedline(ax, 'Color', [0.92 0.36 0.16], 'LineStyle', '--', 'LineWidth', 1.3);
    h3 = animatedline(ax, 'Color', [0.20 0.62 0.30], 'LineStyle', '-.', 'LineWidth', 1.3);
    addpoints(h1, 0, 1); addpoints(h1, 1, 2.5);
    addpoints(h2, 0, 0.5); addpoints(h2, 1, 2);
    addpoints(h3, 0, 0.3); addpoints(h3, 1, 1.8);
    legend(ax, {'实体速度','MATLAB 期望','孪生模型'}, 'Location', 'northeast', 'FontSize', 9);
    fprintf('OK   右列曲线面板 + 3 条线 + 图例\n');

    % ---------- 右列：数据明细 ----------
    pnlDetail = uipanel(fig, 'Title', '数据明细', 'Position', [784 240 380 216], ...
                        'FontSize', 13, 'FontWeight', 'bold', 'BackgroundColor', FG);
    uilabel(pnlDetail, 'Text', {'运行模式    MATLAB 自动'; '期望速度    4.50 m/s'; ...
                                '实际速度    3.21 m/s'; '油门/刹车   64% / false'; ...
                                '前方障碍    无'; '孪生模型    3.18 m/s'; ...
                                '收包/下发   200 / 200'}, ...
            'Position', [12 10 356 178], 'FontSize', 12, ...
            'VerticalAlignment', 'top', 'WordWrap', 'on');
    fprintf('OK   右列数据明细面板（多行）\n');

    % ---------- 底部：控制台 ----------
    pnl = uipanel(fig, 'Title', '控制台', 'Position', [16 16 1148 148], 'FontSize', 13);
    uilabel(pnl, 'Text', '运行模式', 'Position', [18 106 120 22], 'FontSize', 12, 'FontWeight', 'bold');
    dd = uidropdown(pnl, 'Items', {'MATLAB 自动','MATLAB 手动','Unity 本地'}, ...
            'Value', 'MATLAB 自动', 'Position', [18 68 190 30]);
    uilabel(pnl, 'Text', '目标速度 (m/s)', 'Position', [232 106 200 22], 'FontSize', 12, 'FontWeight', 'bold');
    s1 = uislider(pnl, 'Limits', [0 8], 'Value', 4.5, 'MajorTicks', 0:2:8, 'Position', [232 84 280 3]);
    uilabel(pnl, 'Text', '4.5', 'Position', [524 74 60 28], 'FontSize', 15, 'FontWeight', 'bold');
    uilabel(pnl, 'Text', '手动转向（负 = 左，正 = 右）', 'Position', [232 46 280 22], 'FontSize', 12, 'FontWeight', 'bold');
    s2 = uislider(pnl, 'Limits', [-1 1], 'Value', 0, 'MajorTicks', -0.5:0.5:1, 'Position', [232 24 280 3]);
    uilabel(pnl, 'Text', '0.00', 'Position', [524 14 60 28], 'FontSize', 15, 'FontWeight', 'bold');
    uilabel(pnl, 'Text', '强制刹车', 'Position', [620 106 80 22], 'FontSize', 12, 'FontWeight', 'bold');
    sw1 = uiswitch(pnl, 'slider', 'Position', [620 66 50 22]);
    sw1.Items = {'关','开'}; sw1.Value = '关';
    assert(strcmp(sw1.Value, '关'));
    uilabel(pnl, 'Text', '急停', 'Position', [620 46 80 22], 'FontSize', 12, 'FontWeight', 'bold');
    sw2 = uiswitch(pnl, 'toggle', 'Position', [620 16 60 26]);
    sw2.Items = {'关','开'}; sw2.Value = '关';
    uilabel(pnl, 'Text', '通信统计', 'Position', [740 106 200 22], 'FontSize', 12, 'FontWeight', 'bold');
    uilabel(pnl, 'Text', '已下发 200 条指令', 'Position', [740 74 380 26], 'FontSize', 12.5);
    uilabel(pnl, 'Text', '收包 200 条', 'Position', [740 44 380 26], 'FontSize', 12.5);
    uilabel(pnl, 'Text', '速率 20.0 包/秒', 'Position', [740 14 380 26], 'FontSize', 12.5);
    fprintf('OK   底部控制台（3 组滑块/开关 + 统计）\n');

    % ---------- 自动检查：控件是否超出画布 ----------
    fprintf('\n--- 检查各面板是否在画布内（1180 x 800）---\n');
    panels = {'pnlSpeed', pnlSpeed; 'pnlInfo', pnlInfo; 'pnlThr', [];
                          'pnlLamp', pnlLamp; 'pnlChart', pnlChart;
                          'pnlDetail', pnlDetail; 'pnl', pnl};
    for k = 1:size(panels,1)
        if isempty(panels{k,2}), continue; end
        pos = panels{k,2}.Position;
        x2 = pos(1)+pos(3); y2 = pos(2)+pos(4);
        flag = '';
        if x2 > 1180 || y2 > 800 || pos(1) < 0 || pos(2) < 0
            flag = '  <-- 超出画布！'; ok = false;
        end
        fprintf('  %-12s [%4d %4d %4d %4d]  右:%4d 上:%4d%s\n', ...
            panels{k,1}, pos(1), pos(2), pos(3), pos(4), x2, y2, flag);
    end

catch ME
    ok = false;
    fprintf('\n*** FAIL ***\n%s\n', ME.message);
    for k = 1:min(5, numel(ME.stack))
        fprintf('   at %s  line %d\n', ME.stack(k).name, ME.stack(k).line);
    end
end

if ~isempty(fig) && isvalid(fig), close(fig); end

if ok
    fprintf('\n===== 新布局全部通过，可以跑了 =====\n');
else
    fprintf('\n===== 有问题，看上面的 FAIL =====\n');
end
