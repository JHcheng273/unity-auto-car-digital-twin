classdef CameraPanel < handle
%CAMERAPANEL MATLAB 面板上的"倒车影像"。
%
% 【它长什么样】
%   一个 uipanel，里面：
%     · 一块实时画面（车载摄像头的实拍）
%     · 一条状态文字（"路径跟踪正常" / "偏离！横向 3.2 m"）
%   正常时面板是白底；一旦偏离，整个面板底色变红、边框变粗 ——
%   和真车倒车影像压线时屏幕变红是一个意思。
%
% 【为什么整块变色而不是只改文字颜色】
%   驾驶时人不会盯着文字看。大面积的颜色变化是余光就能感知的，
%   这才是"倒车影像"那种提示强度。
%
% 【性能】
%   画面更新用 `image` 的 CData（0.02 ms），**不要**用 uiimage.ImageSource
%   （实测 38.7 ms，慢 1900 倍，因为它每次都重新上传+缩放整张图）。
%
% 【用法】
%   cp = CameraPanel(fig, [x y w h]);
%   ...
%   cp.Update(img, deviated, infoText);   % img 可以是 [] （没新帧就只更新状态）
%   ...
%   delete(cp);

    properties (SetAccess = private)
        Panel           % 承载一切的 uipanel
        FramesShown = 0 % 显示过多少帧
        IsDeviated = false
    end

    properties (Access = private)
        imgHandle       % uiaxes 里那个 image 对象
        ax
        lblStatus
        lblHint
        baseColor = [1 1 1]           % 正常时的底色
        devColor  = [0.98 0.82 0.82]  % 偏离时的底色（淡红，别太刺眼）
        devDistLimit = 2.0            % 横向阈值（先给默认值，setLimits 可覆盖）
        devAngLimit  = 35             % 夹角阈值
    end

    methods
        %% ---------- 构造：搭出面板 ----------
        function obj = CameraPanel(parent, pos)
            if nargin < 2 || isempty(pos), pos = [20 20 380 300]; end

            obj.Panel = uipanel(parent, 'Title', '车载摄像头 · 实时路况', ...
                'Position', pos, 'FontSize', 13, 'FontWeight', 'bold', ...
                'BackgroundColor', obj.baseColor);

            % ---- 画面区 ----
            % 面板内部坐标是相对的，减去标题栏高度（约 30px）再留边距
            innerW = pos(3) - 24;
            innerH = max(60, pos(4) - 96);

            obj.ax = uiaxes(obj.Panel, 'Position', [12, 74, innerW, innerH]);
            obj.ax.XTick = [];  obj.ax.YTick = [];
            obj.ax.Box = 'on';
            obj.ax.XLim = [0.5 240.5];
            obj.ax.YLim = [0.5 180.5];
            obj.ax.YDir = 'reverse';          % ★ 图像坐标原点在左上，要和 imread 一致
            obj.ax.Color = [0.12 0.12 0.14];  % 还没画面时是深灰（不是白，避免误以为有图）
            obj.ax.Toolbar.Visible = 'off';

            % 先放一张全黑的占位图，尺寸跟 Unity 那边一致
            obj.imgHandle = image(obj.ax, zeros(180, 240, 3, 'uint8'));
            obj.imgHandle.AlphaDataMapping = 'none';

            % ---- 状态条 ----
            obj.lblStatus = uilabel(obj.Panel, 'Text', '等待画面…', ...
                'Position', [12, 40, innerW, 26], 'FontSize', 13, ...
                'FontWeight', 'bold', 'FontColor', [0.35 0.38 0.44]);

            obj.lblHint = uilabel(obj.Panel, ...
                'Text', sprintf('偏离判定：横向 > %.1f m 或 夹角 > %.0f°', ...
                                obj.devDistLimit, obj.devAngLimit), ...
                'Position', [12, 14, innerW, 22], 'FontSize', 11, ...
                'FontColor', [0.50 0.53 0.58]);
        end

        %% ---------- 每轮更新 ----------
        function Update(obj, img, deviated, infoText)
        %UPDATE 刷新画面与偏离状态。
        %
        % img       H×W×3 uint8 新一帧；传 [] 表示"这轮没新帧"，
        %           那就只更新偏离状态，不动画面（省掉一次图像上传）。
        % deviated  true/false，是否偏离路径
        % infoText  状态条文字（一般是 check_deviation 返回的 info）

            if nargin < 4 || isempty(infoText), infoText = ''; end

            % ---- 画面 ----
            if ~isempty(img)
                % CData 直接换引用，不重传图形 —— 这是快的唯一原因
                obj.imgHandle.CData = img;
                obj.FramesShown = obj.FramesShown + 1;

                % 第一帧到达时把坐标轴范围对准真实尺寸，
                % 否则图像会被拉伸（占位图是 240×180，万一分辨率改了就歪了）
                if obj.FramesShown == 1
                    obj.ax.XLim = [0.5, size(img, 2) + 0.5];
                    obj.ax.YLim = [0.5, size(img, 1) + 0.5];
                end
            end

            % ---- 偏离状态 ----
            obj.IsDeviated = logical(deviated);

            if obj.IsDeviated
                obj.Panel.BackgroundColor = obj.devColor;
                obj.lblStatus.Text = infoText;
                obj.lblStatus.FontColor = [0.78 0.10 0.08];
                obj.ax.Box = 'on';
                obj.ax.XColor = [0.85 0.15 0.12];
                obj.ax.YColor = [0.85 0.15 0.12];
                obj.ax.LineWidth = 2.5;
            else
                obj.Panel.BackgroundColor = obj.baseColor;
                obj.lblStatus.Text = infoText;
                obj.lblStatus.FontColor = [0.16 0.55 0.25];
                obj.ax.XColor = [0.75 0.78 0.82];
                obj.ax.YColor = [0.75 0.78 0.82];
                obj.ax.LineWidth = 1;
            end
        end

        %% ---------- 单独改提示文字（不碰画面）----------
        function SetStatus(obj, txt, color)
            if nargin < 3 || isempty(color), color = [0.35 0.38 0.44]; end
            obj.lblStatus.Text = txt;
            obj.lblStatus.FontColor = color;
        end

        %% ---------- 底部小字：阈值说明 + 实时帧率 ----------
        function SetFps(obj, fps, rxInfo)
        %SETFPS 把显示帧率和收包统计写到底部小字里。
            if nargin < 3 || isempty(rxInfo), rxInfo = ''; end
            if fps > 0
                txt = sprintf('偏离判定：横向 > %.1f m 或 夹角 > %.0f°   ·   画面 %.1f 帧/秒%s', ...
                              obj.devDistLimit, obj.devAngLimit, fps, rxInfo);
            else
                txt = sprintf('偏离判定：横向 > %.1f m 或 夹角 > %.0f°%s', ...
                              obj.devDistLimit, obj.devAngLimit, rxInfo);
            end
            obj.lblHint.Text = txt;
        end

        function setLimits(obj, distLimit, angLimit)
            obj.devDistLimit = distLimit;
            obj.devAngLimit  = angLimit;
        end

        function delete(~)
            % uipanel 会随父窗口一起销毁，这里不需要额外操作。
            % 保留这个方法是为了让 delete(cp) 不报错（handle 类的约定）。
        end
    end
end
