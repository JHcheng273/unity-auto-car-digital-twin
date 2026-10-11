%% VideoReceiver 链路验证
%
% 思路：在 MATLAB 里**扮演 Unity**，按 CameraStreamer.cs 的协议
% 把一帧 JPEG 切片打出去，再用 VideoReceiver 收回来解。两边都在同一进程里，
% 走真实的 UDP 回环（127.0.0.1）。
%
% 这样能在不改 Unity、不动场景的情况下，把"包格式对不对、乱序能不能拼、
% 缺片会不会丢帧"这几件最容易出错的事全验一遍。
%
% 覆盖的用例：
%   1. 单片帧（小图）—— 最基本
%   2. 多片帧（大图）—— 主力场景
%   3. 乱序到达 —— 网络本来就可能乱序，拼接必须按 partIdx 排序
%   4. 故意丢一片 —— 必须整帧丢弃，且 FramesBad 要涨
%   5. 状态包（JSON）串进同一端口 —— 必须被忽略，不能崩
%   6. 连续 30 帧 —— 拼帧状态在多帧之间不能串

clear; clc;

here = fileparts(mfilename('fullpath'));
root = fileparts(here);
matlabDir = fullfile(root, 'matlab');
addpath(matlabDir);
fprintf("已加入搜索路径：%s\n\n", matlabDir);

PORT = 5027;          % 用个不常见的端口，别跟正在跑的仪表盘（5007）打架
HEADER = 14;
MAXPAY = 1400;

pass = 0; fail = 0;

%% ---------- 准备一张真实感的测试图（有渐变、有图块，方便看出错位）----------
W = 240; H = 180;
[Xg, Yg] = meshgrid(1:W, 1:H);
img0 = uint8(cat(3, ...
    mod(Xg * 1.2, 255), ...                       % R：横向渐变
    mod(Yg * 1.4, 255), ...                       % G：纵向渐变
    zeros(H, W) + 128));                           % B：常量
% 再画几个方块，拼错位时一眼能看出来
img0(30:70,  30:70,  :) = 255;
img0(100:140, 150:200, 2) = 0;
img0(20:40, 180:220, 1) = 0;

%% ---------- 先建接收端，再建发送端 ----------
% ★ 顺序很重要：UDP 不会为"还没开始监听"的端口排队。
%   先发后收的话，包全丢了，测试结果全是 FAIL 但其实是测试脚本的错。
vr = VideoReceiver(PORT);
pause(0.2);   % 给端口一点时间真正 bind 上

% ★ 发送端必须给足 Timeout（默认太短）。
%   MATLAB 的 udpport 写是异步的，连发多包时如果 Timeout 太小，
%   会报「DRAIN timed-out while waiting for data to be sent」并丢包。
%   （这是 MATLAB 特有的坑；Unity 那边用的是原始 socket 的 Send()，没有这个问题。）
cu = udpport("datagram", "IPV4", "Timeout", 10);

% ★★ 这个是最阴的一个坑：udpport 有个属性 OutputDatagramSize，默认只有 512 字节。
%    write() 超过 512 的部分会被**静默截断** —— 不报错、不警告，
%    对方收到的就是半截包。找这个问题花了很久。
%    真实系统里 Unity 用原始 socket 发送，没有这个限制；
%    但只要是 MATLAB 当发送端，就必须显式把它调大。
cu.OutputDatagramSize = 1600;
fprintf("测试端口 %d（发送端 OutputDatagramSize=%d）\n\n", PORT, cu.OutputDatagramSize);

