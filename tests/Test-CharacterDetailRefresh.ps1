param([string]$AssemblyPath = "$PSScriptRoot/../bin/Release/net48/ExaltHelper.exe")
$ErrorActionPreference = 'Stop'
if ([Threading.Thread]::CurrentThread.ApartmentState -ne 'STA') { throw 'Run this UI regression with powershell.exe -STA.' }
$priorHudData = $env:EXALTHELPER_HUD_DATA
$env:EXALTHELPER_HUD_DATA = Join-Path ([IO.Path]::GetTempPath()) ('ExaltCharacterRefresh-' + [guid]::NewGuid().ToString('N'))
Add-Type -AssemblyName System.Drawing,System.Windows.Forms
Add-Type -ReferencedAssemblies System.Core,System.Drawing,System.Windows.Forms -TypeDefinition @'
using System;
using System.Collections;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
public static class CharacterDetailRefreshTest {
 const BindingFlags F=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
 static Assembly assembly; static int checks;
 static Type T(string name){return assembly.GetType("ExaltHelper."+name,true);}
 static object New(string name){return Activator.CreateInstance(T(name),true);}
 static object Get(object obj,string name){Type type=obj as Type??obj.GetType();object owner=obj is Type?null:obj;var field=type.GetField(name,F);return field!=null?field.GetValue(owner):type.GetProperty(name,F).GetValue(owner,null);}
 static void Set(object obj,string name,object value){Type type=obj as Type??obj.GetType();object owner=obj is Type?null:obj;var field=type.GetField(name,F);if(field!=null)field.SetValue(owner,value);else type.GetProperty(name,F).SetValue(owner,value,null);}
 static object Call(object obj,string name,params object[] values){Type type=obj as Type??obj.GetType();return type.GetMethod(name,F).Invoke(obj is Type?null:obj,values);}
 static void Check(bool condition,string message){if(!condition)throw new Exception(message);checks++;}
 static void Pump(int milliseconds){var watch=Stopwatch.StartNew();while(watch.ElapsedMilliseconds<milliseconds){Application.DoEvents();Thread.Sleep(8);}Application.DoEvents();}
 static Control Tabs(Control page){return ((Control)Get(page,"details")).Controls.Cast<Control>().First(c=>c.GetType().Name=="AccountDetailTabs");}
 static Control Detail(Control page,int index){return (Control)((IList)Get(Tabs(page),"pages"))[index];}
 static Control Inventory(Control page){return Detail(page,0).Controls.Cast<Control>().First(c=>c.GetType().Name=="AccountItemGrid");}
 static DataGridView Grid(Control page,int index){return Detail(page,index).Controls.OfType<DataGridView>().Single();}
 static object Section(Control grid,string title){return ((IEnumerable)Get(grid,"sections")).Cast<object>().Single(s=>(string)Get(s,"Title")==title);}
 static string First(DataGridView grid){return Convert.ToString(grid.Rows[grid.FirstDisplayedScrollingRowIndex].Cells[0].Value);}
 static string Selected(DataGridView grid){return Convert.ToString(grid.CurrentRow.Cells[0].Value);}
 static DataGridViewRow Row(DataGridView grid,string name){return grid.Rows.Cast<DataGridViewRow>().First(r=>Convert.ToString(r.Cells[0].Value)==name);}
 static void Refresh(Control page,object state){Set(state,"Revision",(long)Get(state,"Revision")+1);Call(page,"Rebuild",false);Pump(30);}
 static object Character(int id){var c=New("AccountCharacter");Set(c,"Id",id);Set(c,"ClassType",782);Set(c,"Level",20);Set(c,"Fame",2000);Set(c,"Name","Refresh fixture");Set(c,"Label","Saved label "+id);Set(c,"Seasonal",true);Set(c,"Stats",new[]{670,385,75,25,50,75,40,60});Set(c,"Inventory",Enumerable.Repeat(2827,28).ToArray());Set(c,"Enchants",new string[28]);Set(c,"QuickSlots",new[]{2827,-1,6661});Set(c,"QuickSlotCounts",new[]{2,0,3});Set(c,"UpdatedUtc",DateTime.UtcNow);Set(c,"HasPcStats",true);var stats=(IDictionary)Get(c,"PcStats");for(int i=0;i<80;i++)stats["test_stat_"+i.ToString("D3")]=i;stats["minutes_active"]=100;return c;}
 public static void Run(string path){
  Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
  assembly=Assembly.LoadFrom(path);var workspace=T("AccountWorkspace");
  ((System.Threading.Timer)Get(workspace,"saveTimer")).Change(-1,-1);
  string previous=Environment.CurrentDirectory;
  try{Environment.CurrentDirectory=Path.GetDirectoryName(path);Call(T("Proxy.DataStructures.GameData"),"LoadGameData");}finally{Environment.CurrentDirectory=previous;}
  var state=New("AccountSnapshot");Set(state,"AccountKey",new string('a',64));Set(state,"Name","Account A");Set(state,"CurrentCharacterId",5);Set(state,"Revision",1L);
  var first=Character(5);var second=Character(7);((IList)Get(state,"Characters")).Add(first);((IList)Get(state,"Characters")).Add(second);Set(workspace,"state",state);
  using(var form=new Form{ClientSize=new Size(1020,720),ShowInTaskbar=false,StartPosition=FormStartPosition.Manual,Location=new Point(-2200,-2200)}){
   Console.WriteLine("Creating character page.");var page=(Control)Call(workspace,"CreatePage","characters");Console.WriteLine("Showing character page.");form.Controls.Add(page);form.Show();Pump(160);
   var inventory=(ScrollableControl)Inventory(page);var tabs=Tabs(page);inventory.AutoScrollPosition=new Point(0,250);inventory.Focus();Pump(30);
   int scroll=inventory.AutoScrollPosition.Y;Check(scroll<-100,"Fixture must scroll into the backpack before refreshing.");
   Check(inventory.Focused,"Fixture must focus the inventory before refreshing.");
   // Exercise the real 750ms timer, including unrelated live-stat revisions.
   Console.WriteLine("Checking timer refreshes.");for(int i=0;i<3;i++){
    ((int[])Get(first,"Stats"))[2]=76+i;Set(first,"Fame",2100+i);Set(state,"Revision",(long)Get(state,"Revision")+1);Pump(850);
    Check(((ScrollableControl)Inventory(page)).AutoScrollPosition.Y==scroll,"A same-character timer refresh must preserve equipment scroll.");
    Check(Object.ReferenceEquals(inventory,Inventory(page))&&!inventory.IsDisposed,"Live refresh must retain the inventory control.");
    Check(Object.ReferenceEquals(tabs,Tabs(page)),"Live refresh must retain the detail tabs.");
    Check(inventory.Focused,"Live refresh must preserve inventory keyboard focus.");
   }
   ((System.Windows.Forms.Timer)Get(page,"timer")).Stop();
   Check(Detail(page,0).Controls.OfType<Label>().Single().Text.Contains("ATT 78/"),"Base stats refresh while the backpack stays scrolled.");
   ((int[])Get(first,"Inventory"))[24]=6661;((string[])Get(first,"Enchants"))[24]="changed-enchantment";((int[])Get(first,"QuickSlotCounts"))[0]=9;Refresh(page,state);
   var backpack=Section(inventory,"Backpack extender");var quick=Section(inventory,"Quickslots");
   Check(((int[])Get(backpack,"Items"))[4]==6661,"A backpack slot update reaches the existing inventory.");
   Check(((string[])Get(backpack,"Enchants"))[4]=="changed-enchantment","An enchant-only slot detail stays current.");
   Check(((int[])Get(quick,"Counts"))[0]==9,"Quickslot stack counts refresh in the existing inventory.");
   Check(inventory.AutoScrollPosition.Y==scroll,"Content changes preserve scroll when the section layout is unchanged.");
   Console.WriteLine("Checking detail tabs.");var label=(TextBox)Get(page,"characterLabel");label.Focus();label.Text="Unsaved character label";label.Select(3,5);Refresh(page,state);
   Check(label.Text=="Unsaved character label"&&label.SelectionStart==3&&label.SelectionLength==5,"Live refresh preserves a label edit and its selection.");
   Set(tabs,"SelectedIndex",1);Pump(20);var fame=Grid(page,1);fame.CurrentCell=fame.Rows[28].Cells[1];fame.FirstDisplayedScrollingRowIndex=22;
   string fameSelection=Selected(fame),fameAnchor=First(fame);Set(first,"Fame",4321);Refresh(page,state);
   Check((int)Get(Tabs(page),"SelectedIndex")==1,"Fame tab remains selected through refresh.");
   Check(Object.ReferenceEquals(fame,Grid(page,1))&&Selected(fame)==fameSelection&&First(fame)==fameAnchor,"Fame scroll and selected row remain stable.");
   Check(Detail(page,1).Controls.OfType<Label>().Single().Text.Contains(4321.ToString("N0")),"Fame summary uses the current snapshot.");
   var characterSearch=(TextBox)Get(page,"search");characterSearch.Text="5";Pump(20);
   Check(Object.ReferenceEquals(tabs,Tabs(page))&&(int)Get(Tabs(page),"SelectedIndex")==1&&First(fame)==fameAnchor,"A filter that retains the selected character preserves its tab and detail scroll.");
   characterSearch.Text="";Pump(20);
   Set(tabs,"SelectedIndex",2);Pump(20);var statistics=Grid(page,2);statistics.CurrentCell=statistics.Rows[35].Cells[1];statistics.FirstDisplayedScrollingRowIndex=30;
   string statisticSelection=Selected(statistics),statisticAnchor=First(statistics);((IDictionary)Get(first,"PcStats"))["aaa_inserted_stat"]=99;((IDictionary)Get(first,"PcStats"))["test_stat_034"]=919;Refresh(page,state);
   Check(Selected(statistics)==statisticSelection&&First(statistics)==statisticAnchor,"Statistics preserve the selected key and scroll anchor when a row is inserted.");
   Check(statistics.Rows.Cast<DataGridViewRow>().Any(r=>Convert.ToInt32(r.Cells[1].Value)==919),"Statistics values refresh without replacing the grid.");
   statistics.Sort(statistics.Columns[1],System.ComponentModel.ListSortDirection.Descending);statistics.CurrentCell=statistics.Rows[20].Cells[0];statistics.FirstDisplayedScrollingRowIndex=18;
   statisticSelection=Selected(statistics);statisticAnchor=First(statistics);((IDictionary)Get(first,"PcStats"))["test_stat_079"]=300;Refresh(page,state);
   Check(statistics.SortOrder==SortOrder.Descending&&Selected(statistics)==statisticSelection&&First(statistics)==statisticAnchor,"Sorted detail rows preserve sorting, selection, and their scroll anchor.");
   Set(tabs,"SelectedIndex",3);Pump(20);var derived=Grid(page,3);var defense=Detail(page,3).Controls.Cast<Control>().SelectMany(c=>c.Controls.Cast<Control>()).OfType<NumericUpDown>().Single();defense.Value=35;
   Set(first,"ObservedStats",new[]{900,500,123,50,60,80,70,85});Set(first,"ObservedUtc",(DateTime?)DateTime.UtcNow);Refresh(page,state);
   Check(Convert.ToString(Row(derived,"ATT").Cells[1].Value)=="123","Derived details use the new live character snapshot.");
   Check(defense.Value==35&&Object.ReferenceEquals(derived,Grid(page,3)),"Derived controls retain the target-defense input.");
   Check(Convert.ToString(Row(derived,"Weapon DPS").Cells[2].Value).Contains("target DEF 35"),"Derived calculations retain the selected target defense.");
   Set(tabs,"SelectedIndex",0);Pump(20);Check(inventory.AutoScrollPosition.Y==scroll,"Inventory scroll survives updates while another detail tab is open.");
   Call(page,"Rebuild",true);Pump(20);Check(Object.ReferenceEquals(inventory,Inventory(page))&&inventory.AutoScrollPosition.Y==scroll,"An explicit refresh of the same character preserves equipment scroll.");
   Set(first,"Inventory",new[]{2827,-1,-1,-1});Set(first,"Enchants",new string[4]);Refresh(page,state);
   Check(inventory.AutoScrollPosition.Y<=0&&inventory.AutoScrollPosition.Y>scroll,"A smaller inventory clamps the old scroll to the new extent.");
   int clamped=inventory.AutoScrollPosition.Y;Set(first,"Inventory",Enumerable.Repeat(2827,28).ToArray());Set(first,"Enchants",new string[28]);Refresh(page,state);
   Check(inventory.AutoScrollPosition.Y==clamped,"Growing an inventory retains its current scroll position.");
   inventory.AutoScrollPosition=new Point(0,200);Set(tabs,"SelectedIndex",1);
   Console.WriteLine("Checking identity changes.");var roster=(DataGridView)Get(page,"table");roster.CurrentCell=roster.Rows.Cast<DataGridViewRow>().Single(r=>(int)Get(r.Tag,"Id")==7).Cells[0];Pump(30);
   Check((int)Get(page,"selectedCharacter")==7&&(int)Get(Tabs(page),"SelectedIndex")==0,"Selecting a different character starts its equipment tab.");
   Check(inventory.IsDisposed&&((ScrollableControl)Inventory(page)).AutoScrollPosition==Point.Empty,"A different character does not inherit the previous inventory scroll.");
   var secondInventory=(ScrollableControl)Inventory(page);secondInventory.AutoScrollPosition=new Point(0,200);Set(Tabs(page),"SelectedIndex",2);
   var accountB=Call(state,"Clone");Set(accountB,"AccountKey",new string('b',64));Set(accountB,"Name","Account B");Set(accountB,"CurrentCharacterId",5);var other=((IList)Get(accountB,"Characters"))[0];Set(other,"Label","Other account");((int[])Get(other,"Stats"))[2]=44;Set(workspace,"state",accountB);Call(page,"Rebuild",false);Pump(30);
   Check((int)Get(page,"selectedCharacter")==5&&(int)Get(Tabs(page),"SelectedIndex")==0,"An account switch selects that account's current character and resets the tab.");
   Check(secondInventory.IsDisposed&&((ScrollableControl)Inventory(page)).AutoScrollPosition==Point.Empty,"Account changes dispose old detail controls and reset scroll.");
   Check(label.Text=="Other account"&&Detail(page,0).Controls.OfType<Label>().Single().Text.Contains("ATT 44/"),"Account changes replace labels and stats even when character IDs overlap.");
   var currentInventory=Inventory(page);var search=(TextBox)Get(page,"search");search.Text="no matching fixture";Pump(20);
   Check(currentInventory.IsDisposed&&((Control)Get(page,"details")).Controls.OfType<Label>().Any(),"Filtering away the selection removes stale details.");
   search.Text="";Pump(20);Check((int)Get(Tabs(page),"SelectedIndex")==0,"Returning from an empty result creates fresh equipment details.");
  }
  Set(workspace,"dirty",false);Console.WriteLine("PASS: "+checks+" character detail refresh checks.");
 }
}
'@
try { [CharacterDetailRefreshTest]::Run((Resolve-Path -LiteralPath $AssemblyPath).Path) }
catch { Write-Output $_.Exception.ToString(); throw }
finally { $env:EXALTHELPER_HUD_DATA = $priorHudData }
