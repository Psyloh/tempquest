using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace VsQuest
{
    public class InLandObjective : ActionObjectiveBase
    {
        public override bool IsCompletable(IPlayer byPlayer, params string[] args)
        {
            return TryGetClaimName(byPlayer, out string name) && NameMatches(args, name);
        }

        public override List<int> GetProgress(IPlayer byPlayer, params string[] args)
        {
            bool ok = IsCompletable(byPlayer, args);
            return ok
                ? new List<int>(new int[] { 1, 1 })
                : new List<int>(new int[] { 0, 1 });
        }

        private static bool NameMatches(string[] args, string claimName)
        {
            if (args == null || args.Length < 1) return false;
            if (string.IsNullOrWhiteSpace(claimName)) return false;

            string expected = args[0];
            if (string.IsNullOrWhiteSpace(expected)) return false;

            return string.Equals(expected.Trim(), claimName.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryGetClaimName(IPlayer byPlayer, out string claimName)
        {
            claimName = null;
            if (byPlayer.Entity?.Pos == null) return false;

            BlockPos pos = byPlayer.Entity.Pos.AsBlockPos;

            var claimsApi = byPlayer?.Entity?.World?.Claims;
            if (claimsApi == null) return false;

            var claims = claimsApi.Get(pos);
            if (claims == null || claims.Length == 0) return false;

            for (int i = 0; i < claims.Length; i++)
            {
                var desc = claims[i]?.Description;
                if (!string.IsNullOrWhiteSpace(desc))
                {
                    claimName = desc;
                    return true;
                }

                var ownerName = claims[i]?.LastKnownOwnerName;
                if (!string.IsNullOrWhiteSpace(ownerName))
                {
                    claimName = ownerName;
                    return true;
                }
            }

            return false;
        }
    }
}
