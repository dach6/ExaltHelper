using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;

namespace ExaltHelper;

[Serializable]
public class Settings
{
	private sealed class SettingsSerializationBinder : SerializationBinder
	{
		public override Type BindToType(string assemblyName, string typeName)
		{
			if (assemblyName.StartsWith("ExaltHelper") || assemblyName.StartsWith("ExaltKitGUI") || assemblyName.StartsWith("MultiTool"))
			{
				if (typeName.StartsWith("ExaltKitGUI."))
				{
					string newTypeName = "ExaltHelper." + typeName.Substring("ExaltKitGUI.".Length);
					Type mappedType = Assembly.GetExecutingAssembly().GetType(newTypeName);
					if (mappedType != null)
					{
						return mappedType;
					}
				}
				Type type = Assembly.GetExecutingAssembly().GetType(typeName);
				if (type != null)
				{
					return type;
				}
			}
			return Type.GetType(typeName + ", " + assemblyName);
		}
	}

	public static string Path = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ExaltMultiTool_Settings.bin");

	private static Settings _default = null;

	public bool AutoLootStackTokens;

	public string ExaltLauncherPath = "";

	public string ExaltLauncherSteamPath = "";

	public string[] SavedAccounts = new string[0];

	public Account[] SavedAccountsEx = new Account[0];

	public bool EnableAutoNexus = true;

	public bool EnableAutoPotHP = true;

	public bool EnableAutoPotMP = true;

	public int AutoNexusPercentageThreshold = 25;

	public bool EnableAutoAbility = true;

	public bool EnableAutoLoot = true;

	public int AutoLootWeaponTierThreshold = 11;

	public int AutoLootArmorTierThreshold = 11;

	public int AutoLootRingTierThreshold = 6;

	public int AutoLootAbilityTierThreshold = 6;

	public bool AutoLootStatPotions = true;

	public bool AutoLootUTs = true;

	public bool AutoLootHealingPotions = true;

	public bool AutoLootManaPotions = true;

	public bool AutoLootHpConsumable = true;

	public bool IgnoreBlind = true;

	public bool IgnoreHallucinating = true;

	public bool IgnoreDrunk = true;

	public bool IgnoreConfused = true;

	public bool IgnoreUnstable;

	public bool IgnoreDarkness = true;

	public bool IgnoreQuiet;

	public bool IgnoreWeak;

	public bool IgnoreSlowed;

	public bool IgnoreSick;

	public bool IgnoreDazed;

	public bool IgnoreStunned;

	public bool IgnoreParalyzed;

	public bool IgnoreBleeding;

	public bool IgnoreArmorBreak;

	public bool IgnorePetStasis;

	public bool IgnorePetrified;

	public bool IgnoreSilence;

	public bool IgnoreCurse;

	public bool IgnoreDrought;

	public bool EnableSafeWalk;

	public bool SafeWalkInShatters;

	public bool AntiLagIgnoreEffects = true;

	public bool AutoAbilityClosestEnemy;

	public bool AutoAbilityWeakestEnemy;

	public int AutoAbilityCustomDelay = 200;

	public bool AutoAbilityPenetratingBlastOffset;

	public int AutoAbilityMinimumEnemyHealthThreshold = 200;

	public int AutoAbilityMinimumManaLeftThreshold = 100;

	public int AbilityMinimumEnemyGroupSize = 3;

	public bool AutoAbilityStrongestEnemy = true;

	public bool EnableTeleportToPlayerCommand = true;

	public bool EnableTeleportToPlayerClosestToQuestCommand = true;

	public bool EnableAntiDebuffs = true;

	public bool IgnoreShowEffects;

	public bool BlockDamageNumbers;

	public int AutoAbilityHealHpPercent = 45;

	public bool EnemyIgnore;

	public bool EnableGlow = true;

	public bool PurpleGlow;

	public bool NoGlow;

	public bool AutoNexusShowInformation = true;

	public bool EnableCustomNexus = true;

	public bool EnableConnectCommand = true;

	public bool EnableGotoCommand = true;

	public bool DisableSystemMessages;

	public bool EnableBazaarTimer = true;

	public int[] FameIngoredEnemies = new int[0];

	public bool AutoAbilityNotifications;

	public string[] AntiLagIgnoredEffects = new string[6] { "Stream", "Line", "Burst", "Flow", "Ring", "Coneblast" };

	public bool EnableAntiAFK = true;

	public int AntiLagAllyPlayerSize = 100;

