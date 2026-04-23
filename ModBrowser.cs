using System;
using System.Reflection;
using DNA.CastleMinerZ;
using DNA.Input;
using Microsoft.Xna.Framework;
using ModLoader;
using static ModLoader.LogSystem;

namespace ModBrowser
{
    [Priority(Priority.Low)]
    public sealed class ModBrowser : ModBase
    {
        private static bool _isShutdown = false; // Guard against double-Shutdown() on game.Exiting after reloads.

        public ModBrowser() : base("ModBrowser", new Version("1.0.0"))
        {
            var game = CastleMinerZGame.Instance;
            if (game != null)
                game.Exiting += (s, e) => Shutdown();
        }

        public override void Start()
        {
            try
            {
                _isShutdown = false;
                MBConfig.LoadApply();
                GamePatches.ApplyAllPatches();

                // Note: OnAfterReload event no longer exists in new ModManager (no unload/reload cycles)

                Log($"{MethodBase.GetCurrentMethod().DeclaringType.Namespace} loaded.");
            }
            catch (Exception ex)
            {
                Log("ModBrowser start failed: " + ex.Message);
            }
        }


        public static void Shutdown()
        {
            if (_isShutdown) { Log("ModBrowser Shutdown() skipped - already shut down."); return; }
            _isShutdown = true;

            try
            {
                // Note: OnAfterReload event no longer exists in new ModManager
                GamePatches.DisableAll();
                Log("ModBrowser shutdown complete.");
            }
            catch (Exception ex)
            {
                Log("ModBrowser shutdown failed: " + ex.Message);
            }
        }

        public override void Tick(InputManager inputManager, GameTime gameTime)
        {
        }
    }
}

