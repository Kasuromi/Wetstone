using System;
using System.Runtime.InteropServices;
using BepInEx.Unity.IL2CPP.Hook;
using ProjectM.Network;
using Stunlock.Network;
using Unity.Collections;
using Unity.Entities;
using Wetstone.API;
using Wetstone.Util;
using static NetworkEvents_Serialize;

namespace Wetstone.Network;

/// Contains the serialization hooks for custom packets.
internal static class SerializationHooks
{
    // chosen by fair dice roll
    internal const int WETSTONE_NETWORK_EVENT_ID = 0x000FD00D;

    private static INativeDetour? _serializeDetour;
    private static INativeDetour? _deserializeDetour;

    // Detour events.
    public static void Initialize()
    {
        unsafe
        {
            _serializeDetour = NativeHookUtil.Detour(typeof(NetworkEvents_Serialize), "SerializeEvent", SerializeHook, out SerializeOriginal);
            _deserializeDetour = NativeHookUtil.Detour(typeof(NetworkEvents_Serialize), "DeserializeEvent", DeserializeHook, out DeserializeOriginal);
        }
    }

    // Undo detours.
    public static void Uninitialize()
    {
        _serializeDetour?.Dispose();
        _deserializeDetour?.Dispose();
    }

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    public unsafe delegate void SerializeEvent(EntityManager entityManager, NetworkEventType networkEventType, ref NetBufferOut netBuffer, Entity entity);

    public static SerializeEvent? SerializeOriginal;

    public unsafe static void SerializeHook(EntityManager entityManager, NetworkEventType networkEventType, ref NetBufferOut netBuffer, Entity entity)
    {
        // if this is not a custom event, just call the original function
        if (networkEventType.EventId != SerializationHooks.WETSTONE_NETWORK_EVENT_ID)
        {
            SerializeOriginal!(entityManager, networkEventType, ref netBuffer, entity);
            return;
        }

        // extract the custom network event
        var data = (CustomNetworkEvent)VWorld.Server.EntityManager.GetComponentObject<Il2CppSystem.Object>(entity, CustomNetworkEvent.ComponentType);

        // write out the event ID and the data
        netBuffer.Write((uint)SerializationHooks.WETSTONE_NETWORK_EVENT_ID);
        data.Serialize(ref netBuffer);
    }

    // --------------------------------------------------------------------------------------

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    public unsafe delegate void DeserializeEvent(EntityCommandBuffer commandBuffer, ref NetBufferIn netBuffer, ref DeserializeNetworkEventParams eventParams);

    public static DeserializeEvent? DeserializeOriginal;

    public unsafe static void DeserializeHook(EntityCommandBuffer commandBuffer, ref NetBufferIn netBuffer, ref DeserializeNetworkEventParams eventParams)
    {
        // read event ID, and if it's not our custom event, call the original function
        var eventId = netBuffer.ReadUInt32();
        if (eventId != SerializationHooks.WETSTONE_NETWORK_EVENT_ID)
        {
            // rewind the buffer
            netBuffer.m_readPosition -= 32;

            DeserializeOriginal!(commandBuffer, ref netBuffer, ref eventParams);
            return;
        }

        var typeName = netBuffer.ReadString(Allocator.Temp);
        if (MessageRegistry._eventHandlers.ContainsKey(typeName))
        {
            var handler = MessageRegistry._eventHandlers[typeName];
            var isFromServer = eventParams.FromCharacter.User == Entity.Null;

            try
            {
                if (isFromServer)
                    handler.OnReceiveFromServer(ref netBuffer);
                else
                    handler.OnReceiveFromClient(eventParams.FromCharacter, ref netBuffer);
            }
            catch (Exception ex)
            {
                WetstonePlugin.Logger.LogError($"Error handling incoming network event {typeName}:");
                WetstonePlugin.Logger.LogError(ex);
            }
        }
    }
}