	public bool AntiLagHideNoPets;

	public bool AntiLagHideAllyPets = true;

	public bool AntiLagHideAllPets;

	public bool AntiLagApplyToGuildMates;

	public bool AutoLootQuests = true;

	public bool EnableMapHack = true;

	public bool AutoLootMarks;

	public int AutoNexusDrinkThreshold = 40;

	public int AutoNexusDrinkMpThreshold = 20;

	public Guid ID = Guid.NewGuid();

	public bool AutoLootOverFillMP;

	public bool AutoLootOverFillHP;

	public bool AutoLootBigBags;

	public bool AutoAbilityAutoMP = true;

	public bool AutoNexusDrinkFromInventory = true;

	public bool ShowRealLHPot = true;

	public bool SafeWalkToggle;

	public bool EnableTeleportToSelf = true;

	public bool EnableLocCommand = true;

	public bool AutoLootEggs;

	public bool AutoNexusReplaceFameWithHealth;

	public int AutoNexusHpPotDelay = 400;

	public int AutoAbilityMinimumGroupSizeThreshold = 1;

	public bool AutoLootAutoDisable = true;

	public bool AutoLootDelay = true;

	public bool AutoNexusUseAnyHealingItem = true;

	public bool TutorialSkipEnabled = true;

	public bool EnemyHighlight = true;

	public bool QueueBypassEscape = true;

	public bool AutoAbilityMysticTargetSelf = true;

	public bool DisableHotkeys;

	public bool EnableSlowWalk;

	public int SlowWalkMultiplier = 25;

	public bool SlowWalkPercentOrSpeed = true;

	public bool ResetClientHp;

	public string TeleportAnchorTarget = "";

	public bool HideInjectPopup;

	public bool EnableAutoNexusOnly = true;

	public int AntiLagPlayerSize = 100;

	public bool EnableO3Helper = true;

	public bool O3IgnoreShield = true;

	public bool O3IgnoreCoins = true;

	public bool O3IgnoreDammah = true;

	public bool DisableSendingIp;

	public bool AutoLootMoveConsumables;

	public int LastLauncherIndex;

	public bool AutoAbilityPeacekeeperSpellbomb;

	public bool HideBattlepassXp;

	public bool AutoNexusSyncHp = true;

	public bool AutoNexusUseClientHp = true;

	public bool AutoAbilityChargeDruidMeter;

	public bool AutoNexusInstantNexus = true;

	public int CurrentPort;

	public HotkeyHandler[] Hotkeys = Array.Empty<HotkeyHandler>();

	public bool AutoAimEnabled = true;

	public bool AutoAimModeMouse;

	public bool AutoAimModeHighestHP;

	public bool AutoAimModeClosest = true;

	public double AutoAimRangeLead = 1.0;

	public double AutoAimMouseDist = 2.0;

	public bool AutoAimFocusBoss = true;

	public bool AutoAimIgnoreWalls = true;

	public bool AutoAimShootInvulnerable;

	public bool AutoAimReverseCultStaff = true;

	public bool AutoAimOffsetColossusSword;

	public bool AutoAimProjectileNoclip;

	public bool AutoAimShootWhileStealthed;

	public int FpsForeground = 60;

	public int FpsBackground = 60;

	public bool FpsVsync = true;

	public object this[string name]
	{
		get
		{
			if (name == "AutoNexusReplaceFameWithHealth")
			{
				return false;
			}
			return GetType().GetField(name).GetValue(this);
		}
		set
		{
			if (name == "AutoNexusReplaceFameWithHealth")
			{
				AutoNexusReplaceFameWithHealth = false;
				return;
			}
			GetType().GetField(name).SetValue(this, value);
		}
	}

	public static Settings Default
	{
		get
		{
			if (_default == null)
			{
				ReadSettings();
			}
			return _default;
		}
	}

	public static event EventHandler SettingsChanged;

	public void Change()
	{
		SettingsChanged?.Invoke(this, EventArgs.Empty);
	}

	public void Save()
	{
		lock (Path)
		{
			using FileStream serializationStream = File.OpenWrite(Path);
			new BinaryFormatter().Serialize(serializationStream, _default);
		}
	}

	public static void DeleteSettings()
	{
		lock (Path)
		{
			if (File.Exists(Path))
			{
				File.Delete(Path);
			}
		}
	}

