using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using Il2CppInterop.Runtime.Injection;
using UnityEngine;

namespace PCBS2MaxSalePrice
{
    [BepInPlugin(Guid, Name, Version)]
    public sealed class Plugin : BasePlugin
    {
        public const string Guid = "com.gabriel.pcbs2.maxsaleprice";
        public const string Name = "PCBS2 Max Sale Price";
        public const string Version = "0.2.1";

        internal static new ManualLogSource Log;
        internal static ConfigEntry<KeyCode> RemoteSaleKey;

        public override void Load()
        {
            Log = base.Log;
            RemoteSaleKey = Config.Bind("General", "RemoteSaleKey", KeyCode.N,
                "Mirando un PC, abre el selector remoto de mostrador de venta.");

            ClassInjector.RegisterTypeInIl2Cpp<MaxSalePriceController>();
            ClassInjector.RegisterTypeInIl2Cpp<RemoteSaleController>();
            var host = new GameObject("PCBS2MaxSalePrice");
            Object.DontDestroyOnLoad(host);
            host.hideFlags = HideFlags.HideAndDontSave;
            host.AddComponent<MaxSalePriceController>();
            host.AddComponent<RemoteSaleController>();

            Log.LogInfo($"[MaxSalePrice] v{Version} cargado. Venta remota='{RemoteSaleKey.Value}'.");
        }
    }
}
