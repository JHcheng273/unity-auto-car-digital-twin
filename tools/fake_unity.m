%% fake_unity.m —— 在 MATLAB 里"扮演 Unity"，给仪表盘喂数据
%
% 【为什么要单独跑一个进程】
%   MATLAB 的 timer 在主线程跑忙循环时**根本不会触发**（实测 1.5 秒只触发 1 次）。
%   而 unity_dashboard 就是个带 pause 的忙循环 —— 所以
%   "在同一个 MATLAB 里一边跑仪表盘一边发数据"是行不通的，timer 会被饿死。
%
%   真实场景本来就是两个进程（Unity + MATLAB），这里照样两个进程，
%   反而更接近实况。
%
% 【它发什么】
%   ① 状态包 → 127.0.0.1:5005   车先沿路径正常行驶，N 秒后故意横向偏离 + 歪车头
%   ② 画面   → 127.0.0.1:5007   按 CameraStreamer.cs 的协议发 JPEG 分片
%
% 用法（由 test_dashboard_e2e.sh 调用）：
%   fake_unity(<持续秒数>)
%
% 单独手动跑也行：
%   cd tools; fake_unity(30)

function fake_unity(seconds)
    if nargin < 1 || isempty(seconds), seconds = 20; end

    STATUS_PORT = 5005;
    VIDEO_PORT  = 5007;

    here = fileparts(mfilename('fullpath'));
    addpath(fullfile(fileparts(here), 'matlab'));

    % ---- 真实航点（和 unity_dashboard.m 的 WP_POINTS 一致）----
    WP = [  8.49,  -3.10;
           -1.51,   6.90;
          -11.51,  -3.10;
           -1.51, -13.10];

    % ---- 造一帧"看起来像路面"的图当摄像头画面 ----
    img = make_road_image(180, 240);
    f = [tempname '.jpg'];
    imwrite(img, f, 'jpg', 'Quality', 60);
    fid = fopen(f, 'r'); JPG = fread(fid, Inf, 'uint8')'; fclose(fid); delete(f);

    % ★ 发送端必须设 OutputDatagramSize：MATLAB 默认 512，超出的会被静默截断
    txS = udpport("datagram", "IPV4", "Timeout", 10);
    txV = udpport("datagram", "IPV4", "Timeout", 10);
    txV.OutputDatagramSize = 1600;

    fprintf("fake_unity 启动：状态→%d，画面→%d，跑 %.0f 秒\n", ...
            STATUS_PORT, VIDEO_PORT, seconds);
    fprintf("  JPEG 一帧 %d 字节，约 %d 片\n", numel(JPG), ceil(numel(JPG)/1400));

    t0 = tic;
    statusPeriod = 0.05;        % 20 Hz
    videoPeriod  = 1/12;        % 12 Hz
    nextStatus = 0;  nextVideo = 0;
    nStatus = 0;  nVideo = 0;  frameId = 0;

    while toc(t0) < seconds
        now = toc(t0);

        if now >= nextStatus
            nextStatus = now + statusPeriod;
            send_status(txS, now, WP, STATUS_PORT);
            nStatus = nStatus + 1;
        end

        if now >= nextVideo
            nextVideo = now + videoPeriod;
            send_video(txV, JPG, frameId, VIDEO_PORT);
            frameId = frameId + 1;
            nVideo = nVideo + 1;
        end

        pause(0.002);   % 让出 CPU，否则 pause 时间不准
    end

    fprintf("fake_unity 结束：发了 %d 个状态包，%d 帧画面\n", nStatus, nVideo);
    delete(txS); delete(txV);
end

%% ---------- 状态包 ----------
function send_status(tx, t, WP, port)
% 车沿第 1 段匀速走；到 6 秒后开始"偏离"：横移 + 歪车头。
% 6 秒这个点选得有讲究 —— 要给 MATLAB 足够时间把仪表盘建起来、
% 开始收包之后再偏离，否则截图时可能还没变色。
    DEV_START = 6.0;

    A = WP(1,:);  B = WP(2,:);
    segLen = hypot(B(1)-A(1), B(2)-A(2));
    dirx = (B(1)-A(1)) / segLen;
    dirz = (B(2)-A(2)) / segLen;

    speed = 3.0;                          % m/s
    along = mod(speed * t, segLen);       % 在第 1 段上走了多远
    x = A(1) + dirx * along;
    z = A(2) + dirz * along;
    yaw = atan2d(dirx, dirz);             % Unity 的 yaw：0=+Z，顺时针为正

    if t > DEV_START
        lat  = (t - DEV_START) * 1.5;     % 每秒横移 1.5 米
        x    = x - dirz * lat;            % 段的垂直方向 = (-dirz, dirx)
        z    = z + dirx * lat;
        yaw  = yaw + 40;                  % 再歪 40 度（阈值 35）
    end

    p = struct('t', round(t,3), 'x', round(x,3), 'z', round(z,3), ...
               'yaw', round(yaw,1), 'speed', speed, 'motor', 0.75, ...
               'braking', false, 'steer', 0.05, 'wp', 1, 'lap', 0, ...
               'state', 0, 'front_dist', 99, 'has_front', false, ...
               'accept', true);

    write(tx, uint8(jsonencode(p)), "uint8", "127.0.0.1", port);
end

%% ---------- 画面 ----------
function send_video(tx, jpg, frameId, port)
    HEADER = 14;  MAXPAY = 1400;
    total = ceil(numel(jpg) / MAXPAY);

    for i = 0:total-1
        off = i * MAXPAY + 1;
        len = min(MAXPAY, numel(jpg) - off + 1);

        pkt = zeros(1, HEADER + len, 'uint8');
        pkt(1) = uint8(86);  pkt(2) = uint8(83);      % 'V','S' 魔数
        pkt(3:6)   = uint8([bitand(bitshift(frameId,-24),255), ...
                            bitand(bitshift(frameId,-16),255), ...
                            bitand(bitshift(frameId,-8),255),  ...
                            bitand(frameId,255)]);
        pkt(7:8)   = uint8([bitshift(i,-8),     bitand(i,255)]);
        pkt(9:10)  = uint8([bitshift(total,-8), bitand(total,255)]);
        pkt(11:12) = uint8([bitshift(len,-8),   bitand(len,255)]);
        pkt(13:14) = uint8([0 0]);
        pkt(HEADER+1:end) = jpg(off:off+len-1);

        write(tx, pkt, "uint8", "127.0.0.1", port);
    end
end

%% ---------- 造图 ----------
function im = make_road_image(H, W)
    im = uint8(zeros(H, W, 3));
    for y = 1:H
        v = uint8(max(40, 130 - y * 0.42));
        im(y,:,1) = v;  im(y,:,2) = v;
        im(y,:,3) = uint8(min(255, double(v) + 10));
    end
    im(1:44, :, 1) = 140;  im(1:44, :, 2) = 185;  im(1:44, :, 3) = 225;
    for y = 45:H
        tt = (y - 45) / max(1, H - 45);
        cw = 22 + 150 * tt;
        cx = W / 2;
        for side = [-1 1]
            xc = round(cx + side * cw);
            for d = 0:1
                xx = xc + d;
                if xx >= 1 && xx <= W
                    im(y, xx, :) = 250;
                end
            end
        end
    end
end
