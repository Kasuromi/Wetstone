
using System;
using System.Reflection;
using BepInEx.Unity.IL2CPP.Hook;
using HarmonyLib;

namespace Wetstone.Util;

public static class NativeHookUtil
{
    public static INativeDetour Detour<T>(string ty, string methodName, T to, out T original) where T : System.Delegate?
    {
        return Detour(Type.GetType(ty), methodName, to, out original);
    }

    public static INativeDetour Detour<T>(Type ty, string methodName, T to, out T original) where T : System.Delegate?
    {
        var method = ty.GetMethod(methodName, AccessTools.all);
        return Detour(method, to, out original);
    }

    public static INativeDetour Detour<T>(MethodInfo method, T to, out T original) where T : System.Delegate?
    {
        var address = Il2CppMethodResolver.ResolveFromMethodInfo(method);
        WetstonePlugin.Logger.LogInfo($"Detouring {method.DeclaringType.FullName}.{method.Name} at {address.ToString("X")}");
        return INativeDetour.CreateAndApply(address, to, out original);
    }
}
