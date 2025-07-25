using System;
using Terraria;
using Terraria.DataStructures;
using TerrariaApi.Server;
using TShockAPI;
using OTAPI;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Microsoft.Xna.Framework;

namespace MultiLineFishing
{
    [ApiVersion(2, 1)]
    public class MultiLineFishing : TerrariaPlugin
    {
        public override string Name => "MultiLineFishing";
        public override string Author => "ChatGPT amd MK";
        public override string Description => "Allows fishing rods to cast multiple lines with per-player config.";
        public override Version Version => new Version(1, 1, 0);

        private static string SavePath => Path.Combine(TShock.SavePath, "MultilineFishing.json");
        private static Dictionary<string, PlayerConfig> playerConfigs = new();

        public MultiLineFishing(Main game) : base(game) { }

        public override void Initialize()
        {
            LoadConfig();
            ServerApi.Hooks.NetSendData.Register(this, OnSendData);
            ServerApi.Hooks.ServerLeave.Register(this, OnLeave);
            ServerApi.Hooks.ServerJoin.Register(this, OnJoin);
            Commands.ChatCommands.Add(new Command("multilinefishing.toggle", ToggleFishing, "multilinefishing", "mlf", "fishinglines"));
        }

        private void OnJoin(JoinEventArgs args)
        {
            var player = TShock.Players[args.Who];
            if (player == null || !player.RealPlayer)
                return;

            var uuid = player.UUID;
            if (!playerConfigs.TryGetValue(uuid, out var config))
                return;

            if (config.Enabled)
                player.SendInfoMessage($"[c/55FF55:MultiLineFishing Enabled: ✅ | Extra Lines: {config.ExtraLines}]");
            else
                player.SendInfoMessage("[c/FF5555:MultiLineFishing Enabled: ❌]");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ServerApi.Hooks.NetSendData.Deregister(this, OnSendData);
                ServerApi.Hooks.ServerLeave.Deregister(this, OnLeave);
                ServerApi.Hooks.ServerJoin.Deregister(this, OnJoin);
                SaveConfig();
            }
            base.Dispose(disposing);
        }

        private void ToggleFishing(CommandArgs args)
        {
            var player = args.Player;

            if (!player.RealPlayer)
            {
                player.SendErrorMessage("Only real players can use this command.");
                return;
            }

            var uuid = player.UUID;

            if (!playerConfigs.ContainsKey(uuid))
                playerConfigs[uuid] = new PlayerConfig();

            if (args.Parameters.Count == 0)
            {
                playerConfigs[uuid].Enabled = !playerConfigs[uuid].Enabled;
                player.SendSuccessMessage($"Multi-line fishing is now {(playerConfigs[uuid].Enabled ? "enabled" : "disabled")}.");
            }
            else if (args.Parameters.Count == 1 && int.TryParse(args.Parameters[0], out int lineCount))
            {
                if (lineCount < 1 || lineCount > 10)
                {
                    player.SendErrorMessage("Please enter a line count between 1 and 10.");
                    return;
                }

                playerConfigs[uuid].ExtraLines = lineCount;
                playerConfigs[uuid].Enabled = true;
                player.SendSuccessMessage($"Multi-line fishing enabled with {lineCount} extra {(lineCount == 1 ? "line" : "lines")}.");
            }
            else
            {
                player.SendErrorMessage("Usage: /multilinefishing [extraLines 1-5]");
                return;
            }

            SaveConfig();
        }

        private void OnLeave(LeaveEventArgs args)
        {
            SaveConfig();
        }

        private void OnSendData(SendDataEventArgs args)
        {
            if (args.MsgId != PacketTypes.ProjectileNew)
                return;

            var projectile = Main.projectile[args.number];
            if (!projectile.friendly || projectile.aiStyle != 61) // Check if bobber
                return;

            var player = TShock.Players[projectile.owner];
            if (player == null || !player.Active || !player.RealPlayer)
                return;

            var uuid = player.UUID;
            if (!playerConfigs.ContainsKey(uuid) || !playerConfigs[uuid].Enabled)
                return;

            int extraLines = playerConfigs[uuid].ExtraLines;

            float angleSpread = 20f; // total degrees spread
            float step = extraLines == 1 ? 0 : angleSpread / (extraLines - 1);

            float startAngle = -angleSpread / 2;

            for (int i = 0; i < extraLines; i++)
            {
                float angle = startAngle + i * step;
                Vector2 newVelocity = projectile.velocity.RotatedBy(MathHelper.ToRadians(angle));
                int newProj = Projectile.NewProjectile(
                    new EntitySource_Parent(projectile),
                    projectile.position.X,
                    projectile.position.Y,
                    newVelocity.X,
                    newVelocity.Y,
                    projectile.type,
                    projectile.damage,
                    projectile.knockBack,
                    projectile.owner
                );

                if (newProj >= 0 && newProj < Main.maxProjectiles)
                {
                Main.projectile[newProj].netUpdate = true;

                TShock.Log.ConsoleInfo($"Spawned extra bobber {i + 1}/{extraLines} for player {player.Name}");
            }
        }

        private void LoadConfig()
        {
            try
            {
                if (!File.Exists(SavePath))
                {
                    playerConfigs = new();
                    return;
                }

                string json = File.ReadAllText(SavePath);
                playerConfigs = JsonSerializer.Deserialize<Dictionary<string, PlayerConfig>>(json) ?? new();
            }
            catch
            {
                TShock.Log.ConsoleError("[MultiLineFishing] Failed to load config. Resetting.");
                playerConfigs = new();
            }
        }

        private void SaveConfig()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SavePath)!);
                File.WriteAllText(SavePath, JsonSerializer.Serialize(playerConfigs, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch
            {
                TShock.Log.ConsoleError("[MultiLineFishing] Failed to save config.");
            }
        }
    }
}