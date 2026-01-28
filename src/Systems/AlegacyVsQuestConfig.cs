using System.Collections.Generic;



namespace VsQuest

{

    public class AlegacyVsQuestConfig

    {

        public BossHuntCoreConfig BossHunt { get; set; } = new BossHuntCoreConfig();



		public BossCombatCoreConfig BossCombat { get; set; } = new BossCombatCoreConfig();



		public QuestTickCoreConfig QuestTick { get; set; } = new QuestTickCoreConfig();



		public ActionItemsCoreConfig ActionItems { get; set; } = new ActionItemsCoreConfig();



		public ClientCoreConfig Client { get; set; } = new ClientCoreConfig();



        public class BossHuntCoreConfig

        {

            public bool Debug { get; set; } = false;



            public double SoftResetIdleHours { get; set; } = 1.0;

            public double SoftResetAntiSpamHours { get; set; } = 0.25;

            public double RelocatePostponeHours { get; set; } = 0.25;

            public double BossEntityScanIntervalHours { get; set; } = 1.0 / 60.0;

            public double DebugLogThrottleHours { get; set; } = 0.02;



            public List<string> SkipBossKeys { get; set; } = new List<string>

            {

                "vsquestdebugging:bosshunt:breathbreaker"

            };

        }



		public class BossCombatCoreConfig

		{

			public double BossKillCreditMinShareCeil { get; set; } = 0.5;

			public double BossKillCreditMinShareFloor { get; set; } = 0.08;

			public float BossKillHealFraction { get; set; } = 0.17f;

		}



		public class QuestTickCoreConfig

		{

			public double MissingQuestLogThrottleHours { get; set; } = 1.0 / 60.0;

			public double PassiveCompletionThrottleHours { get; set; } = 1.0 / 3600.0;

		}



		public class ActionItemsCoreConfig

		{

			public int HotbarEnforcerMaxSlotsPerTick { get; set; } = 64;



			public string BossHuntTrackerActionItemId { get; set; } = "albase:bosshunt-tracker";

			public float BossHuntTrackerCastDurationSec { get; set; } = 3f;

			public float BossHuntTrackerCastSlowdown { get; set; } = -0.5f;

			public string BossHuntTrackerCastSpeedStatKey { get; set; } = "alegacyvsquest:actionitemcast";



			public int InventoryScanIntervalMs { get; set; } = 1000;

			public int HotbarEnforceIntervalMs { get; set; } = 500;

		}



		public class ClientCoreConfig

		{

			public BossMusicCoreConfig BossMusic { get; set; } = new BossMusicCoreConfig();

			public ViewDistanceFogCoreConfig ViewDistanceFog { get; set; } = new ViewDistanceFogCoreConfig();

		}



		public class BossMusicCoreConfig

		{

			public float VolumeMul { get; set; } = 0.3f;

			public float DefaultFadeOutSeconds { get; set; } = 2f;

		}



		public class ViewDistanceFogCoreConfig

		{

			public int TickIntervalMs { get; set; } = 100;

			public float BaseDensity { get; set; } = 0.00125f;

			public float FogMinMul { get; set; } = 0.03f;

			public float NegativeFogDensityAddMul { get; set; } = 0.006f;

			public float PositiveFogDensitySubMul { get; set; } = 0.0009f;

		}

    }

}

