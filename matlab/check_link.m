%% Unity <-> MATLAB 链路自检
%
% 【什么时候用它】
%   仪表盘显示"无连接"、或者报"端口 5005 被占用"的时候。
%   它会告诉你：端口到底被谁占着、该怎么恢复。
%
% 【怎么跑】
%   在 MATLAB 命令窗口敲这两行：
%     cd('C:\Users\cheng\WorkBuddy AI\2026-09-21-22-43-24\matlab')
%     check_link
%
% 【注意】
%   跑之前先把旧的仪表盘图窗关掉，否则它自己也占着 5005。

fprintf('\n========== Unity <-> MATLAB 链路自检 ==========\n\n');

%% 1. 找出存活的 udpport 对象（残留的"僵尸端口"）
fprintf('[1] 检查残留的 udpport 对象\n');
objs = [];
try
    objs = udpportfind;
catch
    fprintf('    本机 MATLAB 没有 udpportfind，跳过这一步\n');
end

if isempty(objs)
    fprintf('    ✔ 没有存活的 udpport 对象\n');
else
    for k = 1:numel(objs)
        try
            fprintf('    ! 发现 udpport #%d：LocalPort = %s，Status = %s\n', ...
                k, string(objs(k).LocalPort), string(objs(k).Status));
        catch
            fprintf('    ! 发现 udpport #%d（读属性失败）\n', k);
        end
    end
    fprintf('    → 正在清理这些残留对象…\n');
    try
        delete(objs);
        pause(0.3);
        fprintf('    ✔ 已清理\n');
    catch ME
        fprintf('    ✘ 清理失败：%s\n', ME.message);
    end
end

%% 2. 测试 5005（MATLAB 监听 Unity 状态的口）
fprintf('\n[2] 测试绑定 5005（MATLAB 收 Unity 状态的口）\n');
ok5005 = false;
try
    u = udpport("datagram", "IPV4", "LocalPort", 5005, "Timeout", 0.05);
    fprintf('    ✔ 绑定成功 —— 端口空闲，可以跑 unity_dashboard\n');
    ok5005 = true;
    delete(u);
    pause(0.2);
catch ME
    fprintf('    ✘ 绑定失败：%s\n', ME.message);
    fprintf('    → 5005 被别的程序或残留句柄占着。\n');
    fprintf('      最省事的办法：直接关掉 MATLAB 重开。\n');
end

%% 3. 测试 5006（Unity 监听 MATLAB 指令的口）
fprintf('\n[3] 测试绑定 5006（Unity 收 MATLAB 指令的口）\n');
try
    u2 = udpport("datagram", "IPV4", "LocalPort", 5006, "Timeout", 0.05);
    fprintf('    ✔ 绑定成功 —— 说明 Unity 现在没在 Play\n');
    delete(u2);
catch ME
    fprintf('    ✘ 绑定失败：%s\n', ME.message);
    fprintf('    → 说明 Unity 正在 Play，占着 5006（这是正常的）\n');
end

%% 4. 结论
fprintf('\n[4] 下一步\n');
if ok5005
    fprintf('    5005 已经可用了。按这个顺序跑：\n');
    fprintf('      ① 先在 MATLAB 敲  unity_dashboard   （它先开始监听，等着）\n');
    fprintf('      ② 再回 Unity 按 Play\n');
    fprintf('    顺序反了也能用，但前提是 Unity 一直还在 Play 中。\n');
else
    fprintf('    5005 还占着 —— 先重启 MATLAB，再回来跑上面两步。\n');
end
fprintf('\n=============================================\n');
