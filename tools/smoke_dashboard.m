%% 冒烟测试：把 unity_dashboard 用到的所有 UI 控件真的创建一遍
%
% 为什么需要：光用 meta.class 查属性名不够，还得确认「属性值的格式」对不对
%   —— 比如 ScaleColorLimits 是 Nx2 矩阵、MajorTickLabels 是 cell 还是 string、
%   uilabel.Text 能不能收 cell 数组做多行。
%   这些只有真的建一遍才知道。
%
% 窗口用 Visible='off' 隐藏，跑完自动关，不会打扰你。

ok = true;
fig = [];

try
    fig = uifigure('Visible', 'off', 'Position', [60 40 1180 780]);

    % ---------- 车速表（半圆）----------
    g1 = uigauge(fig, 'semicircular');
    g1.Position = [20 384 340 306];
    g1.Limits = [0 60];
    g1.MajorTicks = 0:10:60;
    g1.MinorTicks = 0:5:60;
    g1.ScaleColors = [0.22 0.65 0.36; 0.95 0.76 0.20; 0.90 0.28 0.22];
    g1.ScaleColorLimits = [0 35; 35 50; 50 60];
    g1.FontSize = 11;
    g1.Value = 12.3;
    fprintf('OK   semicircular gauge\n');

    % ---------- 线性表（0~100）----------
    g2 = uigauge(fig, 'linear');
    g2.Position = [390 586 380 104];
    g2.Limits = [0 100];
    g2.MajorTicks = 0:25:100;
    g2.MajorTickLabels = {'0','25','50','75','100'};
    g2.Orientation = 'horizontal';
    g2.ScaleColors = [0.25 0.60 0.90];
    g2.ScaleColorLimits = [0 100];
    g2.Value = 70;
    fprintf('OK   linear gauge 0..100\n');

    % ---------- 线性表（-100~100，带中文刻度标签）----------
    g3 = uigauge(fig, 'linear');
    g3.Position = [390 326 380 104];
    g3.Limits = [-100 100];
    g3.MajorTicks = -100:50:100;
    g3.MajorTickLabels = {'左满','-50','0','50','右满'};
    g3.Orientation = 'horizontal';
    g3.ScaleColors = [0.38 0.45 0.85];
    g3.ScaleColorLimits = [-100 100];
    g3.Value = -31;
    fprintf('OK   linear gauge -100..100\n');

    % ---------- 指示灯 ----------
    L = uilamp(fig, 'Position', [400 296 26 26]);
    L.Color = [0.62 0.65 0.69];
    L.Color = [0.20 0.75 0.35];
    fprintf('OK   lamp\n');

    % ---------- 多行标签 ----------
    lb = uilabel(fig, 'Text', {'第一行','第二行'}, 'Position', [790 180 370 140], ...
        'FontSize', 12.5, 'VerticalAlignment', 'top', ...
        'BackgroundColor', [1 1 1], 'FontColor', [0.22 0.25 0.31]);
    lb.Text = {'a','b','c'};
    fprintf('OK   multi-line label\n');

    % ---------- 滑块 ----------
    s = uislider(fig, 'Limits', [0 8], 'Value', 4.5, 'MajorTicks', 0:2:8, ...
        'Position', [240 78 300 3]);
    s.ValueChangedFcn = @(src,evt) disp(src.Value);
    fprintf('OK   slider\n');

    % ---------- 开关（两种样式）----------
    % 坑：uiswitch 的 Value 是 Items 里的字符串，不是 true/false
    sw1 = uiswitch(fig, 'slider', 'Position', [660 66 50 22]);
    sw1.Items = {'关', '开'};
    sw1.Value = '开';
    assert(strcmp(sw1.Value, '开'));
    sw2 = uiswitch(fig, 'toggle', 'Position', [660 20 60 26]);
    sw2.Items = {'关', '开'};
    sw2.Value = '关';
    fprintf('OK   switch slider + toggle\n');

    % ---------- 下拉框 ----------
    dd = uidropdown(fig, 'Items', {'MATLAB 自动','MATLAB 手动','Unity 本地'}, ...
        'Value', 'MATLAB 自动', 'Position', [20 62 190 30]);
    dd.Value = 'Unity 本地';
    fprintf('OK   dropdown\n');

    % ---------- 面板 ----------
    p = uipanel(fig, 'Title', '控制台', 'Position', [20 20 1140 145], 'FontSize', 13);
    fprintf('OK   panel with Title\n');

    % ---------- 坐标区 + 动画线 + 图例 ----------
    ax = uiaxes(fig, 'Position', [790 330 370 370]);
    ax.Title.String = '速度对比 (m/s)';
    ax.XLabel.String = '时间 (s)';
    ax.YLabel.String = '速度';
    grid(ax, 'on'); hold(ax, 'on');

    h1 = animatedline(ax, 'Color', [0.16 0.42 0.86], 'LineWidth', 1.6);
    h2 = animatedline(ax, 'Color', [0.92 0.36 0.16], 'LineStyle', '--', 'LineWidth', 1.3);
    addpoints(h1, 0, 1); addpoints(h1, 1, 2);
    addpoints(h2, 0, 0.5); addpoints(h2, 1, 1.5);
    legend(ax, {'实体速度','MATLAB 期望'}, 'Location', 'northwest', 'FontSize', 9);
    fprintf('OK   uiaxes + animatedline + legend\n');

    % ---------- uigauge 确实没有 Title / Label ----------
    fprintf('\n--- 确认 uigauge 的属性缺口 ---\n');
    try
        g1.Title = 'x';
        fprintf('  Title 居然能用？\n');
    catch
        fprintf('  确认：uigauge 没有 Title 属性\n');
    end
    try
        g1.Label = 'x';
        fprintf('  Label 居然能用？\n');
    catch
        fprintf('  确认：uigauge 没有 Label 属性\n');
    end

catch ME
    ok = false;
    fprintf('\n*** FAIL ***\n%s\n', ME.message);
    for k = 1:min(4, numel(ME.stack))
        fprintf('   at %s  line %d\n', ME.stack(k).name, ME.stack(k).line);
    end
end

if ~isempty(fig) && isvalid(fig), close(fig); end

if ok
    fprintf('\n===== 全部控件创建成功，可以跑了 =====\n');
else
    fprintf('\n===== 有控件创建失败，看上面的 FAIL =====\n');
end
