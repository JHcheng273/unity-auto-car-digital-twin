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
                'Position', pos, 'FontSize', 12.5, 'FontWeight', 'bold', ...
                'BackgroundColor', obj.baseColor);

            % ---- 内部纵向分区（★ 全部按 pos(4) 算）----
            % 踩过的坑：原来写死"上方留 96 给标题+状态+提示"，
            %   面板高 472 时画面区还剩 376，看着正常；
            %   但窗口一旦被系统压缩，pos(4) 变小而 96 不变，
            %   画面区不但变矮，还会被下面两个 label 盖住 ——
            %   于是看起来"画面是一条、字还重叠"。
            % 现在：先给底部留固定高度的两条文字，剩下的全给画面。
            %
            % ★★ 还有个更隐蔽的坑：**画面区必须保持图像本身的宽高比**。
            %   摄像头出来是 240×180（4:3 横的），但第四列被压到只有 293 宽，
            %   剩下的高度 420 全给画面区的话就是 271×420 —— 一个**竖长条**。
            %   把 4:3 的横图塞进竖长条，画面就会被纵向拉伸 + 上下留黑，
            %   看着像"只有一条"或者"图变形了"。
            %   正确做法：在可用框里做**等比缩放**（letterbox），
            %   算出一个同样是 4:3 的内框，水平居中、垂直居中。
            innerW = max(80, pos(3) - 22);
            STATUS_H = 26;                   % 状态条高
            HINT_H   = 20;                   % 提示小字高
            BOT_PAD  = 10;
            bottomBlk = BOT_PAD + HINT_H + 6 + STATUS_H;   % 底部文字块总高
            statusY   = BOT_PAD + HINT_H + 6;
            hintY     = BOT_PAD;
            imgY = bottomBlk + 6;
            boxH = max(70, pos(4) - imgY - 8);   % 可用框
            boxW = innerW;

            % 等比缩放（源图 240×180 = 4:3）
            srcAR = 240 / 180;
            if boxW / boxH > srcAR
                fitH = boxH;  fitW = round(boxH * srcAR);   % 框太宽 → 左右留白
            else
                fitW = boxW;  fitH = round(boxW / srcAR);   % 框太高 → 上下留白
            end
            fitX = 11 + round((boxW - fitW) / 2);
            fitY = imgY + round((boxH - fitH) / 2);

            obj.ax = uiaxes(obj.Panel, 'Position', [fitX, fitY, fitW, fitH]);
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
            % ★ WordWrap 打开：偏离信息有时候很长（"偏离！横向 13.4 m + 夹角 42°"），
            %   窄面板下一行放不下会被直接裁掉右半截 —— 截图里就是这个现象。
            obj.lblStatus = uilabel(obj.Panel, 'Text', '等待画面…', ...
                'Position', [11, statusY, innerW, STATUS_H], 'FontSize', 12, ...
                'FontWeight', 'bold', 'WordWrap', 'on', ...
                'FontColor', [0.35 0.38 0.44]);

            obj.lblHint = uilabel(obj.Panel, ...
                'Text', sprintf('偏离判定：横向 > %.1f m 或 夹角 > %.0f°', ...
                                obj.devDistLimit, obj.devAngLimit), ...
                'Position', [11, hintY, innerW, HINT_H], 'FontSize', 10.5, ...
                'WordWrap', 'on', 'FontColor', [0.50 0.53 0.58]);
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
                txt = sprintf('阈值 横向 %.1f m / 夹角 %.0f°   画面 %.1f fps%s', ...
                              obj.devDistLimit, obj.devAngLimit, fps, rxInfo);
            else
                txt = sprintf('阈值 横向 %.1f m / 夹角 %.0f°%s', ...
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