%% ===================== 用例 1/2/3/4/5：单片、多片、乱序、丢片、脏包 =====================
function bytes = slice_send(cu, jpg, frameId, port, header, maxpay, dropIdx)
%SLICE_SEND 按 Unity 的协议切片发送。dropIdx 指定的片故意不发（1 基）。
%
% ★ 参数一共 7 个，所以判"有没有传 dropIdx"要用 nargin >= 7。
%   我第一版写了 nargin >= 8，结果"故意丢片"这一步从来没生效 ——
%   用例 4 一直报"缺片应该是 bad"，其实根本没缺片。
    if nargin < 7, dropIdx = 0; end

    total = ceil(numel(jpg) / maxpay);
    parts = cell(1, total);
    for i = 0:total-1
        off = i * maxpay + 1;
        len = min(maxpay, numel(jpg) - off + 1);
        payload = jpg(off : off + len - 1);

        pkt = zeros(1, header + len, 'uint8');
        pkt(1) = uint8('V');  pkt(2) = uint8('S');
        pkt(3:6)   = uint8([  bitand(bitshift(frameId,-24),255), ...
                              bitand(bitshift(frameId,-16),255), ...
                              bitand(bitshift(frameId,-8),255),  ...
                              bitand(frameId,255) ]);
        pkt(7:8)   = uint8([bitshift(i,-8),  bitand(i,255)]);
        pkt(9:10)  = uint8([bitshift(total,-8), bitand(total,255)]);
        pkt(11:12) = uint8([bitshift(len,-8), bitand(len,255)]);
        pkt(13:14) = uint8([0 0]);
        pkt(header+1 : end) = payload;
        parts{i+1} = pkt;
    end

    for i = 1:total
        if i == dropIdx, continue; end      % 故意丢掉这一片
        write(cu, parts{i}, "uint8", "127.0.0.1", port);
    end
    bytes = numel(jpg);
end

function img = drain(vr, nExpect, timeoutSec)
%DRAIN 反复 Poll 直到解出 nExpect 帧或超时。
    img = [];
    t0 = tic;
    got = 0;
    while toc(t0) < timeoutSec && got < nExpect
        one = vr.Poll();
        if ~isempty(one)
            img = one;
            got = got + 1;
        end
        pause(0.005);
    end
end

%% ---- 用例 1：单片帧（把图缩到很小，让它塞进 1 片）----
tiny = imresize(img0, [8 8]);
tb = imencode_jpg(tiny, 20);
fprintf("--- 用例1：单片帧（JPEG %d 字节，1 片）---\n", numel(tb));
total1 = ceil(numel(tb) / MAXPAY);
slice_send(cu, tb, 100, PORT, HEADER, MAXPAY);
out = drain(vr, 1, 2.0);
if ~isempty(out) && total1 == 1
    fprintf(" [OK] 单片帧收到，尺寸 %dx%d\n", size(out,1), size(out,2)); pass = pass + 1;
else
    fprintf(" [FAIL] 单片帧没收到（total=%d）\n", total1); fail = fail + 1;
end

%% ---- 用例 2：多片帧，顺序到达 ----
jb = imencode_jpg(img0, 60);
total2 = ceil(numel(jb) / MAXPAY);
fprintf("\n--- 用例2：多片帧顺序发送（JPEG %d 字节，%d 片）---\n", numel(jb), total2);
slice_send(cu, jb, 101, PORT, HEADER, MAXPAY);
out = drain(vr, 1, 2.0);
if ~isempty(out) && isequal(size(out), [H W 3])
    fprintf(" [OK] 拼回完整帧，尺寸 %dx%d\n", size(out,1), size(out,2)); pass = pass + 1;
else
    fprintf(" [FAIL] 多片帧拼接失败\n"); fail = fail + 1;
end

%% ---- 用例 3：乱序到达 ----
fprintf("\n--- 用例3：乱序发送（%d 片倒着发）---\n", total2);
% 先切好再逆序发
% 直接复用：把每一片单独算出来，倒序 write
parts = {};
for i = 0:total2-1
    off = i * MAXPAY + 1;
    len = min(MAXPAY, numel(jb) - off + 1);
    pkt = zeros(1, HEADER + len, 'uint8');
    pkt(1) = uint8('V'); pkt(2) = uint8('S');
    fid = 102;
    pkt(3:6) = uint8([bitand(bitshift(fid,-24),255), bitand(bitshift(fid,-16),255), ...
                      bitand(bitshift(fid,-8),255), bitand(fid,255)]);
    pkt(7:8) = uint8([bitshift(i,-8), bitand(i,255)]);
    pkt(9:10) = uint8([bitshift(total2,-8), bitand(total2,255)]);
    pkt(11:12) = uint8([bitshift(len,-8), bitand(len,255)]);
    pkt(HEADER+1:end) = jb(off:off+len-1);
    parts{i+1} = pkt;
end
for i = numel(parts):-1:1
    write(cu, parts{i}, "uint8", "127.0.0.1", PORT);
    pause(0.001);
end
out = drain(vr, 1, 2.0);
if ~isempty(out) && isequal(size(out), [H W 3])
    fprintf(" [OK] 乱序也能拼回（按 partIdx 排序正确）\n"); pass = pass + 1;
else
    fprintf(" [FAIL] 乱序拼接失败\n"); fail = fail + 1;
end

