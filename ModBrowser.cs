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
                MBConfig.LoadApply();
                GamePatches.ApplyAllPatches();
                Log($"{MethodBase.GetCurrentMethod().DeclaringType.Namespace} loaded.");
            }
            catch (Exception ex)
            {
                Log("ModBrowser start failed: " + ex.Message);
            }
        }

        public static void Shutdown()
        {
            try
            {
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