	private static void ReadSettings()
	{
		if (File.Exists(Path))
		{
			using (FileStream fileStream = File.OpenRead(Path))
			{
				_default = (Settings)new BinaryFormatter
				{
					Binder = new SettingsSerializationBinder()
				}.Deserialize(fileStream);
				_default.AutoNexusReplaceFameWithHealth = false;
				try
				{
					fileStream.Position = 0L;
					byte[] array = new byte[fileStream.Length];
					if (fileStream.Read(array, 0, array.Length) >= 10)
					{
						string text = Encoding.UTF8.GetString(array);
						if (!text.Contains("EnableAutoNexusOnly"))
						{
							_default.EnableAutoNexusOnly = true;
						}
						if (!text.Contains("AntiLagPlayerSize"))
						{
							_default.AntiLagPlayerSize = 100;
						}
						if (!text.Contains("EnableO3Helper"))
						{
							_default.EnableO3Helper = true;
						}
						if (!text.Contains("O3IgnoreShield"))
						{
							_default.O3IgnoreShield = true;
						}
						if (!text.Contains("AutoAbilityPeacekeeperSpellbomb"))
						{
							_default.AutoAbilityPeacekeeperSpellbomb = true;
						}
						if (!text.Contains("FpsForeground"))
						{
							_default.FpsForeground = 60;
							_default.FpsBackground = 60;
							_default.FpsVsync = true;
						}
						if (!text.Contains("AutoAimShootWhileStealthed"))
						{
							_default.AutoAimShootWhileStealthed = true;
						}
						if (!text.Contains("AutoNexusSyncHp"))
						{
							_default.AutoNexusSyncHp = true;
						}
						if (!text.Contains("AutoNexusUseClientHp"))
						{
							_default.AutoNexusUseClientHp = true;
						}
						if (!text.Contains("AutoNexusInstantNexus"))
						{
							_default.AutoNexusInstantNexus = true;
						}
						int num = 0;
						FieldInfo[] fields = typeof(Settings).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
						foreach (FieldInfo fieldInfo in fields)
						{
							if (fieldInfo.FieldType == typeof(string) && !(fieldInfo.GetValue(_default) is string))
							{
								num++;
								fieldInfo.SetValue(_default, string.Empty);
							}
						}
						if (_default.SavedAccounts == null)
						{
							num++;
						}
						if (_default.SavedAccountsEx == null)
						{
							num++;
						}
						if (_default.FameIngoredEnemies == null)
						{
							num++;
						}
						if (_default.AntiLagIgnoredEffects == null)
						{
							num++;
						}
						if (_default.Hotkeys == null)
						{
							num++;
						}
						if (num > 7)
						{
							_default = new Settings();
						}
					}
					return;
				}
				catch (Exception exception)
				{
					Console.WriteLine($"Failed setting default values: {exception}");
					return;
				}
			}
		}
		_default = new Settings();
	}

	private static bool FileContainsField(FileStream fs, string fieldName)
	{
		fs.Position = 0L;
		byte[] array = new byte[fs.Length];
		fs.Read(array, 0, array.Length);
		return Encoding.UTF8.GetString(array).Contains(fieldName);
	}

	[OnDeserialized]
	private void After(StreamingContext sc)
	{
		AutoNexusReplaceFameWithHealth = false;
		if (Hotkeys == null)
		{
			Hotkeys = Array.Empty<HotkeyHandler>();
		}
		if (!AutoAimModeClosest && !AutoAimModeMouse && !AutoAimModeHighestHP)
		{
			AutoAimEnabled = true;
			AutoAimModeMouse = false;
			AutoAimModeHighestHP = false;
			AutoAimModeClosest = true;
			AutoAimRangeLead = 1.0;
			AutoAimMouseDist = 2.0;
			AutoAimFocusBoss = true;
			AutoAimIgnoreWalls = true;
			AutoAimShootInvulnerable = false;
			AutoAimReverseCultStaff = true;
			AutoAimOffsetColossusSword = false;
			AutoAimProjectileNoclip = false;
			FpsForeground = 60;
			FpsBackground = 60;
			FpsVsync = true;
		}
		if (SavedAccounts != null && (SavedAccountsEx == null || SavedAccounts.Any()))
		{
			SavedAccountsEx = (from account in SavedAccounts
				select account.Replace("steamworks:", "steamworks_").Replace("kongregate:", "kongregate") into account
				select account.Split(':') into split
				select new Account
				{
					Label = split[0],
					Email = split[0],
					Password = split[1],
					Icon = 0
				}).ToArray();
			SavedAccounts = Array.Empty<string>();
		}
	}
}
