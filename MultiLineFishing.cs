using Terraria;
using TerrariaApi.Server;
using TShockAPI;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

namespace MultiLineFishing
{
    [ApiVersion(2, 1)]
    public class MultiLineFishing : TerrariaPlugin
    {
        public override string Name => "MultiLineFishing";
        public override string Author => "MK + ChatGPT";
        public override string Description => "Lets players cast multiple fishing lines.";
        public override Version Version => new Version(1, 0);

        private const int MaxLines = 10;
        private static HashSet<int> castingPlayers = new();

        public MultiLineFishing(Main game) : base(game) { }

        public override void Initialize()
        {
            ServerApi.Hooks.NetGetData.Register(this, OnGetData);
            ServerApi.Hooks.ServerLeave.Register(this, OnLeave);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ServerApi.Hooks.NetGetData.Deregister(this, OnGetData);
                ServerApi.Hooks.ServerLeave.Deregister(this, OnLeave);
            }
            base.Dispose(disposing);
        }

        private void OnLeave(LeaveEventArgs args)
        {
            castingPlayers.Remove(args.Who);
        }

        private void OnGetData(GetDataEventArgs args)
        {
            if (args.MsgID != PacketTypes.PlayerUpdate)
                return;

            int playerId = args.Msg.whoAmI;
            Player player = Main.player[playerId];

            if (player == null || !player.active || player.dead)
                return;

            // Check if the player is using a fishing rod
            if (!player.controlUseItem || player.HeldItem?.fishingPole <= 0)
            {
                castingPlayers.Remove(playerId);
                return;
            }

            // Prevent multiple spawns during same use
            if (castingPlayers.Contains(playerId))
                return;

            castingPlayers.Add(playerId);

            float spreadAngle = 30f; // degrees
            float step = spreadAngle / (MaxLines - 1);
            float startAngle = -spreadAngle / 2;

            for (int i = 0; i < MaxLines; i++)
            {
                float angle = startAngle + (step * i);
                Vector2 velocity = player.DirectionTo(Main.MouseWorld).RotatedBy(MathHelper.ToRadians(angle)) * 12f;

                int projIndex = Projectile.NewProjectile(
                    null,
                    player.MountedCenter,
                    velocity,
                    Terraria.ID.ProjectileID.BobberWooden,
                    0,
                    0f,
                    playerId
                );

                if (projIndex >= 0 && projIndex < Main.maxProjectiles)
                {
                    Main.projectile[projIndex].netUpdate = true;
                }
            }
        }
    }
}