classdef VideoReceiver < handle
%VIDEORECEIVER 从 UDP 收 Unity 摄像头画面，按包头拼帧，解成 RGB 图像。
%
% 【它解决什么问题】
%   Unity 那边一帧 JPEG 有 7 KB 左右，而 UDP 单包上限约 1472 字节，
%   所以一帧被拆成好几片发出来。这个类负责：
%     1. 收片
%     2. 按 (frameId, partIdx, partTotal) 把同一帧的片拼回去
%     3. 收全了就交给 imdecode 解成图像；缺片就整帧丢弃
%     4. 顺手统计"丢帧率"——如果丢帧率高，说明 MATLAB 收不过来，
%        该降 Unity 那边的分辨率或帧率了
%
% 【为什么是一个 class（handle）而不是一堆函数】
%   拼接状态（当前帧号、已收到的片、每帧的字节缓冲）要跨多次调用保留。
%   用 handle 类最自然；用函数就得靠 global/persistent，容易出事。
%
% 【包头格式，必须和 Unity 的 CameraStreamer.cs 完全一致】
%   偏移  长度  含义
%   0     2     magic = 'V','S'（用来和状态包区分：状态包是 JSON，首字节 '{'）
%   2     4     frameId  帧号，大端
%   6     2     partIdx  第几片（0 起）
%   8     2     partTotal 共几片
%   10    2     payloadLen 本片有效字节数
%   12    2     reserved 预留
%   共 14 字节
%
% 【用法】
%   vr = VideoReceiver(5007);        % 监听 5007
%   ...
%   img = vr.Poll();                 % 每轮主循环调一次
%   if ~isempty(img)
%       imshow(img);                 % img 是 H×W×3 uint8
%   end
%   delete(vr);                      % 用完关端口
%
% 【★ 端口用完了必须 delete】
%   和状态包那个 udpport 一样，不释放的话端口会一直被占着，
%   下次重跑就报"端口被占用"，而且只能重启 MATLAB 才能清掉。

    properties (SetAccess = private)
        Port            % 监听端口
        FramesOk = 0    % 成功解出来的帧数
        FramesBad = 0   % 因为缺片/解不开而丢掉的帧数
        BytesRx = 0     % 收到的总字节数（含包头）
        LastSize = [0 0] % 上一帧的 [高 宽]
        Skipped = 0     % 为了不卡主循环而主动跳过的解码次数（不是错误）
    end

    properties (Access = private)
        u                       % udpport 对象
        curFrame = -1           % 正在拼的帧号
        curTotal = 0            % 这一帧共有几片
        curParts                % cell 数组，第 k 个位置存第 k 片的 payload
        curGot = 0              % 这一帧已经收到几片
        staleCount = 0          % 连续收到多少个"不一样帧号"的片（说明前面的帧永远凑不齐了）
        pendingJpg = []         % 已拼好、等着解码的 JPEG 字节（见 Poll 的 ★ 说明）
        pendingCount = 0        % 这一轮里凑齐了多少帧（用来统计"跳过了多少帧没解"）
    end

    methods
        %% ---------- 构造：建端口 ----------
        function obj = VideoReceiver(port)
            if nargin < 1 || isempty(port), port = 5007; end
            obj.Port = port;

            % 先清掉同一个会话里可能残留的、占着这个端口的 udpport。
            % （脚本中途 Ctrl+C 会留下僵尸端口，新对象删不掉它）
            try
                stale = udpportfind;
                for k = 1:numel(stale)
                    try
                        if double(stale(k).LocalPort) == port
                            fprintf("[VideoReceiver] 清掉残留 udpport（正占 %d）\n", port);
                            delete(stale(k));
                        end
                    catch
                    end
                end
                pause(0.1);
            catch
                % udpportfind 不可用就算了
            end

            obj.u = udpport("datagram", "IPV4", "LocalPort", port, ...
                            "Timeout", 0.01);
            obj.curParts = {};
        end

        %% ---------- 主入口：把当前能收的片都收掉，返回最新解出的一帧 ----------
        function img = Poll(obj, maxDecode)
        %POLL 处理所有等待中的数据报，返回"这一轮解出的最新一帧"。
        %
        % maxDecode  这一轮最多**解几张图**（默认 1）。见下面 ★ 的说明。
        %
        % 返回 []：这轮没凑出完整帧（正常情况，别当错误处理）。
        % 返回 H×W×3 uint8：新的一帧，可以直接 imshow / image 显示。
        %
        % ★★ 这里有两个"吞吐 vs 卡顿"的取舍，都是实测踩出来的：
        %
        %   ① 要"尽量多收包"，而不是一圈只收一个。
        %      一帧有 3~8 片，一圈只收一个包的话，帧率会被主循环次数卡死。
        %
        %   ② 但**解码必须限量**！这是关键。
        %      实测一次 imread 要 31.6 ms。如果队列里积压了 200 个包（约 66 帧），
        %      全解一遍就是 2.1 秒 —— 主循环整整卡住两秒，
        %      期间状态包一个都读不到，包间隔被拉到 4 秒，
        %      连数字孪生模型都会被一个巨大的 dt 冲爆。
        %
        %      而我们**只需要最新那一帧**：用户看的是实时画面，
        %      中间积压的那 65 帧解出来根本没机会显示，纯属浪费。
        %      所以：包照收（把队列清空，保持"最新"），
        %      但只有**最后凑齐的那一帧**才真的去解 JPEG。

            img = [];
            if isempty(obj.u) || ~isvalid(obj.u), return; end
            if nargin < 2 || isempty(maxDecode), maxDecode = 1; end

            n = obj.u.NumDatagramsAvailable;
            if n <= 0, return; end

            n = min(n, 400);   % 上限只是防呆，正常情况队列不会这么深

            for k = 1:n
                d = read(obj.u, 1, "uint8");
                if isempty(d), break; end
                raw = uint8(d(1).Data);
                obj.BytesRx = obj.BytesRx + numel(raw);

                % HandlePacket 返回"拼好了但还没解码"的字节流，
                % 或者 [] 表示这包还没凑齐一帧
                jpg = obj.HandlePacket(raw);
                if ~isempty(jpg)
                    % ★ 只留最后一个：前面凑齐的帧直接扔掉，不解码
                    obj.pendingJpg = jpg;
                    obj.pendingCount = obj.pendingCount + 1;
                end
            end

            % ---- 一轮结束后，最多解 maxDecode 张 ----
            if ~isempty(obj.pendingJpg)
                img = obj.DecodePending();
            end
        end

        %% ---------- 丢弃：关端口 ----------
        function delete(obj)
            if ~isempty(obj.u)
                try
                    delete(obj.u);
                catch
                end
                obj.u = [];
            end
        end

        %% ---------- 清空积压的包和拼接状态 ----------
        function Flush(obj)
        %FLUSH 丢掉所有排队中的数据报，并把拼接状态复位。
        %
        % 什么时候需要它：Unity 重新 Play 之后，端口里可能还堆着上一轮
        % 没收完的残包。不清掉的话，新一轮的头几帧会跟旧帧号混在一起，
        % 表现为"画面卡一帧再跳变"。重连时调一次就好。
            if isempty(obj.u) || ~isvalid(obj.u), return; end

            n = obj.u.NumDatagramsAvailable;
            for k = 1:n
                try
                    read(obj.u, 1, "uint8");
                catch
                    break;
                end
            end

            obj.curFrame = -1;
            obj.curTotal = 0;
            obj.curParts = {};
            obj.curGot   = 0;
            obj.pendingJpg = [];
            obj.pendingCount = 0;
        end
    end

    %% ================= 内部实现 =================
    methods (Access = private)

        function jpg = HandlePacket(obj, raw)
        %HANDLEPACKET 处理一个数据报。
        %   返回：拼完整了 → 这一帧的 JPEG 字节流（**还没解码**）
        %         []       → 还没凑齐，或者这包无效
        %
        % ★ 注意这里只做"拼字节"，不做"解 JPEG" —— 解码很贵（31.6 ms），
        %   由 Poll 统一限流后再解。见 Poll 的注释。
            jpg = [];

            if numel(raw) < 14, return; end

            % ---- 校验 magic，顺便和状态包区分开 ----
            if raw(1) ~= uint8('V') || raw(2) ~= uint8('S')
                return;   % 不是视频包（可能是状态包串到同一个端口了）
            end

            frameId  = obj.ReadInt32(raw, 3);         % 1 基下标：偏移 2 → 下标 3
            partIdx  = obj.ReadUInt16(raw, 7);        % 偏移 6 → 下标 7
            partTotal= obj.ReadUInt16(raw, 9);        % 偏移 8 → 下标 9
            payLen   = obj.ReadUInt16(raw, 11);       % 偏移 10 → 下标 11

            if partTotal < 1 || partIdx >= partTotal, return; end
            if payLen < 1 || (14 + payLen) > numel(raw), return; end

            % ---- 换帧了？ ----
            if frameId ~= obj.curFrame
                % 上一帧还没凑齐就来了新帧 → 记一次丢帧
                if obj.curFrame >= 0 && obj.curGot < obj.curTotal && obj.curTotal > 0
                    obj.FramesBad = obj.FramesBad + 1;
                end
                % 开始拼新帧
                obj.curFrame = frameId;
                obj.curTotal = partTotal;
                obj.curParts = cell(1, partTotal);
                obj.curGot   = 0;
            elseif partTotal ~= obj.curTotal
                % 同帧号但总片数变了 —— 不可能，肯定是脏包，跳过
                return;
            end

            % ---- 已经收过这一片了？（UDP 不会重复，但重复包不致命，忽略即可）----
            if partIdx < numel(obj.curParts) && ~isempty(obj.curParts{partIdx + 1})
                return;
            end

            payload = raw(15 : 14 + payLen);   % 偏移 14 之后是载荷
            obj.curParts{partIdx + 1} = payload;
            obj.curGot = obj.curGot + 1;

            % ---- 凑齐了？交出去（只拼字节，先不解码）----
            if obj.curGot >= obj.curTotal
                jpg = obj.AssembleBytes();
                obj.curFrame = -1;
                obj.curTotal = 0;
                obj.curParts = {};
                obj.curGot   = 0;
            end
        end

        function buf = AssembleBytes(obj)
        %ASSEMBLEBYTES 把所有片按 partIdx 顺序接成一个字节流。
        %   有任何一片缺失 → 记一次丢帧，返回 []。
        %   注意：这里**不做 JPEG 解码**，也不校验内容 —— 那是 DecodePending 的活。
            buf = [];

            % 片可能乱序到达，要按 partIdx 顺序接
            totalBytes = 0;
            for i = 1:numel(obj.curParts)
                if isempty(obj.curParts{i})
                    % 有空位 → 有片没收到，整帧作废
                    obj.FramesBad = obj.FramesBad + 1;
                    return;
                end
                totalBytes = totalBytes + numel(obj.curParts{i});
            end

            buf = zeros(1, totalBytes, 'uint8');
            off = 0;
            for i = 1:numel(obj.curParts)
                p = obj.curParts{i};
                buf(off + 1 : off + numel(p)) = p;
                off = off + numel(p);
            end
        end

        function img = DecodePending(obj)
        %DECODEPENDING 把 pendingJpg 解成图像。解完就清空，避免重复解。
            img = [];
            buf = obj.pendingJpg;
            obj.pendingJpg = [];
            if isempty(buf), return; end

            % 这一轮凑齐了几帧，却只解了这一帧 —— 剩下的算"主动跳过"
            if obj.pendingCount > 1
                obj.Skipped = obj.Skipped + (obj.pendingCount - 1);
            end
            obj.pendingCount = 0;

            % ---- ★ 先验 JPEG 魔数，再解码 ----
            %   正常的 JPEG 一定以 FF D8 开头。拼错位/收错包时会在这里就露馅，
            %   比等 imread 抛异常再回溯要清楚得多，也省掉一次无谓的解码。
            if numel(buf) < 4 || buf(1) ~= 255 || buf(2) ~= 216
                obj.FramesBad = obj.FramesBad + 1;
                if obj.FramesBad <= 3
                    fprintf("[VideoReceiver] 拼出来的数据不是 JPEG（前 4 字节：%s）\n", ...
                            mat2str(buf(1:min(4, numel(buf)))));
                end
                return;
            end

            try
                img = obj.DecodeJpeg(buf);
            catch ME
                obj.FramesBad = obj.FramesBad + 1;
                if obj.FramesBad <= 3
                    fprintf("[VideoReceiver] JPEG 解码失败：%s\n", ME.message);
                end
                return;
            end

            obj.FramesOk = obj.FramesOk + 1;
            obj.LastSize = [size(img, 1), size(img, 2)];
        end

        function img = DecodeJpeg(obj, buf)
        %DECODEJPEG uint8 行向量 → H×W×3 uint8 图像。
        %
        % 【★ 为什么是"写临时文件 + imread"这种笨办法】
        %   R2025a 实测下来，只有这条路真的能走：
        %     · imdecode  —— 本机根本不存在这个函数（那是老版本/Octave 的名字）
        %     · imread(bytes) —— 报 badImageSourceDatatype，它只认真实文件路径
        %     · javax.imageio.ImageIO.read(ByteArrayInputStream)
        %         —— 在 R2025a 的 Java 桥下返回了个空的 double，取不出图像。
        %            这是 MATLAB 与 JVM 之间字节数组传递的兼容问题，不是我们写错了
        %     · imwrite/readJPEG —— 同理不存在
        %
        % 【性能实测（240×180 JPEG）】
        %     写文件 10.6 ms + imread 19.4 ms ≈ 30 ms/帧  → 上限约 33 FPS
        %   12 FPS 的目标下留了足够余量。所以这条路虽然"土"，但完全够用。
        %
        % 【★ 文件名一定要固定】
        %   曾经用 tempname 每帧建一个唯一文件名，结果单帧开销飙到 428 ms
        %   —— 因为 tempname 每次都要在临时目录里真正创建/校验文件。
        %   固定成一个名字（同一个 MATLAB 会话内只写这一个）后降到 30 ms。

            tf = fullfile(tempdir, sprintf('vr_frame_%d.jpg', double(obj.Port)));
            fid = fopen(tf, 'w');
            if fid < 0
                error('写不了临时文件 %s', tf);
            end
            fwrite(fid, buf, 'uint8');
            fclose(fid);

            img = imread(tf);

            % 灰度图补成三通道，省得下游到处判断维度
            if size(img, 3) == 1
                img = repmat(img, 1, 1, 3);
            end
        end

        %% ---- 字节序小工具（统一按大端读）----
        function v = ReadInt32(~, b, i)
            v = double(b(i)) * 16777216 + double(b(i+1)) * 65536 + ...
                double(b(i+2)) * 256 + double(b(i+3));
        end

        function v = ReadUInt16(~, b, i)
            v = double(b(i)) * 256 + double(b(i+1));
        end
    end
end
