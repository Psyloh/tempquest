using Vintagestory.API.Common;

namespace VsQuest
{
    public static class ModClassRegistry
    {
        public static void RegisterAll(ICoreAPI api)
        {
            api.RegisterEntityBehaviorClass("questgiver", typeof(EntityBehaviorQuestGiver));
            api.RegisterEntityBehaviorClass("questtarget", typeof(EntityBehaviorQuestTarget));
            api.RegisterEntityBehaviorClass("bossnametag", typeof(EntityBehaviorBossNameTag));
            api.RegisterEntityBehaviorClass("alegacyvsquestbosshealthbar", typeof(EntityBehaviorBossHealthbarOverride));
            api.RegisterEntityBehaviorClass("bossrespawn", typeof(EntityBehaviorBossRespawn));
            api.RegisterEntityBehaviorClass("bossdespair", typeof(EntityBehaviorBossDespair));
            api.RegisterEntityBehaviorClass("bosscombatmarker", typeof(EntityBehaviorBossCombatMarker));
            api.RegisterEntityBehaviorClass("bosshuntcombatmarker", typeof(EntityBehaviorBossHuntCombatMarker));
            api.RegisterEntityBehaviorClass("bossmusicurl", typeof(EntityBehaviorBossMusicUrlController));
            api.RegisterEntityBehaviorClass("bosshastargetsync", typeof(EntityBehaviorBossHasTargetSync));
            api.RegisterEntityBehaviorClass("bosssummonritual", typeof(EntityBehaviorBossSummonRitual));
            api.RegisterEntityBehaviorClass("bossgrowthritual", typeof(EntityBehaviorBossGrowthRitual));
            api.RegisterEntityBehaviorClass("bossrebirth2", typeof(EntityBehaviorBossRebirth2));
            api.RegisterEntityBehaviorClass("bosscastphase", typeof(EntityBehaviorBossCastPhase));
            api.RegisterEntityBehaviorClass("bossdash", typeof(EntityBehaviorBossDash));
            api.RegisterEntityBehaviorClass("bossteleport", typeof(EntityBehaviorBossTeleport));
            api.RegisterEntityBehaviorClass("bosshook", typeof(EntityBehaviorBossHook));
            api.RegisterEntityBehaviorClass("bossgrab", typeof(EntityBehaviorBossGrab));
            api.RegisterEntityBehaviorClass("bossdamageshield", typeof(EntityBehaviorBossDamageShield));
            api.RegisterEntityBehaviorClass("bossintoxaura", typeof(EntityBehaviorBossIntoxicationAura));
            api.RegisterEntityBehaviorClass("bossoxygendrainaura", typeof(EntityBehaviorBossOxygenDrainAura));
            api.RegisterEntityBehaviorClass("bosscloning", typeof(EntityBehaviorBossCloning));
            api.RegisterEntityBehaviorClass("bossrandomlightning", typeof(EntityBehaviorBossRandomLightning));
            api.RegisterEntityBehaviorClass("bossplayerclone", typeof(EntityBehaviorBossPlayerClone));
            api.RegisterEntityBehaviorClass("bossformswap", typeof(EntityBehaviorBossFormSwap));
            api.RegisterEntityBehaviorClass("bossformswaplist", typeof(EntityBehaviorBossFormSwapList));
            api.RegisterEntityBehaviorClass("shiverdebug", typeof(EntityBehaviorShiverDebug));

            api.RegisterItemClass("ItemDebugTool", typeof(ItemDebugTool));
            api.RegisterItemClass("ItemEntitySpawner", typeof(ItemEntitySpawner));

            api.RegisterBlockClass("BlockCooldownPlaceholder", typeof(BlockCooldownPlaceholder));
            api.RegisterBlockEntityClass("CooldownPlaceholder", typeof(BlockEntityCooldownPlaceholder));

            api.RegisterBlockClass("BlockQuestSpawner", typeof(BlockQuestSpawner));
            api.RegisterBlockEntityClass("QuestSpawner", typeof(BlockEntityQuestSpawner));

            api.RegisterBlockClass("BlockBossHuntAnchor", typeof(BlockBossHuntAnchor));
            api.RegisterBlockEntityClass("BossHuntAnchor", typeof(BlockEntityBossHuntAnchor));
        }
    }
}
