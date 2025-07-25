using Terraria;
using TerrariaApi.Server;
using TShockAPI;
using Microsoft.Xna.Framework;
using OTAPI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MultiLineFishing
{
    [ApiVersion(2, 1)]
    public class MultiLineFishing : TerrariaPlugin
    {
        public override string Name => "MultiLineFishing";
        public override string Author => "Gian + ChatGPT";
        public override string Description => "Lets players cast multiple fishing lines.";
        public override Version Version => new Version(1, 0);

        private const int MaxLines = 10;
        private static Dictionary<int, List<Vector2>> castLines = new();
        private static HashSet<int> castingPlayers = new();

        public MultiLineFishing(Main game) : base(game) { }

        public override void Initialize()
        {
            ServerApi.Hooks.GameUpdate.Register(this, OnGameUpdate);
            ServerApi.Hooks.NetGetData.Register(this, OnGetData);
            ServerApi.Hooks.ServerLeave.Register(this, OnLeave);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ServerApi.Hooks.GameUpdate.Deregister(this, OnGameUpdate);
                ServerApi.Hooks.NetGetData.Deregister(this, OnGetData);
                ServerApi.Hooks.ServerLeave.Deregister(this, OnLeave);
            }
            base.Dispose(disposing);
        }

        private void OnLeave(LeaveEventArgs args)
        {
            castLines.Remove(args.Who);
            castingPlayers.Remove(args.Who);
        }

        private void OnGameUpdate(EventArgs args)
        {
            foreach (var pair in castLines)
            {
                int playerId = pair.Key;
                var player = Main.player[playerId];

                for (int i = 0; i < pair.Value.Count; i++)
                {
                    Vector2 target = pair.Value[i];
                    var projIndex = Projectile.NewProjectile(
                        Terraria.Entity.GetSource_NaturalSpawn(),
                        player.Center,
                        Vector2.Zero,
                        Terraria.ID.ProjectileID.BobberWooden,
                        0,
                        0f,
                        playerId
                    );

                    if (projIndex >= 0 && projIndex < Main.projectile.Length)
                    {
                        var proj = Main.projectile[projIndex];
                        proj.aiStyle = Terraria.ID.ProjectileID.BobberWooden;
                        proj.position = target;
                        proj.velocity = Vector2.Zero;
                        proj.netUpdate = true;
                    }
                }
            }
        }

        private void OnGetData(GetDataEventArgs args)
        {
            if (args.MsgID == PacketTypes.PlayerUpdate)
            {
                var reader = new BinaryReader(args.Msg.ReadBuffer);
                byte playerId = reader.ReadByte();
                Player player = Main.player[playerId];

                if (player.controlUseItem && player.HeldItem != null &&
                    player.HeldItem.fishingPole > 0 &&
                    !castingPlayers.Contains(playerId))
                {
                    castingPlayers.Add(playerId);

                    var lines = new List<Vector2>();
                    var angleStep = MathF.PI / (MaxLines + 1);

                    for (int i = 1; i <= MaxLines; i++)
                    {
                        float angle = -MathF.PI / 2 + angleStep * i;
                        Vector2 direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 20f;
                        lines.Add(player.Center + direction);
                    }

                    castLines[playerId] = lines;
                }

                if (!player.controlUseItem)
                {
                    castingPlayers.Remove(playerId);
                }
            }
        }
    }
}