%% ---- 用例 4：故意丢一片 → 必须整帧丢弃，FramesBad 要涨 ----
% ★ 先 Flush：前面几个用例可能还有残包排在队列里。
%   不清掉的话，drain 会立刻取到旧帧，用例就测不出"缺片会不会丢帧"了。
vr.Flush();
badBefore = vr.FramesBad;
fprintf("\n--- 用例4：故意丢第 2 片 ---\n");

% ★ 关键：把"发缺片帧"和"发完整帧"分成两步，中间插一次 Poll。
%   否则两次发送的包会堆在同一个队列里，一次 Poll 全处理完，
%   你就分不清拿到的图到底是哪一帧了 —— 我第一版就是这么写错的。
slice_send(cu, jb, 103, PORT, HEADER, MAXPAY, 2);    % 丢第 2 片
pause(0.3);
out = drain(vr, 1, 0.6);                              % 这轮不该出图

slice_send(cu, jb, 104, PORT, HEADER, MAXPAY);        % 补一个完整帧
out2 = drain(vr, 1, 1.5);

if isempty(out) && ~isempty(out2) && vr.FramesBad > badBefore
    fprintf(" [OK] 缺片整帧丢弃，丢帧计数 %d → %d\n", badBefore, vr.FramesBad);
    pass = pass + 1;
else
    fprintf(" [FAIL] 缺片处理不对（out 空=%d, out2 空=%d, bad %d→%d）\n", ...
            isempty(out), isempty(out2), badBefore, vr.FramesBad);
    fail = fail + 1;
end

%% ---- 用例 5：状态包（JSON）串进视频端口 → 必须忽略不崩 ----
fprintf("\n--- 用例5：把 JSON 状态包发到视频端口 ---\n");
junk = uint8('{"t":1.5,"x":1.0,"z":2.0,"speed":3.1,"wp":0}');
write(cu, junk, "uint8", "127.0.0.1", PORT);
pause(0.05);
slice_send(cu, jb, 105, PORT, HEADER, MAXPAY);
out = drain(vr, 1, 2.0);
if ~isempty(out) && isequal(size(out), [H W 3])
    fprintf(" [OK] 脏包被忽略，后续正常帧不受影响\n"); pass = pass + 1;
else
    fprintf(" [FAIL] 脏包影响了后续接收\n"); fail = fail + 1;
end

%% ---- 用例 6：连续 30 帧，检查多帧之间的拼接状态不串 ----
fprintf("\n--- 用例6：连续 30 帧 ---\n");
okBefore = vr.FramesOk;
for k = 1:30
    % 每帧内容略有不同，模拟真实画面在变
    im = img0;  im(1:5, 1:5, 1) = uint8(k * 8);
    b = imencode_jpg(im, 60);
    slice_send(cu, b, 200 + k, PORT, HEADER, MAXPAY);
    one = drain(vr, 1, 0.8);
    if isempty(one), break; end
end
got = vr.FramesOk - okBefore;
if got == 30
    fprintf(" [OK] 30 帧全部拼解成功\n"); pass = pass + 1;
else
    fprintf(" [FAIL] 只成功 %d / 30 帧\n", got); fail = fail + 1;
end

%% ---------- 汇总 ----------
fprintf("\n===== 结果：%d 通过，%d 失败 =====\n", pass, fail);
fprintf("累计：收到 %d 字节，成功 %d 帧，丢弃 %d 帧\n", ...
        vr.BytesRx, vr.FramesOk, vr.FramesBad);
fprintf("接收端最后解析出的图像尺寸：%dx%d\n", vr.LastSize(1), vr.LastSize(2));

% 存一张实际拼出来的图，可以直接看
if ~isempty(out)
    imwrite(out, fullfile(here, "vr_test_output.png"));
    fprintf("已把最后一帧存成 vr_test_output.png，可以直接打开看是否正确\n");
end

delete(vr);
clear cu;


%% ---------- 本地小工具：把图像编成 JPEG 字节 ----------
function buf = imencode_jpg(img, q)
%IMENCODE_JPG 图像 → JPEG 字节行向量。
% 本机 R2025a 没有 imencode（那是 Octave 的函数），所以只能走临时文件。
    f = [tempname '.jpg'];
    imwrite(img, f, 'jpg', 'Quality', q);
    fid = fopen(f, 'r'); buf = fread(fid, Inf, 'uint8')'; fclose(fid);
    delete(f);
    buf = uint8(buf(:)');
end
