param(
    [string]$AssemblyPath = "$PSScriptRoot/../bin/Release/net48/ExaltHelper.exe",
    [string]$OutputDirectory = "$PSScriptRoot/../artifacts/hud-refresh-performance",
    [switch]$BenchmarkOnly
)
$ErrorActionPreference = 'Stop'
if ([IO.DriveInfo]::new([IO.Path]::GetPathRoot([IO.Path]::GetFullPath($OutputDirectory))).AvailableFreeSpace -le 5GB) { throw 'Disk guard stopped work.' }
$taskAssembly = (Resolve-Path -LiteralPath $AssemblyPath).Path
$taskRoot = Join-Path ([IO.Path]::GetFullPath($OutputDirectory)) ([DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0,8))
[void][IO.Directory]::CreateDirectory($taskRoot)
$taskPriorData = $env:EXALTHELPER_HUD_DATA
$env:EXALTHELPER_HUD_DATA = Join-Path $taskRoot 'data'
$taskHarness = Join-Path $taskRoot 'HudRefreshPerformance.dll'
Add-Type -OutputAssembly $taskHarness -ReferencedAssemblies System.Core,System.Drawing,System.Windows.Forms,Accessibility -TypeDefinition @'
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

public sealed class HudRefreshPerformance : MarshalByRefObject
{
    const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    Assembly app;
    readonly List<string> report = new List<string>();
    readonly Dictionary<string,MethodInfo> methods = new Dictionary<string,MethodInfo>();
    int checks;
    Type T(string name) { return app.GetType("ExaltHelper." + name,true); }
    object New(string name) { return Activator.CreateInstance(T(name),true); }
    object Get(object o,string name) { Type t=o as Type??o.GetType(); object target=o is Type?null:o; var f=t.GetField(name,F); return f!=null?f.GetValue(target):t.GetProperty(name,F).GetValue(target,null); }
    void Set(object o,string name,object value) { Type t=o as Type??o.GetType(); object target=o is Type?null:o; var f=t.GetField(name,F); if(f!=null)f.SetValue(target,value);else t.GetProperty(name,F).SetValue(target,value,null); }
    object Call(object o,string name,params object[] args)
    {
        Type t=o as Type??o.GetType();string key=t.FullName+"."+name+"#"+args.Length;
        MethodInfo method;if(!methods.TryGetValue(key,out method)){method=t.GetMethods(F).Single(m=>m.Name==name&&m.GetParameters().Length==args.Length);methods[key]=method;}
        return method.Invoke(o is Type?null:o,args);
    }
    void Check(bool ok,string message) { if(!ok)throw new InvalidOperationException(message);checks++;report.Add("PASS "+message); }
    void Bump(object state) { Set(state,"Revision",(long)Get(state,"Revision")+1); }
    void Bench(string name,int iterations,Action action)
    {
        for(int i=0;i<3;i++)action();
        GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();
        long before=AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize;
        int collections=GC.CollectionCount(0);
        var timer=Stopwatch.StartNew();for(int i=0;i<iterations;i++)action();timer.Stop();
        long allocated=AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize-before;
        string line=String.Format(System.Globalization.CultureInfo.InvariantCulture,"BENCH {0}: iterations={1} msPerCall={2:F4} bytesPerCall={3} gen0={4}",name,iterations,timer.Elapsed.TotalMilliseconds/iterations,allocated/iterations,GC.CollectionCount(0)-collections);
        report.Add(line);Console.WriteLine(line);
    }
    object SeedAccount()
    {
        var state=New("AccountSnapshot");Set(state,"AccountKey",new string('d',64));Set(state,"Name","Refresh fixture");Set(state,"UpdatedUtc",DateTime.UtcNow);Set(state,"Revision",1L);
        Set(state,"Gold",123);Set(state,"RegularForgeFire",700);Set(state,"SeasonalForgeFire",900);Set(state,"CurrentCharacterId",1);Set(state,"CharacterSlots",40);
        for(int i=0;i<64;i++)
        {
            var character=New("AccountCharacter");Set(character,"Id",i+1);Set(character,"Name","Fixture "+i);Set(character,"ClassType",782);Set(character,"Level",20);Set(character,"HasPcStats",true);Set(character,"Seasonal",i%2==0);
            Set(character,"Inventory",Enumerable.Range(0,20).Select(n=>2580+n%5).ToArray());Set(character,"Enchants",Enumerable.Repeat("",20).ToArray());
            ((IDictionary)Get(character,"PcStats"))["minutes_active"]=90;
            ((IList)Get(state,i<24?"Characters":"DeadCharacters")).Add(character);
        }
        for(int i=0;i<10;i++) { var storage=New("AccountStorage");Set(storage,"Name","Fixture storage "+i);Set(storage,"Seasonal",i%2==0);Set(storage,"Items",Enumerable.Range(0,1000).Select(n=>2580+n%19).ToArray());Set(storage,"Enchants",Enumerable.Repeat("",1000).ToArray());((IList)Get(state,"Storage")).Add(storage); }
        Set(T("AccountWorkspace"),"state",state);return state;
    }
    void SeedMissions(string fixtures)
    {
        Call(T("MissionWorkspace"),"GetMissions");
        object catalog=Call(T("MissionCatalog"),"Parse",File.ReadAllText(Path.Combine(fixtures,"missions-season52.json")),File.ReadAllText(Path.Combine(fixtures,"missions-season52.xml")));
        Set(T("MissionWorkspace"),"catalog",catalog);Set(T("MissionWorkspace"),"baselineLoaded",true);
        object cache=Get(T("MissionWorkspace"),"cache");Set(cache,"DailySetUtc",DateTime.UtcNow);Set(cache,"QuestsReceivedUtc",DateTime.UtcNow);
        for(int i=0;i<12;i++){var quest=New("MissionQuest");Set(quest,"Id","perf-"+i);Set(quest,"Name","Daily fixture "+i);Set(quest,"Category",7000);Set(quest,"Requirements",new[]{7728+i,7728+i,7728+i});((IList)Get(cache,"Quests")).Add(quest);}
    }
    object Live()
    {
        object live=New("Proxy.DataStructures.DpsSnapshot");Set(live,"DungeonName","The Nest");Set(live,"DungeonStartTime",DateTime.Now.AddMinutes(-2));
        object player=Get(live,"LocalPlayer");Set(player,"Name","Refresh fixture");Set(player,"MaxHp",770);Set(player,"CurrentHp",700);Set(player,"MaxMp",385);Set(player,"CurrentMp",300);
        return live;
    }
    void ItemIndexChecks(object prior)
    {
        var state=New("AccountSnapshot");Set(state,"AccountKey",new string('f',64));Set(state,"Revision",10L);Set(T("AccountWorkspace"),"state",state);
        try
        {
            var regular=New("AccountStorage");Set(regular,"Items",new[]{7001});((IList)Get(state,"Storage")).Add(regular);
            var seasonal=New("AccountStorage");Set(seasonal,"Seasonal",true);Set(seasonal,"Items",new[]{7001,7001});((IList)Get(state,"Storage")).Add(seasonal);
            var character=New("AccountCharacter");Set(character,"Inventory",new[]{7001,7001});Set(character,"QuickSlots",new[]{7001,7002,-1});Set(character,"QuickSlotCounts",new[]{7,0,99});((IList)Get(state,"Characters")).Add(character);
            var dead=New("AccountCharacter");Set(dead,"Inventory",Enumerable.Repeat(7001,100).ToArray());((IList)Get(state,"DeadCharacters")).Add(dead);
            foreach(int count in new[]{3,50,4}){var pet=New("AccountPet");Set(pet,"Id",42);Set(pet,"Seasonal",count==4);Set(pet,"Inventory",new[]{7001});Set(pet,"StackCounts",new[]{count});((IList)Get(state,"Pets")).Add(pet);}
            Check((int)Call(T("AccountWorkspace"),"GetItemCount",7001,false)==13,"Item index combines regular storage, character and stack counts while excluding dead gear and duplicate pets");
            Check((int)Call(T("AccountWorkspace"),"GetItemCount",7001,true)==6,"Item index keeps seasonal storage and pets separate");
            Check((int)Call(T("AccountWorkspace"),"GetItemCount",7002,false)==1,"Item index treats an observed item with zero stack count as one");
            Check((int)Call(T("AccountWorkspace"),"GetItemCount",9999,false)==0,"Item index reports zero for absent items");
            Set(regular,"Items",new[]{7001,7001});Bump(state);Check((int)Call(T("AccountWorkspace"),"GetItemCount",7001,false)==14,"Inventory revision refreshes item counts immediately");
            Set(character,"QuickSlotCounts",new[]{8,0,99});Bump(state);Check((int)Call(T("AccountWorkspace"),"GetItemCount",7001,false)==15,"Stack revision refreshes item counts immediately");
            Set(character,"Seasonal",true);Bump(state);Check((int)Call(T("AccountWorkspace"),"GetItemCount",7001,false)==5&&(int)Call(T("AccountWorkspace"),"GetItemCount",7001,true)==16,"Character mode change moves counts to the correct storage side");
            var switched=New("AccountSnapshot");Set(switched,"AccountKey",new string('a',64));Set(switched,"Revision",(long)Get(state,"Revision"));Set(T("AccountWorkspace"),"state",switched);
            Check((int)Call(T("AccountWorkspace"),"GetItemCount",7001,false)==0&&(int)Call(T("AccountWorkspace"),"GetItemCount",7001,true)==0,"Account switch cannot reuse another account's item index at an equal revision");
        }
        finally {Set(T("AccountWorkspace"),"state",prior);}
    }
    void InventoryHoverChecks()
    {
        ((System.Threading.Tasks.Task)Call(T("HudItemPresentation"),"WarmUpAsync")).GetAwaiter().GetResult();
        using(var grid=(Control)New("AccountItemGrid"))
        {
            grid.Size=new Size(500,240);IntPtr handle=grid.Handle;
            Type sectionType=T("AccountItemGrid").GetNestedType("Section",F);object section=Activator.CreateInstance(sectionType,true);
            Set(section,"Title","Fixture Vault");Set(section,"Items",new[]{2580,-1});Set(section,"Counts",new[]{7,0});Set(section,"Timestamp",new DateTime(2030,1,15,12,0,0));
            Array sections=Array.CreateInstance(sectionType,1);sections.SetValue(section,0);Call(grid,"SetSections",sections,"");
            var first=new MouseEventArgs(MouseButtons.None,0,25,52,0);Call(grid,"OnMouseMove",first);
            string tooltip=(string)Get(grid,"currentTip");
            Check(tooltip.Contains("Fixture Vault / slot 1")&&tooltip.Contains("Stack: 7")&&tooltip.Contains("Observed"),"Inventory rich tooltip retains container, slot, stack and observation context");
            Check(!tooltip.Contains("Loading item details"),"Inventory rich tooltip uses completed shared item details");
            int paints=0;grid.Invalidated+=delegate{paints++;};Bench("inventory-same-slot-hover",500,()=>Call(grid,"OnMouseMove",first));Check(paints==0,"Movement within the same inventory slot does not redraw it");
            Set(section,"Counts",new[]{9,0});Call(grid,"SetSections",sections,"");Call(grid,"OnMouseMove",first);Check(((string)Get(grid,"currentTip")).Contains("Stack: 9"),"Replacing inventory sections invalidates the cached tooltip");
            Call(grid,"OnMouseMove",new MouseEventArgs(MouseButtons.None,0,79,52,0));Check(((string)Get(grid,"currentTip")).Contains("slot 2")&&((string)Get(grid,"currentTip")).EndsWith("Empty"),"Empty-slot hover remains accurate after a populated slot");
            Call(grid,"OnScroll",new ScrollEventArgs(ScrollEventType.ThumbPosition,10));Check((string)Get(grid,"currentTip")=="","Inventory scroll clears the prior slot tooltip");
        }
    }
    public void Run(string assemblyPath,string root,string fixtures,bool benchmarkOnly)
    {
        Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);AppDomain.MonitoringIsEnabled=true;
        app=Assembly.LoadFrom(assemblyPath);
        var prefs=Get(T("HudPreferences"),"Current");Set(prefs,"Page","settings");Set(prefs,"SoundAlerts",false);Set(prefs,"AlwaysOnTop",false);Set(prefs,"Maximized",false);
        Set(Get(prefs,"Callouts"),"AutoCopyDungeons",false);
        var gate=(SemaphoreSlim)Get(T("AccountWorkspace"),"RefreshGate");if(!gate.Wait(0))throw new InvalidOperationException("Offline refresh gate busy");
        try
        {
            object state=SeedAccount();SeedMissions(fixtures);object live=Live();Call(T("HudJournal"),"ObserveSnapshot",live);
            using(var form=(Form)New("ExaltDashboardForm"))
            {
                ((System.Windows.Forms.Timer)Get(form,"refreshTimer")).Stop();((System.Windows.Forms.Timer)Get(form,"saveTimer")).Stop();Set(form,"ready",false);form.Location=new Point(-30000,-30000);IntPtr handle=form.Handle;
                Call(form,"UpdateSnapshot",live);Call(form,"RefreshDashboard");
                var widgets=(IDictionary)Get(form,"widgets");var widgetBar=(Control)Get(form,"widgetBar");int invalidated=0,layouts=0;
                foreach(Control control in widgets.Values){IntPtr childHandle=control.Handle;control.Invalidated+=delegate{invalidated++;};}
                widgetBar.Layout+=delegate{layouts++;};
                Bench("widgets-unchanged",100,()=>Call(form,"RefreshWidgets"));
                report.Add("EVENTS widgets-unchanged invalidated="+invalidated+" layouts="+layouts);
                int widgetInvalidations=invalidated,widgetLayouts=layouts;
                Bench("missions-model",20,()=>Call(T("MissionWorkspace"),"GetMissions"));
                Bench("quests-model",20,()=>Call(T("MissionWorkspace"),"GetQuests"));
                Bench("tracked-tasks-unchanged",50,()=>Call(form,"RefreshTasks"));
                Bench("settings-refresh-unchanged",50,()=>Call(form,"RefreshDashboard"));
                Bench("settings-live-snapshot",30,()=>{Call(form,"UpdateSnapshot",Live());Call(form,"RefreshDashboard");});
                Bench("account-revision-live-stat",30,()=>{Bump(state);Call(form,"RefreshDashboard");});
                MethodInfo tick=form.GetType().GetMethod("RefreshDashboardTick",F);
                Bench("hidden-dashboard-tick",100,()=>{if(tick!=null)tick.Invoke(form,null);else Call(form,"RefreshDashboard");});
                if(!benchmarkOnly)
                {
                    Check(widgetInvalidations==0,"Unchanged widgets do not invalidate cards");Check(widgetLayouts==0,"Unchanged widgets do not relayout their bar");
                    ItemIndexChecks(state);
                    InventoryHoverChecks();
                    object summary=Call(T("AccountWorkspace"),"ReadHudSummary");Check(Object.ReferenceEquals(summary,Call(T("AccountWorkspace"),"ReadHudSummary")),"Unchanged account summary is reused");
                    Check((int)Get(summary,"CharacterCount")==24&&(long)Get(summary,"PlaytimeMinutes")==5760,"Summary retains active and deceased character totals");
                    Check(Call(T("AccountWorkspace"),"SnapshotIfChanged",(long)Get(state,"Revision"),(string)Get(state,"AccountKey"))==null,"Unchanged account page avoids cloning inventory");
                    Set(state,"Gold",987);Bump(state);Call(form,"RefreshWidgets");Check(((Control)widgets["Gold"]).Text.EndsWith("987"),"Account currency changes reach widgets immediately");
                    Set(Get(live,"LocalPlayer"),"CurrentHp",345);Call(T("HudJournal"),"ObserveSnapshot",live);Call(form,"UpdateSnapshot",live);Call(form,"RefreshDashboard");Check(((Control)widgets["HP"]).Text.Contains("345 / 770"),"Live HP changes reach widgets immediately");
                    var oldControls=((Control)Get(form,"taskBar")).Controls.Cast<Control>().ToArray();Call(form,"RefreshTasks");Check(oldControls.SequenceEqual(((Control)Get(form,"taskBar")).Controls.Cast<Control>()),"Unchanged tracked task controls are retained");
                    string accountKey=(string)Get(state,"AccountKey");long revision=(long)Get(state,"Revision");Set(state,"AccountKey",new string('e',64));Check(Call(T("AccountWorkspace"),"SnapshotIfChanged",revision,accountKey)!=null,"Account identity invalidates snapshot cache even at equal revision");Check((string)Get(Call(T("AccountWorkspace"),"ReadHudSummary"),"AccountKey")!=accountKey,"Account summary switches identity at equal revision");Set(state,"AccountKey",accountKey);Bump(state);
                    int before=invalidated;Set(state,"Gold",654);Bump(state);Call(form,"UpdateSnapshot",Live());tick.Invoke(form,null);Check(invalidated==before,"Hidden timer does no widget painting");Check(((Control)widgets["Gold"]).Text.EndsWith("987"),"Hidden timer defers visual currency changes");Call(form,"RefreshDashboard");Check(((Control)widgets["Gold"]).Text.EndsWith("654"),"Explicit refresh catches up hidden currency changes");
                    Set(form,"lastNoticeCheck",DateTime.MinValue);tick.Invoke(form,null);Check((DateTime)Get(form,"lastNoticeCheck")>DateTime.UtcNow.AddSeconds(-5),"Hidden timer still evaluates reminders");
                    DateTime now=new DateTime(2030,1,15,12,34,12,DateTimeKind.Utc);Set(T("MissionWorkspace"),"catalog",New("MissionCatalog"));Set(T("MissionWorkspace"),"cache",New("MissionCache"));
                    Check((DateTime)Call(T("MissionWorkspace"),"NextTaskRefreshUtc",now)==now.Date.AddDays(1),"Task cache expires at the next UTC day");
                    object catalog=Get(T("MissionWorkspace"),"catalog"),definition=New("MissionDefinition"),progress=New("MissionProgress");Set(definition,"SeasonId",900);Set(definition,"Id",1);Set(definition,"IntervalDays",1.0);((IList)Get(catalog,"Definitions")).Add(definition);Set(progress,"RawState",3);Set(progress,"ClaimedAt",new DateTimeOffset(now.AddDays(-1).AddSeconds(10)).ToUnixTimeSeconds());((IDictionary)Get(catalog,"Progress"))["mission:900:1"]=progress;
                    Check((DateTime)Call(T("MissionWorkspace"),"NextTaskRefreshUtc",now)==now.AddSeconds(10),"Task cache expires at an exact cooldown deadline");
                    Set(progress,"ClaimedAt",new DateTimeOffset(now.AddDays(-1).AddSeconds(125)).ToUnixTimeSeconds());Check((DateTime)Call(T("MissionWorkspace"),"NextTaskRefreshUtc",now)==now.AddSeconds(6),"Cooldown minute label refresh follows its claim-time boundary");
                    object quest=New("MissionQuest");Set(quest,"Expiration",new DateTimeOffset(now.AddSeconds(2)).ToUnixTimeSeconds().ToString());((IList)Get(Get(T("MissionWorkspace"),"cache"),"Quests")).Add(quest);Check((DateTime)Call(T("MissionWorkspace"),"NextTaskRefreshUtc",now)==now.AddSeconds(2),"Quest expiry invalidates task cache promptly");
                }
            }
            report.Add("PASS total="+checks);
        }
        finally {Set(T("AccountWorkspace"),"dirty",false);gate.Release();try{Call(T("HudSounds"),"Shutdown");}catch{}File.WriteAllLines(Path.Combine(root,"review.txt"),report);}
        Console.WriteLine("Refresh performance: "+checks+" checks. "+root);
    }
}
'@
[void][Reflection.Assembly]::Load([IO.File]::ReadAllBytes($taskHarness))
$taskSetup = New-Object AppDomainSetup
$taskSetup.ApplicationBase = Split-Path $taskAssembly -Parent
$taskDomain = [AppDomain]::CreateDomain('Offline dashboard refresh performance', $null, $taskSetup)
try {
    $taskRunner = $taskDomain.CreateInstanceFromAndUnwrap($taskHarness, 'HudRefreshPerformance')
    $taskRunner.Run($taskAssembly, $taskRoot, (Join-Path $PSScriptRoot 'fixtures'), $BenchmarkOnly.IsPresent)
} finally {
    [AppDomain]::Unload($taskDomain)
    $env:EXALTHELPER_HUD_DATA = $taskPriorData
    @{Assembly=$taskAssembly;SHA256=(Get-FileHash -LiteralPath $taskAssembly -Algorithm SHA256).Hash;Output=$taskRoot;BenchmarkOnly=$BenchmarkOnly.IsPresent;Method='Hidden form with native handles; no Show, activation or desktop capture. Stopwatch and AppDomain allocation counters after warmup; 64 character/10000 storage-slot fixture.'} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $taskRoot 'manifest.json') -Encoding UTF8
}
