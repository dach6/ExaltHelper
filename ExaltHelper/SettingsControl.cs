using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using ExaltHelper.Proxy;
using ExaltHelper.Proxy.Networking;

namespace ExaltHelper;

internal class SettingsControl : UserControl
{
	private readonly SynchronizationContext _uiCtx;

	private readonly HashSet<Keys> _down = new HashSet<Keys>();

	private Dictionary<Keys, HotkeyHandler> _hotkeyMap;

	private bool focused;

	private static IntPtr hook = IntPtr.Zero;

	private static Win32.LowLevelKeyboardProc proc = null;

	public Dictionary<string, Control> SettingsControls = new Dictionary<string, Control>();

	private IContainer components;

	private CheckBox chkAutoAbilityMysticTargetSelf;

	private CheckBox chkAutoAbilityPenetratingBlastOffset;

	private Label lblAutoAbilityCustomDelayMilliseconds;

	private NumericUpDown numAutoAbilityCustomDelay;

	private Label lblAutoAbilityCustomDelay;

	private NumericUpDown numAutoNexusHpPotDelay;

	private Label lblAutoDrinkDelayMilliseconds;

	private Label lblAutoDrinkDelay;

	private Panel pnlSafeWalk;

	private CheckBox chkSafeWalkInShatters;

	private Panel sepSafeWalk;

	private CheckBox chkEnableSafeWalk;

	private Panel pnlOther;

	private CheckBox chkEnableTeleportToPlayerClosestToQuestCommand;

	private CheckBox chkEnableTeleportToPlayerCommand;

	private Panel sepOther;

	private Panel pnlAntiDebuffs;

	private Panel sepServerSide;

	private Panel sepClientSide;

	private CheckBox chkIgnoreSilence;

	private CheckBox chkIgnorePetrified;

	private CheckBox chkIgnorePetStasis;

	private CheckBox chkIgnoreArmorBreak;

	private CheckBox chkIgnoreBleeding;

	private CheckBox chkIgnoreParalyzed;

	private CheckBox chkIgnoreStunned;

	private CheckBox chkIgnoreDazed;

	private CheckBox chkIgnoreSick;

	private CheckBox chkIgnoreSlowed;

	private CheckBox chkIgnoreWeak;

	private CheckBox chkIgnoreQuiet;

	private Label lblServerSideDebuffs;

	private CheckBox chkIgnoreDarkness;

	private CheckBox chkIgnoreUnstable;

	private CheckBox chkIgnoreConfused;

	private CheckBox chkIgnoreDrunk;

	private CheckBox chkIgnoreHallucinating;

	private CheckBox chkIgnoreBlind;

	private Label lblClientSideDebuffs;

	private Panel sepAntiDebuffs;

	private CheckBox chkEnableAntiDebuffs;

	private Panel pnlAutoLoot;

	private NumericUpDown numAutoLootRingTierThreshold;

	private Label lblMinimumRingTier;

	private NumericUpDown numAutoLootAbilityTierThreshold;

	private Label lblMinimumAbilityTier;

	private CheckBox chkAutoLootUTs;

	private CheckBox chkAutoLootHealingPotions;

	private CheckBox chkAutoLootStatPotions;

	private NumericUpDown numAutoLootArmorTierThreshold;

	private Label lblMinimumArmorTier;

	private NumericUpDown numAutoLootWeaponTierThreshold;

	private Label lblMinimumWeaponTier;

	private Panel sepAutoLoot;

	private CheckBox chkEnableAutoLoot;

	private Panel pnlAutoAbility;

	private Panel sepAutoAbility;

	private CheckBox chkEnableAutoAbility;

	private Panel pnlAutoNexus;

	private Label lblAutoNexusAfter;

	private Panel sepAutoNexus;

	private NumericUpDown numAutoNexusPercentageThreshold;

	private CheckBox chkEnableAutoNexus;

	private Label lblMinimumEnemyHPAfter;

	private NumericUpDown numAutoAbilityMinimumEnemyHealthThreshold;

	private Label lblMinimumEnemyHP;

	private Label lblMinimumMPLeftAfter;

	private NumericUpDown numAutoAbilityMinimumManaLeftThreshold;

	private Label lblMinimumMPLeft;

	private NumericUpDown numAutoAbilityMinimumGroupSizeThreshold;

	private Label lblMinimumEnemyGroupSize;

	private Label lblMinimumEnemyGroupSizeAfter;

	private RadioButton chkAutoAbilityStrongestEnemy;

	private RadioButton chkAutoAbilityWeakestEnemy;

	private RadioButton chkAutoAbilityClosestEnemy;

	private Label lblAutoAbilityHealHpPercentAfter;

	private NumericUpDown numAutoAbilityHealHpPercent;

	private Label lblAutoAbilityHealHpPercent;

	private CheckBox chkAutoNexusShowInformation;

	private CheckBox chkEnableCustomNexus;

	private Panel pnlConnection;

	private Panel sepConnection;

	private Label lblConnection;

	private CheckBox chkEnableConnectCommand;

	private Label lblTools;

	private CheckBox chkEnableGotoCommand;

	private CheckBox chkDisableSystemMessages;

	private CheckBox chkEnableBazaarTimer;

	private CheckBox chkAutoAbilityNotifications;

	private CheckBox chkEnableAntiAFK;

	private Panel pnlAntiLag;

	private Label label1;

	private Panel panel2;

	private LinkLabel btnEditIgnoredEffectList;

	private CheckBox chkAntiLagIgnoreEffects;

	private NumericUpDown numAntiLagAllyPlayerSize;

	private Label lblAntiLagAllySize;

	private Label label2;

	private CheckBox chkAntiLagApplyToGuildMates;

	private CheckBox chkAutoLootQuests;

	private CheckBox chkEnableMapHack;

	private CheckBox chkAutoLootMarks;

	private CheckBox chkAutoLootOverFillHP;

	private CheckBox chkAutoLootOverFillMP;

	private CheckBox chkAutoLootBigBags;

	private Panel pnlO3;

	private CheckBox chkO3IgnoreDammah;

	private CheckBox chkO3IgnoreCoins;

	private CheckBox chkO3IgnoreShield;

	private Panel sepO3;

	private CheckBox chkEnableO3Helper;

	private CheckBox chkEnableTeleportToSelf;

	private CheckBox chkShowRealLHPot;

	private CheckBox chkAutoAbilityAutoMP;

	private CheckBox chkEnableLocCommand;

	private CheckBox chkAutoLootEggs;

	private CheckBox chkAutoNexusReplaceFameWithHealth;

	private CheckBox chkAutoLootAutoDisable;

	private CheckBox chkAutoLootDelay;

	private ToolTip ttDescriptions;

	private CheckBox chkAutoNexusDrinkFromInventory;

	private Label label3;

	private NumericUpDown numAutoNexusDrinkThreshold;

	private Label label5;

	private NumericUpDown numAutoNexusDrinkMpThreshold;

	private CheckBox chkEnableAutoPotHP;

	private CheckBox chkEnableAutoPotMP;

	private CheckBox chkQueueBypassEscape;

	private Panel pnlAutoAim;

	private Label lblAutoAimRangeLead;

	private NumericUpDown numAutoAimRangeLead;

	private RadioButton chkAutoAimModeMouse;

	private RadioButton chkAutoAimModeHighestHP;

	private RadioButton chkAutoAimModeClosest;

	private CheckBox chkAutoAimIgnoreWalls;

	private Panel panel3;

	private CheckBox chkAutoAimEnabled;

	private Label lblAutoAimMouseDist;

	private NumericUpDown numAutoAimMouseDist;

	private CheckBox chkAutoAimFocusBoss;

	private CheckBox chkAutoAimShootInvulnerable;

	private Button btnHotkeys;

	private System.Windows.Forms.Timer tmrForeground;

	private RadioButton chkNoGlow;

	private RadioButton chkPurpleGlow;

	private RadioButton chkEnableGlow;

	private Label lblAutoAimAcceleratingWeaponWarning;

	private CheckBox chkAutoAimOffsetColossusSword;

	private RadioButton chkAntiLagHideAllPets;

	private RadioButton chkAntiLagHideNoPets;

	private RadioButton chkAntiLagHideAllyPets;

	private CheckBox chkDisableHotkeys;

	private CheckBox chkResetClientHp;

	private Label lblTeleportAnchor;

	private TextBox txtTeleportAnchorTarget;

	private CheckBox chkHideInjectPopup;

	private CheckBox chkEnableAutoNexusOnly;

	private CheckBox chkIgnoreDrought;

	private Label lblAntiLagPlayerSize;

	private NumericUpDown numAntiLagPlayerSize;

	private Label lblO3;

	private Label label4;

	private CheckBox chkEnableSlowWalk;

	private NumericUpDown numSlowWalkMultiplier;

	private Label lblSlowWalkHotkey;

	private CheckBox chkAutoAimProjectileNoclip;

	private CheckBox chkDisableSendingIp;

	private CheckBox chkAutoLootManaPotions;

	private CheckBox chkAutoLootMoveConsumables;

	private CheckBox chkAutoLootStackTokens;

	private Panel panel1;

	private Label label7;

	private Panel panel4;

	private Button btnCopySupportId;

	private CheckBox chkAutoAbilityPeacekeeperSpellbomb;

	private CheckBox chkIgnoreCurse;

	private Panel pnlFps;

	private CheckBox chkFpsVsync;

	private Panel sepFps;

	private Label lblFpsForeground;

	private NumericUpDown numFpsForeground;

	private Label lblFpsBackground;

	private NumericUpDown numFpsBackground;

	private Label lblFps;

	private Label lblFpsVsyncWarning;

	private CheckBox chkHideBattlepassXp;

	private CheckBox chkSlowWalkPercentOrSpeed;

	private CheckBox chkAutoAimShootWhileStealthed;

	private CheckBox chkAutoNexusSyncHp;

	private CheckBox chkAutoNexusUseClientHp;

	private CheckBox chkAutoAbilityChargeDruidMeter;

	private CheckBox chkAutoNexusInstantNexus;

	public SettingsControl()
	{
		base.HandleDestroyed += SettingsControl_HandleDestroyed;
		InitializeComponent();
		ParseSettings(this);
		Scan(this);
		UpdateHotkeys();
		_uiCtx = SynchronizationContext.Current;
	}

	private void UpdateHotkeys()
	{
		_hotkeyMap = (from h in Settings.Default.Hotkeys
			group h by NormalizeKey(h.Key)).ToDictionary((IGrouping<Keys, HotkeyHandler> g) => g.Key, (IGrouping<Keys, HotkeyHandler> g) => g.First());
	}

	private void SettingsControl_Load(object sender, EventArgs e)
	{
		Settings.SettingsChanged += delegate
		{
			Invoke((MethodInvoker)delegate
			{
				ParseSettings(this);
			});
		};
		proc = KeyboardHookCallback;
		if (!Settings.Default.DisableHotkeys)
		{
			InstallHook();
		}
		btnHotkeys.Enabled = true;
	}

	public void InstallHook()
	{
		using Process process = Process.GetCurrentProcess();
		using ProcessModule processModule = process.MainModule;
		IntPtr moduleHandle = Win32.GetModuleHandle(processModule.ModuleName);
		if (moduleHandle == IntPtr.Zero)
		{
			Program.ShowError("Failed to initialize Hotkey system, it will be disabled for now.", base.ParentForm);
			return;
		}
		hook = Win32.SetWindowsHookEx(13, proc, moduleHandle, 0u);
		if (hook == IntPtr.Zero)
		{
			Program.ShowError("Failed to hook Hotkey system, it will be disabled for now.", base.ParentForm);
		}
	}

	public void DisableHook()
	{
		Win32.UnhookWindowsHookEx(hook);
	}

	private void SettingsControl_HandleDestroyed(object sender, EventArgs e)
	{
		if (hook == IntPtr.Zero)
		{
			return;
		}
		try
		{
			Win32.UnhookWindowsHookEx(hook);
		}
		catch
		{
		}
	}

	private void tmrForeground_Tick(object sender, EventArgs e)
	{
		IntPtr foregroundWindow = Win32.GetForegroundWindow();
		if (foregroundWindow == IntPtr.Zero)
		{
			focused = false;
			return;
		}
		if (!Convert.ToBoolean(Win32.GetWindowThreadProcessId(foregroundWindow, out var lpdwProcessId)))
		{
			focused = false;
			return;
		}
		bool flag = false;
		try
		{
			Process processById = Process.GetProcessById((int)lpdwProcessId);
			if (processById.HasExited)
			{
				focused = false;
				return;
			}
			flag = processById.ProcessName == "RotMG Exalt";
		}
		catch (ArgumentException)
		{
			focused = false;
			return;
		}
		focused = Debugger.IsAttached | flag;
	}

	private void Scan(Control parent)
	{
		Type[] source = new Type[4]
		{
			typeof(CheckBox),
			typeof(RadioButton),
			typeof(NumericUpDown),
			typeof(TextBox)
		};
		foreach (Control control in parent.Controls)
		{
			if (!source.Contains(control.GetType()))
			{
				Scan(control);
				continue;
			}
			string key = control.Name.Substring(3);
			SettingsControls[key] = control;
		}
	}

	private static Keys NormalizeKey(Keys key)
	{
		switch (key)
		{
		case Keys.LShiftKey:
		case Keys.RShiftKey:
			return Keys.ShiftKey;
		case Keys.LControlKey:
		case Keys.RControlKey:
			return Keys.ControlKey;
		default:
			return key;
		}
	}

	private IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
	{
		if (nCode >= 0)
		{
			int num = wParam.ToInt32();
			bool isDown = num == 256;
			bool flag = num == 257;
			if ((isDown | flag) && Volatile.Read(ref focused))
			{
				Keys keys = NormalizeKey((Keys)Marshal.PtrToStructure<Win32.KBDLLHOOKSTRUCT>(lParam).vkCode);
				if (isDown)
				{
					if (!_down.Add(keys))
					{
						return Win32.CallNextHookEx(hook, nCode, wParam, lParam);
					}
				}
				else if (flag)
				{
					_down.Remove(keys);
				}
				if (_hotkeyMap.TryGetValue(keys, out var handler) && SettingsControls.TryGetValue(handler.Setting, out var control))
				{
					_uiCtx.Post(delegate
					{
						ApplyHotkey(control, handler, isDown);
					}, null);
				}
			}
		}
		return Win32.CallNextHookEx(hook, nCode, wParam, lParam);
	}

	private void ApplyHotkey(Control control, HotkeyHandler handler, bool isDown)
	{
		if (!(control is NumericUpDown numericUpDown))
		{
			if (!(control is CheckBox checkBox))
			{
				if (!(control is RadioButton radioButton))
				{
					TextBox textBox = control as TextBox;
					if (((textBox != null) & isDown) && textBox.Name == "txtTeleportAnchorTarget")
					{
						ClientsHelper.TeleportAnchor();
					}
				}
				else if (isDown)
				{
					radioButton.Checked = !radioButton.Checked;
				}
			}
			else if (isDown)
			{
				if (checkBox.Name == "chkResetClientHp")
				{
					ClientsHelper.ResetHp();
				}
				else
				{
					checkBox.Checked = handler.Result == -2 || !checkBox.Checked;
				}
			}
			else
			{
				CheckBox checkBox2 = checkBox;
				if (!isDown && handler.Result == -2)
				{
					checkBox2.Checked = false;
				}
			}
		}
		else if (isDown)
		{
			numericUpDown.Value = handler.Result;
		}
	}

	private void ParseSettings(Control control)
	{
		if (control is NumericUpDown numericUpDown)
		{
			object value = Settings.Default[control.Name.Substring(3)];
			numericUpDown.MouseWheel += delegate(object s, MouseEventArgs e)
			{
				((HandledMouseEventArgs)e).Handled = true;
			};
			numericUpDown.Value = CommonUtils.Clamp((int)numericUpDown.Minimum, (int)numericUpDown.Maximum, Convert.ToInt32(value));
			NumericUpDown_ValueChanged(numericUpDown, EventArgs.Empty);
		}
		else if (control is CheckBox checkBox)
		{
			checkBox.Checked = (bool)Settings.Default[control.Name.Substring(3)];
			CheckBox_ValueChanged(checkBox, EventArgs.Empty);
		}
		else if (control is RadioButton radioButton)
		{
			radioButton.Checked = (bool)Settings.Default[control.Name.Substring(3)];
			RadioButton_ValueChanged(radioButton, EventArgs.Empty);
		}
		else if (control is TextBox textBox)
		{
			textBox.Text = (string)Settings.Default[control.Name.Substring(3)];
			TextBox_ValueChanged(textBox, EventArgs.Empty);
		}
		else
		{
			foreach (Control control2 in control.Controls)
			{
				ParseSettings(control2);
			}
		}
		chkPercentOrSpeed_CheckedChanged(null, null);
	}

	private void btnCopySupportId_Click(object sender, EventArgs e)
	{
		Clipboard.SetText(Settings.Default.ID.ToString());
	}

	private void NumericUpDown_ValueChanged(object sender, EventArgs e)
	{
		NumericUpDown numericUpDown = sender as NumericUpDown;
		string text = numericUpDown.Name.Substring(3);
		Settings.Default[text] = (int)numericUpDown.Value;
		if (RegistryHelper.IsRegistrySetting(text))
		{
			RegistryHelper.SetSetting("SOFTWARE\\RealmStock\\MultiTool", text, (int)numericUpDown.Value);
		}
		else if (numericUpDown == numAutoNexusPercentageThreshold)
		{
			numAutoNexusPercentageThreshold.ForeColor = ((numAutoNexusPercentageThreshold.Value >= 15m) ? Color.White : Color.Red);
		}
	}

	private void RadioButton_ValueChanged(object sender, EventArgs e)
	{
		if (!(sender is RadioButton radioButton))
		{
			return;
		}
		string text = radioButton.Name.Substring(3);
		Settings.Default[text] = radioButton.Checked;
		if (!RegistryHelper.IsRegistrySetting(text))
		{
			return;
		}
		if (radioButton.Checked)
		{
			switch (text)
			{
			case "AutoAimModeClosest":
				Settings.Default.AutoAimModeClosest = true;
				Settings.Default.AutoAimModeHighestHP = false;
				Settings.Default.AutoAimModeMouse = false;
				RegistryHelper.SetSetting("SOFTWARE\\RealmStock\\MultiTool", "AutoAimMode", 0);
				break;
			case "AutoAimModeHighestHP":
				Settings.Default.AutoAimModeHighestHP = true;
				Settings.Default.AutoAimModeClosest = false;
				Settings.Default.AutoAimModeMouse = false;
				RegistryHelper.SetSetting("SOFTWARE\\RealmStock\\MultiTool", "AutoAimMode", 1);
				break;
			case "AutoAimModeMouse":
				Settings.Default.AutoAimModeMouse = true;
				Settings.Default.AutoAimModeClosest = false;
				Settings.Default.AutoAimModeHighestHP = false;
				RegistryHelper.SetSetting("SOFTWARE\\RealmStock\\MultiTool", "AutoAimMode", 2);
				break;
			default:
				RegistryHelper.SetSetting("SOFTWARE\\RealmStock\\MultiTool", text, 1);
				break;
			}
		}
		else if (text != "AutoAimModeClosest" && text != "AutoAimModeHighestHP" && text != "AutoAimModeMouse")
		{
			RegistryHelper.SetSetting("SOFTWARE\\RealmStock\\MultiTool", text, 0);
		}
	}

	private void CheckBox_ValueChanged(object sender, EventArgs e)
	{
		CheckBox checkBox = sender as CheckBox;
		string text = checkBox.Name.Substring(3);
		Settings.Default[text] = checkBox.Checked;
		if (RegistryHelper.IsRegistrySetting(text))
		{
			RegistryHelper.SetSetting("SOFTWARE\\RealmStock\\MultiTool", text, checkBox.Checked ? 1 : 0);
		}
	}

	private void TextBox_ValueChanged(object sender, EventArgs e)
	{
		TextBox textBox = sender as TextBox;
		string name = textBox.Name.Substring(3);
		string text = textBox.Text;
		StringBuilder stringBuilder = new StringBuilder();
		string text2 = text;
		foreach (char c in text2)
		{
			if (char.IsLetter(c))
			{
				stringBuilder.Append(c);
			}
		}
		string text3 = stringBuilder.ToString();
		if (text3 != text)
		{
			textBox.Text = text3;
		}
		Settings.Default[name] = text3;
	}

	private void btnEditIgnoredEnemyList_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
	{
		new IgnoredEnemiesForm().ShowDialog();
	}

	private void btnEditIgnoredEffectList_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
	{
		new IgnoreEffectsForm().ShowDialog();
	}

	private void chkEnableGotoCommand_CheckedChanged(object sender, EventArgs e)
	{
		if (chkEnableGotoCommand.Checked != Settings.Default.EnableGotoCommand)
		{
			Program.ShowWarning("BE CAREFUL CONNECTING TO IPs\n\nYour account can be stolen if you connect to an IP that is given to you\n\nOnly connect to IPs you trust", base.ParentForm);
		}
	}

	private void btnHotkeys_Click(object sender, EventArgs e)
	{
		new HotkeysSettingsForm(this).ShowDialog();
		UpdateHotkeys();
	}

	private void chkDisableHotkeys_CheckedChanged(object sender, EventArgs e)
	{
		if (chkDisableHotkeys.Checked)
		{
			DisableHook();
		}
		else
		{
			InstallHook();
		}
	}

	private void txtTeleportAnchorName_KeyPress(object sender, KeyPressEventArgs e)
	{
		if (!char.IsControl(e.KeyChar) && !char.IsLetter(e.KeyChar))
		{
			e.Handled = true;
		}
	}

