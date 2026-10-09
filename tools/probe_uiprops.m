%% 探针：列出 Unity 仪表盘用到的所有 UI 控件的真实属性名
% 背景：uigauge 没有 Title 属性（我写错了），所以先把每个控件的属性列表打出来，
%       免得改完一个又撞下一个。用 meta.class 查，不需要图形界面。

classes = { ...
    'matlab.ui.control.SemicircularGauge', ...
    'matlab.ui.control.LinearGauge', ...
    'matlab.ui.control.Lamp', ...
    'matlab.ui.control.Slider', ...
    'matlab.ui.control.Switch', ...
    'matlab.ui.control.Label', ...
    'matlab.ui.control.DropDown', ...
    'matlab.ui.control.Button', ...
    'matlab.ui.control.NumericEditField', ...
    'matlab.ui.control.UIAxes'};

for i = 1:numel(classes)
    fprintf('\n===== %s =====\n', classes{i});
    c = meta.class.fromName(classes{i});
    if isempty(c)
        fprintf('  (类不存在)\n');
        continue;
    end

    names = {};
    while ~isempty(c)
        for k = 1:numel(c.PropertyList)
            p = c.PropertyList(k);
            if ~p.Constant
                names{end+1} = p.Name;   %#ok<SAGROW>
            end
        end
        if isempty(c.SuperclassList), break; end
        c = c.SuperclassList(1);
    end

    names = unique(names);
    fprintf('  %s\n', strjoin(names, ', '));
end

%% 特别确认：这几个我想用的属性到底存不存在
fprintf('\n===== 重点确认 =====\n');
probe('matlab.ui.control.SemicircularGauge', {'Limits','MajorTicks','MinorTicks', ...
      'ScaleColors','ScaleColorLimits','Label','Title','FontSize','Value','Orientation'});
probe('matlab.ui.control.LinearGauge', {'Limits','MajorTicks','MajorTickLabels', ...
      'ScaleColors','ScaleColorLimits','Label','Title','Orientation','Value'});
probe('matlab.ui.control.Lamp', {'Color','Position'});
probe('matlab.ui.control.Label', {'Text','FontColor','BackgroundColor','VerticalAlignment', ...
      'HorizontalAlignment','FontSize','FontWeight'});
probe('matlab.ui.control.Slider', {'Limits','Value','MajorTicks','ValueChangedFcn'});
probe('matlab.ui.control.Switch', {'Value','Items'});
probe('matlab.ui.control.DropDown', {'Items','Value','ValueChangedFcn'});

function probe(clsName, props)
    c = meta.class.fromName(clsName);
    all = {};
    while ~isempty(c)
        for k = 1:numel(c.PropertyList)
            all{end+1} = c.PropertyList(k).Name;   %#ok<AGROW>
        end
        if isempty(c.SuperclassList), break; end
        c = c.SuperclassList(1);
    end
    all = unique(all);

    fprintf('\n%s\n', clsName);
    for j = 1:numel(props)
        if any(strcmp(all, props{j}))
            fprintf('  [有] %s\n', props{j});
        else
            fprintf('  [无] %s   <<< 要改\n', props{j});
        end
    end
end
