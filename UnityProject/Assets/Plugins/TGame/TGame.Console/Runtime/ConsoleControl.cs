using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using TGame.Console.Command;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace TGame.Console
{
    public static class ConsoleControl
    {
        public static List<CommandContainer> cmdList = new();
        public static Dictionary<string, CommandContainer> cmdMap = new();
        public static Dictionary<Type, MethodInfo> parseMap = new();
        public static Dictionary<Type, MethodInfo> valueTipMap = new();


        public static void Init()
        {
            BakeCommand();
            BakeStringToValue();
            BakeValueTip();
        }
        
        public static void BakeStringToValue()
        {
            parseMap.Clear();
            Debug.Log("BakeStringToValue");
            var assemblies =  AppDomain.CurrentDomain.GetAssemblies();
            foreach (var assembly in assemblies)
            {
                var types = assembly.GetTypes();
                foreach (var type in types)
                {
                    var properties = type.GetProperties(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                    foreach (var prop in properties)
                    {
                        var attr = prop.GetCustomAttribute<ValueTipAttribute>();
                        if (attr == null) continue;
                        valueTipMap[attr.ValueType] = prop.GetMethod;
                    }

                    var methods = type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                    foreach (var method in methods)
                    {
                        var attr = method.GetCustomAttribute<ValueTipAttribute>();
                        if (attr == null) continue;
                        valueTipMap[attr.ValueType] = method;
                    }
                }
                foreach (var type in types)
                {
                    var methods = type.GetMethods();
                    var methodInfos = methods.Where(info => Attribute.IsDefined(info, typeof(StringToValueAttribute)));
                    foreach (var methodInfo in methodInfos)
                    {
                        var ats = methodInfo.GetCustomAttributes<StringToValueAttribute>();
                        foreach (var attribute in ats)
                        {
                            var valueType = attribute.ValueType;
                            parseMap[valueType] = methodInfo;
                        }
                    }
                    var properties = type.GetProperties();
                    var propertyInfos = properties.Where(info => Attribute.IsDefined(info, typeof(StringToValueAttribute)));
                    foreach (var propertyInfo in propertyInfos)
                    {
                        var ats = propertyInfo.GetCustomAttributes<StringToValueAttribute>();
                        foreach (var attribute in ats)
                        {
                            var valueType = attribute.ValueType;
                            parseMap[valueType] = propertyInfo.GetMethod;
                        }
                    }
                }
            }

            
        }

        public static void BakeValueTip()
        {
            valueTipMap.Clear();
            Debug.Log("BakeValueTip");
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var assembly in assemblies)
            {
                var types = assembly.GetTypes();
                foreach (var type in types)
                {
                    var properties = type.GetProperties(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                    foreach (var prop in properties)
                    {
                        var attr = prop.GetCustomAttribute<ValueTipAttribute>();
                        if (attr == null) continue;
                        valueTipMap[attr.ValueType] = prop.GetMethod;
                    }

                    var methods = type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                    foreach (var method in methods)
                    {
                        var attr = method.GetCustomAttribute<ValueTipAttribute>();
                        if (attr == null) continue;
                        valueTipMap[attr.ValueType] = method;
                    }
                }
            }
        }

        public static void BakeCommand()
        {
            cmdList.Clear();
            cmdMap.Clear();
            Debug.Log("BakeCommand");
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            List<Type> types = new();
            foreach (var assembly in assemblies)
            {
                types.AddRange(assembly.GetTypes());
            }
            var commandList = types
                .Where(t => Attribute.IsDefined(t, typeof(CommandAttribute)));
            foreach (var type in commandList)
            {
                var commandAttribute = type.GetCustomAttribute<CommandAttribute>();
                if(commandAttribute is null)
                    continue;
                if(!Debug.isDebugBuild && commandAttribute.IsDebug)
                    continue;
                var methods = type.GetMethods();
                foreach (var method in methods)
                {
                    var commandMethodAttribute = method.GetCustomAttribute<CommandMethodAttribute>();
                    if(commandMethodAttribute is null)
                        continue;
                    if(!Debug.isDebugBuild && commandMethodAttribute.IsDebug)
                        continue;
                    CommandContainer container = new CommandContainer();
                    var cmdName = string.IsNullOrEmpty(commandAttribute.Name) ? type.Name : commandAttribute.Name;
                    var methodName = string.IsNullOrEmpty(commandMethodAttribute.Name) ? method.Name : commandMethodAttribute.Name;
                    container.CommandName = cmdName;
                    container.MethodName = methodName;
                    container.CommandNote = commandMethodAttribute.NoteStr;
                    container.MethodInfo = method;
                    var parameters = method.GetParameters();
                    foreach (var parameterInfo in parameters)
                    {
                        CommandParameter parameter = new CommandParameter();
                        var parameterAttribute = parameterInfo.GetCustomAttribute<CommandParameterAttribute>();
                        if (parameterAttribute is not null)
                        {
                            parameter.Name = parameterAttribute.Name;
                            var parameterType = parameterInfo.ParameterType;
                            var getTipFunc = parameterAttribute.GetTipListFunc;
                            if (!string.IsNullOrEmpty(getTipFunc))
                            {
                                var getListMethod =
                                    methods.FirstOrDefault(_ => _.Name == parameterAttribute.GetTipListFunc);
                                if(getListMethod != default)
                                    parameter.GetList = Delegate.CreateDelegate(typeof(Func<IEnumerable<string>>),getListMethod) as Func<IEnumerable<string>>;
                            }
                            else if(parameterAttribute.UseDefaultTip)
                            {
                                if(valueTipMap.TryGetValue(parameterType,out var methodInfo))
                                    parameter.GetList = Delegate.CreateDelegate(typeof(Func<IEnumerable<string>>),methodInfo) as Func<IEnumerable<string>>;
                            }

                        }

                        parameter.ValueType = parameterInfo.ParameterType;
                        parameter.Name ??= parameterInfo.Name;
                        if (parameterInfo.IsDefined(typeof(ParamArrayAttribute), false))
                        {
                            parameter.ValueType = parameterInfo.ParameterType.GetElementType();
                            parameter.IsDySize = true;
                        }
                        container.Parameters.Add(parameter);
                    }
                    cmdMap[$"/{container.CommandName} /{container.MethodName}"] = container;
                }
            }
            cmdList.AddRange(cmdMap.Values.ToList());
        }
        public static void CommandTipList(string command, in HashSet<CommandTip> tipList)
        {
            tipList.Clear();
            if (string.IsNullOrEmpty(command) || !command.StartsWith("/"))
                return;

            string trimmed = command.TrimEnd();
            bool endsWithSpace = command.Length > 0 && command[command.Length - 1] == ' ';
            string[] tokens = trimmed.Split(' ');

            if (tokens.Length < 1 || !tokens[0].StartsWith("/"))
                return;

            // Level 1: only class token typed (e.g. "/" or "/Ap" or "/Application")
            if (tokens.Length == 1)
            {
                GetClassLevelTips(tokens[0], tipList);
                return;
            }

            // Token 1 must be a method token (starts with "/")
            if (!tokens[1].StartsWith("/"))
                return;

            string classToken = tokens[0];
            string methodInput = tokens[1];
            string exactKey = $"{classToken} {methodInput}";

            // Exact command match → show param overview or value suggestions
            if (cmdMap.TryGetValue(exactKey, out var container))
            {
                GetCommandDetailTips(container, command, endsWithSpace, tipList);
                return;
            }

            // Level 2: partial method match
            GetMethodLevelTips(classToken, methodInput, tipList);
        }

        static void GetClassLevelTips(string classInput, HashSet<CommandTip> tipList)
        {
            var seen = new HashSet<string>();
            foreach (var (_, container) in cmdMap)
            {
                string classPrefix = "/" + container.CommandName;
                if (!seen.Add(classPrefix))
                    continue;

                if (classInput.Length <= 1 || classPrefix.StartsWith(classInput, StringComparison.OrdinalIgnoreCase))
                {
                    StringBuilder sb = new StringBuilder();
                    sb.Append($"<color=#FFAA00>{classPrefix}</color>");
                    if (!string.IsNullOrEmpty(container.CommandNote))
                        sb.Append($" <color=#888888>[{container.CommandNote}]</color>");

                    tipList.Add(new CommandTip
                    {
                        ShowStr = sb.ToString(),
                        InputStr = classPrefix + " /",
                        Command = container,
                        TipType = CommandTipType.ClassLevel
                    });
                }
            }
        }

        static void GetMethodLevelTips(string classToken, string methodInput, HashSet<CommandTip> tipList)
        {
            foreach (var (_, container) in cmdMap)
            {
                string cmdClass = "/" + container.CommandName;
                if (!cmdClass.Equals(classToken, StringComparison.OrdinalIgnoreCase))
                    continue;

                string cmdMethod = "/" + container.MethodName;
                if (!cmdMethod.StartsWith(methodInput, StringComparison.OrdinalIgnoreCase))
                    continue;

                StringBuilder sb = new StringBuilder();
                sb.Append($"<color=#FFAA00>{cmdClass}</color> /<color=white>{container.MethodName}</color>");

                if (container.Parameters.Count > 0)
                {
                    foreach (var p in container.Parameters)
                        sb.Append($" <color=#55FF55><{p.Name}></color>");
                }
                else
                {
                    sb.Append($" <color=#888888>✓ 立即执行</color>");
                }

                if (!string.IsNullOrEmpty(container.CommandNote))
                    sb.Append($" <color=#888888>[{container.CommandNote}]</color>");

                tipList.Add(new CommandTip
                {
                    ShowStr = sb.ToString(),
                    InputStr = $"{cmdClass} /{container.MethodName} ",
                    Command = container,
                    TipType = CommandTipType.MethodLevel
                });
            }
        }

        static void GetCommandDetailTips(CommandContainer container, string fullCommand, bool endsWithSpace, HashSet<CommandTip> tipList)
        {
            // Determine which parameter the user is currently on
            // Full command format: /ClassName /MethodName #param1 value1 #param2 ...
            string[] tokens = fullCommand.TrimEnd().Split(' ');
            string lastToken = tokens.Length > 0 ? tokens[^1] : "";

            // Check if user just finished a #paramName token (ends with space after #xxx)
            // → show value suggestions for that parameter
            if (endsWithSpace && lastToken.StartsWith("#"))
            {
                string paramName = lastToken.Substring(1);
                var param = container.Parameters.Find(p =>
                    p.Name.Equals(paramName, StringComparison.OrdinalIgnoreCase));
                if (param?.GetList != null)
                {
                    foreach (var val in param.GetList())
                    {
                        tipList.Add(new CommandTip
                        {
                            ShowStr = $"  <color=#FFAA00>{val}</color>",
                            InputStr = val,
                            Command = container,
                            TipType = CommandTipType.ValueTip
                        });
                    }
                }
                return;
            }

            // Check if the last token is a value preceded by #param
            // e.g. "/Time /SetTimeScale #S 0.5" → filter value suggestions
            int hashIndex = -1;
            for (int i = 0; i < tokens.Length; i++)
            {
                if (tokens[i].StartsWith("#"))
                {
                    hashIndex = i;
                    // Next token (if exists and doesn't start with #) is the value
                    if (i + 1 < tokens.Length && !tokens[i + 1].StartsWith("#") && !tokens[i + 1].StartsWith("/"))
                    {
                        // User is typing a value for this param
                        string paramName = tokens[i].Substring(1);
                        string valueInput = tokens[i + 1];
                        var param = container.Parameters.Find(p =>
                            p.Name.Equals(paramName, StringComparison.OrdinalIgnoreCase));
                        if (param?.GetList != null)
                        {
                            foreach (var val in param.GetList())
                            {
                                if (val.StartsWith(valueInput, StringComparison.OrdinalIgnoreCase))
                                {
                                    tipList.Add(new CommandTip
                                    {
                                        ShowStr = $"  <color=#FFAA00>{val}</color>",
                                        InputStr = val,
                                        Command = container,
                                        TipType = CommandTipType.ValueTip
                                    });
                                }
                            }
                        }
                        return;
                    }
                }
            }

            // Default: show all parameters of this command as a usage hint
            StringBuilder sb = new StringBuilder();
            sb.Append($"<color=#FFAA00>/{container.CommandName}</color> /<color=white>{container.MethodName}</color>");
            foreach (var p in container.Parameters)
                sb.Append($" <color=#55FF55>#{p.Name}({p.ValueType.Name})</color>");
            if (!string.IsNullOrEmpty(container.CommandNote))
                sb.Append($" <color=#888888>[{container.CommandNote}]</color>");

            tipList.Add(new CommandTip
            {
                ShowStr = sb.ToString(),
                InputStr = $"/{container.CommandName} /{container.MethodName} ",
                Command = container,
                TipType = CommandTipType.MethodLevel
            });
        }

        private static List<object> parameters = new();
        private static Dictionary<string, string> parameterMap = new();

        public static void ExecuteCommand(string command)
        {
            if (!command.StartsWith("/"))
            {
                Debug.LogError("指令格式错误");
                return;
            }

            Debug.Log($"ExecuteCommand:<color=green>{command}</color>");

            var parts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
            {
                Debug.LogError("指令不完整: 需要 /ClassName /MethodName");
                return;
            }

            if (!parts[0].StartsWith("/") || !parts[1].StartsWith("/"))
            {
                Debug.LogError("指令格式错误: 类名和方法名应以 / 开头");
                return;
            }

            string cmdName = parts[0].Substring(1);
            string cmdMethod = parts[1].Substring(1);
            if (string.IsNullOrEmpty(cmdName) || string.IsNullOrEmpty(cmdMethod))
            {
                Debug.LogError("指令为空，解析失败");
                return;
            }

            string title = $"/{cmdName} /{cmdMethod}";
            if (!cmdMap.TryGetValue(title, out var container))
            {
                Debug.LogWarning($"[{title}] 未找到指令");
                return;
            }

            if (container.Parameters.Count <= 0)
            {
                container.MethodInfo.Invoke(null, null);
                return;
            }

            // Parse #paramName value pairs
            parameterMap.Clear();
            int i = 2;
            while (i < parts.Length)
            {
                if (!parts[i].StartsWith("#"))
                {
                    Debug.LogError($"参数格式错误: '{parts[i]}' 应以 # 开头");
                    return;
                }
                string paramName = parts[i].Substring(1);
                i++;

                if (i >= parts.Length)
                {
                    Debug.LogError($"参数 #{paramName} 缺少值");
                    return;
                }
                string paramValue = parts[i];
                i++;

                parameterMap[paramName] = paramValue;
            }

            if (parameterMap.Count != container.Parameters.Count)
            {
                var expected = string.Join(", ", container.Parameters.ConvertAll(p => $"#{p.Name}"));
                var got = string.Join(", ", new List<string>(parameterMap.Keys).ConvertAll(k => $"#{k}"));
                Debug.LogError($"[{title}] 参数数量不匹配: 期望 [{expected}], 实际 [{got}]");
                return;
            }

            parameters.Clear();
            foreach (var param in container.Parameters)
            {
                if (!parameterMap.TryGetValue(param.Name, out var pValue))
                {
                    Debug.LogError($"[{title}] 缺少参数 #{param.Name}");
                    return;
                }

                if (param.IsDySize)
                {
                    var strList = pValue.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    var extra = Array.CreateInstance(param.ValueType, strList.Length);
                    for (int j = 0; j < strList.Length; j++)
                    {
                        if (!TryCreatObject(strList[j], param.ValueType, out var obj))
                        {
                            Debug.LogError($"[{title}] 参数 #{param.Name} 值 '{strList[j]}' 转换失败");
                            return;
                        }
                        extra.SetValue(obj, j);
                    }
                    parameters.Add(extra);
                }
                else
                {
                    if (!TryCreatObject(pValue, param.ValueType, out var obj))
                    {
                        Debug.LogError($"[{title}] 参数 #{param.Name} 值 '{pValue}' 转换失败");
                        return;
                    }
                    parameters.Add(obj);
                }
            }

            try
            {
                container.MethodInfo.Invoke(null, parameters.ToArray());
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[{title}] 指令执行失败: {e.Message}");
            }
        }
        static bool TryCreatObject(string value, Type valueType,out object obj,bool isDySize = false)
        {
            obj = null;
            if (!parseMap.TryGetValue(valueType, out var methodInfo))
                return false;
            try
            {
                obj = methodInfo.Invoke(null, new object[]{value});
            }
            catch (Exception e)
            {
                return false;
            }

            return true;
        }
    }
}