	private void chkPercentOrSpeed_CheckedChanged(object sender, EventArgs e)
	{
		if (chkSlowWalkPercentOrSpeed.Checked)
		{
			chkSlowWalkPercentOrSpeed.Text = "% Multiplier";
		}
		else
		{
			chkSlowWalkPercentOrSpeed.Text = "Set Speed";
		}
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing && components != null)
		{
			components.Dispose();
		}
		base.Dispose(disposing);
	}

	private void InitializeComponent()
	{
		this.components = new System.ComponentModel.Container();
		System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ExaltHelper.SettingsControl));
		this.pnlSafeWalk = new System.Windows.Forms.Panel();
		this.chkSlowWalkPercentOrSpeed = new System.Windows.Forms.CheckBox();
		this.chkEnableSlowWalk = new System.Windows.Forms.CheckBox();
		this.label4 = new System.Windows.Forms.Label();
		this.chkSafeWalkInShatters = new System.Windows.Forms.CheckBox();
		this.sepSafeWalk = new System.Windows.Forms.Panel();
		this.lblSlowWalkHotkey = new System.Windows.Forms.Label();
		this.chkEnableSafeWalk = new System.Windows.Forms.CheckBox();
		this.numSlowWalkMultiplier = new System.Windows.Forms.NumericUpDown();
		this.pnlOther = new System.Windows.Forms.Panel();
		this.lblTeleportAnchor = new System.Windows.Forms.Label();
		this.txtTeleportAnchorTarget = new System.Windows.Forms.TextBox();
		this.chkDisableHotkeys = new System.Windows.Forms.CheckBox();
		this.chkNoGlow = new System.Windows.Forms.RadioButton();
		this.chkPurpleGlow = new System.Windows.Forms.RadioButton();
		this.chkEnableGlow = new System.Windows.Forms.RadioButton();
		this.chkQueueBypassEscape = new System.Windows.Forms.CheckBox();
		this.chkEnableLocCommand = new System.Windows.Forms.CheckBox();
		this.chkShowRealLHPot = new System.Windows.Forms.CheckBox();
		this.chkEnableTeleportToSelf = new System.Windows.Forms.CheckBox();
		this.chkHideInjectPopup = new System.Windows.Forms.CheckBox();
		this.chkEnableMapHack = new System.Windows.Forms.CheckBox();
		this.chkEnableAntiAFK = new System.Windows.Forms.CheckBox();
		this.chkHideBattlepassXp = new System.Windows.Forms.CheckBox();
		this.chkEnableBazaarTimer = new System.Windows.Forms.CheckBox();
		this.chkDisableSystemMessages = new System.Windows.Forms.CheckBox();
		this.lblTools = new System.Windows.Forms.Label();
		this.chkEnableCustomNexus = new System.Windows.Forms.CheckBox();
		this.chkEnableTeleportToPlayerClosestToQuestCommand = new System.Windows.Forms.CheckBox();
		this.chkEnableTeleportToPlayerCommand = new System.Windows.Forms.CheckBox();
		this.sepOther = new System.Windows.Forms.Panel();
		this.pnlAntiDebuffs = new System.Windows.Forms.Panel();
		this.sepServerSide = new System.Windows.Forms.Panel();
		this.sepClientSide = new System.Windows.Forms.Panel();
		this.chkIgnoreSilence = new System.Windows.Forms.CheckBox();
		this.chkIgnorePetrified = new System.Windows.Forms.CheckBox();
		this.chkIgnorePetStasis = new System.Windows.Forms.CheckBox();
		this.chkIgnoreArmorBreak = new System.Windows.Forms.CheckBox();
		this.chkIgnoreBleeding = new System.Windows.Forms.CheckBox();
		this.chkIgnoreParalyzed = new System.Windows.Forms.CheckBox();
		this.chkIgnoreCurse = new System.Windows.Forms.CheckBox();
		this.chkIgnoreDrought = new System.Windows.Forms.CheckBox();
		this.chkIgnoreStunned = new System.Windows.Forms.CheckBox();
		this.chkIgnoreDazed = new System.Windows.Forms.CheckBox();
		this.chkIgnoreSick = new System.Windows.Forms.CheckBox();
		this.chkIgnoreSlowed = new System.Windows.Forms.CheckBox();
		this.chkIgnoreWeak = new System.Windows.Forms.CheckBox();
		this.chkIgnoreQuiet = new System.Windows.Forms.CheckBox();
		this.lblServerSideDebuffs = new System.Windows.Forms.Label();
		this.chkIgnoreDarkness = new System.Windows.Forms.CheckBox();
		this.chkIgnoreUnstable = new System.Windows.Forms.CheckBox();
		this.chkIgnoreConfused = new System.Windows.Forms.CheckBox();
		this.chkIgnoreDrunk = new System.Windows.Forms.CheckBox();
		this.chkIgnoreHallucinating = new System.Windows.Forms.CheckBox();
		this.chkIgnoreBlind = new System.Windows.Forms.CheckBox();
		this.lblClientSideDebuffs = new System.Windows.Forms.Label();
		this.sepAntiDebuffs = new System.Windows.Forms.Panel();
		this.chkEnableAntiDebuffs = new System.Windows.Forms.CheckBox();
		this.pnlAutoLoot = new System.Windows.Forms.Panel();
		this.chkAutoLootManaPotions = new System.Windows.Forms.CheckBox();
		this.chkAutoLootDelay = new System.Windows.Forms.CheckBox();
		this.chkAutoLootAutoDisable = new System.Windows.Forms.CheckBox();
		this.chkAutoLootEggs = new System.Windows.Forms.CheckBox();
		this.chkAutoLootBigBags = new System.Windows.Forms.CheckBox();
		this.chkAutoLootOverFillMP = new System.Windows.Forms.CheckBox();
		this.chkAutoLootOverFillHP = new System.Windows.Forms.CheckBox();
		this.chkAutoLootMoveConsumables = new System.Windows.Forms.CheckBox();
		this.chkAutoLootStackTokens = new System.Windows.Forms.CheckBox();
		this.chkAutoLootMarks = new System.Windows.Forms.CheckBox();
		this.chkAutoLootQuests = new System.Windows.Forms.CheckBox();
		this.numAutoLootRingTierThreshold = new System.Windows.Forms.NumericUpDown();
		this.lblMinimumRingTier = new System.Windows.Forms.Label();
		this.numAutoLootAbilityTierThreshold = new System.Windows.Forms.NumericUpDown();
		this.lblMinimumAbilityTier = new System.Windows.Forms.Label();
		this.chkAutoLootUTs = new System.Windows.Forms.CheckBox();
		this.chkAutoLootHealingPotions = new System.Windows.Forms.CheckBox();
		this.chkAutoLootStatPotions = new System.Windows.Forms.CheckBox();
		this.numAutoLootArmorTierThreshold = new System.Windows.Forms.NumericUpDown();
		this.lblMinimumArmorTier = new System.Windows.Forms.Label();
		this.numAutoLootWeaponTierThreshold = new System.Windows.Forms.NumericUpDown();
		this.lblMinimumWeaponTier = new System.Windows.Forms.Label();
		this.sepAutoLoot = new System.Windows.Forms.Panel();
		this.chkEnableAutoLoot = new System.Windows.Forms.CheckBox();
		this.pnlAutoAbility = new System.Windows.Forms.Panel();
		this.chkAutoAbilityPeacekeeperSpellbomb = new System.Windows.Forms.CheckBox();
		this.chkAutoAbilityMysticTargetSelf = new System.Windows.Forms.CheckBox();
		this.chkAutoAbilityAutoMP = new System.Windows.Forms.CheckBox();
		this.chkAutoAbilityChargeDruidMeter = new System.Windows.Forms.CheckBox();
		this.chkAutoAbilityPenetratingBlastOffset = new System.Windows.Forms.CheckBox();
		this.chkAutoAbilityNotifications = new System.Windows.Forms.CheckBox();
		this.lblAutoAbilityHealHpPercentAfter = new System.Windows.Forms.Label();
		this.numAutoAbilityHealHpPercent = new System.Windows.Forms.NumericUpDown();
		this.lblAutoAbilityHealHpPercent = new System.Windows.Forms.Label();
		this.chkAutoAbilityClosestEnemy = new System.Windows.Forms.RadioButton();
		this.lblAutoAbilityCustomDelay = new System.Windows.Forms.Label();
		this.lblMinimumEnemyGroupSize = new System.Windows.Forms.Label();
		this.chkAutoAbilityStrongestEnemy = new System.Windows.Forms.RadioButton();
		this.lblAutoAbilityCustomDelayMilliseconds = new System.Windows.Forms.Label();
		this.numAutoAbilityCustomDelay = new System.Windows.Forms.NumericUpDown();
		this.numAutoAbilityMinimumGroupSizeThreshold = new System.Windows.Forms.NumericUpDown();
		this.chkAutoAbilityWeakestEnemy = new System.Windows.Forms.RadioButton();
		this.lblMinimumEnemyGroupSizeAfter = new System.Windows.Forms.Label();
		this.lblMinimumMPLeftAfter = new System.Windows.Forms.Label();
		this.numAutoAbilityMinimumManaLeftThreshold = new System.Windows.Forms.NumericUpDown();
		this.lblMinimumMPLeft = new System.Windows.Forms.Label();
		this.lblMinimumEnemyHPAfter = new System.Windows.Forms.Label();
		this.numAutoAbilityMinimumEnemyHealthThreshold = new System.Windows.Forms.NumericUpDown();
		this.lblMinimumEnemyHP = new System.Windows.Forms.Label();
		this.sepAutoAbility = new System.Windows.Forms.Panel();
		this.chkEnableAutoAbility = new System.Windows.Forms.CheckBox();
		this.pnlAutoNexus = new System.Windows.Forms.Panel();
		this.numAutoNexusPercentageThreshold = new System.Windows.Forms.NumericUpDown();
		this.chkEnableAutoPotMP = new System.Windows.Forms.CheckBox();
		this.chkEnableAutoNexusOnly = new System.Windows.Forms.CheckBox();
		this.chkEnableAutoPotHP = new System.Windows.Forms.CheckBox();
		this.chkAutoNexusSyncHp = new System.Windows.Forms.CheckBox();
		this.chkAutoNexusUseClientHp = new System.Windows.Forms.CheckBox();
		this.chkAutoNexusDrinkFromInventory = new System.Windows.Forms.CheckBox();
		this.label5 = new System.Windows.Forms.Label();
		this.label3 = new System.Windows.Forms.Label();
		this.numAutoNexusDrinkMpThreshold = new System.Windows.Forms.NumericUpDown();
		this.numAutoNexusDrinkThreshold = new System.Windows.Forms.NumericUpDown();
		this.chkAutoNexusReplaceFameWithHealth = new System.Windows.Forms.CheckBox();
		this.lblAutoDrinkDelayMilliseconds = new System.Windows.Forms.Label();
		this.numAutoNexusHpPotDelay = new System.Windows.Forms.NumericUpDown();
		this.lblAutoDrinkDelay = new System.Windows.Forms.Label();
		this.chkAutoNexusShowInformation = new System.Windows.Forms.CheckBox();
		this.lblAutoNexusAfter = new System.Windows.Forms.Label();
		this.sepAutoNexus = new System.Windows.Forms.Panel();
		this.chkEnableAutoNexus = new System.Windows.Forms.CheckBox();
		this.pnlConnection = new System.Windows.Forms.Panel();
		this.chkDisableSendingIp = new System.Windows.Forms.CheckBox();
		this.chkEnableGotoCommand = new System.Windows.Forms.CheckBox();
		this.chkEnableConnectCommand = new System.Windows.Forms.CheckBox();
		this.lblConnection = new System.Windows.Forms.Label();
		this.sepConnection = new System.Windows.Forms.Panel();
		this.pnlAntiLag = new System.Windows.Forms.Panel();
		this.chkAntiLagHideAllyPets = new System.Windows.Forms.RadioButton();
		this.chkAntiLagHideNoPets = new System.Windows.Forms.RadioButton();
		this.chkAntiLagHideAllPets = new System.Windows.Forms.RadioButton();
		this.chkAntiLagApplyToGuildMates = new System.Windows.Forms.CheckBox();
		this.label2 = new System.Windows.Forms.Label();
		this.lblAntiLagPlayerSize = new System.Windows.Forms.Label();
		this.numAntiLagPlayerSize = new System.Windows.Forms.NumericUpDown();
		this.lblAntiLagAllySize = new System.Windows.Forms.Label();
		this.numAntiLagAllyPlayerSize = new System.Windows.Forms.NumericUpDown();
		this.btnEditIgnoredEffectList = new System.Windows.Forms.LinkLabel();
		this.label1 = new System.Windows.Forms.Label();
		this.chkAntiLagIgnoreEffects = new System.Windows.Forms.CheckBox();
		this.panel2 = new System.Windows.Forms.Panel();
		this.pnlO3 = new System.Windows.Forms.Panel();
		this.lblO3 = new System.Windows.Forms.Label();
		this.chkO3IgnoreDammah = new System.Windows.Forms.CheckBox();
		this.chkO3IgnoreCoins = new System.Windows.Forms.CheckBox();
		this.chkO3IgnoreShield = new System.Windows.Forms.CheckBox();
		this.sepO3 = new System.Windows.Forms.Panel();
		this.chkEnableO3Helper = new System.Windows.Forms.CheckBox();
		this.ttDescriptions = new System.Windows.Forms.ToolTip(this.components);
		this.pnlAutoAim = new System.Windows.Forms.Panel();
		this.chkAutoAimOffsetColossusSword = new System.Windows.Forms.CheckBox();
		this.lblAutoAimAcceleratingWeaponWarning = new System.Windows.Forms.Label();
		this.chkAutoAimProjectileNoclip = new System.Windows.Forms.CheckBox();
		this.chkAutoAimShootInvulnerable = new System.Windows.Forms.CheckBox();
		this.chkAutoAimShootWhileStealthed = new System.Windows.Forms.CheckBox();
		this.chkAutoAimFocusBoss = new System.Windows.Forms.CheckBox();
		this.lblAutoAimMouseDist = new System.Windows.Forms.Label();
		this.numAutoAimMouseDist = new System.Windows.Forms.NumericUpDown();
		this.lblAutoAimRangeLead = new System.Windows.Forms.Label();
		this.numAutoAimRangeLead = new System.Windows.Forms.NumericUpDown();
		this.chkAutoAimModeMouse = new System.Windows.Forms.RadioButton();
		this.chkAutoAimModeHighestHP = new System.Windows.Forms.RadioButton();
		this.chkAutoAimModeClosest = new System.Windows.Forms.RadioButton();
		this.chkAutoAimIgnoreWalls = new System.Windows.Forms.CheckBox();
		this.panel3 = new System.Windows.Forms.Panel();
		this.chkAutoAimEnabled = new System.Windows.Forms.CheckBox();
		this.panel1 = new System.Windows.Forms.Panel();
		this.btnCopySupportId = new System.Windows.Forms.Button();
		this.label7 = new System.Windows.Forms.Label();
		this.chkResetClientHp = new System.Windows.Forms.CheckBox();
		this.panel4 = new System.Windows.Forms.Panel();
		this.btnHotkeys = new System.Windows.Forms.Button();
		this.pnlFps = new System.Windows.Forms.Panel();
		this.lblFpsBackground = new System.Windows.Forms.Label();
		this.lblFpsVsyncWarning = new System.Windows.Forms.Label();
		this.lblFpsForeground = new System.Windows.Forms.Label();
		this.numFpsBackground = new System.Windows.Forms.NumericUpDown();
		this.numFpsForeground = new System.Windows.Forms.NumericUpDown();
		this.chkFpsVsync = new System.Windows.Forms.CheckBox();
		this.sepFps = new System.Windows.Forms.Panel();
		this.lblFps = new System.Windows.Forms.Label();
		this.tmrForeground = new System.Windows.Forms.Timer(this.components);
		this.chkAutoNexusInstantNexus = new System.Windows.Forms.CheckBox();
		this.pnlSafeWalk.SuspendLayout();
		((System.ComponentModel.ISupportInitialize)this.numSlowWalkMultiplier).BeginInit();
		this.pnlOther.SuspendLayout();
		this.pnlAntiDebuffs.SuspendLayout();
		this.pnlAutoLoot.SuspendLayout();
		((System.ComponentModel.ISupportInitialize)this.numAutoLootRingTierThreshold).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.numAutoLootAbilityTierThreshold).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.numAutoLootArmorTierThreshold).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.numAutoLootWeaponTierThreshold).BeginInit();
		this.pnlAutoAbility.SuspendLayout();
		((System.ComponentModel.ISupportInitialize)this.numAutoAbilityHealHpPercent).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.numAutoAbilityCustomDelay).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.numAutoAbilityMinimumGroupSizeThreshold).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.numAutoAbilityMinimumManaLeftThreshold).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.numAutoAbilityMinimumEnemyHealthThreshold).BeginInit();
		this.pnlAutoNexus.SuspendLayout();
		((System.ComponentModel.ISupportInitialize)this.numAutoNexusPercentageThreshold).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.numAutoNexusDrinkMpThreshold).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.numAutoNexusDrinkThreshold).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.numAutoNexusHpPotDelay).BeginInit();
		this.pnlConnection.SuspendLayout();
		this.pnlAntiLag.SuspendLayout();
		((System.ComponentModel.ISupportInitialize)this.numAntiLagPlayerSize).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.numAntiLagAllyPlayerSize).BeginInit();
		this.pnlO3.SuspendLayout();
		this.pnlAutoAim.SuspendLayout();
		((System.ComponentModel.ISupportInitialize)this.numAutoAimMouseDist).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.numAutoAimRangeLead).BeginInit();
		this.panel1.SuspendLayout();
		this.pnlFps.SuspendLayout();
		((System.ComponentModel.ISupportInitialize)this.numFpsBackground).BeginInit();
		((System.ComponentModel.ISupportInitialize)this.numFpsForeground).BeginInit();
		base.SuspendLayout();
		this.pnlSafeWalk.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
		this.pnlSafeWalk.BackColor = System.Drawing.Color.FromArgb(25, 60, 25);
		this.pnlSafeWalk.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
		this.pnlSafeWalk.Controls.Add(this.chkSlowWalkPercentOrSpeed);
		this.pnlSafeWalk.Controls.Add(this.chkEnableSlowWalk);
		this.pnlSafeWalk.Controls.Add(this.label4);
		this.pnlSafeWalk.Controls.Add(this.chkSafeWalkInShatters);
		this.pnlSafeWalk.Controls.Add(this.sepSafeWalk);
		this.pnlSafeWalk.Controls.Add(this.lblSlowWalkHotkey);
		this.pnlSafeWalk.Controls.Add(this.chkEnableSafeWalk);
		this.pnlSafeWalk.Controls.Add(this.numSlowWalkMultiplier);
		this.pnlSafeWalk.Location = new System.Drawing.Point(6, 1212);
		this.pnlSafeWalk.Name = "pnlSafeWalk";
		this.pnlSafeWalk.Size = new System.Drawing.Size(518, 115);
		this.pnlSafeWalk.TabIndex = 26;
		this.ttDescriptions.SetToolTip(this.pnlSafeWalk, "Automatically replace harmful liquid tiles with non-walkable tiles");
		this.chkSlowWalkPercentOrSpeed.AutoSize = true;
		this.chkSlowWalkPercentOrSpeed.Checked = true;
		this.chkSlowWalkPercentOrSpeed.CheckState = System.Windows.Forms.CheckState.Checked;
		this.chkSlowWalkPercentOrSpeed.Location = new System.Drawing.Point(386, 62);
		this.chkSlowWalkPercentOrSpeed.Name = "chkSlowWalkPercentOrSpeed";
		this.chkSlowWalkPercentOrSpeed.Size = new System.Drawing.Size(97, 21);
		this.chkSlowWalkPercentOrSpeed.TabIndex = 42;
		this.chkSlowWalkPercentOrSpeed.Text = "% Multiplier";
		this.chkSlowWalkPercentOrSpeed.UseVisualStyleBackColor = true;
		this.chkSlowWalkPercentOrSpeed.CheckedChanged += new System.EventHandler(chkPercentOrSpeed_CheckedChanged);
		this.chkEnableSlowWalk.AutoSize = true;
		this.chkEnableSlowWalk.BackColor = System.Drawing.Color.FromArgb(25, 60, 25);
		this.chkEnableSlowWalk.ForeColor = System.Drawing.Color.White;
		this.chkEnableSlowWalk.Location = new System.Drawing.Point(29, 62);
		this.chkEnableSlowWalk.Name = "chkEnableSlowWalk";
		this.chkEnableSlowWalk.Size = new System.Drawing.Size(239, 21);
		this.chkEnableSlowWalk.TabIndex = 16;
		this.chkEnableSlowWalk.Text = "Slow walk (lower player movespeed)";
		this.chkEnableSlowWalk.UseVisualStyleBackColor = false;
		this.chkEnableSlowWalk.CheckedChanged += new System.EventHandler(CheckBox_ValueChanged);
		this.label4.AutoSize = true;
		this.label4.BackColor = System.Drawing.Color.FromArgb(25, 60, 25);
		this.label4.Font = new System.Drawing.Font("Segoe UI", 12f, System.Drawing.FontStyle.Bold);
		this.label4.Location = new System.Drawing.Point(8, 4);
		this.label4.Name = "label4";
		this.label4.Size = new System.Drawing.Size(94, 21);
		this.label4.TabIndex = 15;
		this.label4.Text = "Walk Mods";
		this.chkSafeWalkInShatters.AutoSize = true;
		this.chkSafeWalkInShatters.BackColor = System.Drawing.Color.FromArgb(25, 60, 25);
		this.chkSafeWalkInShatters.ForeColor = System.Drawing.Color.White;
		this.chkSafeWalkInShatters.Location = new System.Drawing.Point(321, 35);
		this.chkSafeWalkInShatters.Name = "chkSafeWalkInShatters";
		this.chkSafeWalkInShatters.Size = new System.Drawing.Size(167, 21);
		this.chkSafeWalkInShatters.TabIndex = 14;
		this.chkSafeWalkInShatters.Text = "Enable in Lair of Shaitan";
		this.ttDescriptions.SetToolTip(this.chkSafeWalkInShatters, "Enables Safe Walk in Shatters and Lair of Shaitan\r\nMay cause issues with dungeon navigation");
		this.chkSafeWalkInShatters.UseVisualStyleBackColor = false;
		this.chkSafeWalkInShatters.CheckedChanged += new System.EventHandler(CheckBox_ValueChanged);
		this.sepSafeWalk.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
		this.sepSafeWalk.BackColor = System.Drawing.Color.FromArgb(25, 60, 25);
		this.sepSafeWalk.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
		this.sepSafeWalk.Location = new System.Drawing.Point(8, 28);
		this.sepSafeWalk.Name = "sepSafeWalk";
		this.sepSafeWalk.Size = new System.Drawing.Size(501, 1);
		this.sepSafeWalk.TabIndex = 6;
		this.lblSlowWalkHotkey.AutoSize = true;
		this.lblSlowWalkHotkey.BackColor = System.Drawing.Color.FromArgb(25, 60, 25);
		this.lblSlowWalkHotkey.Font = new System.Drawing.Font("Segoe UI", 9.75f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
		this.lblSlowWalkHotkey.ForeColor = System.Drawing.Color.White;
		this.lblSlowWalkHotkey.Location = new System.Drawing.Point(47, 86);
		this.lblSlowWalkHotkey.Name = "lblSlowWalkHotkey";
		this.lblSlowWalkHotkey.Size = new System.Drawing.Size(186, 17);
		this.lblSlowWalkHotkey.TabIndex = 41;
		this.lblSlowWalkHotkey.Text = "(Recommended to hotkey this)";
		this.chkEnableSafeWalk.AutoSize = true;
		this.chkEnableSafeWalk.BackColor = System.Drawing.Color.FromArgb(25, 60, 25);
		this.chkEnableSafeWalk.Font = new System.Drawing.Font("Segoe UI", 9.75f);
		this.chkEnableSafeWalk.ForeColor = System.Drawing.Color.White;
		this.chkEnableSafeWalk.Location = new System.Drawing.Point(29, 35);
		this.chkEnableSafeWalk.Name = "chkEnableSafeWalk";
		this.chkEnableSafeWalk.Size = new System.Drawing.Size(282, 21);
		this.chkEnableSafeWalk.TabIndex = 1;
		this.chkEnableSafeWalk.Text = "Safe walk (make damaging tiles unwalkable)";
		this.ttDescriptions.SetToolTip(this.chkEnableSafeWalk, "Automatically replace harmful liquid tiles with non-walkable tiles");
		this.chkEnableSafeWalk.UseVisualStyleBackColor = false;
		this.chkEnableSafeWalk.CheckedChanged += new System.EventHandler(CheckBox_ValueChanged);
		this.numSlowWalkMultiplier.BackColor = System.Drawing.Color.FromArgb(25, 60, 25);
		this.numSlowWalkMultiplier.Font = new System.Drawing.Font("Segoe UI", 8.25f);
		this.numSlowWalkMultiplier.ForeColor = System.Drawing.Color.White;
		this.numSlowWalkMultiplier.Location = new System.Drawing.Point(321, 61);
		this.numSlowWalkMultiplier.Minimum = new decimal(new int[4] { 1, 0, 0, 0 });
		this.numSlowWalkMultiplier.Name = "numSlowWalkMultiplier";
		this.numSlowWalkMultiplier.Size = new System.Drawing.Size(57, 22);
		this.numSlowWalkMultiplier.TabIndex = 39;
		this.numSlowWalkMultiplier.Value = new decimal(new int[4] { 25, 0, 0, 0 });
		this.numSlowWalkMultiplier.ValueChanged += new System.EventHandler(NumericUpDown_ValueChanged);
		this.pnlOther.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
		this.pnlOther.BackColor = System.Drawing.Color.FromArgb(54, 51, 52);
		this.pnlOther.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
		this.pnlOther.Controls.Add(this.lblTeleportAnchor);
		this.pnlOther.Controls.Add(this.txtTeleportAnchorTarget);
		this.pnlOther.Controls.Add(this.chkDisableHotkeys);
		this.pnlOther.Controls.Add(this.chkNoGlow);
		this.pnlOther.Controls.Add(this.chkPurpleGlow);
		this.pnlOther.Controls.Add(this.chkEnableGlow);
		this.pnlOther.Controls.Add(this.chkQueueBypassEscape);
		this.pnlOther.Controls.Add(this.chkEnableLocCommand);
		this.pnlOther.Controls.Add(this.chkShowRealLHPot);
		this.pnlOther.Controls.Add(this.chkEnableTeleportToSelf);
		this.pnlOther.Controls.Add(this.chkHideInjectPopup);
		this.pnlOther.Controls.Add(this.chkEnableMapHack);
		this.pnlOther.Controls.Add(this.chkEnableAntiAFK);
		this.pnlOther.Controls.Add(this.chkHideBattlepassXp);
		this.pnlOther.Controls.Add(this.chkEnableBazaarTimer);
		this.pnlOther.Controls.Add(this.chkDisableSystemMessages);
		this.pnlOther.Controls.Add(this.lblTools);
		this.pnlOther.Controls.Add(this.chkEnableCustomNexus);
		this.pnlOther.Controls.Add(this.chkEnableTeleportToPlayerClosestToQuestCommand);
		this.pnlOther.Controls.Add(this.chkEnableTeleportToPlayerCommand);
		this.pnlOther.Controls.Add(this.sepOther);
		this.pnlOther.ForeColor = System.Drawing.Color.White;
		this.pnlOther.Location = new System.Drawing.Point(6, 1636);
		this.pnlOther.Name = "pnlOther";
		this.pnlOther.Size = new System.Drawing.Size(518, 441);
		this.pnlOther.TabIndex = 25;
		this.ttDescriptions.SetToolTip(this.pnlOther, "Misc. Tools");
		this.lblTeleportAnchor.AutoSize = true;
		this.lblTeleportAnchor.Location = new System.Drawing.Point(254, 250);
		this.lblTeleportAnchor.Name = "lblTeleportAnchor";
		this.lblTeleportAnchor.Size = new System.Drawing.Size(197, 17);
		this.lblTeleportAnchor.TabIndex = 50;
		this.lblTeleportAnchor.Text = "Hotkey Teleport to Player Name:";
		this.ttDescriptions.SetToolTip(this.lblTeleportAnchor, "Assign this to a hotkey to teleport to the entered playername");
		this.txtTeleportAnchorTarget.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
		this.txtTeleportAnchorTarget.Location = new System.Drawing.Point(251, 275);
		this.txtTeleportAnchorTarget.MaxLength = 15;
		this.txtTeleportAnchorTarget.Name = "txtTeleportAnchorTarget";
		this.txtTeleportAnchorTarget.Size = new System.Drawing.Size(255, 25);
		this.txtTeleportAnchorTarget.TabIndex = 49;
		this.ttDescriptions.SetToolTip(this.txtTeleportAnchorTarget, "Assign this to a hotkey to teleport to the entered playername");
		this.txtTeleportAnchorTarget.TextChanged += new System.EventHandler(TextBox_ValueChanged);
		this.chkDisableHotkeys.AutoSize = true;
		this.chkDisableHotkeys.Location = new System.Drawing.Point(30, 357);
		this.chkDisableHotkeys.Name = "chkDisableHotkeys";
		this.chkDisableHotkeys.Size = new System.Drawing.Size(178, 21);
		this.chkDisableHotkeys.TabIndex = 48;
		this.chkDisableHotkeys.Text = "Disable ExaltHelper Hotkeys";
		this.ttDescriptions.SetToolTip(this.chkDisableHotkeys, "Use if you are getting Input Lag");
		this.chkDisableHotkeys.UseVisualStyleBackColor = true;
		this.chkDisableHotkeys.CheckedChanged += new System.EventHandler(CheckBox_ValueChanged);
		this.chkNoGlow.AutoSize = true;
		this.chkNoGlow.Location = new System.Drawing.Point(367, 194);
		this.chkNoGlow.Name = "chkNoGlow";
		this.chkNoGlow.Size = new System.Drawing.Size(125, 21);
		this.chkNoGlow.TabIndex = 47;
		this.chkNoGlow.TabStop = true;
		this.chkNoGlow.Text = "Unchanged Glow";
		this.ttDescriptions.SetToolTip(this.chkNoGlow, "Don't modify your character's glow");
		this.chkNoGlow.UseVisualStyleBackColor = true;
		this.chkNoGlow.CheckedChanged += new System.EventHandler(RadioButton_ValueChanged);
		this.chkPurpleGlow.AutoSize = true;
		this.chkPurpleGlow.Location = new System.Drawing.Point(225, 194);
		this.chkPurpleGlow.Name = "chkPurpleGlow";
		this.chkPurpleGlow.Size = new System.Drawing.Size(96, 21);
		this.chkPurpleGlow.TabIndex = 46;
		this.chkPurpleGlow.TabStop = true;
		this.chkPurpleGlow.Text = "Purple Glow";
		this.ttDescriptions.SetToolTip(this.chkPurpleGlow, "Purple supporter glow!");
		this.chkPurpleGlow.UseVisualStyleBackColor = true;
		this.chkPurpleGlow.CheckedChanged += new System.EventHandler(RadioButton_ValueChanged);
		this.chkEnableGlow.AutoSize = true;
		this.chkEnableGlow.Location = new System.Drawing.Point(30, 194);
		this.chkEnableGlow.Name = "chkEnableGlow";
		this.chkEnableGlow.Size = new System.Drawing.Size(164, 21);
		this.chkEnableGlow.TabIndex = 45;
		this.chkEnableGlow.TabStop = true;
		this.chkEnableGlow.Text = "Enable Red Player Glow";
		this.ttDescriptions.SetToolTip(this.chkEnableGlow, "Enables custom red player glow on yourself\r\nTo help you know you are on ExaltHelper");
		this.chkEnableGlow.UseVisualStyleBackColor = true;
		this.chkEnableGlow.CheckedChanged += new System.EventHandler(RadioButton_ValueChanged);
		this.chkQueueBypassEscape.AutoSize = true;
		this.chkQueueBypassEscape.Location = new System.Drawing.Point(30, 330);
		this.chkQueueBypassEscape.Name = "chkQueueBypassEscape";
		this.chkQueueBypassEscape.Size = new System.Drawing.Size(197, 21);
		this.chkQueueBypassEscape.TabIndex = 43;
		this.chkQueueBypassEscape.Text = "Bypass Realm Reentry Queue";
		this.ttDescriptions.SetToolTip(this.chkQueueBypassEscape, "Sends a special character in chat in addition to the Nexus packet, which lets you have a 30 second grace period of rejoining a full realm with zero queue.");
		this.chkQueueBypassEscape.UseVisualStyleBackColor = true;
		this.chkQueueBypassEscape.CheckedChanged += new System.EventHandler(CheckBox_ValueChanged);
		this.chkEnableLocCommand.AutoSize = true;
		this.chkEnableLocCommand.BackColor = System.Drawing.Color.FromArgb(54, 51, 52);
		this.chkEnableLocCommand.ForeColor = System.Drawing.Color.White;
		this.chkEnableLocCommand.Location = new System.Drawing.Point(30, 141);
		this.chkEnableLocCommand.Name = "chkEnableLocCommand";
		this.chkEnableLocCommand.Size = new System.Drawing.Size(276, 21);
		this.chkEnableLocCommand.TabIndex = 41;
		this.chkEnableLocCommand.Text = "Command: /loc : Show current coordinates";
		this.ttDescriptions.SetToolTip(this.chkEnableLocCommand, "Enables the command to show your current in-game coordinates");
		this.chkEnableLocCommand.UseVisualStyleBackColor = false;
		this.chkEnableLocCommand.CheckedChanged += new System.EventHandler(CheckBox_ValueChanged);
		this.chkShowRealLHPot.AutoSize = true;
		this.chkShowRealLHPot.ForeColor = System.Drawing.Color.White;
		this.chkShowRealLHPot.Location = new System.Drawing.Point(30, 303);
		this.chkShowRealLHPot.Name = "chkShowRealLHPot";
		this.chkShowRealLHPot.Size = new System.Drawing.Size(184, 21);
		this.chkShowRealLHPot.TabIndex = 40;
		this.chkShowRealLHPot.Text = "Show Real Pot in Lost Halls";
		this.ttDescriptions.SetToolTip(this.chkShowRealLHPot, "Shows the real Pot in Lost Halls");
		this.chkShowRealLHPot.UseVisualStyleBackColor = true;
		this.chkShowRealLHPot.CheckedChanged += new System.EventHandler(CheckBox_ValueChanged);
		this.chkEnableTeleportToSelf.AutoSize = true;
		this.chkEnableTeleportToSelf.BackColor = System.Drawing.Color.FromArgb(54, 51, 52);
		this.chkEnableTeleportToSelf.ForeColor = System.Drawing.Color.White;
		this.chkEnableTeleportToSelf.Location = new System.Drawing.Point(30, 114);
		this.chkEnableTeleportToSelf.Name = "chkEnableTeleportToSelf";
		this.chkEnableTeleportToSelf.Size = new System.Drawing.Size(236, 21);
		this.chkEnableTeleportToSelf.TabIndex = 39;
		this.chkEnableTeleportToSelf.Text = "Command: /tp : Teleport to yourself";
		this.ttDescriptions.SetToolTip(this.chkEnableTeleportToSelf, "Allows you to teleport to yourself\r\nDoable via /teleport <your name>\r\nUse to abuse teleport invincibility");
		this.chkEnableTeleportToSelf.UseVisualStyleBackColor = false;
		this.chkEnableTeleportToSelf.CheckedChanged += new System.EventHandler(CheckBox_ValueChanged);
		this.chkHideInjectPopup.AutoSize = true;
		this.chkHideInjectPopup.ForeColor = System.Drawing.Color.White;
		this.chkHideInjectPopup.Location = new System.Drawing.Point(30, 384);
		this.chkHideInjectPopup.Name = "chkHideInjectPopup";
		this.chkHideInjectPopup.Size = new System.Drawing.Size(159, 21);
		this.chkHideInjectPopup.TabIndex = 38;
		this.chkHideInjectPopup.Text = "Hide Popup on Launch";
		this.chkHideInjectPopup.UseVisualStyleBackColor = true;
		this.chkHideInjectPopup.CheckedChanged += new System.EventHandler(CheckBox_ValueChanged);
		this.chkEnableMapHack.AutoSize = true;
		this.chkEnableMapHack.ForeColor = System.Drawing.Color.White;
		this.chkEnableMapHack.Location = new System.Drawing.Point(30, 276);
		this.chkEnableMapHack.Name = "chkEnableMapHack";
		this.chkEnableMapHack.Size = new System.Drawing.Size(112, 21);
		this.chkEnableMapHack.TabIndex = 38;
		this.chkEnableMapHack.Text = "Show Full Map";
		this.ttDescriptions.SetToolTip(this.chkEnableMapHack, "Pre-loads static maps so you see the full map\r\nCan cause lag momentarily while entering a map");
		this.chkEnableMapHack.UseVisualStyleBackColor = true;
		this.chkEnableMapHack.CheckedChanged += new System.EventHandler(CheckBox_ValueChanged);
		this.chkEnableAntiAFK.AutoSize = true;
		this.chkEnableAntiAFK.ForeColor = System.Drawing.Color.White;
		this.chkEnableAntiAFK.Location = new System.Drawing.Point(30, 249);
		this.chkEnableAntiAFK.Name = "chkEnableAntiAFK";
		this.chkEnableAntiAFK.Size = new System.Drawing.Size(126, 21);
		this.chkEnableAntiAFK.TabIndex = 37;
		this.chkEnableAntiAFK.Text = "Anti AFK Timeout";
		this.ttDescriptions.SetToolTip(this.chkEnableAntiAFK, "Prevents the game from disconnecting you for being AFK");
		this.chkEnableAntiAFK.UseVisualStyleBackColor = true;
		this.chkEnableAntiAFK.CheckedChanged += new System.EventHandler(CheckBox_ValueChanged);
		this.chkHideBattlepassXp.AutoSize = true;
		this.chkHideBattlepassXp.ForeColor = System.Drawing.Color.White;
		this.chkHideBattlepassXp.Location = new System.Drawing.Point(30, 411);
		this.chkHideBattlepassXp.Name = "chkHideBattlepassXp";
		this.chkHideBattlepassXp.Size = new System.Drawing.Size(182, 21);
		this.chkHideBattlepassXp.TabIndex = 27;
		this.chkHideBattlepassXp.Text = "Disable +BXP Notifications";
		this.chkHideBattlepassXp.UseVisualStyleBackColor = true;
		this.chkHideBattlepassXp.CheckedChanged += new System.EventHandler(CheckBox_ValueChanged);
		this.chkEnableBazaarTimer.AutoSize = true;
		this.chkEnableBazaarTimer.ForeColor = System.Drawing.Color.White;
		this.chkEnableBazaarTimer.Location = new System.Drawing.Point(30, 222);
		this.chkEnableBazaarTimer.Name = "chkEnableBazaarTimer";
		this.chkEnableBazaarTimer.Size = new System.Drawing.Size(174, 21);
		this.chkEnableBazaarTimer.TabIndex = 27;
		this.chkEnableBazaarTimer.Text = "Bazaar Portal Entry Timer";
		this.ttDescriptions.SetToolTip(this.chkEnableBazaarTimer, "Shows a timer after your enter a Bazaar until you can use portals");
		this.chkEnableBazaarTimer.UseVisualStyleBackColor = true;
		this.chkEnableBazaarTimer.CheckedChanged += new System.EventHandler(CheckBox_ValueChanged);
		this.chkDisableSystemMessages.AutoSize = true;
		this.chkDisableSystemMessages.BackColor = System.Drawing.Color.FromArgb(54, 51, 52);
		this.chkDisableSystemMessages.Font = new System.Drawing.Font("Segoe UI Semibold", 9.75f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, 0);
		this.chkDisableSystemMessages.ForeColor = System.Drawing.Color.White;
		this.chkDisableSystemMessages.Location = new System.Drawing.Point(30, 34);
		this.chkDisableSystemMessages.Name = "chkDisableSystemMessages";
		this.chkDisableSystemMessages.Size = new System.Drawing.Size(189, 21);
		this.chkDisableSystemMessages.TabIndex = 26;
		this.chkDisableSystemMessages.Text = "Hide Cheat Chat Messages";
		this.ttDescriptions.SetToolTip(this.chkDisableSystemMessages, "Hides all messages and prompts produced by MultTool\r\nto appear more legit on your screen");
		this.chkDisableSystemMessages.UseVisualStyleBackColor = false;
		this.chkDisableSystemMessages.CheckedChanged += new System.EventHandler(CheckBox_ValueChanged);
		this.lblTools.AutoSize = true;
		this.lblTools.Font = new System.Drawing.Font("Segoe UI", 12f, System.Drawing.FontStyle.Bold);
		this.lblTools.ForeColor = System.Drawing.Color.White;
		this.lblTools.Location = new System.Drawing.Point(8, 3);
		this.lblTools.Name = "lblTools";
		this.lblTools.Size = new System.Drawing.Size(50, 21);
		this.lblTools.TabIndex = 24;
		this.lblTools.Text = "Tools";
		this.ttDescriptions.SetToolTip(this.lblTools, "Misc. Tools");
		this.chkEnableCustomNexus.AutoSize = true;
		this.chkEnableCustomNexus.BackColor = System.Drawing.Color.FromArgb(54, 51, 52);
		this.chkEnableCustomNexus.ForeColor = System.Drawing.Color.White;
		this.chkEnableCustomNexus.Location = new System.Drawing.Point(30, 168);
		this.chkEnableCustomNexus.Name = "chkEnableCustomNexus";
		this.chkEnableCustomNexus.Size = new System.Drawing.Size(185, 21);
		this.chkEnableCustomNexus.TabIndex = 23;
		this.chkEnableCustomNexus.Text = "Custom Nexus Appearance";
		this.ttDescriptions.SetToolTip(this.chkEnableCustomNexus, "Enables custom nexus water effect\r\nTo help you know you are on ExaltHelper");
		this.chkEnableCustomNexus.UseVisualStyleBackColor = false;
		this.chkEnableCustomNexus.CheckedChanged += new System.EventHandler(CheckBox_ValueChanged);
		this.chkEnableTeleportToPlayerClosestToQuestCommand.AutoSize = true;
		this.chkEnableTeleportToPlayerClosestToQuestCommand.BackColor = System.Drawing.Color.FromArgb(54, 51, 52);
		this.chkEnableTeleportToPlayerClosestToQuestCommand.ForeColor = System.Drawing.Color.White;
		this.chkEnableTeleportToPlayerClosestToQuestCommand.Location = new System.Drawing.Point(30, 60);
		this.chkEnableTeleportToPlayerClosestToQuestCommand.Name = "chkEnableTeleportToPlayerClosestToQuestCommand";
		this.chkEnableTeleportToPlayerClosestToQuestCommand.Size = new System.Drawing.Size(405, 21);
		this.chkEnableTeleportToPlayerClosestToQuestCommand.TabIndex = 18;
		this.chkEnableTeleportToPlayerClosestToQuestCommand.Text = "Command: /tpq : Teleport to player closest to your current quest";
		this.ttDescriptions.SetToolTip(this.chkEnableTeleportToPlayerClosestToQuestCommand, "Enables the command to teleport to the player closes to your current quest");
		this.chkEnableTeleportToPlayerClosestToQuestCommand.UseVisualStyleBackColor = false;
		this.chkEnableTeleportToPlayerClosestToQuestCommand.CheckedChanged += new System.EventHandler(CheckBox_ValueChanged);
		this.chkEnableTeleportToPlayerCommand.AutoSize = true;
		this.chkEnableTeleportToPlayerCommand.BackColor = System.Drawing.Color.FromArgb(54, 51, 52);
		this.chkEnableTeleportToPlayerCommand.ForeColor = System.Drawing.Color.White;
		this.chkEnableTeleportToPlayerCommand.Location = new System.Drawing.Point(30, 87);
		this.chkEnableTeleportToPlayerCommand.Name = "chkEnableTeleportToPlayerCommand";
		this.chkEnableTeleportToPlayerCommand.Size = new System.Drawing.Size(336, 21);
		this.chkEnableTeleportToPlayerCommand.TabIndex = 17;
		this.chkEnableTeleportToPlayerCommand.Text = "Command: /tp <name> : Teleport to specified player";
		this.ttDescriptions.SetToolTip(this.chkEnableTeleportToPlayerCommand, "Enable the shorted teleport command");
		this.chkEnableTeleportToPlayerCommand.UseVisualStyleBackColor = false;
		this.chkEnableTeleportToPlayerCommand.CheckedChanged += new System.EventHandler(CheckBox_ValueChanged);
		this.sepOther.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
		this.sepOther.BackColor = System.Drawing.Color.FromArgb(54, 51, 52);
		this.sepOther.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
		this.sepOther.Location = new System.Drawing.Point(8, 28);
		this.sepOther.Name = "sepOther";
		this.sepOther.Size = new System.Drawing.Size(501, 1);
		this.sepOther.TabIndex = 6;
		this.pnlAntiDebuffs.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
		this.pnlAntiDebuffs.BackColor = System.Drawing.Color.FromArgb(44, 41, 72);
		this.pnlAntiDebuffs.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
		this.pnlAntiDebuffs.Controls.Add(this.sepServerSide);
		this.pnlAntiDebuffs.Controls.Add(this.sepClientSide);
		this.pnlAntiDebuffs.Controls.Add(this.chkIgnoreSilence);
		this.pnlAntiDebuffs.Controls.Add(this.chkIgnorePetrified);
		this.pnlAntiDebuffs.Controls.Add(this.chkIgnorePetStasis);
		this.pnlAntiDebuffs.Controls.Add(this.chkIgnoreArmorBreak);
		this.pnlAntiDebuffs.Controls.Add(this.chkIgnoreBleeding);
		this.pnlAntiDebuffs.Controls.Add(this.chkIgnoreParalyzed);
		this.pnlAntiDebuffs.Controls.Add(this.chkIgnoreCurse);
		this.pnlAntiDebuffs.Controls.Add(this.chkIgnoreDrought);
		this.pnlAntiDebuffs.Controls.Add(this.chkIgnoreStunned);
		this.pnlAntiDebuffs.Controls.Add(this.chkIgnoreDazed);
		this.pnlAntiDebuffs.Controls.Add(this.chkIgnoreSick);
		this.pnlAntiDebuffs.Controls.Add(this.chkIgnoreSlowed);
		this.pnlAntiDebuffs.Controls.Add(this.chkIgnoreWeak);
		this.pnlAntiDebuffs.Controls.Add(this.chkIgnoreQuiet);
		this.pnlAntiDebuffs.Controls.Add(this.lblServerSideDebuffs);
		this.pnlAntiDebuffs.Controls.Add(this.chkIgnoreDarkness);
		this.pnlAntiDebuffs.Controls.Add(this.chkIgnoreUnstable);
		this.pnlAntiDebuffs.Controls.Add(this.chkIgnoreConfused);
		this.pnlAntiDebuffs.Controls.Add(this.chkIgnoreDrunk);
		this.pnlAntiDebuffs.Controls.Add(this.chkIgnoreHallucinating);
		this.pnlAntiDebuffs.Controls.Add(this.chkIgnoreBlind);
		this.pnlAntiDebuffs.Controls.Add(this.lblClientSideDebuffs);
		this.pnlAntiDebuffs.Controls.Add(this.sepAntiDebuffs);
		this.pnlAntiDebuffs.Controls.Add(this.chkEnableAntiDebuffs);
		this.pnlAntiDebuffs.ForeColor = System.Drawing.Color.White;
		this.pnlAntiDebuffs.Location = new System.Drawing.Point(6, 959);
		this.pnlAntiDebuffs.Name = "pnlAntiDebuffs";
		this.pnlAntiDebuffs.Size = new System.Drawing.Size(518, 247);
		this.pnlAntiDebuffs.TabIndex = 24;
		this.ttDescriptions.SetToolTip(this.pnlAntiDebuffs, "Block attack debuffs from affecting your character");
		this.sepServerSide.BackColor = System.Drawing.Color.FromArgb(44, 41, 72);
		this.sepServerSide.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
		this.sepServerSide.Location = new System.Drawing.Point(200, 53);
		this.sepServerSide.Name = "sepServerSide";
		this.sepServerSide.Size = new System.Drawing.Size(300, 1);
		this.sepServerSide.TabIndex = 36;
		this.sepClientSide.BackColor = System.Drawing.Color.FromArgb(44, 41, 72);
		this.sepClientSide.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
		this.sepClientSide.Location = new System.Drawing.Point(30, 53);
		this.sepClientSide.Name = "sepClientSide";
		this.sepClientSide.Size = new System.Drawing.Size(150, 1);
		this.sepClientSide.TabIndex = 35;
		this.chkIgnoreSilence.AutoSize = true;
		this.chkIgnoreSilence.BackColor = System.Drawing.Color.FromArgb(44, 41, 72);
		this.chkIgnoreSilence.ForeColor = System.Drawing.Color.White;
		this.chkIgnoreSilence.Location = new System.Drawing.Point(351, 193);
		this.chkIgnoreSilence.Name = "chkIgnoreSilence";
		this.chkIgnoreSilence.Size = new System.Drawing.Size(109, 21);
		this.chkIgnoreSilence.TabIndex = 34;
		this.chkIgnoreSilence.Text = "Ignore Silence";
		this.ttDescriptions.SetToolTip(this.chkIgnoreSilence, "Ignores this debuff (will cause DC over time)");
		this.chkIgnoreSilence.UseVisualStyleBackColor = false;
		this.chkIgnoreSilence.CheckedChanged += new System.EventHandler(CheckBox_ValueChanged);
		this.chkIgnorePetrified.AutoSize = true;
		this.chkIgnorePetrified.BackColor = System.Drawing.Color.FromArgb(44, 41, 72);
		this.chkIgnorePetrified.ForeColor = System.Drawing.Color.White;
		this.chkIgnorePetrified.Location = new System.Drawing.Point(351, 166);
		this.chkIgnorePetrified.Name = "chkIgnorePetrified";
		this.chkIgnorePetrified.Size = new System.Drawing.Size(117, 21);
		this.chkIgnorePetrified.TabIndex = 33;
		this.chkIgnorePetrified.Text = "Ignore Petrified";
		this.ttDescriptions.SetToolTip(this.chkIgnorePetrified, "Ignores this debuff (will cause DC over time)");
		this.chkIgnorePetrified.UseVisualStyleBackColor = false;
		this.chkIgnorePetrified.CheckedChanged += new System.EventHandler(CheckBox_ValueChanged);
		this.chkIgnorePetStasis.AutoSize = true;
		this.chkIgnorePetStasis.BackColor = System.Drawing.Color.FromArgb(44, 41, 72);
		this.chkIgnorePetStasis.ForeColor = System.Drawing.Color.White;
		this.chkIgnorePetStasis.Location = new System.Drawing.Point(351, 139);
		this.chkIgnorePetStasis.Name = "chkIgnorePetStasis";
		this.chkIgnorePetStasis.Size = new System.Drawing.Size(124, 21);
		this.chkIgnorePetStasis.TabIndex = 32;
		this.chkIgnorePetStasis.Text = "Ignore Pet Stasis";
		this.ttDescriptions.SetToolTip(this.chkIgnorePetStasis, "Ignores this debuff (will cause DC over time)");
		this.chkIgnorePetStasis.UseVisualStyleBackColor = false;
		this.chkIgnorePetStasis.CheckedChanged += new System.EventHandler(CheckBox_ValueChanged);
		this.chkIgnoreArmorBreak.AutoSize = true;
		this.chkIgnoreArmorBreak.BackColor = System.Drawing.Color.FromArgb(44, 41, 72);
		this.chkIgnoreArmorBreak.ForeColor = System.Drawing.Color.White;
		this.chkIgnoreArmorBreak.Location = new System.Drawing.Point(351, 112);
		this.chkIgnoreArmorBreak.Name = "chkIgnoreArmorBreak";
		this.chkIgnoreArmorBreak.Size = new System.Drawing.Size(142, 21);
		this.chkIgnoreArmorBreak.TabIndex = 31;
		this.chkIgnoreArmorBreak.Text = "Ignore Armor Break";
		this.ttDescriptions.SetToolTip(this.chkIgnoreArmorBreak, "Ignores this debuff (will cause DC over time)");
		this.chkIgnoreArmorBreak.UseVisualStyleBackColor = false;
		this.chkIgnoreArmorBreak.CheckedChanged += new System.EventHandler(CheckBox_ValueChanged);
		this.chkIgnoreBleeding.AutoSize = true;
		this.chkIgnoreBleeding.BackColor = System.Drawing.Color.FromArgb(44, 41, 72);
		this.chkIgnoreBleeding.ForeColor = System.Drawing.Color.White;
		this.chkIgnoreBleeding.Location = new System.Drawing.Point(351, 85);
		this.chkIgnoreBleeding.Name = "chkIgnoreBleeding";
		this.chkIgnoreBleeding.Size = new System.Drawing.Size(119, 21);
		this.chkIgnoreBleeding.TabIndex = 30;
		this.chkIgnoreBleeding.Text = "Ignore Bleeding";
		this.ttDescriptions.SetToolTip(this.chkIgnoreBleeding, "Ignores this debuff (will cause DC over time)");
		this.chkIgnoreBleeding.UseVisualStyleBackColor = false;
		this.chkIgnoreBleeding.CheckedChanged += new System.EventHandler(CheckBox_ValueChanged);
		this.chkIgnoreParalyzed.AutoSize = true;
		this.chkIgnoreParalyzed.BackColor = System.Drawing.Color.FromArgb(44, 41, 72);
		this.chkIgnoreParalyzed.ForeColor = System.Drawing.Color.White;
		this.chkIgnoreParalyzed.Location = new System.Drawing.Point(351, 58);
		this.chkIgnoreParalyzed.Name = "chkIgnoreParalyzed";
		this.chkIgnoreParalyzed.Size = new System.Drawing.Size(125, 21);
		this.chkIgnoreParalyzed.TabIndex = 29;
		this.chkIgnoreParalyzed.Text = "Ignore Paralyzed";
		this.ttDescriptions.SetToolTip(this.chkIgnoreParalyzed, "Ignores this debuff (will cause DC over time)");
		this.chkIgnoreParalyzed.UseVisualStyleBackColor = false;
		this.chkIgnoreParalyzed.CheckedChanged += new System.EventHandler(CheckBox_ValueChanged);
		this.chkIgnoreCurse.AutoSize = true;
		this.chkIgnoreCurse.BackColor = System.Drawing.Color.FromArgb(44, 41, 72);
		this.chkIgnoreCurse.ForeColor = System.Drawing.Color.White;
		this.chkIgnoreCurse.Location = new System.Drawing.Point(200, 220);
		this.chkIgnoreCurse.Name = "chkIgnoreCurse";
		this.chkIgnoreCurse.Size = new System.Drawing.Size(102, 21);
		this.chkIgnoreCurse.TabIndex = 28;
		this.chkIgnoreCurse.Text = "Ignore Curse";
		this.chkIgnoreCurse.UseVisualStyleBackColor = false;
		this.chkIgnoreCurse.CheckedChanged += new System.EventHandler(CheckBox_ValueChanged);
		this.chkIgnoreDrought.AutoSize = true;
		this.chkIgnoreDrought.BackColor = System.Drawing.Color.FromArgb(44, 41, 72);
		this.chkIgnoreDrought.ForeColor = System.Drawing.Color.White;
		this.chkIgnoreDrought.Location = new System.Drawing.Point(351, 220);
		this.chkIgnoreDrought.Name = "chkIgnoreDrought";
		this.chkIgnoreDrought.Size = new System.Drawing.Size(117, 21);
		this.chkIgnoreDrought.TabIndex = 28;
		this.chkIgnoreDrought.Text = "Ignore Drought";
		this.chkIgnoreDrought.UseVisualStyleBackColor = false;
		this.chkIgnoreDrought.CheckedChanged += new System.EventHandler(CheckBox_ValueChanged);
		this.chkIgnoreStunned.AutoSize = true;
		this.chkIgnoreStunned.BackColor = System.Drawing.Color.FromArgb(44, 41, 72);
		this.chkIgnoreStunned.ForeColor = System.Drawing.Color.White;
		this.chkIgnoreStunned.Location = new System.Drawing.Point(200, 193);
		this.chkIgnoreStunned.Name = "chkIgnoreStunned";
		this.chkIgnoreStunned.Size = new System.Drawing.Size(116, 21);
		this.chkIgnoreStunned.TabIndex = 28;
		this.chkIgnoreStunned.Text = "Ignore Stunned";
		this.ttDescriptions.SetToolTip(this.chkIgnoreStunned, "Ignores this debuff (will cause DC over time)");
		this.chkIgnoreStunned.UseVisualStyleBackColor = false;
		this.chkIgnoreStunned.CheckedChanged += new System.EventHandler(CheckBox_ValueChanged);
		this.chkIgnoreDazed.AutoSize = true;
		this.chkIgnoreDazed.BackColor = System.Drawing.Color.FromArgb(44, 41, 72);
		this.chkIgnoreDazed.ForeColor = System.Drawing.Color.White;
		this.chkIgnoreDazed.Location = new System.Drawing.Point(200, 166);
		this.chkIgnoreDazed.Name = "chkIgnoreDazed";
		this.chkIgnoreDazed.Size = new System.Drawing.Size(106, 21);
		this.chkIgnoreDazed.TabIndex = 27;
		this.chkIgnoreDazed.Text = "Ignore Dazed";
		this.ttDescriptions.SetToolTip(this.chkIgnoreDazed, "Ignores this debuff (will cause DC over time)");
		this.chkIgnoreDazed.UseVisualStyleBackColor = false;
		this.chkIgnoreDazed.CheckedChanged += new System.EventHandler(CheckBox_ValueChanged);
		this.chkIgnoreSick.AutoSize = true;
		this.chkIgnoreSick.BackColor = System.Drawing.Color.FromArgb(44, 41, 72);
		this.chkIgnoreSick.ForeColor = System.Drawing.Color.White;
		this.chkIgnoreSick.Location = new System.Drawing.Point(200, 139);
		this.chkIgnoreSick.Name = "chkIgnoreSick";
		this.chkIgnoreSick.Size = new System.Drawing.Size(91, 21);
		this.chkIgnoreSick.TabIndex = 26;
		this.chkIgnoreSick.Text = "Ignore Sick";
		this.ttDescriptions.SetToolTip(this.chkIgnoreSick, "Ignores this debuff (will cause DC over time)");
		this.chkIgnoreSick.UseVisualStyleBackColor = false;
		this.chkIgnoreSick.CheckedChanged += new System.EventHandler(CheckBox_ValueChanged);
		this.chkIgnoreSlowed.AutoSize = true;
		this.chkIgnoreSlowed.BackColor = System.Drawing.Color.FromArgb(44, 41, 72);
		this.chkIgnoreSlowed.ForeColor = System.Drawing.Color.White;
		this.chkIgnoreSlowed.Location = new System.Drawing.Point(200, 112);
		this.chkIgnoreSlowed.Name = "chkIgnoreSlowed";
		this.chkIgnoreSlowed.Size = new System.Drawing.Size(111, 21);
		this.chkIgnoreSlowed.TabIndex = 25;
		this.chkIgnoreSlowed.Text = "Ignore Slowed";
		this.ttDescriptions.SetToolTip(this.chkIgnoreSlowed, "Ignores this debuff (will cause DC over time)");
		this.chkIgnoreSlowed.UseVisualStyleBackColor = false;
		this.chkIgnoreSlowed.CheckedChanged += new System.EventHandler(CheckBox_ValueChanged);
		this.chkIgnoreWeak.AutoSize = true;
		this.chkIgnoreWeak.BackColor = System.Drawing.Color.FromArgb(44, 41, 72);
		this.chkIgnoreWeak.ForeColor = System.Drawing.Color.White;
		this.chkIgnoreWeak.Location = new System.Drawing.Point(200, 85);
		this.chkIgnoreWeak.Name = "chkIgnoreWeak";
		this.chkIgnoreWeak.Size = new System.Drawing.Size(100, 21);
		this.chkIgnoreWeak.TabIndex = 24;
		this.chkIgnoreWeak.Text = "Ignore Weak";
		this.ttDescriptions.SetToolTip(this.chkIgnoreWeak, "Ignores this debuff (will cause DC over time)");
		this.chkIgnoreWeak.UseVisualStyleBackColor = false;
		this.chkIgnoreWeak.CheckedChanged += new System.EventHandler(CheckBox_ValueChanged);
		this.chkIgnoreQuiet.AutoSize = true;
		this.chkIgnoreQuiet.BackColor = System.Drawing.Color.FromArgb(44, 41, 72);
		this.chkIgnoreQuiet.ForeColor = System.Drawing.Color.White;
		this.chkIgnoreQuiet.Location = new System.Drawing.Point(200, 58);
		this.chkIgnoreQuiet.Name = "chkIgnoreQuiet";
		this.chkIgnoreQuiet.Size = new System.Drawing.Size(100, 21);
		this.chkIgnoreQuiet.TabIndex = 23;
		this.chkIgnoreQuiet.Text = "Ignore Quiet";
		this.ttDescriptions.SetToolTip(this.chkIgnoreQuiet, "Ignores this debuff (will cause DC over time)");
		this.chkIgnoreQuiet.UseVisualStyleBackColor = false;
		this.chkIgnoreQuiet.CheckedChanged += new System.EventHandler(CheckBox_ValueChanged);
		this.lblServerSideDebuffs.AutoSize = true;
		this.lblServerSideDebuffs.BackColor = System.Drawing.Color.FromArgb(44, 41, 72);
		this.lblServerSideDebuffs.Font = new System.Drawing.Font("Segoe UI", 9.75f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, 0);
		this.lblServerSideDebuffs.ForeColor = System.Drawing.Color.White;
		this.lblServerSideDebuffs.Location = new System.Drawing.Point(197, 32);
		this.lblServerSideDebuffs.Name = "lblServerSideDebuffs";
		this.lblServerSideDebuffs.Size = new System.Drawing.Size(164, 17);
		this.lblServerSideDebuffs.TabIndex = 22;
		this.lblServerSideDebuffs.Text = "Server Side (DC possible)";
		this.ttDescriptions.SetToolTip(this.lblServerSideDebuffs, "Debuffs that are controlled by the server");
		this.chkIgnoreDarkness.AutoSize = true;
		this.chkIgnoreDarkness.BackColor = System.Drawing.Color.FromArgb(44, 41, 72);
		this.chkIgnoreDarkness.ForeColor = System.Drawing.Color.White;
		this.chkIgnoreDarkness.Location = new System.Drawing.Point(30, 193);
		this.chkIgnoreDarkness.Name = "chkIgnoreDarkness";
		this.chkIgnoreDarkness.Size = new System.Drawing.Size(122, 21);
		this.chkIgnoreDarkness.TabIndex = 21;
		this.chkIgnoreDarkness.Text = "Ignore Darkness";
		this.ttDescriptions.SetToolTip(this.chkIgnoreDarkness, "Ignores this debuff (no DC issues)");
		this.chkIgnoreDarkness.UseVisualStyleBackColor = false;
		this.chkIgnoreDarkness.CheckedChanged += new System.EventHandler(CheckBox_ValueChanged);
		this.chkIgnoreUnstable.AutoSize = true;
		this.chkIgnoreUnstable.BackColor = System.Drawing.Color.FromArgb(44, 41, 72);
		this.chkIgnoreUnstable.ForeColor = System.Drawing.Color.White;
		this.chkIgnoreUnstable.Location = new System.Drawing.Point(30, 166);
		this.chkIgnoreUnstable.Name = "chkIgnoreUnstable";
		this.chkIgnoreUnstable.Size = new System.Drawing.Size(120, 21);
		this.chkIgnoreUnstable.TabIndex = 20;
		this.chkIgnoreUnstable.Text = "Ignore Unstable";
		this.ttDescriptions.SetToolTip(this.chkIgnoreUnstable, "Ignores this debuff (no DC issues)");
		this.chkIgnoreUnstable.UseVisualStyleBackColor = false;
		this.chkIgnoreUnstable.CheckedChanged += new System.EventHandler(CheckBox_ValueChanged);
		this.chkIgnoreConfused.AutoSize = true;
		this.chkIgnoreConfused.BackColor = System.Drawing.Color.FromArgb(44, 41, 72);
		this.chkIgnoreConfused.ForeColor = System.Drawing.Color.White;
		this.chkIgnoreConfused.Location = new System.Drawing.Point(30, 139);
		this.chkIgnoreConfused.Name = "chkIgnoreConfused";
		this.chkIgnoreConfused.Size = new System.Drawing.Size(124, 21);
		this.chkIgnoreConfused.TabIndex = 19;
		this.chkIgnoreConfused.Text = "Ignore Confused";
		this.ttDescriptions.SetToolTip(this.chkIgnoreConfused, "Ignores this debuff (no DC issues)");
		this.chkIgnoreConfused.UseVisualStyleBackColor = false;
		this.chkIgnoreConfused.CheckedChanged += new System.EventHandler(CheckBox_ValueChanged);
		this.chkIgnoreDrunk.AutoSize = true;
		this.chkIgnoreDrunk.BackColor = System.Drawing.Color.FromArgb(44, 41, 72);
		this.chkIgnoreDrunk.ForeColor = System.Drawing.Color.White;
		this.chkIgnoreDrunk.Location = new System.Drawing.Point(30, 112);
		this.chkIgnoreDrunk.Name = "chkIgnoreDrunk";
		this.chkIgnoreDrunk.Size = new System.Drawing.Size(103, 21);
		this.chkIgnoreDrunk.TabIndex = 18;
		this.chkIgnoreDrunk.Text = "Ignore Drunk";
		this.ttDescriptions.SetToolTip(this.chkIgnoreDrunk, "Ignores this debuff (no DC issues)");
		this.chkIgnoreDrunk.UseVisualStyleBackColor = false;
		this.chkIgnoreDrunk.CheckedChanged += new System.EventHandler(CheckBox_ValueChanged);
		this.chkIgnoreHallucinating.AutoSize = true;
		this.chkIgnoreHallucinating.BackColor = System.Drawing.Color.FromArgb(44, 41, 72);
		this.chkIgnoreHallucinating.ForeColor = System.Drawing.Color.White;
		this.chkIgnoreHallucinating.Location = new System.Drawing.Point(30, 85);
		this.chkIgnoreHallucinating.Name = "chkIgnoreHallucinating";
		this.chkIgnoreHallucinating.Size = new System.Drawing.Size(143, 21);
		this.chkIgnoreHallucinating.TabIndex = 17;
		this.chkIgnoreHallucinating.Text = "Ignore Hallucination";
		this.ttDescriptions.SetToolTip(this.chkIgnoreHallucinating, "Ignores this debuff (no DC issues)");
		this.chkIgnoreHallucinating.UseVisualStyleBackColor = false;
		this.chkIgnoreHallucinating.CheckedChanged += new System.EventHandler(CheckBox_ValueChanged);
		this.chkIgnoreBlind.AutoSize = true;
		this.chkIgnoreBlind.BackColor = System.Drawing.Color.FromArgb(44, 41, 72);
		this.chkIgnoreBlind.ForeColor = System.Drawing.Color.White;
		this.chkIgnoreBlind.Location = new System.Drawing.Point(30, 58);
		this.chkIgnoreBlind.Name = "chkIgnoreBlind";
		this.chkIgnoreBlind.Size = new System.Drawing.Size(123, 21);
		this.chkIgnoreBlind.TabIndex = 16;
		this.chkIgnoreBlind.Text = "Ignore Blindness";
		this.ttDescriptions.SetToolTip(this.chkIgnoreBlind, "Ignores this debuff (no DC issues)");
		this.chkIgnoreBlind.UseVisualStyleBackColor = false;
		this.chkIgnoreBlind.CheckedChanged += new System.EventHandler(CheckBox_ValueChanged);
		this.lblClientSideDebuffs.AutoSize = true;
		this.lblClientSideDebuffs.BackColor = System.Drawing.Color.FromArgb(44, 41, 72);
		this.lblClientSideDebuffs.Font = new System.Drawing.Font("Segoe UI", 9.75f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, 0);
		this.lblClientSideDebuffs.ForeColor = System.Drawing.Color.White;
		this.lblClientSideDebuffs.Location = new System.Drawing.Point(27, 32);
		this.lblClientSideDebuffs.Name = "lblClientSideDebuffs";
		this.lblClientSideDebuffs.Size = new System.Drawing.Size(128, 17);
		this.lblClientSideDebuffs.TabIndex = 15;
		this.lblClientSideDebuffs.Text = "Client Side (No DC)";
		this.ttDescriptions.SetToolTip(this.lblClientSideDebuffs, "Debuffs that are controlled by the client");
		this.sepAntiDebuffs.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
		this.sepAntiDebuffs.BackColor = System.Drawing.Color.FromArgb(44, 41, 72);
		this.sepAntiDebuffs.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
		this.sepAntiDebuffs.Location = new System.Drawing.Point(8, 28);
		this.sepAntiDebuffs.Name = "sepAntiDebuffs";
		this.sepAntiDebuffs.Size = new System.Drawing.Size(501, 1);
		this.sepAntiDebuffs.TabIndex = 6;
		this.chkEnableAntiDebuffs.AutoSize = true;
		this.chkEnableAntiDebuffs.BackColor = System.Drawing.Color.FromArgb(44, 41, 72);
		this.chkEnableAntiDebuffs.Font = new System.Drawing.Font("Segoe UI", 12f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, 0);
		this.chkEnableAntiDebuffs.ForeColor = System.Drawing.Color.White;
		this.chkEnableAntiDebuffs.Location = new System.Drawing.Point(8, 3);
		this.chkEnableAntiDebuffs.Name = "chkEnableAntiDebuffs";
		this.chkEnableAntiDebuffs.Size = new System.Drawing.Size(125, 25);
		this.chkEnableAntiDebuffs.TabIndex = 1;
		this.chkEnableAntiDebuffs.Text = "Anti Debuffs";
		this.ttDescriptions.SetToolTip(this.chkEnableAntiDebuffs, "Block attack debuffs from affecting your character");
		this.chkEnableAntiDebuffs.UseVisualStyleBackColor = false;
		this.chkEnableAntiDebuffs.CheckedChanged += new System.EventHandler(CheckBox_ValueChanged);
		this.pnlAutoLoot.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
		this.pnlAutoLoot.BackColor = System.Drawing.Color.FromArgb(64, 21, 62);
		this.pnlAutoLoot.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
		this.pnlAutoLoot.Controls.Add(this.chkAutoLootManaPotions);
		this.pnlAutoLoot.Controls.Add(this.chkAutoLootDelay);
		this.pnlAutoLoot.Controls.Add(this.chkAutoLootAutoDisable);
		this.pnlAutoLoot.Controls.Add(this.chkAutoLootEggs);
		this.pnlAutoLoot.Controls.Add(this.chkAutoLootBigBags);
		this.pnlAutoLoot.Controls.Add(this.chkAutoLootOverFillMP);
		this.pnlAutoLoot.Controls.Add(this.chkAutoLootOverFillHP);
		this.pnlAutoLoot.Controls.Add(this.chkAutoLootMoveConsumables);
		this.pnlAutoLoot.Controls.Add(this.chkAutoLootStackTokens);
		this.pnlAutoLoot.Controls.Add(this.chkAutoLootMarks);
		this.pnlAutoLoot.Controls.Add(this.chkAutoLootQuests);
		this.pnlAutoLoot.Controls.Add(this.numAutoLootRingTierThreshold);
		this.pnlAutoLoot.Controls.Add(this.lblMinimumRingTier);
		this.pnlAutoLoot.Controls.Add(this.numAutoLootAbilityTierThreshold);
		this.pnlAutoLoot.Controls.Add(this.lblMinimumAbilityTier);
		this.pnlAutoLoot.Controls.Add(this.chkAutoLootUTs);
		this.pnlAutoLoot.Controls.Add(this.chkAutoLootHealingPotions);
		this.pnlAutoLoot.Controls.Add(this.chkAutoLootStatPotions);
		this.pnlAutoLoot.Controls.Add(this.numAutoLootArmorTierThreshold);
		this.pnlAutoLoot.Controls.Add(this.lblMinimumArmorTier);
		this.pnlAutoLoot.Controls.Add(this.numAutoLootWeaponTierThreshold);
		this.pnlAutoLoot.Controls.Add(this.lblMinimumWeaponTier);
		this.pnlAutoLoot.Controls.Add(this.sepAutoLoot);
		this.pnlAutoLoot.Controls.Add(this.chkEnableAutoLoot);
		this.pnlAutoLoot.ForeColor = System.Drawing.Color.White;
		this.pnlAutoLoot.Location = new System.Drawing.Point(6, 751);
		this.pnlAutoLoot.Name = "pnlAutoLoot";
		this.pnlAutoLoot.Size = new System.Drawing.Size(518, 202);
		this.pnlAutoLoot.TabIndex = 23;
		this.ttDescriptions.SetToolTip(this.pnlAutoLoot, "Automatically loot items near your character based on various restrictions");
		this.chkAutoLootManaPotions.AutoSize = true;
		this.chkAutoLootManaPotions.BackColor = System.Drawing.Color.FromArgb(64, 21, 62);
		this.chkAutoLootManaPotions.ForeColor = System.Drawing.Color.White;
		this.chkAutoLootManaPotions.Location = new System.Drawing.Point(138, 117);
		this.chkAutoLootManaPotions.Name = "chkAutoLootManaPotions";
		this.chkAutoLootManaPotions.Size = new System.Drawing.Size(105, 21);
		this.chkAutoLootManaPotions.TabIndex = 31;
		this.chkAutoLootManaPotions.Text = "Loot MP Pots";
		this.ttDescriptions.SetToolTip(this.chkAutoLootManaPotions, "Automatically loot MP potions");
		this.chkAutoLootManaPotions.UseVisualStyleBackColor = false;
		this.chkAutoLootManaPotions.CheckedChanged += new System.EventHandler(CheckBox_ValueChanged);
		this.chkAutoLootDelay.AutoSize = true;
		this.chkAutoLootDelay.BackColor = System.Drawing.Color.FromArgb(64, 21, 62);
		this.chkAutoLootDelay.ForeColor = System.Drawing.Color.White;
		this.chkAutoLootDelay.Location = new System.Drawing.Point(387, 90);
		this.chkAutoLootDelay.Name = "chkAutoLootDelay";
		this.chkAutoLootDelay.Size = new System.Drawing.Size(97, 21);
		this.chkAutoLootDelay.TabIndex = 30;
		this.chkAutoLootDelay.Text = "Public Delay";
		this.ttDescriptions.SetToolTip(this.chkAutoLootDelay, "Adds a delay before looting publicly visible bags\r\nin order to not appear like a hacker");
		this.chkAutoLootDelay.UseVisualStyleBackColor = false;
		this.chkAutoLootDelay.CheckedChanged += new System.EventHandler(CheckBox_ValueChanged);
		this.chkAutoLootAutoDisable.AutoSize = true;
		this.chkAutoLootAutoDisable.BackColor = System.Drawing.Color.FromArgb(64, 21, 62);
		this.chkAutoLootAutoDisable.ForeColor = System.Drawing.Color.White;
		this.chkAutoLootAutoDisable.Location = new System.Drawing.Point(251, 90);
		this.chkAutoLootAutoDisable.Name = "chkAutoLootAutoDisable";
		this.chkAutoLootAutoDisable.Size = new System.Drawing.Size(130, 21);
		this.chkAutoLootAutoDisable.TabIndex = 29;
		this.chkAutoLootAutoDisable.Text = "Disable when AFK";
		this.ttDescriptions.SetToolTip(this.chkAutoLootAutoDisable, "Disable Auto Loot when you haven't moved in a while\r\nto avoid looking like a hacker");
		this.chkAutoLootAutoDisable.UseVisualStyleBackColor = false;
		this.chkAutoLootAutoDisable.CheckedChanged += new System.EventHandler(CheckBox_ValueChanged);
		this.chkAutoLootEggs.AutoSize = true;
		this.chkAutoLootEggs.BackColor = System.Drawing.Color.FromArgb(64, 21, 62);
		this.chkAutoLootEggs.ForeColor = System.Drawing.Color.White;
		this.chkAutoLootEggs.Location = new System.Drawing.Point(349, 144);
		this.chkAutoLootEggs.Name = "chkAutoLootEggs";
		this.chkAutoLootEggs.Size = new System.Drawing.Size(86, 21);
		this.chkAutoLootEggs.TabIndex = 28;
		this.chkAutoLootEggs.Text = "Loot Eggs";
		this.ttDescriptions.SetToolTip(this.chkAutoLootEggs, "Automatically loot pet eggs");
		this.chkAutoLootEggs.UseVisualStyleBackColor = false;
		this.chkAutoLootEggs.CheckedChanged += new System.EventHandler(CheckBox_ValueChanged);
		this.chkAutoLootBigBags.AutoSize = true;
		this.chkAutoLootBigBags.BackColor = System.Drawing.Color.FromArgb(64, 21, 62);
		this.chkAutoLootBigBags.ForeColor = System.Drawing.Color.White;
		this.chkAutoLootBigBags.Location = new System.Drawing.Point(138, 90);
		this.chkAutoLootBigBags.Name = "chkAutoLootBigBags";
		this.chkAutoLootBigBags.Size = new System.Drawing.Size(107, 21);
		this.chkAutoLootBigBags.TabIndex = 27;
		this.chkAutoLootBigBags.Text = "Big Loot Bags";
		this.ttDescriptions.SetToolTip(this.chkAutoLootBigBags, "Make your loot bags visually bigger so you can notice them");
		this.chkAutoLootBigBags.UseVisualStyleBackColor = false;
		this.chkAutoLootBigBags.CheckedChanged += new System.EventHandler(CheckBox_ValueChanged);
		this.chkAutoLootOverFillMP.AutoSize = true;
		this.chkAutoLootOverFillMP.BackColor = System.Drawing.Color.FromArgb(64, 21, 62);
		this.chkAutoLootOverFillMP.ForeColor = System.Drawing.Color.White;
		this.chkAutoLootOverFillMP.Location = new System.Drawing.Point(372, 117);
		this.chkAutoLootOverFillMP.Name = "chkAutoLootOverFillMP";
		this.chkAutoLootOverFillMP.Size = new System.Drawing.Size(120, 21);
		this.chkAutoLootOverFillMP.TabIndex = 26;
		this.chkAutoLootOverFillMP.Text = "Overfill MP Pots";
		this.ttDescriptions.SetToolTip(this.chkAutoLootOverFillMP, "Loot MP potions to bag if potion slot is full");
		this.chkAutoLootOverFillMP.UseVisualStyleBackColor = false;
		this.chkAutoLootOverFillMP.CheckedChanged += new System.EventHandler(CheckBox_ValueChanged);
		this.chkAutoLootOverFillHP.AutoSize = true;
		this.chkAutoLootOverFillHP.BackColor = System.Drawing.Color.FromArgb(64, 21, 62);
		this.chkAutoLootOverFillHP.ForeColor = System.Drawing.Color.White;
		this.chkAutoLootOverFillHP.Location = new System.Drawing.Point(249, 117);
		this.chkAutoLootOverFillHP.Name = "chkAutoLootOverFillHP";
		this.chkAutoLootOverFillHP.Size = new System.Drawing.Size(117, 21);
		this.chkAutoLootOverFillHP.TabIndex = 25;
		this.chkAutoLootOverFillHP.Text = "Overfill HP Pots";
		this.ttDescriptions.SetToolTip(this.chkAutoLootOverFillHP, "Loot HP potions to bag if potion slot is full");
		this.chkAutoLootOverFillHP.UseVisualStyleBackColor = false;
		this.chkAutoLootOverFillHP.CheckedChanged += new System.EventHandler(CheckBox_ValueChanged);
		this.chkAutoLootMoveConsumables.AutoSize = true;
		this.chkAutoLootMoveConsumables.BackColor = System.Drawing.Color.FromArgb(64, 21, 62);
		this.chkAutoLootMoveConsumables.ForeColor = System.Drawing.Color.White;
		this.chkAutoLootMoveConsumables.Location = new System.Drawing.Point(29, 171);
		this.chkAutoLootMoveConsumables.Name = "chkAutoLootMoveConsumables";
		this.chkAutoLootMoveConsumables.Size = new System.Drawing.Size(233, 21);
		this.chkAutoLootMoveConsumables.TabIndex = 24;
		this.chkAutoLootMoveConsumables.Text = "Stack Consumables into Quick Slots";
		this.chkAutoLootMoveConsumables.UseVisualStyleBackColor = false;
		this.chkAutoLootMoveConsumables.CheckedChanged += new System.EventHandler(CheckBox_ValueChanged);
		this.chkAutoLootStackTokens.AutoSize = true;
		this.chkAutoLootStackTokens.BackColor = System.Drawing.Color.FromArgb(64, 21, 62);
		this.chkAutoLootStackTokens.ForeColor = System.Drawing.Color.White;
		this.chkAutoLootStackTokens.Location = new System.Drawing.Point(268, 171);
		this.chkAutoLootStackTokens.Name = "chkAutoLootStackTokens";
		this.chkAutoLootStackTokens.Size = new System.Drawing.Size(132, 21);
		this.chkAutoLootStackTokens.TabIndex = 24;
		this.chkAutoLootStackTokens.Text = "Auto Stack Tokens";
		this.chkAutoLootStackTokens.UseVisualStyleBackColor = false;
		this.chkAutoLootStackTokens.Visible = false;
		this.chkAutoLootStackTokens.CheckedChanged += new System.EventHandler(CheckBox_ValueChanged);
		this.chkAutoLootMarks.AutoSize = true;
		this.chkAutoLootMarks.BackColor = System.Drawing.Color.FromArgb(64, 21, 62);
		this.chkAutoLootMarks.ForeColor = System.Drawing.Color.White;
		this.chkAutoLootMarks.Location = new System.Drawing.Point(30, 144);
		this.chkAutoLootMarks.Name = "chkAutoLootMarks";
		this.chkAutoLootMarks.Size = new System.Drawing.Size(93, 21);
		this.chkAutoLootMarks.TabIndex = 24;
		this.chkAutoLootMarks.Text = "Loot Marks";
		this.ttDescriptions.SetToolTip(this.chkAutoLootMarks, "Automatically loot dungeon marks");
		this.chkAutoLootMarks.UseVisualStyleBackColor = false;
		this.chkAutoLootMarks.CheckedChanged += new System.EventHandler(CheckBox_ValueChanged);
		this.chkAutoLootQuests.AutoSize = true;
		this.chkAutoLootQuests.BackColor = System.Drawing.Color.FromArgb(64, 21, 62);
		this.chkAutoLootQuests.ForeColor = System.Drawing.Color.White;
		this.chkAutoLootQuests.Location = new System.Drawing.Point(30, 90);
		this.chkAutoLootQuests.Name = "chkAutoLootQuests";
		this.chkAutoLootQuests.Size = new System.Drawing.Size(102, 21);
		this.chkAutoLootQuests.TabIndex = 23;
		this.chkAutoLootQuests.Text = "Show Quests";
		this.ttDescriptions.SetToolTip(this.chkAutoLootQuests, "Show quests that will lead you to loot bags");
		this.chkAutoLootQuests.UseVisualStyleBackColor = false;
		this.chkAutoLootQuests.CheckedChanged += new System.EventHandler(CheckBox_ValueChanged);
		this.numAutoLootRingTierThreshold.BackColor = System.Drawing.Color.FromArgb(64, 21, 62);
		this.numAutoLootRingTierThreshold.Font = new System.Drawing.Font("Segoe UI", 8.25f);
		this.numAutoLootRingTierThreshold.ForeColor = System.Drawing.Color.White;
		this.numAutoLootRingTierThreshold.Location = new System.Drawing.Point(353, 62);
		this.numAutoLootRingTierThreshold.Maximum = new decimal(new int[4] { 20, 0, 0, 0 });
		this.numAutoLootRingTierThreshold.Minimum = new decimal(new int[4] { 1, 0, 0, 0 });
		this.numAutoLootRingTierThreshold.Name = "numAutoLootRingTierThreshold";
		this.numAutoLootRingTierThreshold.Size = new System.Drawing.Size(40, 22);
		this.numAutoLootRingTierThreshold.TabIndex = 20;
		this.ttDescriptions.SetToolTip(this.numAutoLootRingTierThreshold, "Minimum tier to loot a ring");
		this.numAutoLootRingTierThreshold.Value = new decimal(new int[4] { 1, 0, 0, 0 });
		this.numAutoLootRingTierThreshold.ValueChanged += new System.EventHandler(NumericUpDown_ValueChanged);
		this.lblMinimumRingTier.AutoSize = true;
		this.lblMinimumRingTier.BackColor = System.Drawing.Color.FromArgb(64, 21, 62);
		this.lblMinimumRingTier.Font = new System.Drawing.Font("Segoe UI", 9.75f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
		this.lblMinimumRingTier.ForeColor = System.Drawing.Color.White;
		this.lblMinimumRingTier.Location = new System.Drawing.Point(222, 63);
		this.lblMinimumRingTier.Name = "lblMinimumRingTier";
		this.lblMinimumRingTier.Size = new System.Drawing.Size(121, 17);
		this.lblMinimumRingTier.TabIndex = 21;
		this.lblMinimumRingTier.Text = "Minimum Ring Tier:";
		this.ttDescriptions.SetToolTip(this.lblMinimumRingTier, "Minimum tier to loot a ring");
		this.numAutoLootAbilityTierThreshold.BackColor = System.Drawing.Color.FromArgb(64, 21, 62);
		this.numAutoLootAbilityTierThreshold.Font = new System.Drawing.Font("Segoe UI", 8.25f);
		this.numAutoLootAbilityTierThreshold.ForeColor = System.Drawing.Color.White;
		this.numAutoLootAbilityTierThreshold.Location = new System.Drawing.Point(353, 34);
		this.numAutoLootAbilityTierThreshold.Maximum = new decimal(new int[4] { 20, 0, 0, 0 });
		this.numAutoLootAbilityTierThreshold.Minimum = new decimal(new int[4] { 1, 0, 0, 0 });
		this.numAutoLootAbilityTierThreshold.Name = "numAutoLootAbilityTierThreshold";
		this.numAutoLootAbilityTierThreshold.Size = new System.Drawing.Size(40, 22);
		this.numAutoLootAbilityTierThreshold.TabIndex = 18;
		this.ttDescriptions.SetToolTip(this.numAutoLootAbilityTierThreshold, "Minimum tier to loot an ability");
		this.numAutoLootAbilityTierThreshold.Value = new decimal(new int[4] { 1, 0, 0, 0 });
		this.numAutoLootAbilityTierThreshold.ValueChanged += new System.EventHandler(NumericUpDown_ValueChanged);
		this.lblMinimumAbilityTier.AutoSize = true;
		this.lblMinimumAbilityTier.BackColor = System.Drawing.Color.FromArgb(64, 21, 62);
		this.lblMinimumAbilityTier.Font = new System.Drawing.Font("Segoe UI", 9.75f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
		this.lblMinimumAbilityTier.ForeColor = System.Drawing.Color.White;
		this.lblMinimumAbilityTier.Location = new System.Drawing.Point(222, 35);
		this.lblMinimumAbilityTier.Name = "lblMinimumAbilityTier";
		this.lblMinimumAbilityTier.Size = new System.Drawing.Size(130, 17);
		this.lblMinimumAbilityTier.TabIndex = 19;
		this.lblMinimumAbilityTier.Text = "Minimum Ability Tier:";
		this.ttDescriptions.SetToolTip(this.lblMinimumAbilityTier, "Minimum tier to loot an ability");
		this.chkAutoLootUTs.AutoSize = true;
		this.chkAutoLootUTs.BackColor = System.Drawing.Color.FromArgb(64, 21, 62);
		this.chkAutoLootUTs.ForeColor = System.Drawing.Color.White;
		this.chkAutoLootUTs.Location = new System.Drawing.Point(133, 144);
		this.chkAutoLootUTs.Name = "chkAutoLootUTs";
		this.chkAutoLootUTs.Size = new System.Drawing.Size(78, 21);
		this.chkAutoLootUTs.TabIndex = 17;
		this.chkAutoLootUTs.Text = "Loot UTs";
		this.ttDescriptions.SetToolTip(this.chkAutoLootUTs, "Automatically loot UTs");
		this.chkAutoLootUTs.UseVisualStyleBackColor = false;
		this.chkAutoLootUTs.CheckedChanged += new System.EventHandler(CheckBox_ValueChanged);
		this.chkAutoLootHealingPotions.AutoSize = true;
		this.chkAutoLootHealingPotions.BackColor = System.Drawing.Color.FromArgb(64, 21, 62);
		this.chkAutoLootHealingPotions.ForeColor = System.Drawing.Color.White;
		this.chkAutoLootHealingPotions.Location = new System.Drawing.Point(30, 117);
		this.chkAutoLootHealingPotions.Name = "chkAutoLootHealingPotions";
		this.chkAutoLootHealingPotions.Size = new System.Drawing.Size(102, 21);
		this.chkAutoLootHealingPotions.TabIndex = 16;
		this.chkAutoLootHealingPotions.Text = "Loot HP Pots";
		this.ttDescriptions.SetToolTip(this.chkAutoLootHealingPotions, "Automatically loot HP potions");
		this.chkAutoLootHealingPotions.UseVisualStyleBackColor = false;
		this.chkAutoLootHealingPotions.CheckedChanged += new System.EventHandler(CheckBox_ValueChanged);
		this.chkAutoLootStatPotions.AutoSize = true;
		this.chkAutoLootStatPotions.BackColor = System.Drawing.Color.FromArgb(64, 21, 62);
		this.chkAutoLootStatPotions.ForeColor = System.Drawing.Color.White;
		this.chkAutoLootStatPotions.Location = new System.Drawing.Point(217, 144);
		this.chkAutoLootStatPotions.Name = "chkAutoLootStatPotions";
		this.chkAutoLootStatPotions.Size = new System.Drawing.Size(126, 21);
		this.chkAutoLootStatPotions.TabIndex = 15;
		this.chkAutoLootStatPotions.Text = "Loot Stat Potions";
		this.ttDescriptions.SetToolTip(this.chkAutoLootStatPotions, "Automatically loot stat increase potions");
		this.chkAutoLootStatPotions.UseVisualStyleBackColor = false;
		this.chkAutoLootStatPotions.CheckedChanged += new System.EventHandler(CheckBox_ValueChanged);
		this.numAutoLootArmorTierThreshold.BackColor = System.Drawing.Color.FromArgb(64, 21, 62);
		this.numAutoLootArmorTierThreshold.Font = new System.Drawing.Font("Segoe UI", 8.25f);
		this.numAutoLootArmorTierThreshold.ForeColor = System.Drawing.Color.White;
		this.numAutoLootArmorTierThreshold.Location = new System.Drawing.Point(171, 62);
		this.numAutoLootArmorTierThreshold.Maximum = new decimal(new int[4] { 20, 0, 0, 0 });
		this.numAutoLootArmorTierThreshold.Minimum = new decimal(new int[4] { 1, 0, 0, 0 });
		this.numAutoLootArmorTierThreshold.Name = "numAutoLootArmorTierThreshold";
		this.numAutoLootArmorTierThreshold.Size = new System.Drawing.Size(40, 22);
		this.numAutoLootArmorTierThreshold.TabIndex = 11;
		this.ttDescriptions.SetToolTip(this.numAutoLootArmorTierThreshold, "Minimum tier to loot an armor");
		this.numAutoLootArmorTierThreshold.Value = new decimal(new int[4] { 1, 0, 0, 0 });
		this.numAutoLootArmorTierThreshold.ValueChanged += new System.EventHandler(NumericUpDown_ValueChanged);
		this.lblMinimumArmorTier.AutoSize = true;
		this.lblMinimumArmorTier.BackColor = System.Drawing.Color.FromArgb(64, 21, 62);
		this.lblMinimumArmorTier.Font = new System.Drawing.Font("Segoe UI", 9.75f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
		this.lblMinimumArmorTier.ForeColor = System.Drawing.Color.White;
		this.lblMinimumArmorTier.Location = new System.Drawing.Point(27, 63);
		this.lblMinimumArmorTier.Name = "lblMinimumArmorTier";
		this.lblMinimumArmorTier.Size = new System.Drawing.Size(132, 17);
		this.lblMinimumArmorTier.TabIndex = 12;
		this.lblMinimumArmorTier.Text = "Minimum Armor Tier:";
		this.ttDescriptions.SetToolTip(this.lblMinimumArmorTier, "Minimum tier to loot an armor");
		this.numAutoLootWeaponTierThreshold.BackColor = System.Drawing.Color.FromArgb(64, 21, 62);
		this.numAutoLootWeaponTierThreshold.Font = new System.Drawing.Font("Segoe UI", 8.25f);
		this.numAutoLootWeaponTierThreshold.ForeColor = System.Drawing.Color.White;
		this.numAutoLootWeaponTierThreshold.Location = new System.Drawing.Point(171, 34);
		this.numAutoLootWeaponTierThreshold.Maximum = new decimal(new int[4] { 20, 0, 0, 0 });
		this.numAutoLootWeaponTierThreshold.Minimum = new decimal(new int[4] { 1, 0, 0, 0 });
		this.numAutoLootWeaponTierThreshold.Name = "numAutoLootWeaponTierThreshold";
		this.numAutoLootWeaponTierThreshold.Size = new System.Drawing.Size(40, 22);
		this.numAutoLootWeaponTierThreshold.TabIndex = 9;
		this.ttDescriptions.SetToolTip(this.numAutoLootWeaponTierThreshold, "Minimum tier to loot a weapon");
		this.numAutoLootWeaponTierThreshold.Value = new decimal(new int[4] { 1, 0, 0, 0 });
		this.numAutoLootWeaponTierThreshold.ValueChanged += new System.EventHandler(NumericUpDown_ValueChanged);
		this.lblMinimumWeaponTier.AutoSize = true;
		this.lblMinimumWeaponTier.BackColor = System.Drawing.Color.FromArgb(64, 21, 62);
		this.lblMinimumWeaponTier.Font = new System.Drawing.Font("Segoe UI", 9.75f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
		this.lblMinimumWeaponTier.ForeColor = System.Drawing.Color.White;
		this.lblMinimumWeaponTier.Location = new System.Drawing.Point(27, 35);
		this.lblMinimumWeaponTier.Name = "lblMinimumWeaponTier";
		this.lblMinimumWeaponTier.Size = new System.Drawing.Size(143, 17);
		this.lblMinimumWeaponTier.TabIndex = 10;
		this.lblMinimumWeaponTier.Text = "Minimum Weapon Tier:";
		this.ttDescriptions.SetToolTip(this.lblMinimumWeaponTier, "Minimum tier to loot a weapon");
		this.sepAutoLoot.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
		this.sepAutoLoot.BackColor = System.Drawing.Color.FromArgb(64, 21, 62);
		this.sepAutoLoot.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
		this.sepAutoLoot.Location = new System.Drawing.Point(8, 28);
		this.sepAutoLoot.Name = "sepAutoLoot";
		this.sepAutoLoot.Size = new System.Drawing.Size(501, 1);
		this.sepAutoLoot.TabIndex = 6;
		this.chkEnableAutoLoot.AutoSize = true;
		this.chkEnableAutoLoot.BackColor = System.Drawing.Color.FromArgb(64, 21, 62);
		this.chkEnableAutoLoot.Font = new System.Drawing.Font("Segoe UI", 12f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, 0);
		this.chkEnableAutoLoot.ForeColor = System.Drawing.Color.White;
		this.chkEnableAutoLoot.Location = new System.Drawing.Point(8, 3);
		this.chkEnableAutoLoot.Name = "chkEnableAutoLoot";
		this.chkEnableAutoLoot.Size = new System.Drawing.Size(104, 25);
		this.chkEnableAutoLoot.TabIndex = 1;
		this.chkEnableAutoLoot.Text = "Auto Loot";
		this.ttDescriptions.SetToolTip(this.chkEnableAutoLoot, "Automatically loot items near your character based on various restrictions");
		this.chkEnableAutoLoot.UseVisualStyleBackColor = false;
		this.chkEnableAutoLoot.CheckedChanged += new System.EventHandler(CheckBox_ValueChanged);
		this.pnlAutoAbility.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
		this.pnlAutoAbility.BackColor = System.Drawing.Color.FromArgb(74, 31, 32);
		this.pnlAutoAbility.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
		this.pnlAutoAbility.Controls.Add(this.chkAutoAbilityPeacekeeperSpellbomb);
		this.pnlAutoAbility.Controls.Add(this.chkAutoAbilityMysticTargetSelf);
		this.pnlAutoAbility.Controls.Add(this.chkAutoAbilityAutoMP);
		this.pnlAutoAbility.Controls.Add(this.chkAutoAbilityChargeDruidMeter);
		this.pnlAutoAbility.Controls.Add(this.chkAutoAbilityPenetratingBlastOffset);
		this.pnlAutoAbility.Controls.Add(this.chkAutoAbilityNotifications);
		this.pnlAutoAbility.Controls.Add(this.lblAutoAbilityHealHpPercentAfter);
		this.pnlAutoAbility.Controls.Add(this.numAutoAbilityHealHpPercent);
		this.pnlAutoAbility.Controls.Add(this.lblAutoAbilityHealHpPercent);
		this.pnlAutoAbility.Controls.Add(this.chkAutoAbilityClosestEnemy);
		this.pnlAutoAbility.Controls.Add(this.lblAutoAbilityCustomDelay);
		this.pnlAutoAbility.Controls.Add(this.lblMinimumEnemyGroupSize);
		this.pnlAutoAbility.Controls.Add(this.chkAutoAbilityStrongestEnemy);
		this.pnlAutoAbility.Controls.Add(this.lblAutoAbilityCustomDelayMilliseconds);
		this.pnlAutoAbility.Controls.Add(this.numAutoAbilityCustomDelay);
		this.pnlAutoAbility.Controls.Add(this.numAutoAbilityMinimumGroupSizeThreshold);
		this.pnlAutoAbility.Controls.Add(this.chkAutoAbilityWeakestEnemy);
		this.pnlAutoAbility.Controls.Add(this.lblMinimumEnemyGroupSizeAfter);
		this.pnlAutoAbility.Controls.Add(this.lblMinimumMPLeftAfter);
		this.pnlAutoAbility.Controls.Add(this.numAutoAbilityMinimumManaLeftThreshold);
		this.pnlAutoAbility.Controls.Add(this.lblMinimumMPLeft);
		this.pnlAutoAbility.Controls.Add(this.lblMinimumEnemyHPAfter);
		this.pnlAutoAbility.Controls.Add(this.numAutoAbilityMinimumEnemyHealthThreshold);
		this.pnlAutoAbility.Controls.Add(this.lblMinimumEnemyHP);
		this.pnlAutoAbility.Controls.Add(this.sepAutoAbility);
		this.pnlAutoAbility.Controls.Add(this.chkEnableAutoAbility);
		this.pnlAutoAbility.Location = new System.Drawing.Point(6, 464);
		this.pnlAutoAbility.Name = "pnlAutoAbility";
		this.pnlAutoAbility.Size = new System.Drawing.Size(518, 281);
		this.pnlAutoAbility.TabIndex = 21;
		this.ttDescriptions.SetToolTip(this.pnlAutoAbility, "Autmatically use your class ability based on various settings\r\nDoes NOT support Archer, Knight, or Ninja");
		this.chkAutoAbilityPeacekeeperSpellbomb.Checked = true;
		this.chkAutoAbilityPeacekeeperSpellbomb.CheckState = System.Windows.Forms.CheckState.Checked;
		this.chkAutoAbilityPeacekeeperSpellbomb.Enabled = false;
		this.chkAutoAbilityPeacekeeperSpellbomb.Location = new System.Drawing.Point(402, 225);
		this.chkAutoAbilityPeacekeeperSpellbomb.Name = "chkAutoAbilityPeacekeeperSpellbomb";
		this.chkAutoAbilityPeacekeeperSpellbomb.Size = new System.Drawing.Size(208, 24);
		this.chkAutoAbilityPeacekeeperSpellbomb.TabIndex = 40;
		this.chkAutoAbilityPeacekeeperSpellbomb.Text = "Peacekeeper Spellbomb Mode";
		this.ttDescriptions.SetToolTip(this.chkAutoAbilityPeacekeeperSpellbomb, "Spam usage of Peacekeeper mace for DPSing");
		this.chkAutoAbilityPeacekeeperSpellbomb.UseVisualStyleBackColor = true;
		this.chkAutoAbilityPeacekeeperSpellbomb.Visible = false;
		this.chkAutoAbilityPeacekeeperSpellbomb.CheckedChanged += new System.EventHandler(CheckBox_ValueChanged);
		this.chkAutoAbilityMysticTargetSelf.Location = new System.Drawing.Point(280, 225);
		this.chkAutoAbilityMysticTargetSelf.Name = "chkAutoAbilityMysticTargetSelf";
		this.chkAutoAbilityMysticTargetSelf.Size = new System.Drawing.Size(161, 24);
		this.chkAutoAbilityMysticTargetSelf.TabIndex = 40;
		this.chkAutoAbilityMysticTargetSelf.Text = "Mystic Orb Target Self";
		this.chkAutoAbilityMysticTargetSelf.UseVisualStyleBackColor = true;
		this.chkAutoAbilityMysticTargetSelf.CheckedChanged += new System.EventHandler(CheckBox_ValueChanged);
		this.chkAutoAbilityAutoMP.AutoSize = true;
		this.chkAutoAbilityAutoMP.BackColor = System.Drawing.Color.FromArgb(74, 31, 32);
		this.chkAutoAbilityAutoMP.Location = new System.Drawing.Point(280, 176);
		this.chkAutoAbilityAutoMP.Name = "chkAutoAbilityAutoMP";
		this.chkAutoAbilityAutoMP.Size = new System.Drawing.Size(176, 21);
		this.chkAutoAbilityAutoMP.TabIndex = 39;
		this.chkAutoAbilityAutoMP.Text = "Auto drink MP after Quiet";
		this.ttDescriptions.SetToolTip(this.chkAutoAbilityAutoMP, "Automatically drink an MP potion after Quiet ends");
		this.chkAutoAbilityAutoMP.UseVisualStyleBackColor = false;
		this.chkAutoAbilityAutoMP.CheckedChanged += new System.EventHandler(CheckBox_ValueChanged);
		this.chkAutoAbilityChargeDruidMeter.AutoSize = true;
		this.chkAutoAbilityChargeDruidMeter.BackColor = System.Drawing.Color.FromArgb(74, 31, 32);
		this.chkAutoAbilityChargeDruidMeter.Location = new System.Drawing.Point(30, 253);
		this.chkAutoAbilityChargeDruidMeter.Name = "chkAutoAbilityChargeDruidMeter";
		this.chkAutoAbilityChargeDruidMeter.Size = new System.Drawing.Size(292, 21);
		this.chkAutoAbilityChargeDruidMeter.TabIndex = 38;
		this.chkAutoAbilityChargeDruidMeter.Text = "Charge Druid Meter (will not shoot projectile)";
		this.ttDescriptions.SetToolTip(this.chkAutoAbilityChargeDruidMeter, "AutoAbility needs a rewrite for ability projectile shooting to work. It is on our TODO list, apologies for the current situation. ");
		this.chkAutoAbilityChargeDruidMeter.UseVisualStyleBackColor = false;
		this.chkAutoAbilityChargeDruidMeter.CheckedChanged += new System.EventHandler(CheckBox_ValueChanged);
		this.chkAutoAbilityPenetratingBlastOffset.AutoSize = true;
		this.chkAutoAbilityPenetratingBlastOffset.BackColor = System.Drawing.Color.FromArgb(74, 31, 32);
		this.chkAutoAbilityPenetratingBlastOffset.Location = new System.Drawing.Point(30, 227);
		this.chkAutoAbilityPenetratingBlastOffset.Name = "chkAutoAbilityPenetratingBlastOffset";
		this.chkAutoAbilityPenetratingBlastOffset.Size = new System.Drawing.Size(195, 21);
		this.chkAutoAbilityPenetratingBlastOffset.TabIndex = 38;
		this.chkAutoAbilityPenetratingBlastOffset.Text = "Offset Penetrating Blast Spell";
		this.chkAutoAbilityPenetratingBlastOffset.UseVisualStyleBackColor = false;
		this.chkAutoAbilityPenetratingBlastOffset.CheckedChanged += new System.EventHandler(CheckBox_ValueChanged);
		this.chkAutoAbilityNotifications.AutoSize = true;
		this.chkAutoAbilityNotifications.BackColor = System.Drawing.Color.FromArgb(74, 31, 32);
		this.chkAutoAbilityNotifications.Location = new System.Drawing.Point(30, 176);
		this.chkAutoAbilityNotifications.Name = "chkAutoAbilityNotifications";
		this.chkAutoAbilityNotifications.Size = new System.Drawing.Size(226, 21);
		this.chkAutoAbilityNotifications.TabIndex = 38;
		this.chkAutoAbilityNotifications.Text = "Show Auto Activation Notifications";
		this.ttDescriptions.SetToolTip(this.chkAutoAbilityNotifications, "Show a notification when auto ");
		this.chkAutoAbilityNotifications.UseVisualStyleBackColor = false;
		this.chkAutoAbilityNotifications.CheckedChanged += new System.EventHandler(CheckBox_ValueChanged);
		this.lblAutoAbilityHealHpPercentAfter.AutoSize = true;
		this.lblAutoAbilityHealHpPercentAfter.BackColor = System.Drawing.Color.FromArgb(74, 31, 32);
		this.lblAutoAbilityHealHpPercentAfter.ForeColor = System.Drawing.Color.White;
		this.lblAutoAbilityHealHpPercentAfter.Location = new System.Drawing.Point(254, 92);
		this.lblAutoAbilityHealHpPercentAfter.Name = "lblAutoAbilityHealHpPercentAfter";
		this.lblAutoAbilityHealHpPercentAfter.Size = new System.Drawing.Size(174, 17);
		this.lblAutoAbilityHealHpPercentAfter.TabIndex = 37;
		this.lblAutoAbilityHealHpPercentAfter.Text = "%  (for Priest healing tomes)";
		this.ttDescriptions.SetToolTip(this.lblAutoAbilityHealHpPercentAfter, "Minimum HP to use healing abilities at");
		this.numAutoAbilityHealHpPercent.BackColor = System.Drawing.Color.FromArgb(74, 31, 32);
		this.numAutoAbilityHealHpPercent.ForeColor = System.Drawing.Color.White;
		this.numAutoAbilityHealHpPercent.Location = new System.Drawing.Point(201, 90);
		this.numAutoAbilityHealHpPercent.Name = "numAutoAbilityHealHpPercent";
		this.numAutoAbilityHealHpPercent.Size = new System.Drawing.Size(50, 25);
		this.numAutoAbilityHealHpPercent.TabIndex = 36;
		this.ttDescriptions.SetToolTip(this.numAutoAbilityHealHpPercent, "Minimum HP to use healing abilities at");
		this.numAutoAbilityHealHpPercent.Value = new decimal(new int[4] { 45, 0, 0, 0 });
		this.numAutoAbilityHealHpPercent.ValueChanged += new System.EventHandler(NumericUpDown_ValueChanged);
		this.lblAutoAbilityHealHpPercent.AutoSize = true;
		this.lblAutoAbilityHealHpPercent.BackColor = System.Drawing.Color.FromArgb(74, 31, 32);
		this.lblAutoAbilityHealHpPercent.ForeColor = System.Drawing.Color.White;
		this.lblAutoAbilityHealHpPercent.Location = new System.Drawing.Point(26, 92);
		this.lblAutoAbilityHealHpPercent.Name = "lblAutoAbilityHealHpPercent";
		this.lblAutoAbilityHealHpPercent.Size = new System.Drawing.Size(140, 17);
		this.lblAutoAbilityHealHpPercent.TabIndex = 35;
		this.lblAutoAbilityHealHpPercent.Text = "Minimum Autoheal HP:";
		this.ttDescriptions.SetToolTip(this.lblAutoAbilityHealHpPercent, "Minimum HP to use healing abilities at");
		this.chkAutoAbilityClosestEnemy.AutoSize = true;
		this.chkAutoAbilityClosestEnemy.BackColor = System.Drawing.Color.FromArgb(74, 31, 32);
		this.chkAutoAbilityClosestEnemy.ForeColor = System.Drawing.Color.White;
		this.chkAutoAbilityClosestEnemy.Location = new System.Drawing.Point(280, 149);
		this.chkAutoAbilityClosestEnemy.Name = "chkAutoAbilityClosestEnemy";
		this.chkAutoAbilityClosestEnemy.Size = new System.Drawing.Size(109, 21);
		this.chkAutoAbilityClosestEnemy.TabIndex = 34;
		this.chkAutoAbilityClosestEnemy.TabStop = true;
		this.chkAutoAbilityClosestEnemy.Text = "Target Closest";
		this.ttDescriptions.SetToolTip(this.chkAutoAbilityClosestEnemy, "Use damaging abilities on the closest nearby enemy");
		this.chkAutoAbilityClosestEnemy.UseVisualStyleBackColor = false;
		this.chkAutoAbilityClosestEnemy.CheckedChanged += new System.EventHandler(RadioButton_ValueChanged);
		this.lblAutoAbilityCustomDelay.AutoSize = true;
		this.lblAutoAbilityCustomDelay.BackColor = System.Drawing.Color.FromArgb(74, 31, 32);
		this.lblAutoAbilityCustomDelay.Font = new System.Drawing.Font("Segoe UI", 9.75f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
		this.lblAutoAbilityCustomDelay.ForeColor = System.Drawing.Color.White;
		this.lblAutoAbilityCustomDelay.Location = new System.Drawing.Point(26, 203);
		this.lblAutoAbilityCustomDelay.Name = "lblAutoAbilityCustomDelay";
		this.lblAutoAbilityCustomDelay.Size = new System.Drawing.Size(157, 17);
		this.lblAutoAbilityCustomDelay.TabIndex = 27;
		this.lblAutoAbilityCustomDelay.Text = "Custom Ability Cooldown:";
		this.lblMinimumEnemyGroupSize.AutoSize = true;
		this.lblMinimumEnemyGroupSize.BackColor = System.Drawing.Color.FromArgb(74, 31, 32);
		this.lblMinimumEnemyGroupSize.Font = new System.Drawing.Font("Segoe UI", 9.75f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
		this.lblMinimumEnemyGroupSize.ForeColor = System.Drawing.Color.White;
		this.lblMinimumEnemyGroupSize.Location = new System.Drawing.Point(26, 122);
		this.lblMinimumEnemyGroupSize.Name = "lblMinimumEnemyGroupSize";
		this.lblMinimumEnemyGroupSize.Size = new System.Drawing.Size(175, 17);
		this.lblMinimumEnemyGroupSize.TabIndex = 27;
		this.lblMinimumEnemyGroupSize.Text = "Minimum Enemy Group Size:";
		this.ttDescriptions.SetToolTip(this.lblMinimumEnemyGroupSize, "Minimum amount of enemies to trigger AoE ability uses");
		this.chkAutoAbilityStrongestEnemy.AutoSize = true;
		this.chkAutoAbilityStrongestEnemy.BackColor = System.Drawing.Color.FromArgb(74, 31, 32);
		this.chkAutoAbilityStrongestEnemy.ForeColor = System.Drawing.Color.White;
		this.chkAutoAbilityStrongestEnemy.Location = new System.Drawing.Point(151, 149);
		this.chkAutoAbilityStrongestEnemy.Name = "chkAutoAbilityStrongestEnemy";
		this.chkAutoAbilityStrongestEnemy.Size = new System.Drawing.Size(123, 21);
		this.chkAutoAbilityStrongestEnemy.TabIndex = 33;
		this.chkAutoAbilityStrongestEnemy.TabStop = true;
		this.chkAutoAbilityStrongestEnemy.Text = "Target Strongest";
		this.ttDescriptions.SetToolTip(this.chkAutoAbilityStrongestEnemy, "Use damaging abilities on the strongest nearby enemy");
		this.chkAutoAbilityStrongestEnemy.UseVisualStyleBackColor = false;
		this.chkAutoAbilityStrongestEnemy.CheckedChanged += new System.EventHandler(RadioButton_ValueChanged);
		this.lblAutoAbilityCustomDelayMilliseconds.AutoSize = true;
		this.lblAutoAbilityCustomDelayMilliseconds.BackColor = System.Drawing.Color.FromArgb(74, 31, 32);
		this.lblAutoAbilityCustomDelayMilliseconds.Font = new System.Drawing.Font("Segoe UI", 9.75f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
		this.lblAutoAbilityCustomDelayMilliseconds.ForeColor = System.Drawing.Color.White;
		this.lblAutoAbilityCustomDelayMilliseconds.Location = new System.Drawing.Point(257, 203);
		this.lblAutoAbilityCustomDelayMilliseconds.Name = "lblAutoAbilityCustomDelayMilliseconds";
		this.lblAutoAbilityCustomDelayMilliseconds.Size = new System.Drawing.Size(79, 17);
		this.lblAutoAbilityCustomDelayMilliseconds.TabIndex = 18;
		this.lblAutoAbilityCustomDelayMilliseconds.Text = "milliseconds";
		this.numAutoAbilityCustomDelay.BackColor = System.Drawing.Color.FromArgb(74, 31, 32);
		this.numAutoAbilityCustomDelay.Font = new System.Drawing.Font("Segoe UI", 8.25f);
		this.numAutoAbilityCustomDelay.ForeColor = System.Drawing.Color.White;
		this.numAutoAbilityCustomDelay.Increment = new decimal(new int[4] { 1000, 0, 0, 0 });
		this.numAutoAbilityCustomDelay.Location = new System.Drawing.Point(201, 203);
		this.numAutoAbilityCustomDelay.Maximum = new decimal(new int[4] { 10000, 0, 0, 0 });
		this.numAutoAbilityCustomDelay.Minimum = new decimal(new int[4] { 1000, 0, 0, 0 });
		this.numAutoAbilityCustomDelay.Name = "numAutoAbilityCustomDelay";
		this.numAutoAbilityCustomDelay.Size = new System.Drawing.Size(50, 22);
		this.numAutoAbilityCustomDelay.TabIndex = 26;
		this.numAutoAbilityCustomDelay.Value = new decimal(new int[4] { 1000, 0, 0, 0 });
		this.numAutoAbilityCustomDelay.ValueChanged += new System.EventHandler(NumericUpDown_ValueChanged);
		this.numAutoAbilityMinimumGroupSizeThreshold.BackColor = System.Drawing.Color.FromArgb(74, 31, 32);
		this.numAutoAbilityMinimumGroupSizeThreshold.Font = new System.Drawing.Font("Segoe UI", 8.25f);
		this.numAutoAbilityMinimumGroupSizeThreshold.ForeColor = System.Drawing.Color.White;
		this.numAutoAbilityMinimumGroupSizeThreshold.Location = new System.Drawing.Point(201, 121);
		this.numAutoAbilityMinimumGroupSizeThreshold.Name = "numAutoAbilityMinimumGroupSizeThreshold";
		this.numAutoAbilityMinimumGroupSizeThreshold.Size = new System.Drawing.Size(50, 22);
		this.numAutoAbilityMinimumGroupSizeThreshold.TabIndex = 26;
		this.ttDescriptions.SetToolTip(this.numAutoAbilityMinimumGroupSizeThreshold, "Minimum amount of enemies surrounding another to trigger AoE ability uses");
		this.numAutoAbilityMinimumGroupSizeThreshold.Value = new decimal(new int[4] { 1, 0, 0, 0 });
		this.numAutoAbilityMinimumGroupSizeThreshold.ValueChanged += new System.EventHandler(NumericUpDown_ValueChanged);
		this.chkAutoAbilityWeakestEnemy.AutoSize = true;
		this.chkAutoAbilityWeakestEnemy.BackColor = System.Drawing.Color.FromArgb(74, 31, 32);
		this.chkAutoAbilityWeakestEnemy.ForeColor = System.Drawing.Color.White;
		this.chkAutoAbilityWeakestEnemy.Location = new System.Drawing.Point(30, 149);
		this.chkAutoAbilityWeakestEnemy.Name = "chkAutoAbilityWeakestEnemy";
		this.chkAutoAbilityWeakestEnemy.Size = new System.Drawing.Size(115, 21);
		this.chkAutoAbilityWeakestEnemy.TabIndex = 32;
		this.chkAutoAbilityWeakestEnemy.TabStop = true;
		this.chkAutoAbilityWeakestEnemy.Text = "Target Weakest";
		this.ttDescriptions.SetToolTip(this.chkAutoAbilityWeakestEnemy, "Use damaging abilities on the weakest nearby enemy");
		this.chkAutoAbilityWeakestEnemy.UseVisualStyleBackColor = false;
		this.chkAutoAbilityWeakestEnemy.CheckedChanged += new System.EventHandler(RadioButton_ValueChanged);
		this.lblMinimumEnemyGroupSizeAfter.AutoSize = true;
		this.lblMinimumEnemyGroupSizeAfter.BackColor = System.Drawing.Color.FromArgb(74, 31, 32);
		this.lblMinimumEnemyGroupSizeAfter.Font = new System.Drawing.Font("Segoe UI", 9.75f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
		this.lblMinimumEnemyGroupSizeAfter.ForeColor = System.Drawing.Color.White;
		this.lblMinimumEnemyGroupSizeAfter.Location = new System.Drawing.Point(274, 122);
		this.lblMinimumEnemyGroupSizeAfter.Name = "lblMinimumEnemyGroupSizeAfter";
		this.lblMinimumEnemyGroupSizeAfter.Size = new System.Drawing.Size(158, 17);
		this.lblMinimumEnemyGroupSizeAfter.TabIndex = 31;
		this.lblMinimumEnemyGroupSizeAfter.Text = "(for abilities that multi-hit)";
		this.ttDescriptions.SetToolTip(this.lblMinimumEnemyGroupSizeAfter, "Minimum amount of enemies to trigger AoE ability uses");
		this.lblMinimumMPLeftAfter.AutoSize = true;
		this.lblMinimumMPLeftAfter.BackColor = System.Drawing.Color.FromArgb(74, 31, 32);
		this.lblMinimumMPLeftAfter.Font = new System.Drawing.Font("Segoe UI", 9.75f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
		this.lblMinimumMPLeftAfter.ForeColor = System.Drawing.Color.White;
		this.lblMinimumMPLeftAfter.Location = new System.Drawing.Point(254, 35);
		this.lblMinimumMPLeftAfter.Name = "lblMinimumMPLeftAfter";
		this.lblMinimumMPLeftAfter.Size = new System.Drawing.Size(117, 17);
		this.lblMinimumMPLeftAfter.TabIndex = 30;
		this.lblMinimumMPLeftAfter.Text = "%  (for all abilities)";
		this.ttDescriptions.SetToolTip(this.lblMinimumMPLeftAfter, "Minimum MP your character must have to\r\ntrigger Auto Ability");
		this.numAutoAbilityMinimumManaLeftThreshold.BackColor = System.Drawing.Color.FromArgb(74, 31, 32);
		this.numAutoAbilityMinimumManaLeftThreshold.Font = new System.Drawing.Font("Segoe UI", 8.25f);
		this.numAutoAbilityMinimumManaLeftThreshold.ForeColor = System.Drawing.Color.White;
		this.numAutoAbilityMinimumManaLeftThreshold.Increment = new decimal(new int[4] { 5, 0, 0, 0 });
		this.numAutoAbilityMinimumManaLeftThreshold.Location = new System.Drawing.Point(201, 34);
		this.numAutoAbilityMinimumManaLeftThreshold.Name = "numAutoAbilityMinimumManaLeftThreshold";
		this.numAutoAbilityMinimumManaLeftThreshold.Size = new System.Drawing.Size(50, 22);
		this.numAutoAbilityMinimumManaLeftThreshold.TabIndex = 28;
		this.ttDescriptions.SetToolTip(this.numAutoAbilityMinimumManaLeftThreshold, "Minimum MP your character must have to\r\ntrigger Auto Ability");
		this.numAutoAbilityMinimumManaLeftThreshold.ValueChanged += new System.EventHandler(NumericUpDown_ValueChanged);
		this.lblMinimumMPLeft.AutoSize = true;
		this.lblMinimumMPLeft.BackColor = System.Drawing.Color.FromArgb(74, 31, 32);
		this.lblMinimumMPLeft.Font = new System.Drawing.Font("Segoe UI", 9.75f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
		this.lblMinimumMPLeft.ForeColor = System.Drawing.Color.White;
		this.lblMinimumMPLeft.Location = new System.Drawing.Point(26, 35);
		this.lblMinimumMPLeft.Name = "lblMinimumMPLeft";
		this.lblMinimumMPLeft.Size = new System.Drawing.Size(113, 17);
		this.lblMinimumMPLeft.TabIndex = 29;
		this.lblMinimumMPLeft.Text = "Minimum MP Left:";
		this.ttDescriptions.SetToolTip(this.lblMinimumMPLeft, "Minimum MP your character must have to\r\ntrigger Auto Ability");
		this.lblMinimumEnemyHPAfter.AutoSize = true;
		this.lblMinimumEnemyHPAfter.BackColor = System.Drawing.Color.FromArgb(74, 31, 32);
		this.lblMinimumEnemyHPAfter.Font = new System.Drawing.Font("Segoe UI", 9.75f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
		this.lblMinimumEnemyHPAfter.ForeColor = System.Drawing.Color.White;
		this.lblMinimumEnemyHPAfter.Location = new System.Drawing.Point(254, 63);
		this.lblMinimumEnemyHPAfter.Name = "lblMinimumEnemyHPAfter";
		this.lblMinimumEnemyHPAfter.Size = new System.Drawing.Size(202, 17);
		this.lblMinimumEnemyHPAfter.TabIndex = 24;
		this.lblMinimumEnemyHPAfter.Text = "\t (for abilities that target enemies)";
		this.ttDescriptions.SetToolTip(this.lblMinimumEnemyHPAfter, "Minimum HP left for a mob to be considered");
		this.numAutoAbilityMinimumEnemyHealthThreshold.BackColor = System.Drawing.Color.FromArgb(74, 31, 32);
		this.numAutoAbilityMinimumEnemyHealthThreshold.Font = new System.Drawing.Font("Segoe UI", 8.25f);
		this.numAutoAbilityMinimumEnemyHealthThreshold.ForeColor = System.Drawing.Color.White;
		this.numAutoAbilityMinimumEnemyHealthThreshold.Increment = new decimal(new int[4] { 5, 0, 0, 0 });
		this.numAutoAbilityMinimumEnemyHealthThreshold.Location = new System.Drawing.Point(201, 62);
		this.numAutoAbilityMinimumEnemyHealthThreshold.Maximum = new decimal(new int[4] { 99999, 0, 0, 0 });
		this.numAutoAbilityMinimumEnemyHealthThreshold.Name = "numAutoAbilityMinimumEnemyHealthThreshold";
		this.numAutoAbilityMinimumEnemyHealthThreshold.Size = new System.Drawing.Size(50, 22);
		this.numAutoAbilityMinimumEnemyHealthThreshold.TabIndex = 20;
		this.ttDescriptions.SetToolTip(this.numAutoAbilityMinimumEnemyHealthThreshold, "Minimum HP left for a mob to be considered");
		this.numAutoAbilityMinimumEnemyHealthThreshold.ValueChanged += new System.EventHandler(NumericUpDown_ValueChanged);
		this.lblMinimumEnemyHP.AutoSize = true;
		this.lblMinimumEnemyHP.BackColor = System.Drawing.Color.FromArgb(74, 31, 32);
		this.lblMinimumEnemyHP.Font = new System.Drawing.Font("Segoe UI", 9.75f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
		this.lblMinimumEnemyHP.ForeColor = System.Drawing.Color.White;
		this.lblMinimumEnemyHP.Location = new System.Drawing.Point(26, 63);
		this.lblMinimumEnemyHP.Name = "lblMinimumEnemyHP";
		this.lblMinimumEnemyHP.Size = new System.Drawing.Size(127, 17);
		this.lblMinimumEnemyHP.TabIndex = 21;
		this.lblMinimumEnemyHP.Text = "Minimum Enemy HP:";
		this.ttDescriptions.SetToolTip(this.lblMinimumEnemyHP, "Minimum HP left for a mob to be considered");
		this.sepAutoAbility.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
		this.sepAutoAbility.BackColor = System.Drawing.Color.FromArgb(74, 31, 32);
		this.sepAutoAbility.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
		this.sepAutoAbility.Location = new System.Drawing.Point(8, 28);
		this.sepAutoAbility.Name = "sepAutoAbility";
		this.sepAutoAbility.Size = new System.Drawing.Size(501, 1);
		this.sepAutoAbility.TabIndex = 5;
		this.chkEnableAutoAbility.AutoSize = true;
		this.chkEnableAutoAbility.BackColor = System.Drawing.Color.FromArgb(74, 31, 32);
		this.chkEnableAutoAbility.Font = new System.Drawing.Font("Segoe UI", 12f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, 0);
		this.chkEnableAutoAbility.ForeColor = System.Drawing.Color.White;
		this.chkEnableAutoAbility.Location = new System.Drawing.Point(8, 3);
		this.chkEnableAutoAbility.Name = "chkEnableAutoAbility";
		this.chkEnableAutoAbility.Size = new System.Drawing.Size(121, 25);
		this.chkEnableAutoAbility.TabIndex = 1;
		this.chkEnableAutoAbility.Text = "Auto Ability";
		this.ttDescriptions.SetToolTip(this.chkEnableAutoAbility, "Autmatically use your class ability based on various settings\r\nDoes NOT support Archer, Knight, or Ninja");
		this.chkEnableAutoAbility.UseVisualStyleBackColor = false;
		this.chkEnableAutoAbility.CheckedChanged += new System.EventHandler(CheckBox_ValueChanged);
		this.pnlAutoNexus.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
		this.pnlAutoNexus.BackColor = System.Drawing.Color.FromArgb(44, 41, 62);
		this.pnlAutoNexus.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
		this.pnlAutoNexus.Controls.Add(this.numAutoNexusPercentageThreshold);
		this.pnlAutoNexus.Controls.Add(this.chkEnableAutoPotMP);
		this.pnlAutoNexus.Controls.Add(this.chkEnableAutoNexusOnly);
		this.pnlAutoNexus.Controls.Add(this.chkEnableAutoPotHP);
		this.pnlAutoNexus.Controls.Add(this.chkAutoNexusInstantNexus);
		this.pnlAutoNexus.Controls.Add(this.chkAutoNexusSyncHp);
		this.pnlAutoNexus.Controls.Add(this.chkAutoNexusUseClientHp);
		this.pnlAutoNexus.Controls.Add(this.chkAutoNexusDrinkFromInventory);
		this.pnlAutoNexus.Controls.Add(this.label5);
		this.pnlAutoNexus.Controls.Add(this.label3);
		this.pnlAutoNexus.Controls.Add(this.numAutoNexusDrinkMpThreshold);
		this.pnlAutoNexus.Controls.Add(this.numAutoNexusDrinkThreshold);
		this.pnlAutoNexus.Controls.Add(this.chkAutoNexusReplaceFameWithHealth);
		this.pnlAutoNexus.Controls.Add(this.lblAutoDrinkDelayMilliseconds);
		this.pnlAutoNexus.Controls.Add(this.numAutoNexusHpPotDelay);
		this.pnlAutoNexus.Controls.Add(this.lblAutoDrinkDelay);
		this.pnlAutoNexus.Controls.Add(this.chkAutoNexusShowInformation);
		this.pnlAutoNexus.Controls.Add(this.lblAutoNexusAfter);
		this.pnlAutoNexus.Controls.Add(this.sepAutoNexus);
		this.pnlAutoNexus.Controls.Add(this.chkEnableAutoNexus);
		this.pnlAutoNexus.Location = new System.Drawing.Point(6, 82);
		this.pnlAutoNexus.Name = "pnlAutoNexus";
		this.pnlAutoNexus.Size = new System.Drawing.Size(518, 198);
		this.pnlAutoNexus.TabIndex = 20;
		this.ttDescriptions.SetToolTip(this.pnlAutoNexus, resources.GetString("pnlAutoNexus.ToolTip"));
		this.numAutoNexusPercentageThreshold.BackColor = System.Drawing.Color.FromArgb(44, 41, 62);
		this.numAutoNexusPercentageThreshold.Font = new System.Drawing.Font("Segoe UI", 8.25f);
		this.numAutoNexusPercentageThreshold.ForeColor = System.Drawing.Color.White;
		this.numAutoNexusPercentageThreshold.Location = new System.Drawing.Point(122, 34);
		this.numAutoNexusPercentageThreshold.Minimum = new decimal(new int[4] { 1, 0, 0, 0 });
		this.numAutoNexusPercentageThreshold.Name = "numAutoNexusPercentageThreshold";
		this.numAutoNexusPercentageThreshold.Size = new System.Drawing.Size(50, 22);
		this.numAutoNexusPercentageThreshold.TabIndex = 1;
		this.ttDescriptions.SetToolTip(this.numAutoNexusPercentageThreshold, "Percentage to trigger Auto Nexus.\r\nWe recommend 15% or more!");
		this.numAutoNexusPercentageThreshold.Value = new decimal(new int[4] { 5, 0, 0, 0 });
		this.numAutoNexusPercentageThreshold.ValueChanged += new System.EventHandler(NumericUpDown_ValueChanged);
		this.chkEnableAutoPotMP.AutoSize = true;
		this.chkEnableAutoPotMP.ForeColor = System.Drawing.Color.White;
		this.chkEnableAutoPotMP.Location = new System.Drawing.Point(11, 113);
		this.chkEnableAutoPotMP.Name = "chkEnableAutoPotMP";
		this.chkEnableAutoPotMP.Size = new System.Drawing.Size(98, 21);
		this.chkEnableAutoPotMP.TabIndex = 25;
		this.chkEnableAutoPotMP.Text = "Drink MP at:";
		this.chkEnableAutoPotMP.UseVisualStyleBackColor = true;
		this.chkEnableAutoPotMP.CheckedChanged += new System.EventHandler(CheckBox_ValueChanged);
		this.chkEnableAutoNexusOnly.Checked = true;
		this.chkEnableAutoNexusOnly.CheckState = System.Windows.Forms.CheckState.Checked;
		this.chkEnableAutoNexusOnly.ForeColor = System.Drawing.Color.White;
		this.chkEnableAutoNexusOnly.Location = new System.Drawing.Point(11, 34);
		this.chkEnableAutoNexusOnly.Name = "chkEnableAutoNexusOnly";
		this.chkEnableAutoNexusOnly.Size = new System.Drawing.Size(112, 21);
		this.chkEnableAutoNexusOnly.TabIndex = 25;
		this.chkEnableAutoNexusOnly.Text = "Auto Nexus at:";
		this.chkEnableAutoNexusOnly.UseVisualStyleBackColor = true;
		this.chkEnableAutoNexusOnly.CheckedChanged += new System.EventHandler(CheckBox_ValueChanged);
		this.chkEnableAutoPotHP.AutoSize = true;
		this.chkEnableAutoPotHP.ForeColor = System.Drawing.Color.White;
		this.chkEnableAutoPotHP.Location = new System.Drawing.Point(11, 60);
		this.chkEnableAutoPotHP.Name = "chkEnableAutoPotHP";
		this.chkEnableAutoPotHP.Size = new System.Drawing.Size(106, 21);
		this.chkEnableAutoPotHP.TabIndex = 25;
		this.chkEnableAutoPotHP.Text = "Auto Drink at:";
		this.ttDescriptions.SetToolTip(this.chkEnableAutoPotHP, "Minimum health for AutoDrink to trigger at");
		this.chkEnableAutoPotHP.UseVisualStyleBackColor = true;
		this.chkEnableAutoPotHP.CheckedChanged += new System.EventHandler(CheckBox_ValueChanged);
		this.chkAutoNexusSyncHp.AutoSize = true;
		this.chkAutoNexusSyncHp.BackColor = System.Drawing.Color.FromArgb(44, 41, 62);
		this.chkAutoNexusSyncHp.Checked = true;
		this.chkAutoNexusSyncHp.CheckState = System.Windows.Forms.CheckState.Checked;
		this.chkAutoNexusSyncHp.ForeColor = System.Drawing.Color.White;
		this.chkAutoNexusSyncHp.Location = new System.Drawing.Point(257, 116);
		this.chkAutoNexusSyncHp.Name = "chkAutoNexusSyncHp";
		this.chkAutoNexusSyncHp.Size = new System.Drawing.Size(140, 21);
		this.chkAutoNexusSyncHp.TabIndex = 24;
		this.chkAutoNexusSyncHp.Text = "Auto Sync Client HP";
		this.ttDescriptions.SetToolTip(this.chkAutoNexusSyncHp, "Automatically set client HP to server HP if they're far apart for too long (30 hp difference after 1 second)");
		this.chkAutoNexusSyncHp.UseVisualStyleBackColor = false;
		this.chkAutoNexusSyncHp.CheckedChanged += new System.EventHandler(CheckBox_ValueChanged);
		this.chkAutoNexusUseClientHp.AutoSize = true;
		this.chkAutoNexusUseClientHp.BackColor = System.Drawing.Color.FromArgb(44, 41, 62);
		this.chkAutoNexusUseClientHp.ForeColor = System.Drawing.Color.White;
		this.chkAutoNexusUseClientHp.Location = new System.Drawing.Point(11, 141);
		this.chkAutoNexusUseClientHp.Name = "chkAutoNexusUseClientHp";
		this.chkAutoNexusUseClientHp.Size = new System.Drawing.Size(469, 21);
		this.chkAutoNexusUseClientHp.TabIndex = 24;
		this.chkAutoNexusUseClientHp.Text = "Use Client HP (disable this to avoids desyncs, but can result in more deaths)";
		this.ttDescriptions.SetToolTip(this.chkAutoNexusUseClientHp, resources.GetString("chkAutoNexusUseClientHp.ToolTip"));
		this.chkAutoNexusUseClientHp.UseVisualStyleBackColor = false;
		this.chkAutoNexusUseClientHp.CheckedChanged += new System.EventHandler(CheckBox_ValueChanged);
		this.chkAutoNexusDrinkFromInventory.AutoSize = true;
		this.chkAutoNexusDrinkFromInventory.BackColor = System.Drawing.Color.FromArgb(44, 41, 62);
		this.chkAutoNexusDrinkFromInventory.ForeColor = System.Drawing.Color.White;
		this.chkAutoNexusDrinkFromInventory.Location = new System.Drawing.Point(257, 62);
		this.chkAutoNexusDrinkFromInventory.Name = "chkAutoNexusDrinkFromInventory";
		this.chkAutoNexusDrinkFromInventory.Size = new System.Drawing.Size(194, 21);
		this.chkAutoNexusDrinkFromInventory.TabIndex = 24;
		this.chkAutoNexusDrinkFromInventory.Text = "Drink potions from inventory";
		this.ttDescriptions.SetToolTip(this.chkAutoNexusDrinkFromInventory, "Auto Drink potions to attempt to save yourself when you are low on HP");
		this.chkAutoNexusDrinkFromInventory.UseVisualStyleBackColor = false;
		this.chkAutoNexusDrinkFromInventory.CheckedChanged += new System.EventHandler(CheckBox_ValueChanged);
		this.label5.AutoSize = true;
		this.label5.BackColor = System.Drawing.Color.FromArgb(44, 41, 62);
		this.label5.Font = new System.Drawing.Font("Segoe UI", 9.75f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
		this.label5.ForeColor = System.Drawing.Color.White;
		this.label5.Location = new System.Drawing.Point(177, 114);
		this.label5.Name = "label5";
		this.label5.Size = new System.Drawing.Size(56, 17);
		this.label5.TabIndex = 23;
		this.label5.Text = "% Mana";
		this.label3.AutoSize = true;
		this.label3.BackColor = System.Drawing.Color.FromArgb(44, 41, 62);
		this.label3.Font = new System.Drawing.Font("Segoe UI", 9.75f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
		this.label3.ForeColor = System.Drawing.Color.White;
		this.label3.Location = new System.Drawing.Point(175, 61);
		this.label3.Name = "label3";
		this.label3.Size = new System.Drawing.Size(60, 17);
		this.label3.TabIndex = 23;
		this.label3.Text = "% Health";
		this.ttDescriptions.SetToolTip(this.label3, "Minimum health for AutoDrink to trigger at");
		this.numAutoNexusDrinkMpThreshold.BackColor = System.Drawing.Color.FromArgb(44, 41, 62);
		this.numAutoNexusDrinkMpThreshold.Font = new System.Drawing.Font("Segoe UI", 8.25f);
		this.numAutoNexusDrinkMpThreshold.ForeColor = System.Drawing.Color.White;
		this.numAutoNexusDrinkMpThreshold.Location = new System.Drawing.Point(122, 115);
		this.numAutoNexusDrinkMpThreshold.Name = "numAutoNexusDrinkMpThreshold";
		this.numAutoNexusDrinkMpThreshold.Size = new System.Drawing.Size(50, 22);
		this.numAutoNexusDrinkMpThreshold.TabIndex = 21;
		this.numAutoNexusDrinkMpThreshold.Value = new decimal(new int[4] { 20, 0, 0, 0 });
		this.numAutoNexusDrinkMpThreshold.ValueChanged += new System.EventHandler(NumericUpDown_ValueChanged);
		this.numAutoNexusDrinkThreshold.BackColor = System.Drawing.Color.FromArgb(44, 41, 62);
		this.numAutoNexusDrinkThreshold.Font = new System.Drawing.Font("Segoe UI", 8.25f);
		this.numAutoNexusDrinkThreshold.ForeColor = System.Drawing.Color.White;
		this.numAutoNexusDrinkThreshold.Location = new System.Drawing.Point(122, 61);
		this.numAutoNexusDrinkThreshold.Name = "numAutoNexusDrinkThreshold";
		this.numAutoNexusDrinkThreshold.Size = new System.Drawing.Size(50, 22);
		this.numAutoNexusDrinkThreshold.TabIndex = 21;
		this.ttDescriptions.SetToolTip(this.numAutoNexusDrinkThreshold, "Minimum health for AutoDrink to trigger at");
		this.numAutoNexusDrinkThreshold.Value = new decimal(new int[4] { 20, 0, 0, 0 });
		this.numAutoNexusDrinkThreshold.ValueChanged += new System.EventHandler(NumericUpDown_ValueChanged);
		this.chkAutoNexusReplaceFameWithHealth.AutoSize = true;
		this.chkAutoNexusReplaceFameWithHealth.ForeColor = System.Drawing.Color.White;
		this.chkAutoNexusReplaceFameWithHealth.Location = new System.Drawing.Point(257, 89);
		this.chkAutoNexusReplaceFameWithHealth.Name = "chkAutoNexusReplaceFameWithHealth";
		this.chkAutoNexusReplaceFameWithHealth.Size = new System.Drawing.Size(214, 21);
		this.chkAutoNexusReplaceFameWithHealth.TabIndex = 20;
		this.chkAutoNexusReplaceFameWithHealth.Text = "Replace Fame Bar with Client HP";
		this.chkAutoNexusReplaceFameWithHealth.Visible = false;
		this.chkAutoNexusReplaceFameWithHealth.Enabled = false;
		this.ttDescriptions.SetToolTip(this.chkAutoNexusReplaceFameWithHealth, "This will show you what the Auto Nexus prediction\r\nthinks your HP is currently at");
		this.chkAutoNexusReplaceFameWithHealth.UseVisualStyleBackColor = true;
		this.chkAutoNexusReplaceFameWithHealth.CheckedChanged += new System.EventHandler(CheckBox_ValueChanged);
		this.lblAutoDrinkDelayMilliseconds.AutoSize = true;
		this.lblAutoDrinkDelayMilliseconds.BackColor = System.Drawing.Color.FromArgb(44, 41, 62);
		this.lblAutoDrinkDelayMilliseconds.Font = new System.Drawing.Font("Segoe UI", 9.75f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
		this.lblAutoDrinkDelayMilliseconds.ForeColor = System.Drawing.Color.White;
		this.lblAutoDrinkDelayMilliseconds.Location = new System.Drawing.Point(175, 87);
		this.lblAutoDrinkDelayMilliseconds.Name = "lblAutoDrinkDelayMilliseconds";
		this.lblAutoDrinkDelayMilliseconds.Size = new System.Drawing.Size(79, 17);
		this.lblAutoDrinkDelayMilliseconds.TabIndex = 18;
		this.lblAutoDrinkDelayMilliseconds.Text = "milliseconds";
		this.ttDescriptions.SetToolTip(this.lblAutoDrinkDelayMilliseconds, "Delay between Auto Drink attempts");
		this.numAutoNexusHpPotDelay.BackColor = System.Drawing.Color.FromArgb(44, 41, 62);
		this.numAutoNexusHpPotDelay.Font = new System.Drawing.Font("Segoe UI", 8.25f);
		this.numAutoNexusHpPotDelay.ForeColor = System.Drawing.Color.White;
		this.numAutoNexusHpPotDelay.Increment = new decimal(new int[4] { 10, 0, 0, 0 });
		this.numAutoNexusHpPotDelay.Location = new System.Drawing.Point(122, 88);
		this.numAutoNexusHpPotDelay.Maximum = new decimal(new int[4] { 2000, 0, 0, 0 });
		this.numAutoNexusHpPotDelay.Name = "numAutoNexusHpPotDelay";
		this.numAutoNexusHpPotDelay.Size = new System.Drawing.Size(50, 22);
		this.numAutoNexusHpPotDelay.TabIndex = 16;
		this.ttDescriptions.SetToolTip(this.numAutoNexusHpPotDelay, "Delay between Auto Drink attempts");
		this.numAutoNexusHpPotDelay.Value = new decimal(new int[4] { 400, 0, 0, 0 });
		this.numAutoNexusHpPotDelay.ValueChanged += new System.EventHandler(NumericUpDown_ValueChanged);
		this.lblAutoDrinkDelay.AutoSize = true;
		this.lblAutoDrinkDelay.BackColor = System.Drawing.Color.FromArgb(44, 41, 62);
		this.lblAutoDrinkDelay.Font = new System.Drawing.Font("Segoe UI", 9.75f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
		this.lblAutoDrinkDelay.ForeColor = System.Drawing.Color.White;
		this.lblAutoDrinkDelay.Location = new System.Drawing.Point(27, 87);
		this.lblAutoDrinkDelay.Name = "lblAutoDrinkDelay";
		this.lblAutoDrinkDelay.Size = new System.Drawing.Size(77, 17);
		this.lblAutoDrinkDelay.TabIndex = 17;
		this.lblAutoDrinkDelay.Text = "Drink Delay:";
		this.ttDescriptions.SetToolTip(this.lblAutoDrinkDelay, "Delay between Auto Drink attempts");
		this.chkAutoNexusShowInformation.AutoSize = true;
		this.chkAutoNexusShowInformation.BackColor = System.Drawing.Color.FromArgb(44, 41, 62);
		this.chkAutoNexusShowInformation.ForeColor = System.Drawing.Color.White;
		this.chkAutoNexusShowInformation.Location = new System.Drawing.Point(257, 35);
		this.chkAutoNexusShowInformation.Name = "chkAutoNexusShowInformation";
		this.chkAutoNexusShowInformation.Size = new System.Drawing.Size(178, 21);
		this.chkAutoNexusShowInformation.TabIndex = 14;
		this.chkAutoNexusShowInformation.Text = "Show reason for nexusing";
		this.ttDescriptions.SetToolTip(this.chkAutoNexusShowInformation, "Shows a chat message after you nexus\r\nIt contains the cause of the nexus and your HP value");
		this.chkAutoNexusShowInformation.UseVisualStyleBackColor = false;
		this.chkAutoNexusShowInformation.CheckedChanged += new System.EventHandler(CheckBox_ValueChanged);
		this.lblAutoNexusAfter.AutoSize = true;
		this.lblAutoNexusAfter.BackColor = System.Drawing.Color.FromArgb(44, 41, 62);
		this.lblAutoNexusAfter.Font = new System.Drawing.Font("Segoe UI", 9.75f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
		this.lblAutoNexusAfter.ForeColor = System.Drawing.Color.White;
		this.lblAutoNexusAfter.Location = new System.Drawing.Point(175, 35);
		this.lblAutoNexusAfter.Name = "lblAutoNexusAfter";
		this.lblAutoNexusAfter.Size = new System.Drawing.Size(60, 17);
		this.lblAutoNexusAfter.TabIndex = 5;
		this.lblAutoNexusAfter.Text = "% Health";
		this.ttDescriptions.SetToolTip(this.lblAutoNexusAfter, "Percentage to trigger Auto Nexus.\r\nWe recommend 15% or more!");
		this.sepAutoNexus.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
		this.sepAutoNexus.BackColor = System.Drawing.Color.FromArgb(44, 41, 62);
		this.sepAutoNexus.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
		this.sepAutoNexus.Location = new System.Drawing.Point(8, 28);
		this.sepAutoNexus.Name = "sepAutoNexus";
		this.sepAutoNexus.Size = new System.Drawing.Size(501, 1);
		this.sepAutoNexus.TabIndex = 4;
		this.chkEnableAutoNexus.AutoSize = true;
		this.chkEnableAutoNexus.BackColor = System.Drawing.Color.FromArgb(44, 41, 62);
		this.chkEnableAutoNexus.Font = new System.Drawing.Font("Segoe UI", 12f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, 0);
		this.chkEnableAutoNexus.ForeColor = System.Drawing.Color.White;
		this.chkEnableAutoNexus.Location = new System.Drawing.Point(8, 3);
		this.chkEnableAutoNexus.Name = "chkEnableAutoNexus";
		this.chkEnableAutoNexus.Size = new System.Drawing.Size(118, 25);
		this.chkEnableAutoNexus.TabIndex = 0;
		this.chkEnableAutoNexus.Text = "Auto Nexus";
		this.ttDescriptions.SetToolTip(this.chkEnableAutoNexus, resources.GetString("chkEnableAutoNexus.ToolTip"));
		this.chkEnableAutoNexus.UseVisualStyleBackColor = false;
		this.chkEnableAutoNexus.CheckedChanged += new System.EventHandler(CheckBox_ValueChanged);
		this.pnlConnection.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
		this.pnlConnection.BackColor = System.Drawing.Color.FromArgb(73, 35, 70);
		this.pnlConnection.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
		this.pnlConnection.Controls.Add(this.chkDisableSendingIp);
		this.pnlConnection.Controls.Add(this.chkEnableGotoCommand);
		this.pnlConnection.Controls.Add(this.chkEnableConnectCommand);
		this.pnlConnection.Controls.Add(this.lblConnection);
		this.pnlConnection.Controls.Add(this.sepConnection);
		this.pnlConnection.Location = new System.Drawing.Point(6, 1333);
		this.pnlConnection.Name = "pnlConnection";
		this.pnlConnection.Size = new System.Drawing.Size(518, 120);
		this.pnlConnection.TabIndex = 27;
		this.ttDescriptions.SetToolTip(this.pnlConnection, "Tools related to quickly hopping servers");
		this.chkDisableSendingIp.AutoSize = true;
		this.chkDisableSendingIp.BackColor = System.Drawing.Color.FromArgb(73, 35, 70);
		this.chkDisableSendingIp.ForeColor = System.Drawing.Color.White;
		this.chkDisableSendingIp.Location = new System.Drawing.Point(30, 87);
		this.chkDisableSendingIp.Name = "chkDisableSendingIp";
		this.chkDisableSendingIp.Size = new System.Drawing.Size(208, 21);
		this.chkDisableSendingIp.TabIndex = 31;
		this.chkDisableSendingIp.Text = "Block sending IPs in public chat";
		this.chkDisableSendingIp.UseVisualStyleBackColor = false;
		this.chkDisableSendingIp.CheckedChanged += new System.EventHandler(CheckBox_ValueChanged);
		this.chkEnableGotoCommand.AutoSize = true;
		this.chkEnableGotoCommand.BackColor = System.Drawing.Color.FromArgb(73, 35, 70);
		this.chkEnableGotoCommand.ForeColor = System.Drawing.Color.White;
		this.chkEnableGotoCommand.Location = new System.Drawing.Point(30, 60);
		this.chkEnableGotoCommand.Name = "chkEnableGotoCommand";
		this.chkEnableGotoCommand.Size = new System.Drawing.Size(290, 21);
		this.chkEnableGotoCommand.TabIndex = 31;
		this.chkEnableGotoCommand.Text = "/goto <ip> : Quick connect server IP address";
		this.ttDescriptions.SetToolTip(this.chkEnableGotoCommand, "Enables the command to direct connect to a specified IP address\r\nThis can be unsafe and is for advanced users\r\nYou must find your own source for IP addresses");
		this.chkEnableGotoCommand.UseVisualStyleBackColor = false;
		this.chkEnableGotoCommand.CheckedChanged += new System.EventHandler(CheckBox_ValueChanged);
		this.chkEnableConnectCommand.AutoSize = true;
		this.chkEnableConnectCommand.BackColor = System.Drawing.Color.FromArgb(73, 35, 70);
		this.chkEnableConnectCommand.ForeColor = System.Drawing.Color.White;
		this.chkEnableConnectCommand.Location = new System.Drawing.Point(30, 33);
		this.chkEnableConnectCommand.Name = "chkEnableConnectCommand";
		this.chkEnableConnectCommand.Size = new System.Drawing.Size(315, 21);
		this.chkEnableConnectCommand.TabIndex = 28;
		this.chkEnableConnectCommand.Text = "/con <server> : Quick connect to specified server";
		this.ttDescriptions.SetToolTip(this.chkEnableConnectCommand, "Enables the comman to quick connect to any server name\r\nYou can use the full or abbreviated name");
		this.chkEnableConnectCommand.UseVisualStyleBackColor = false;
		this.chkEnableConnectCommand.CheckedChanged += new System.EventHandler(CheckBox_ValueChanged);
		this.lblConnection.AutoSize = true;
		this.lblConnection.Font = new System.Drawing.Font("Segoe UI", 12f, System.Drawing.FontStyle.Bold);
		this.lblConnection.ForeColor = System.Drawing.Color.White;
		this.lblConnection.Location = new System.Drawing.Point(8, 3);
		this.lblConnection.Name = "lblConnection";
		this.lblConnection.Size = new System.Drawing.Size(98, 21);
		this.lblConnection.TabIndex = 15;
		this.lblConnection.Text = "Connection";
		this.ttDescriptions.SetToolTip(this.lblConnection, "Tools related to quickly hopping servers");
		this.sepConnection.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
		this.sepConnection.BackColor = System.Drawing.Color.FromArgb(44, 51, 42);
		this.sepConnection.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
		this.sepConnection.Location = new System.Drawing.Point(8, 28);
		this.sepConnection.Name = "sepConnection";
		this.sepConnection.Size = new System.Drawing.Size(501, 1);
		this.sepConnection.TabIndex = 6;
		this.pnlAntiLag.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
		this.pnlAntiLag.BackColor = System.Drawing.Color.FromArgb(44, 71, 42);
		this.pnlAntiLag.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
		this.pnlAntiLag.Controls.Add(this.chkAntiLagHideAllyPets);
		this.pnlAntiLag.Controls.Add(this.chkAntiLagHideNoPets);
		this.pnlAntiLag.Controls.Add(this.chkAntiLagHideAllPets);
		this.pnlAntiLag.Controls.Add(this.chkAntiLagApplyToGuildMates);
		this.pnlAntiLag.Controls.Add(this.label2);
		this.pnlAntiLag.Controls.Add(this.lblAntiLagPlayerSize);
		this.pnlAntiLag.Controls.Add(this.numAntiLagPlayerSize);
		this.pnlAntiLag.Controls.Add(this.lblAntiLagAllySize);
		this.pnlAntiLag.Controls.Add(this.numAntiLagAllyPlayerSize);
		this.pnlAntiLag.Controls.Add(this.btnEditIgnoredEffectList);
		this.pnlAntiLag.Controls.Add(this.label1);
		this.pnlAntiLag.Controls.Add(this.chkAntiLagIgnoreEffects);
		this.pnlAntiLag.Controls.Add(this.panel2);
		this.pnlAntiLag.ForeColor = System.Drawing.Color.White;
		this.pnlAntiLag.Location = new System.Drawing.Point(6, 1459);
		this.pnlAntiLag.Name = "pnlAntiLag";
		this.pnlAntiLag.Size = new System.Drawing.Size(518, 171);
		this.pnlAntiLag.TabIndex = 29;
		this.ttDescriptions.SetToolTip(this.pnlAntiLag, "Features to reduce network and FPS lag");
		this.chkAntiLagHideAllyPets.AutoSize = true;
		this.chkAntiLagHideAllyPets.Location = new System.Drawing.Point(200, 112);
		this.chkAntiLagHideAllyPets.Name = "chkAntiLagHideAllyPets";
		this.chkAntiLagHideAllyPets.Size = new System.Drawing.Size(105, 21);
		this.chkAntiLagHideAllyPets.TabIndex = 45;
		this.chkAntiLagHideAllyPets.TabStop = true;
		this.chkAntiLagHideAllyPets.Text = "Hide Ally Pets";
		this.chkAntiLagHideAllyPets.UseVisualStyleBackColor = true;
		this.chkAntiLagHideAllyPets.CheckedChanged += new System.EventHandler(RadioButton_ValueChanged);
		this.chkAntiLagHideNoPets.AutoSize = true;
		this.chkAntiLagHideNoPets.Location = new System.Drawing.Point(30, 112);
		this.chkAntiLagHideNoPets.Name = "chkAntiLagHideNoPets";
		this.chkAntiLagHideNoPets.Size = new System.Drawing.Size(103, 21);
		this.chkAntiLagHideNoPets.TabIndex = 44;
		this.chkAntiLagHideNoPets.TabStop = true;
		this.chkAntiLagHideNoPets.Text = "Show All Pets";
		this.chkAntiLagHideNoPets.UseVisualStyleBackColor = true;
		this.chkAntiLagHideNoPets.CheckedChanged += new System.EventHandler(RadioButton_ValueChanged);
		this.chkAntiLagHideAllPets.AutoSize = true;
		this.chkAntiLagHideAllPets.Location = new System.Drawing.Point(367, 112);
		this.chkAntiLagHideAllPets.Name = "chkAntiLagHideAllPets";
		this.chkAntiLagHideAllPets.Size = new System.Drawing.Size(99, 21);
		this.chkAntiLagHideAllPets.TabIndex = 46;
		this.chkAntiLagHideAllPets.TabStop = true;
		this.chkAntiLagHideAllPets.Text = "Hide All Pets";
		this.chkAntiLagHideAllPets.UseVisualStyleBackColor = true;
		this.chkAntiLagHideAllPets.CheckedChanged += new System.EventHandler(RadioButton_ValueChanged);
		this.chkAntiLagApplyToGuildMates.AutoSize = true;
		this.chkAntiLagApplyToGuildMates.BackColor = System.Drawing.Color.FromArgb(44, 71, 42);
		this.chkAntiLagApplyToGuildMates.ForeColor = System.Drawing.Color.White;
		this.chkAntiLagApplyToGuildMates.Location = new System.Drawing.Point(30, 85);
		this.chkAntiLagApplyToGuildMates.Name = "chkAntiLagApplyToGuildMates";
		this.chkAntiLagApplyToGuildMates.Size = new System.Drawing.Size(205, 21);
		this.chkAntiLagApplyToGuildMates.TabIndex = 43;
		this.chkAntiLagApplyToGuildMates.Text = "Change Guild Mate Player Size";
		this.ttDescriptions.SetToolTip(this.chkAntiLagApplyToGuildMates, "Enables custom size for guild mates");
		this.chkAntiLagApplyToGuildMates.UseVisualStyleBackColor = false;
		this.chkAntiLagApplyToGuildMates.CheckedChanged += new System.EventHandler(CheckBox_ValueChanged);
		this.label2.AutoSize = true;
		this.label2.BackColor = System.Drawing.Color.FromArgb(44, 71, 42);
		this.label2.Font = new System.Drawing.Font("Segoe UI", 9.75f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
		this.label2.ForeColor = System.Drawing.Color.White;
		this.label2.Location = new System.Drawing.Point(238, 36);
		this.label2.Name = "label2";
		this.label2.Size = new System.Drawing.Size(154, 17);
		this.label2.TabIndex = 41;
		this.label2.Text = "(0 = HIDE, 100 = normal)";
		this.ttDescriptions.SetToolTip(this.label2, "Alter the size of other players in-game to make it easier to see");
		this.lblAntiLagPlayerSize.AutoSize = true;
		this.lblAntiLagPlayerSize.BackColor = System.Drawing.Color.FromArgb(44, 71, 42);
		this.lblAntiLagPlayerSize.Font = new System.Drawing.Font("Segoe UI", 9.75f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
		this.lblAntiLagPlayerSize.ForeColor = System.Drawing.Color.White;
		this.lblAntiLagPlayerSize.Location = new System.Drawing.Point(27, 61);
		this.lblAntiLagPlayerSize.Name = "lblAntiLagPlayerSize";
		this.lblAntiLagPlayerSize.Size = new System.Drawing.Size(118, 17);
		this.lblAntiLagPlayerSize.TabIndex = 40;
		this.lblAntiLagPlayerSize.Text = "Change Player Size";
		this.numAntiLagPlayerSize.BackColor = System.Drawing.Color.FromArgb(44, 71, 42);
		this.numAntiLagPlayerSize.Font = new System.Drawing.Font("Segoe UI", 8.25f);
		this.numAntiLagPlayerSize.ForeColor = System.Drawing.Color.White;
		this.numAntiLagPlayerSize.Location = new System.Drawing.Point(176, 59);
		this.numAntiLagPlayerSize.Maximum = new decimal(new int[4] { 1000, 0, 0, 0 });
		this.numAntiLagPlayerSize.Name = "numAntiLagPlayerSize";
		this.numAntiLagPlayerSize.Size = new System.Drawing.Size(57, 22);
		this.numAntiLagPlayerSize.TabIndex = 40;
		this.numAntiLagPlayerSize.Value = new decimal(new int[4] { 100, 0, 0, 0 });
		this.numAntiLagPlayerSize.ValueChanged += new System.EventHandler(NumericUpDown_ValueChanged);
		this.lblAntiLagAllySize.AutoSize = true;
		this.lblAntiLagAllySize.BackColor = System.Drawing.Color.FromArgb(44, 71, 42);
		this.lblAntiLagAllySize.Font = new System.Drawing.Font("Segoe UI", 9.75f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
		this.lblAntiLagAllySize.ForeColor = System.Drawing.Color.White;
		this.lblAntiLagAllySize.Location = new System.Drawing.Point(27, 36);
		this.lblAntiLagAllySize.Name = "lblAntiLagAllySize";
		this.lblAntiLagAllySize.Size = new System.Drawing.Size(142, 17);
		this.lblAntiLagAllySize.TabIndex = 41;
		this.lblAntiLagAllySize.Text = "Change Ally Player Size";
		this.ttDescriptions.SetToolTip(this.lblAntiLagAllySize, "Alter the size of other players in-game to make it easier to see");
		this.numAntiLagAllyPlayerSize.BackColor = System.Drawing.Color.FromArgb(44, 71, 42);
		this.numAntiLagAllyPlayerSize.Font = new System.Drawing.Font("Segoe UI", 8.25f);
		this.numAntiLagAllyPlayerSize.ForeColor = System.Drawing.Color.White;
		this.numAntiLagAllyPlayerSize.Location = new System.Drawing.Point(176, 34);
		this.numAntiLagAllyPlayerSize.Maximum = new decimal(new int[4] { 1000, 0, 0, 0 });
		this.numAntiLagAllyPlayerSize.Name = "numAntiLagAllyPlayerSize";
		this.numAntiLagAllyPlayerSize.Size = new System.Drawing.Size(57, 22);
		this.numAntiLagAllyPlayerSize.TabIndex = 39;
		this.ttDescriptions.SetToolTip(this.numAntiLagAllyPlayerSize, "Alter the size of other players in-game to make it easier to see");
		this.numAntiLagAllyPlayerSize.Value = new decimal(new int[4] { 100, 0, 0, 0 });
		this.numAntiLagAllyPlayerSize.ValueChanged += new System.EventHandler(NumericUpDown_ValueChanged);
		this.btnEditIgnoredEffectList.AutoSize = true;
		this.btnEditIgnoredEffectList.BackColor = System.Drawing.Color.Transparent;
		this.btnEditIgnoredEffectList.LinkColor = System.Drawing.Color.Gold;
		this.btnEditIgnoredEffectList.Location = new System.Drawing.Point(181, 140);
		this.btnEditIgnoredEffectList.Name = "btnEditIgnoredEffectList";
		this.btnEditIgnoredEffectList.Size = new System.Drawing.Size(176, 17);
		this.btnEditIgnoredEffectList.TabIndex = 38;
		this.btnEditIgnoredEffectList.TabStop = true;
		this.btnEditIgnoredEffectList.Text = "Add/Remove Ignored Effects";
		this.ttDescriptions.SetToolTip(this.btnEditIgnoredEffectList, "Hides the selected effects");
		this.btnEditIgnoredEffectList.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(btnEditIgnoredEffectList_LinkClicked);
		this.label1.AutoSize = true;
		this.label1.Font = new System.Drawing.Font("Segoe UI", 12f, System.Drawing.FontStyle.Bold);
		this.label1.ForeColor = System.Drawing.Color.White;
		this.label1.Location = new System.Drawing.Point(8, 3);
		this.label1.Name = "label1";
		this.label1.Size = new System.Drawing.Size(73, 21);
		this.label1.TabIndex = 25;
		this.label1.Text = "Anti Lag";
		this.ttDescriptions.SetToolTip(this.label1, "Features to reduce network and FPS lag");
		this.chkAntiLagIgnoreEffects.AutoSize = true;
		this.chkAntiLagIgnoreEffects.BackColor = System.Drawing.Color.FromArgb(44, 71, 42);
		this.chkAntiLagIgnoreEffects.ForeColor = System.Drawing.Color.White;
		this.chkAntiLagIgnoreEffects.Location = new System.Drawing.Point(29, 139);
		this.chkAntiLagIgnoreEffects.Name = "chkAntiLagIgnoreEffects";
		this.chkAntiLagIgnoreEffects.Size = new System.Drawing.Size(146, 21);
		this.chkAntiLagIgnoreEffects.TabIndex = 37;
		this.chkAntiLagIgnoreEffects.Text = "Hide Ignored Effects";
		this.ttDescriptions.SetToolTip(this.chkAntiLagIgnoreEffects, "Hides the selected effects");
		this.chkAntiLagIgnoreEffects.UseVisualStyleBackColor = false;
		this.chkAntiLagIgnoreEffects.CheckedChanged += new System.EventHandler(CheckBox_ValueChanged);
		this.panel2.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
		this.panel2.BackColor = System.Drawing.Color.FromArgb(44, 51, 42);
		this.panel2.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
		this.panel2.Location = new System.Drawing.Point(8, 28);
		this.panel2.Name = "panel2";
		this.panel2.Size = new System.Drawing.Size(501, 1);
		this.panel2.TabIndex = 6;
		this.pnlO3.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
		this.pnlO3.BackColor = System.Drawing.Color.FromArgb(28, 28, 28);
		this.pnlO3.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
		this.pnlO3.Controls.Add(this.lblO3);
		this.pnlO3.Controls.Add(this.chkO3IgnoreDammah);
		this.pnlO3.Controls.Add(this.chkO3IgnoreCoins);
		this.pnlO3.Controls.Add(this.chkO3IgnoreShield);
		this.pnlO3.Controls.Add(this.sepO3);
		this.pnlO3.Controls.Add(this.chkEnableO3Helper);
		this.pnlO3.Location = new System.Drawing.Point(6, 2083);
		this.pnlO3.Name = "pnlO3";
		this.pnlO3.Size = new System.Drawing.Size(518, 162);
		this.pnlO3.TabIndex = 28;
		this.ttDescriptions.SetToolTip(this.pnlO3, "Tools to assist your fight against Oryx 3");
		this.lblO3.AutoSize = true;
		this.lblO3.BackColor = System.Drawing.Color.FromArgb(28, 28, 28);
		this.lblO3.Font = new System.Drawing.Font("Segoe UI", 9.75f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
		this.lblO3.ForeColor = System.Drawing.Color.White;
		this.lblO3.Location = new System.Drawing.Point(27, 36);
		this.lblO3.MaximumSize = new System.Drawing.Size(414, 34);
		this.lblO3.Name = "lblO3";
		this.lblO3.Size = new System.Drawing.Size(414, 34);
		this.lblO3.TabIndex = 33;
		this.lblO3.Text = "Tested only o3 guard phase, shots will appear to hit but be cancelled. Let us know if there are issues!";
		this.chkO3IgnoreDammah.AutoSize = true;
		this.chkO3IgnoreDammah.BackColor = System.Drawing.Color.FromArgb(28, 28, 28);
		this.chkO3IgnoreDammah.ForeColor = System.Drawing.Color.White;
		this.chkO3IgnoreDammah.Location = new System.Drawing.Point(30, 130);
		this.chkO3IgnoreDammah.Name = "chkO3IgnoreDammah";
		this.chkO3IgnoreDammah.Size = new System.Drawing.Size(235, 21);
		this.chkO3IgnoreDammah.TabIndex = 31;
		this.chkO3IgnoreDammah.Text = "Ignore Dammah during interruption";
		this.ttDescriptions.SetToolTip(this.chkO3IgnoreDammah, "Prevent damage to Dammah during interruption phase");
		this.chkO3IgnoreDammah.UseVisualStyleBackColor = false;
		this.chkO3IgnoreDammah.CheckedChanged += new System.EventHandler(CheckBox_ValueChanged);
		this.chkO3IgnoreCoins.AutoSize = true;
		this.chkO3IgnoreCoins.BackColor = System.Drawing.Color.FromArgb(28, 28, 28);
		this.chkO3IgnoreCoins.ForeColor = System.Drawing.Color.White;
		this.chkO3IgnoreCoins.Location = new System.Drawing.Point(30, 103);
		this.chkO3IgnoreCoins.Name = "chkO3IgnoreCoins";
		this.chkO3IgnoreCoins.Size = new System.Drawing.Size(199, 21);
		this.chkO3IgnoreCoins.TabIndex = 30;
		this.chkO3IgnoreCoins.Text = "Ignore wrong Gemsbok coins";
		this.ttDescriptions.SetToolTip(this.chkO3IgnoreCoins, "Prevent damage to the wrong Gemsbok coins");
		this.chkO3IgnoreCoins.UseVisualStyleBackColor = false;
		this.chkO3IgnoreCoins.CheckedChanged += new System.EventHandler(CheckBox_ValueChanged);
		this.chkO3IgnoreShield.AutoSize = true;
		this.chkO3IgnoreShield.BackColor = System.Drawing.Color.FromArgb(28, 28, 28);
		this.chkO3IgnoreShield.ForeColor = System.Drawing.Color.White;
		this.chkO3IgnoreShield.Location = new System.Drawing.Point(30, 76);
		this.chkO3IgnoreShield.Name = "chkO3IgnoreShield";
		this.chkO3IgnoreShield.Size = new System.Drawing.Size(198, 21);
		this.chkO3IgnoreShield.TabIndex = 14;
		this.chkO3IgnoreShield.Text = "Ignore O3 during shield raise";
		this.ttDescriptions.SetToolTip(this.chkO3IgnoreShield, "Prevent damage to Oryx 3 during shield raise");
		this.chkO3IgnoreShield.UseVisualStyleBackColor = false;
		this.chkO3IgnoreShield.CheckedChanged += new System.EventHandler(CheckBox_ValueChanged);
		this.sepO3.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
		this.sepO3.BackColor = System.Drawing.Color.FromArgb(54, 51, 52);
		this.sepO3.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
		this.sepO3.Location = new System.Drawing.Point(8, 28);
		this.sepO3.Name = "sepO3";
		this.sepO3.Size = new System.Drawing.Size(501, 1);
		this.sepO3.TabIndex = 6;
		this.chkEnableO3Helper.AutoSize = true;
		this.chkEnableO3Helper.BackColor = System.Drawing.Color.FromArgb(28, 28, 28);
		this.chkEnableO3Helper.Font = new System.Drawing.Font("Segoe UI", 12f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, 0);
		this.chkEnableO3Helper.ForeColor = System.Drawing.Color.White;
		this.chkEnableO3Helper.Location = new System.Drawing.Point(8, 3);
		this.chkEnableO3Helper.Name = "chkEnableO3Helper";
		this.chkEnableO3Helper.Size = new System.Drawing.Size(134, 25);
		this.chkEnableO3Helper.TabIndex = 1;
		this.chkEnableO3Helper.Text = "Oryx 3 Helper";
		this.ttDescriptions.SetToolTip(this.chkEnableO3Helper, "Tools to assist your fight against Oryx 3");
		this.chkEnableO3Helper.UseVisualStyleBackColor = false;
		this.chkEnableO3Helper.CheckedChanged += new System.EventHandler(CheckBox_ValueChanged);
		this.ttDescriptions.AutomaticDelay = 0;
		this.ttDescriptions.AutoPopDelay = 5000;
		this.ttDescriptions.InitialDelay = 0;
		this.ttDescriptions.ReshowDelay = 0;
		this.ttDescriptions.ShowAlways = true;
		this.ttDescriptions.ToolTipIcon = System.Windows.Forms.ToolTipIcon.Info;
		this.ttDescriptions.ToolTipTitle = "Description";
		this.ttDescriptions.UseAnimation = false;
		this.ttDescriptions.UseFading = false;
		this.pnlAutoAim.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
		this.pnlAutoAim.BackColor = System.Drawing.Color.DarkSlateGray;
		this.pnlAutoAim.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
		this.pnlAutoAim.Controls.Add(this.chkAutoAimOffsetColossusSword);
		this.pnlAutoAim.Controls.Add(this.lblAutoAimAcceleratingWeaponWarning);
		this.pnlAutoAim.Controls.Add(this.chkAutoAimProjectileNoclip);
		this.pnlAutoAim.Controls.Add(this.chkAutoAimShootInvulnerable);
		this.pnlAutoAim.Controls.Add(this.chkAutoAimShootWhileStealthed);
		this.pnlAutoAim.Controls.Add(this.chkAutoAimFocusBoss);
		this.pnlAutoAim.Controls.Add(this.lblAutoAimMouseDist);
		this.pnlAutoAim.Controls.Add(this.numAutoAimMouseDist);
		this.pnlAutoAim.Controls.Add(this.lblAutoAimRangeLead);
		this.pnlAutoAim.Controls.Add(this.numAutoAimRangeLead);
		this.pnlAutoAim.Controls.Add(this.chkAutoAimModeMouse);
		this.pnlAutoAim.Controls.Add(this.chkAutoAimModeHighestHP);
		this.pnlAutoAim.Controls.Add(this.chkAutoAimModeClosest);
		this.pnlAutoAim.Controls.Add(this.chkAutoAimIgnoreWalls);
		this.pnlAutoAim.Controls.Add(this.panel3);
		this.pnlAutoAim.Controls.Add(this.chkAutoAimEnabled);
		this.pnlAutoAim.Location = new System.Drawing.Point(6, 286);
		this.pnlAutoAim.Name = "pnlAutoAim";
		this.pnlAutoAim.Size = new System.Drawing.Size(518, 172);
		this.pnlAutoAim.TabIndex = 32;
		this.ttDescriptions.SetToolTip(this.pnlAutoAim, "Automatically aim at enemies");
		this.chkAutoAimOffsetColossusSword.AutoSize = true;
		this.chkAutoAimOffsetColossusSword.BackColor = System.Drawing.Color.DarkSlateGray;
		this.chkAutoAimOffsetColossusSword.Location = new System.Drawing.Point(345, 110);
		this.chkAutoAimOffsetColossusSword.Name = "chkAutoAimOffsetColossusSword";
		this.chkAutoAimOffsetColossusSword.Size = new System.Drawing.Size(134, 21);
		this.chkAutoAimOffsetColossusSword.TabIndex = 44;
		this.chkAutoAimOffsetColossusSword.Text = "Offset Colo Sword";
		this.chkAutoAimOffsetColossusSword.UseVisualStyleBackColor = false;
		this.chkAutoAimOffsetColossusSword.Visible = false;
		this.lblAutoAimAcceleratingWeaponWarning.AutoSize = true;
		this.lblAutoAimAcceleratingWeaponWarning.BackColor = System.Drawing.Color.DarkSlateGray;
		this.lblAutoAimAcceleratingWeaponWarning.Font = new System.Drawing.Font("Segoe UI", 8.25f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
		this.lblAutoAimAcceleratingWeaponWarning.Location = new System.Drawing.Point(27, 36);
		this.lblAutoAimAcceleratingWeaponWarning.Name = "lblAutoAimAcceleratingWeaponWarning";
		this.lblAutoAimAcceleratingWeaponWarning.Size = new System.Drawing.Size(467, 13);
		this.lblAutoAimAcceleratingWeaponWarning.TabIndex = 43;
		this.lblAutoAimAcceleratingWeaponWarning.Text = "Warning: Weapons with acceleration are not currently supported and will aim incorrectly.";
		this.chkAutoAimProjectileNoclip.AutoSize = true;
		this.chkAutoAimProjectileNoclip.BackColor = System.Drawing.Color.DarkSlateGray;
		this.chkAutoAimProjectileNoclip.Location = new System.Drawing.Point(345, 83);
		this.chkAutoAimProjectileNoclip.Name = "chkAutoAimProjectileNoclip";
		this.chkAutoAimProjectileNoclip.Size = new System.Drawing.Size(122, 21);
		this.chkAutoAimProjectileNoclip.TabIndex = 40;
		this.chkAutoAimProjectileNoclip.Text = "Projectile Noclip";
		this.ttDescriptions.SetToolTip(this.chkAutoAimProjectileNoclip, "Make your projectiles pass through all objects");
		this.chkAutoAimProjectileNoclip.UseVisualStyleBackColor = false;
		this.chkAutoAimProjectileNoclip.CheckedChanged += new System.EventHandler(CheckBox_ValueChanged);
		this.chkAutoAimShootInvulnerable.AutoSize = true;
		this.chkAutoAimShootInvulnerable.BackColor = System.Drawing.Color.DarkSlateGray;
		this.chkAutoAimShootInvulnerable.Location = new System.Drawing.Point(180, 83);
		this.chkAutoAimShootInvulnerable.Name = "chkAutoAimShootInvulnerable";
		this.chkAutoAimShootInvulnerable.Size = new System.Drawing.Size(138, 21);
		this.chkAutoAimShootInvulnerable.TabIndex = 40;
		this.chkAutoAimShootInvulnerable.Text = "Aim at Invulnerable";
		this.ttDescriptions.SetToolTip(this.chkAutoAimShootInvulnerable, "Shoot an enemies with the blue shield invulnerability buff");
		this.chkAutoAimShootInvulnerable.UseVisualStyleBackColor = false;
		this.chkAutoAimShootInvulnerable.CheckedChanged += new System.EventHandler(CheckBox_ValueChanged);
		this.chkAutoAimShootWhileStealthed.AutoSize = true;
		this.chkAutoAimShootWhileStealthed.BackColor = System.Drawing.Color.DarkSlateGray;
		this.chkAutoAimShootWhileStealthed.Location = new System.Drawing.Point(180, 110);
		this.chkAutoAimShootWhileStealthed.Name = "chkAutoAimShootWhileStealthed";
		this.chkAutoAimShootWhileStealthed.Size = new System.Drawing.Size(155, 21);
		this.chkAutoAimShootWhileStealthed.TabIndex = 39;
		this.chkAutoAimShootWhileStealthed.Text = "Shoot While Stealthed";
		this.chkAutoAimShootWhileStealthed.UseVisualStyleBackColor = false;
		this.chkAutoAimShootWhileStealthed.CheckedChanged += new System.EventHandler(CheckBox_ValueChanged);
		this.chkAutoAimFocusBoss.AutoSize = true;
		this.chkAutoAimFocusBoss.BackColor = System.Drawing.Color.DarkSlateGray;
		this.chkAutoAimFocusBoss.Location = new System.Drawing.Point(30, 110);
		this.chkAutoAimFocusBoss.Name = "chkAutoAimFocusBoss";
		this.chkAutoAimFocusBoss.Size = new System.Drawing.Size(122, 21);
		this.chkAutoAimFocusBoss.TabIndex = 39;
		this.chkAutoAimFocusBoss.Text = "Prioritize Bosses";
		this.ttDescriptions.SetToolTip(this.chkAutoAimFocusBoss, "Aim at bosses first if they are nearby");
		this.chkAutoAimFocusBoss.UseVisualStyleBackColor = false;
		this.chkAutoAimFocusBoss.CheckedChanged += new System.EventHandler(CheckBox_ValueChanged);
		this.lblAutoAimMouseDist.AutoSize = true;
		this.lblAutoAimMouseDist.BackColor = System.Drawing.Color.DarkSlateGray;
		this.lblAutoAimMouseDist.Location = new System.Drawing.Point(27, 138);
		this.lblAutoAimMouseDist.Name = "lblAutoAimMouseDist";
		this.lblAutoAimMouseDist.Size = new System.Drawing.Size(148, 17);
		this.lblAutoAimMouseDist.TabIndex = 38;
		this.lblAutoAimMouseDist.Text = "Mouse Bounding Range";
		this.ttDescriptions.SetToolTip(this.lblAutoAimMouseDist, "Range enemies need to be within to be shot at");
		this.numAutoAimMouseDist.BackColor = System.Drawing.Color.DarkSlateGray;
		this.numAutoAimMouseDist.ForeColor = System.Drawing.Color.White;
		this.numAutoAimMouseDist.Location = new System.Drawing.Point(181, 136);
		this.numAutoAimMouseDist.Maximum = new decimal(new int[4] { 25, 0, 0, 0 });
		this.numAutoAimMouseDist.Minimum = new decimal(new int[4] { 1, 0, 0, 0 });
		this.numAutoAimMouseDist.Name = "numAutoAimMouseDist";
		this.numAutoAimMouseDist.Size = new System.Drawing.Size(52, 25);
		this.numAutoAimMouseDist.TabIndex = 37;
		this.ttDescriptions.SetToolTip(this.numAutoAimMouseDist, "Range enemies need to be within to be shot at");
		this.numAutoAimMouseDist.Value = new decimal(new int[4] { 3, 0, 0, 0 });
		this.numAutoAimMouseDist.ValueChanged += new System.EventHandler(NumericUpDown_ValueChanged);
		this.lblAutoAimRangeLead.AutoSize = true;
		this.lblAutoAimRangeLead.BackColor = System.Drawing.Color.DarkSlateGray;
		this.lblAutoAimRangeLead.Location = new System.Drawing.Point(251, 138);
		this.lblAutoAimRangeLead.Name = "lblAutoAimRangeLead";
		this.lblAutoAimRangeLead.Size = new System.Drawing.Size(110, 17);
		this.lblAutoAimRangeLead.TabIndex = 36;
		this.lblAutoAimRangeLead.Text = "Extra Lead Range";
		this.ttDescriptions.SetToolTip(this.lblAutoAimRangeLead, "Pads your weapon distance when searching for an enemy to make autoaim shoot an approaching enemies sooner");
		this.numAutoAimRangeLead.BackColor = System.Drawing.Color.DarkSlateGray;
		this.numAutoAimRangeLead.ForeColor = System.Drawing.Color.White;
		this.numAutoAimRangeLead.Location = new System.Drawing.Point(367, 136);
		this.numAutoAimRangeLead.Maximum = new decimal(new int[4] { 5, 0, 0, 0 });
		this.numAutoAimRangeLead.Name = "numAutoAimRangeLead";
		this.numAutoAimRangeLead.Size = new System.Drawing.Size(52, 25);
		this.numAutoAimRangeLead.TabIndex = 35;
		this.ttDescriptions.SetToolTip(this.numAutoAimRangeLead, "Pads your weapon distance when searching for an enemy to make autoaim shoot an approaching enemies sooner");
		this.numAutoAimRangeLead.Value = new decimal(new int[4] { 1, 0, 0, 0 });
		this.numAutoAimRangeLead.ValueChanged += new System.EventHandler(NumericUpDown_ValueChanged);
		this.chkAutoAimModeMouse.AutoSize = true;
		this.chkAutoAimModeMouse.BackColor = System.Drawing.Color.DarkSlateGray;
		this.chkAutoAimModeMouse.Location = new System.Drawing.Point(30, 56);
		this.chkAutoAimModeMouse.Name = "chkAutoAimModeMouse";
		this.chkAutoAimModeMouse.Size = new System.Drawing.Size(154, 21);
		this.chkAutoAimModeMouse.TabIndex = 34;
		this.chkAutoAimModeMouse.TabStop = true;
		this.chkAutoAimModeMouse.Text = "Aim Closest to Mouse";
		this.ttDescriptions.SetToolTip(this.chkAutoAimModeMouse, "Aim at the enemy closest to your mouse");
		this.chkAutoAimModeMouse.UseVisualStyleBackColor = false;
		this.chkAutoAimModeMouse.CheckedChanged += new System.EventHandler(RadioButton_ValueChanged);
		this.chkAutoAimModeHighestHP.AutoSize = true;
		this.chkAutoAimModeHighestHP.BackColor = System.Drawing.Color.DarkSlateGray;
		this.chkAutoAimModeHighestHP.Location = new System.Drawing.Point(345, 56);
		this.chkAutoAimModeHighestHP.Name = "chkAutoAimModeHighestHP";
		this.chkAutoAimModeHighestHP.Size = new System.Drawing.Size(116, 21);
		this.chkAutoAimModeHighestHP.TabIndex = 33;
		this.chkAutoAimModeHighestHP.TabStop = true;
		this.chkAutoAimModeHighestHP.Text = "Aim Highest HP";
		this.ttDescriptions.SetToolTip(this.chkAutoAimModeHighestHP, "Aim at the enemy with the highest HP near you");
		this.chkAutoAimModeHighestHP.UseVisualStyleBackColor = false;
		this.chkAutoAimModeHighestHP.CheckedChanged += new System.EventHandler(RadioButton_ValueChanged);
		this.chkAutoAimModeClosest.AutoSize = true;
		this.chkAutoAimModeClosest.BackColor = System.Drawing.Color.DarkSlateGray;
		this.chkAutoAimModeClosest.Location = new System.Drawing.Point(190, 56);
		this.chkAutoAimModeClosest.Name = "chkAutoAimModeClosest";
		this.chkAutoAimModeClosest.Size = new System.Drawing.Size(149, 21);
		this.chkAutoAimModeClosest.TabIndex = 32;
		this.chkAutoAimModeClosest.TabStop = true;
		this.chkAutoAimModeClosest.Text = "Aim Closest to Player";
		this.ttDescriptions.SetToolTip(this.chkAutoAimModeClosest, "Aim at enemy closest to your player");
		this.chkAutoAimModeClosest.UseVisualStyleBackColor = false;
		this.chkAutoAimModeClosest.CheckedChanged += new System.EventHandler(RadioButton_ValueChanged);
		this.chkAutoAimIgnoreWalls.AutoSize = true;
		this.chkAutoAimIgnoreWalls.BackColor = System.Drawing.Color.DarkSlateGray;
		this.chkAutoAimIgnoreWalls.ForeColor = System.Drawing.Color.White;
		this.chkAutoAimIgnoreWalls.Location = new System.Drawing.Point(30, 83);
		this.chkAutoAimIgnoreWalls.Name = "chkAutoAimIgnoreWalls";
		this.chkAutoAimIgnoreWalls.Size = new System.Drawing.Size(99, 21);
		this.chkAutoAimIgnoreWalls.TabIndex = 30;
		this.chkAutoAimIgnoreWalls.Text = "Ignore Walls";
		this.ttDescriptions.SetToolTip(this.chkAutoAimIgnoreWalls, "Skip aiming at objects like destructible walls");
		this.chkAutoAimIgnoreWalls.UseVisualStyleBackColor = false;
		this.chkAutoAimIgnoreWalls.CheckedChanged += new System.EventHandler(CheckBox_ValueChanged);
		this.panel3.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
		this.panel3.BackColor = System.Drawing.Color.DarkSlateGray;
		this.panel3.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
		this.panel3.Location = new System.Drawing.Point(8, 28);
		this.panel3.Name = "panel3";
		this.panel3.Size = new System.Drawing.Size(501, 1);
		this.panel3.TabIndex = 6;
		this.ttDescriptions.SetToolTip(this.panel3, "Automatically aim at enemies");
		this.chkAutoAimEnabled.AutoSize = true;
		this.chkAutoAimEnabled.BackColor = System.Drawing.Color.DarkSlateGray;
		this.chkAutoAimEnabled.Font = new System.Drawing.Font("Segoe UI", 12f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, 0);
		this.chkAutoAimEnabled.ForeColor = System.Drawing.Color.White;
		this.chkAutoAimEnabled.Location = new System.Drawing.Point(8, 3);
		this.chkAutoAimEnabled.Name = "chkAutoAimEnabled";
		this.chkAutoAimEnabled.Size = new System.Drawing.Size(101, 25);
		this.chkAutoAimEnabled.TabIndex = 1;
		this.chkAutoAimEnabled.Text = "Auto Aim";
		this.ttDescriptions.SetToolTip(this.chkAutoAimEnabled, "Automatically aim at enemies");
		this.chkAutoAimEnabled.UseVisualStyleBackColor = false;
		this.chkAutoAimEnabled.CheckedChanged += new System.EventHandler(CheckBox_ValueChanged);
		this.panel1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
		this.panel1.BackColor = System.Drawing.Color.FromArgb(54, 51, 52);
		this.panel1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
		this.panel1.Controls.Add(this.btnCopySupportId);
		this.panel1.Controls.Add(this.label7);
		this.panel1.Controls.Add(this.chkResetClientHp);
		this.panel1.Controls.Add(this.panel4);
		this.panel1.Controls.Add(this.btnHotkeys);
		this.panel1.ForeColor = System.Drawing.Color.White;
		this.panel1.Location = new System.Drawing.Point(6, 6);
		this.panel1.Name = "panel1";
		this.panel1.Size = new System.Drawing.Size(518, 70);
		this.panel1.TabIndex = 36;
		this.ttDescriptions.SetToolTip(this.panel1, "Misc. Tools");
		this.btnCopySupportId.BackColor = System.Drawing.Color.FromArgb(40, 40, 40);
		this.btnCopySupportId.FlatAppearance.BorderColor = System.Drawing.Color.Gray;
		this.btnCopySupportId.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
		this.btnCopySupportId.Font = new System.Drawing.Font("Segoe UI", 8.25f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, 0);
		this.btnCopySupportId.ForeColor = System.Drawing.Color.White;
		this.btnCopySupportId.Location = new System.Drawing.Point(177, 36);
		this.btnCopySupportId.Name = "btnCopySupportId";
		this.btnCopySupportId.Size = new System.Drawing.Size(114, 25);
		this.btnCopySupportId.TabIndex = 35;
		this.btnCopySupportId.Text = "Copy Support ID";
		this.btnCopySupportId.UseVisualStyleBackColor = false;
		this.btnCopySupportId.Click += new System.EventHandler(btnCopySupportId_Click);
		this.label7.AutoSize = true;
		this.label7.Font = new System.Drawing.Font("Segoe UI", 12f, System.Drawing.FontStyle.Bold);
		this.label7.ForeColor = System.Drawing.Color.White;
		this.label7.Location = new System.Drawing.Point(8, 3);
		this.label7.Name = "label7";
		this.label7.Size = new System.Drawing.Size(69, 21);
		this.label7.TabIndex = 24;
		this.label7.Text = "General";
		this.ttDescriptions.SetToolTip(this.label7, "Misc. Tools");
		this.chkResetClientHp.AutoSize = true;
		this.chkResetClientHp.Location = new System.Drawing.Point(425, 39);
		this.chkResetClientHp.Name = "chkResetClientHp";
		this.chkResetClientHp.Size = new System.Drawing.Size(87, 21);
		this.chkResetClientHp.TabIndex = 34;
		this.chkResetClientHp.Text = "Reset CHP";
		this.chkResetClientHp.UseVisualStyleBackColor = true;
		this.chkResetClientHp.Visible = false;
		this.panel4.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
		this.panel4.BackColor = System.Drawing.Color.FromArgb(54, 51, 52);
		this.panel4.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
		this.panel4.Location = new System.Drawing.Point(8, 28);
		this.panel4.Name = "panel4";
		this.panel4.Size = new System.Drawing.Size(501, 1);
		this.panel4.TabIndex = 6;
		this.btnHotkeys.BackColor = System.Drawing.Color.FromArgb(40, 40, 40);
		this.btnHotkeys.Enabled = false;
		this.btnHotkeys.FlatAppearance.BorderColor = System.Drawing.Color.Gray;
		this.btnHotkeys.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
		this.btnHotkeys.Font = new System.Drawing.Font("Segoe UI", 8.25f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, 0);
		this.btnHotkeys.ForeColor = System.Drawing.Color.White;
		this.btnHotkeys.Location = new System.Drawing.Point(8, 36);
		this.btnHotkeys.Name = "btnHotkeys";
		this.btnHotkeys.Size = new System.Drawing.Size(162, 25);
		this.btnHotkeys.TabIndex = 33;
		this.btnHotkeys.Text = "Manage Custom Hotkeys";
		this.btnHotkeys.UseVisualStyleBackColor = false;
		this.btnHotkeys.Click += new System.EventHandler(btnHotkeys_Click);
		this.pnlFps.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
		this.pnlFps.BackColor = System.Drawing.Color.FromArgb(43, 28, 54);
		this.pnlFps.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
		this.pnlFps.Controls.Add(this.lblFpsBackground);
		this.pnlFps.Controls.Add(this.lblFpsVsyncWarning);
		this.pnlFps.Controls.Add(this.lblFpsForeground);
		this.pnlFps.Controls.Add(this.numFpsBackground);
		this.pnlFps.Controls.Add(this.numFpsForeground);
		this.pnlFps.Controls.Add(this.chkFpsVsync);
		this.pnlFps.Controls.Add(this.sepFps);
		this.pnlFps.Controls.Add(this.lblFps);
		this.pnlFps.Location = new System.Drawing.Point(6, 2251);
		this.pnlFps.Name = "pnlFps";
		this.pnlFps.Size = new System.Drawing.Size(518, 118);
		this.pnlFps.TabIndex = 37;
		this.ttDescriptions.SetToolTip(this.pnlFps, "For custom FPS values");
		this.lblFpsBackground.AutoSize = true;
		this.lblFpsBackground.BackColor = System.Drawing.Color.FromArgb(43, 28, 54);
		this.lblFpsBackground.Font = new System.Drawing.Font("Segoe UI", 9.75f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
		this.lblFpsBackground.ForeColor = System.Drawing.Color.White;
		this.lblFpsBackground.Location = new System.Drawing.Point(29, 62);
		this.lblFpsBackground.MaximumSize = new System.Drawing.Size(414, 34);
		this.lblFpsBackground.Name = "lblFpsBackground";
		this.lblFpsBackground.Size = new System.Drawing.Size(104, 17);
		this.lblFpsBackground.TabIndex = 33;
		this.lblFpsBackground.Text = "Background FPS:";
		this.ttDescriptions.SetToolTip(this.lblFpsBackground, "FPS when Exalt is in the background (alt tabbed, minimized) (lower values recommended to save resources)");
		this.lblFpsVsyncWarning.AutoSize = true;
		this.lblFpsVsyncWarning.BackColor = System.Drawing.Color.FromArgb(43, 28, 54);
		this.lblFpsVsyncWarning.Font = new System.Drawing.Font("Segoe UI", 9.75f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
		this.lblFpsVsyncWarning.ForeColor = System.Drawing.Color.White;
		this.lblFpsVsyncWarning.Location = new System.Drawing.Point(139, 88);
		this.lblFpsVsyncWarning.MaximumSize = new System.Drawing.Size(414, 34);
		this.lblFpsVsyncWarning.Name = "lblFpsVsyncWarning";
		this.lblFpsVsyncWarning.Size = new System.Drawing.Size(373, 17);
		this.lblFpsVsyncWarning.TabIndex = 33;
		this.lblFpsVsyncWarning.Text = "(VSync on overrides FPS settings and uses monitor hz instead.)";
		this.lblFpsForeground.AutoSize = true;
		this.lblFpsForeground.BackColor = System.Drawing.Color.FromArgb(43, 28, 54);
		this.lblFpsForeground.Font = new System.Drawing.Font("Segoe UI", 9.75f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
		this.lblFpsForeground.ForeColor = System.Drawing.Color.White;
		this.lblFpsForeground.Location = new System.Drawing.Point(29, 36);
		this.lblFpsForeground.MaximumSize = new System.Drawing.Size(414, 34);
		this.lblFpsForeground.Name = "lblFpsForeground";
		this.lblFpsForeground.Size = new System.Drawing.Size(104, 17);
		this.lblFpsForeground.TabIndex = 33;
		this.lblFpsForeground.Text = "Foreground FPS:";
		this.ttDescriptions.SetToolTip(this.lblFpsForeground, "FPS when Exalt is focused (while playing)");
		this.numFpsBackground.BackColor = System.Drawing.Color.FromArgb(43, 28, 54);
		this.numFpsBackground.Font = new System.Drawing.Font("Segoe UI", 8.25f);
		this.numFpsBackground.ForeColor = System.Drawing.Color.White;
		this.numFpsBackground.Location = new System.Drawing.Point(144, 63);
		this.numFpsBackground.Maximum = new decimal(new int[4] { 4000, 0, 0, 0 });
		this.numFpsBackground.Minimum = new decimal(new int[4] { 1, 0, 0, 0 });
		this.numFpsBackground.Name = "numFpsBackground";
		this.numFpsBackground.Size = new System.Drawing.Size(57, 22);
		this.numFpsBackground.TabIndex = 60;
		this.ttDescriptions.SetToolTip(this.numFpsBackground, "FPS when Exalt is in the background (alt tabbed, minimized) (lower values recommended to save resources)");
		this.numFpsBackground.Value = new decimal(new int[4] { 60, 0, 0, 0 });
		this.numFpsBackground.ValueChanged += new System.EventHandler(NumericUpDown_ValueChanged);
		this.numFpsForeground.BackColor = System.Drawing.Color.FromArgb(43, 28, 54);
		this.numFpsForeground.Font = new System.Drawing.Font("Segoe UI", 8.25f);
		this.numFpsForeground.ForeColor = System.Drawing.Color.White;
		this.numFpsForeground.Location = new System.Drawing.Point(144, 37);
		this.numFpsForeground.Maximum = new decimal(new int[4] { 4000, 0, 0, 0 });
		this.numFpsForeground.Minimum = new decimal(new int[4] { 1, 0, 0, 0 });
		this.numFpsForeground.Name = "numFpsForeground";
		this.numFpsForeground.Size = new System.Drawing.Size(57, 22);
		this.numFpsForeground.TabIndex = 59;
		this.ttDescriptions.SetToolTip(this.numFpsForeground, "FPS when Exalt is focused (while playing)");
		this.numFpsForeground.Value = new decimal(new int[4] { 60, 0, 0, 0 });
		this.numFpsForeground.ValueChanged += new System.EventHandler(NumericUpDown_ValueChanged);
		this.chkFpsVsync.AutoSize = true;
		this.chkFpsVsync.BackColor = System.Drawing.Color.FromArgb(43, 28, 54);
		this.chkFpsVsync.Checked = true;
		this.chkFpsVsync.CheckState = System.Windows.Forms.CheckState.Checked;
		this.chkFpsVsync.ForeColor = System.Drawing.Color.White;
		this.chkFpsVsync.Location = new System.Drawing.Point(29, 87);
		this.chkFpsVsync.Name = "chkFpsVsync";
		this.chkFpsVsync.Size = new System.Drawing.Size(104, 21);
		this.chkFpsVsync.TabIndex = 61;
		this.chkFpsVsync.Text = "Enable VSync";
		this.ttDescriptions.SetToolTip(this.chkFpsVsync, "Prevent screen tearing");
		this.chkFpsVsync.UseVisualStyleBackColor = false;
		this.chkFpsVsync.CheckedChanged += new System.EventHandler(CheckBox_ValueChanged);
		this.sepFps.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
		this.sepFps.BackColor = System.Drawing.Color.FromArgb(54, 51, 52);
		this.sepFps.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
		this.sepFps.Location = new System.Drawing.Point(8, 28);
		this.sepFps.Name = "sepFps";
		this.sepFps.Size = new System.Drawing.Size(501, 1);
		this.sepFps.TabIndex = 6;
		this.lblFps.AutoSize = true;
		this.lblFps.Font = new System.Drawing.Font("Segoe UI", 12f, System.Drawing.FontStyle.Bold);
		this.lblFps.ForeColor = System.Drawing.Color.White;
		this.lblFps.Location = new System.Drawing.Point(8, 3);
		this.lblFps.Name = "lblFps";
		this.lblFps.Size = new System.Drawing.Size(105, 21);
		this.lblFps.TabIndex = 24;
		this.lblFps.Text = "FPS Controls";
		this.ttDescriptions.SetToolTip(this.lblFps, "For setting custom FPS values");
		this.tmrForeground.Enabled = true;
		this.tmrForeground.Tick += new System.EventHandler(tmrForeground_Tick);
		this.chkAutoNexusInstantNexus.AutoSize = true;
		this.chkAutoNexusInstantNexus.BackColor = System.Drawing.Color.FromArgb(44, 41, 62);
		this.chkAutoNexusInstantNexus.Checked = true;
		this.chkAutoNexusInstantNexus.CheckState = System.Windows.Forms.CheckState.Checked;
		this.chkAutoNexusInstantNexus.ForeColor = System.Drawing.Color.White;
		this.chkAutoNexusInstantNexus.Location = new System.Drawing.Point(11, 168);
		this.chkAutoNexusInstantNexus.Name = "chkAutoNexusInstantNexus";
		this.chkAutoNexusInstantNexus.Size = new System.Drawing.Size(369, 21);
		this.chkAutoNexusInstantNexus.TabIndex = 24;
		this.chkAutoNexusInstantNexus.Text = "Instant Nexus (safer, but can put you back in Nexus queue)";
		this.chkAutoNexusInstantNexus.UseVisualStyleBackColor = false;
		this.chkAutoNexusInstantNexus.CheckedChanged += new System.EventHandler(CheckBox_ValueChanged);
		base.AutoScaleDimensions = new System.Drawing.SizeF(96f, 96f);
		base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
		this.AutoScroll = true;
		this.AutoValidate = System.Windows.Forms.AutoValidate.Disable;
		this.BackColor = System.Drawing.Color.FromArgb(10, 14, 23);
		this.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
		base.Controls.Add(this.pnlFps);
		base.Controls.Add(this.panel1);
		base.Controls.Add(this.pnlAutoAim);
		base.Controls.Add(this.pnlAutoLoot);
		base.Controls.Add(this.pnlAntiLag);
		base.Controls.Add(this.pnlO3);
		base.Controls.Add(this.pnlConnection);
		base.Controls.Add(this.pnlSafeWalk);
		base.Controls.Add(this.pnlOther);
		base.Controls.Add(this.pnlAntiDebuffs);
		base.Controls.Add(this.pnlAutoAbility);
		base.Controls.Add(this.pnlAutoNexus);
		this.DoubleBuffered = true;
		this.Font = new System.Drawing.Font("Segoe UI", 9.75f);
		this.ForeColor = System.Drawing.SystemColors.ControlLight;
		base.Name = "SettingsControl";
		base.Size = new System.Drawing.Size(530, 2372);
		base.Load += new System.EventHandler(SettingsControl_Load);
		this.pnlSafeWalk.ResumeLayout(false);
		this.pnlSafeWalk.PerformLayout();
		((System.ComponentModel.ISupportInitialize)this.numSlowWalkMultiplier).EndInit();
		this.pnlOther.ResumeLayout(false);
		this.pnlOther.PerformLayout();
		this.pnlAntiDebuffs.ResumeLayout(false);
		this.pnlAntiDebuffs.PerformLayout();
		this.pnlAutoLoot.ResumeLayout(false);
		this.pnlAutoLoot.PerformLayout();
		((System.ComponentModel.ISupportInitialize)this.numAutoLootRingTierThreshold).EndInit();
		((System.ComponentModel.ISupportInitialize)this.numAutoLootAbilityTierThreshold).EndInit();
		((System.ComponentModel.ISupportInitialize)this.numAutoLootArmorTierThreshold).EndInit();
		((System.ComponentModel.ISupportInitialize)this.numAutoLootWeaponTierThreshold).EndInit();
		this.pnlAutoAbility.ResumeLayout(false);
		this.pnlAutoAbility.PerformLayout();
		((System.ComponentModel.ISupportInitialize)this.numAutoAbilityHealHpPercent).EndInit();
		((System.ComponentModel.ISupportInitialize)this.numAutoAbilityCustomDelay).EndInit();
		((System.ComponentModel.ISupportInitialize)this.numAutoAbilityMinimumGroupSizeThreshold).EndInit();
		((System.ComponentModel.ISupportInitialize)this.numAutoAbilityMinimumManaLeftThreshold).EndInit();
		((System.ComponentModel.ISupportInitialize)this.numAutoAbilityMinimumEnemyHealthThreshold).EndInit();
		this.pnlAutoNexus.ResumeLayout(false);
		this.pnlAutoNexus.PerformLayout();
		((System.ComponentModel.ISupportInitialize)this.numAutoNexusPercentageThreshold).EndInit();
		((System.ComponentModel.ISupportInitialize)this.numAutoNexusDrinkMpThreshold).EndInit();
		((System.ComponentModel.ISupportInitialize)this.numAutoNexusDrinkThreshold).EndInit();
		((System.ComponentModel.ISupportInitialize)this.numAutoNexusHpPotDelay).EndInit();
		this.pnlConnection.ResumeLayout(false);
		this.pnlConnection.PerformLayout();
		this.pnlAntiLag.ResumeLayout(false);
		this.pnlAntiLag.PerformLayout();
		((System.ComponentModel.ISupportInitialize)this.numAntiLagPlayerSize).EndInit();
		((System.ComponentModel.ISupportInitialize)this.numAntiLagAllyPlayerSize).EndInit();
		this.pnlO3.ResumeLayout(false);
		this.pnlO3.PerformLayout();
		this.pnlAutoAim.ResumeLayout(false);
		this.pnlAutoAim.PerformLayout();
		((System.ComponentModel.ISupportInitialize)this.numAutoAimMouseDist).EndInit();
		((System.ComponentModel.ISupportInitialize)this.numAutoAimRangeLead).EndInit();
		this.panel1.ResumeLayout(false);
		this.panel1.PerformLayout();
		this.pnlFps.ResumeLayout(false);
		this.pnlFps.PerformLayout();
		((System.ComponentModel.ISupportInitialize)this.numFpsBackground).EndInit();
		((System.ComponentModel.ISupportInitialize)this.numFpsForeground).EndInit();
		base.ResumeLayout(false);
	}
}
