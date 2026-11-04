param(
    [string]$AssemblyPath = "$PSScriptRoot/../bin/Release/net48/ExaltHelper.exe",
    [string]$ArtifactDirectory = "$PSScriptRoot/../artifacts/item-presentation"
)
$ErrorActionPreference='Stop'
if ([IO.DriveInfo]::new([IO.Path]::GetPathRoot([IO.Path]::GetFullPath($ArtifactDirectory))).AvailableFreeSpace -le 5GB) { throw 'STOP: disk guard' }
$testRoot=Join-Path ([IO.Path]::GetFullPath($ArtifactDirectory)) ([Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testRoot -Force | Out-Null
$env:EXALTHELPER_HUD_DATA=Join-Path $testRoot 'data'
Add-Type -ReferencedAssemblies System.Core,System.Drawing,System.Windows.Forms -TypeDefinition @'
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;

public static class HudItemPresentationRegression
{
    const BindingFlags F=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
    static Assembly app; static int checks,providerCalls; static Type engine,viewType;
    public sealed class Fixture { public int[] Ids=new[]{0xb0b}; public string Name="Sword of Acclaim", Rarity="Rare", Enchant="Loot Bonus III", Source="Lost Halls"; public bool Shiny; }
    public sealed class BoundRow { public string Item { get; set; } public string Source { get; set; } }
    static object Get(object value,string name) { Type type=value as Type??value.GetType();var field=type.GetField(name,F);return field!=null?field.GetValue(value is Type?null:value):type.GetProperty(name,F).GetValue(value is Type?null:value,null); }
    static void Set(object value,string name,object data) { Type type=value as Type??value.GetType();var field=type.GetField(name,F);if(field!=null)field.SetValue(value is Type?null:value,data);else type.GetProperty(name,F).SetValue(value is Type?null:value,data,null); }
    static object Call(object value,string name,params object[] args) { Type type=value as Type??value.GetType();return type.GetMethod(name,F).Invoke(value is Type?null:value,args); }
    static void Check(bool success,string message) { if(!success)throw new Exception(message);checks++; }
    static object View(int id,string name=null,string rarity=null,bool shiny=false,string enchant=null,string source=null,string details=null) {return Call(engine,"Create",id,name,rarity,shiny,enchant,source,details,null);}
    public static object Items(object value)
    {
        providerCalls++;var fixture=(Fixture)value;Array result=Array.CreateInstance(viewType,fixture.Ids.Length);
        for(int i=0;i<fixture.Ids.Length;i++)result.SetValue(View(fixture.Ids[i],fixture.Name,fixture.Rarity,fixture.Shiny,fixture.Enchant+" / slot "+i,fixture.Source,"Observed fixture"),i);
        return result;
    }
    static Delegate Provider()
    {
        var method=engine.GetMethod("Attach",F);var type=method.GetParameters()[2].ParameterType;var input=Expression.Parameter(typeof(object),"tag");
        return Expression.Lambda(type,Expression.Convert(Expression.Call(typeof(HudItemPresentationRegression).GetMethod("Items"),input),type.GetGenericArguments()[1]),input).Compile();
    }
    static void Pump() { for(int i=0;i<4;i++)Application.DoEvents(); }
    static object[] Models(object binding,DataGridViewRow row)
    {
        var entries=(IDictionary)Get(binding,"prepared");var prepared=entries[row];return ((IEnumerable)Get(prepared,"Items")).Cast<object>().ToArray();
    }
    static object Tip(object binding) { return Get(Get(binding,"tooltip"),"Tip"); }
    static void Snapshot(Control control,string path) { using(var bitmap=new Bitmap(control.Width,control.Height)){control.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size));bitmap.Save(path,ImageFormat.Png);} }
    static void Hover(object binding,DataGridView grid,int row,int item)
    {
        var bounds=grid.GetCellDisplayRectangle(0,row,false);int size=(int)Call(engine,"IconSize",bounds);
        Call(binding,"MouseMoved",grid,new MouseEventArgs(MouseButtons.None,0,bounds.Left+6+item*(size+6)+size/2,bounds.Top+bounds.Height/2,0));
        ((Timer)Get(Tip(binding),"timer")).Stop();
    }
    static void Metadata()
    {
        var task=(Task)Call(engine,"WarmUpAsync");Check(task.Wait(30000),"Item metadata warmup did not finish.");
        var sword=View(0xb0b,null,"Legendary",true,"Loot Bonus III","Stone Guardian / Lost Halls","Observed today");
        Check((string)Get(sword,"Name")=="Sword of Acclaim" && (string)Get(sword,"TierLabel")=="T12","Catalog display name or exact tier was lost.");
        string tip=(string)Get(sword,"Tooltip");Check(tip.Contains("Legendary")&&tip.Contains("Shiny")&&tip.Contains("Loot Bonus III")&&tip.Contains("Stone Guardian / Lost Halls")&&tip.Contains("Observed today"),"Tooltip omitted instance rarity, shiny, enchantment, source, or details.");
        Check(tip.Contains("Damage per projectile:")&&tip.Contains("220")&&tip.Contains("275")&&tip.Contains("Projectile range:"),"Known weapon stats did not reach the item tooltip.");
        var icon=Get(sword,"Icon") as Bitmap;Check(icon!=null && icon.Width>0,"Known catalog item icon is unavailable in the candidate.");
        var same=View(0xb0b);Check(ReferenceEquals(icon,Get(same,"Icon")),"Repeated item models cloned their shared sprite.");
        var other=View(0xb0b,null,"Common",false,"Different enchant","Other dungeon");Check((string)Get(other,"Tooltip")!=(string)Get(sword,"Tooltip") && !(bool)Get(other,"Shiny"),"Same-item instances leaked shiny/enchantment data.");
        var unknown=View(987654321,"Uncatalogued relic",null,false,null,"Unknown source");Check(Get(unknown,"Icon")==null && (string)Get(unknown,"Name")=="Uncatalogued relic" && ((string)Get(unknown,"Tooltip")).Contains("not been identified"),"Missing icons or metadata did not preserve a meaningful name fallback.");
        Check((int)Call(engine,"ResolveItemId","Sword of Acclaim")==0xb0b && (int)Call(engine,"ResolveItemId","Acclaim")==-1 && (int)Call(engine,"ResolveItemId","unobserved item")==-1,"Name resolution guessed partial names or missed an exact match.");
        var catalog=(IDictionary)Get(app.GetType("ExaltHelper.HudCalculatorData",true),"Items");
        var candidates=catalog.Values.Cast<object>().Where(x=>(bool)Get(x,"Equipment")).SelectMany(x=>new[]{new{Name=(string)Get(x,"Name"),Id=(int)Get(x,"Id")},new{Name=(string)Get(x,"ObjectName"),Id=(int)Get(x,"Id")}}).Where(x=>!string.IsNullOrWhiteSpace(x.Name));
        var ambiguous=candidates.GroupBy(x=>x.Name,StringComparer.OrdinalIgnoreCase).FirstOrDefault(x=>x.Select(a=>a.Id).Distinct().Count()>1);
        if(ambiguous!=null)Check((int)Call(engine,"ResolveItemId",ambiguous.Key)==-1,"Ambiguous display names were assigned an arbitrary item icon.");
        var db=(IDictionary)Get(app.GetType("ExaltHelper.ItemSpriteManager",true),"itemDb");
        foreach(string tier in new[]{"UT","ST"}) { var item=db.Values.Cast<object>().First(x=>((string[])Get(x,"Labels")).Contains(tier));Check((string)Get(View((int)Get(item,"ItemId")),"TierLabel")==tier,"Untiered/set-tier classification was lost: "+tier); }
        for(int i=0;i<1100;i++)View(10000000+i,"Unknown "+i);
        Check(((IDictionary)Get(engine,"cache")).Count<=1024,"Metadata cache grew past its fixed bound.");
        Check(((IDictionary)Get(engine,"itemNames")).Count<=65536,"Exact-name index exceeded its bound.");
    }
    static void Grid(string root)
    {
        using(var form=new Form{ClientSize=new Size(740,450),StartPosition=FormStartPosition.Manual,Location=new Point(-2500,-2500),ShowInTaskbar=false})
        using(var grid=(DataGridView)Activator.CreateInstance(app.GetType("ExaltHelper.HudDataGridView",true),true)) {
            grid.Dock=DockStyle.Fill;grid.AllowUserToAddRows=false;grid.RowHeadersVisible=false;grid.BackgroundColor=Color.FromArgb(18,23,33);grid.BorderStyle=BorderStyle.None;grid.EnableHeadersVisualStyles=false;grid.Font=new Font("Segoe UI",9.5f);
            grid.DefaultCellStyle.BackColor=Color.FromArgb(18,23,33);grid.DefaultCellStyle.ForeColor=Color.FromArgb(226,234,245);grid.DefaultCellStyle.SelectionBackColor=Color.FromArgb(21,51,57);grid.DefaultCellStyle.SelectionForeColor=Color.White;grid.RowTemplate.Height=38;
            grid.ColumnHeadersDefaultCellStyle.BackColor=Color.FromArgb(25,34,51);grid.ColumnHeadersDefaultCellStyle.ForeColor=Color.FromArgb(226,234,245);
            grid.Columns.Add("Item","ITEM");grid.Columns[0].Width=430;grid.Columns.Add("Source","SOURCE");grid.Columns[1].Width=260;form.Controls.Add(grid);
            var a=new Fixture{Name="Zulu sword",Enchant="First enchant",Shiny=false};var b=new Fixture{Name="Alpha sword",Enchant="Second enchant",Shiny=true,Rarity="Legendary"};var strip=new Fixture{Name="Equipment",Ids=new[]{0xb0b,0xb0b,0xb0b,0xb0b},Enchant="Strip enchant"};
            int index=grid.Rows.Add(a.Name,a.Source);grid.Rows[index].Tag=a;index=grid.Rows.Add(b.Name,b.Source);grid.Rows[index].Tag=b;index=grid.Rows.Add("Four equipment items",strip.Source);grid.Rows[index].Tag=strip;
            index=grid.Rows.Add("Uncatalogued relic","Unknown source");grid.Rows[index].Tag=new Fixture{Name="Uncatalogued relic",Ids=new[]{987654321},Rarity="Unknown rarity"};
            object binding=Call(engine,"Attach",grid,"Item",Provider(),false);
            Check(!grid.ShowCellToolTips,"Native item tooltips were not suppressed immediately, before row preparation.");
            form.Show();Pump();Call(engine,"Refresh",grid);Pump();
            Check((string)grid.Rows[0].Cells[0].Value==a.Name && (string)grid.Rows[1].Cells[0].Value==b.Name,"Item painting replaced string values needed for sorting and accessibility.");
            Check(((string)Get(Models(binding,grid.Rows[0])[0],"Tooltip")).Contains("First enchant")&&((string)Get(Models(binding,grid.Rows[1])[0],"Tooltip")).Contains("Second enchant"),"Adjacent same-ID items shared instance tooltip data.");
            Hover(binding,grid,1,0);Check(((string)Get(binding,"hoverText")).Contains("Second enchant")&&(bool)Get(Get(binding,"hoverItem"),"Shiny"),"Hover picked the wrong same-item instance.");
            Hover(binding,grid,2,1);Check(((string)Get(binding,"hoverText")).Contains("slot 1"),"Icon-strip hit testing did not select the hovered item.");
            using(var bitmap=new Bitmap(420,300,PixelFormat.Format24bppRgb))using(var graphics=Graphics.FromImage(bitmap)) {Call(Tip(binding),"Draw",graphics,new Rectangle(0,0,420,300));bitmap.Save(Path.Combine(root,"item-tooltip.png"),ImageFormat.Png);}
            Check(grid.Rows[1].Cells[0].ToolTipText=="","Item preparation repopulated a competing native tooltip.");
            var stripBounds=grid.GetCellDisplayRectangle(0,2,false);int iconSize=(int)Call(engine,"IconSize",stripBounds);
            Call(binding,"MouseMoved",grid,new MouseEventArgs(MouseButtons.None,0,stripBounds.Left+6+iconSize+2,stripBounds.Top+stripBounds.Height/2,0));
            Check((string)Get(binding,"hoverText")=="","An icon-strip gap was assigned to a neighboring item.");
            grid.Columns[0].Width=78;Pump();Hover(binding,grid,2,0);
            Check(((string)Get(binding,"hoverText")).Contains("slot 0")&&((string)Get(binding,"hoverText")).Contains("slot 3")&&Get(binding,"hoverItem")==null,"Overflow tooltip discarded hidden item instance details.");
            grid.CurrentCell=grid.Rows[2].Cells[0];var help=new KeyEventArgs(Keys.F1);Call(binding,"KeyPressed",grid,help);
            Check(help.Handled&&help.SuppressKeyPress&&(bool)Get(Tip(binding),"IsVisible")&&grid.AccessibleDescription.Contains("slot 3"),"F1 did not expose all item details through the themed/accessibility path.");
            Call(binding,"KeyPressed",grid,new KeyEventArgs(Keys.Escape));Check(!(bool)Get(Tip(binding),"IsVisible")&&string.IsNullOrEmpty(grid.AccessibleDescription),"Escape left an item tooltip or stale accessible description visible.");
            grid.Columns[0].Width=430;Pump();
            var changed=new Fixture{Name=a.Name,Enchant="Rebound enchant",Shiny=true,Rarity="Divine"};grid.Rows[0].Tag=changed;Call(engine,"Refresh",grid);Pump();Check(((string)Get(Models(binding,grid.Rows[0])[0],"Tooltip")).Contains("Rebound enchant")&&(bool)Get(Models(binding,grid.Rows[0])[0],"Shiny"),"Tag replacement with unchanged cell text kept stale item details.");
            changed.Enchant="Mutated same-tag enchant";Call(engine,"Refresh",grid);Pump();Check(((string)Get(Models(binding,grid.Rows[0])[0],"Tooltip")).Contains("Mutated same-tag enchant"),"Explicit refresh missed an enchant-only change on the same tag object.");
            grid.Sort(grid.Columns[0],ListSortDirection.Ascending);Pump();Hover(binding,grid,0,0);Check(ReferenceEquals(grid.Rows[0].Tag,b)&&((string)Get(binding,"hoverText")).Contains("Second enchant"),"Sorting detached hover details from their row tags.");
            Call(binding,"HideTip");int calls=providerCalls;using(var bitmap=new Bitmap(grid.Width,grid.Height)){for(int i=0;i<20;i++)grid.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size));}Check(providerCalls==calls,"Cell painting invoked item preparation or metadata lookup.");
            Snapshot(form,Path.Combine(root,"items-dark.png"));
            foreach(DataGridViewRow row in grid.Rows)row.Height=30;Pump();Snapshot(form,Path.Combine(root,"items-compact.png"));Check(grid.Rows.Cast<DataGridViewRow>().All(x=>x.Height==30),"Item renderer forced a larger row height.");
            var prefType=app.GetType("ExaltHelper.HudPreferences",true);var prefs=Get(prefType,"Current");var theme=app.GetType("ExaltHelper.HudTheme",true);var previous=(Color[])Get(theme,"Palette");Set(prefs,"Theme","Light");Call(theme,"Apply",form,previous);Pump();Snapshot(form,Path.Combine(root,"items-light.png"));
            Color light=(Color)Call(engine,"RarityColor",Models(binding,grid.Rows[0])[0]);Set(prefs,"Theme","Midnight");Color dark=(Color)Call(engine,"RarityColor",Models(binding,grid.Rows[0])[0]);Check(light.ToArgb()!=dark.ToArgb(),"Item rarity color did not adapt to the light theme.");
            grid.Rows.Clear();for(int i=0;i<420;i++){int row=grid.Rows.Add("Item "+i,"Source");grid.Rows[row].Tag=new Fixture{Name="Item "+i,Enchant="Enchant "+i};}Call(engine,"Refresh",grid);Pump();
            for(int first=0;first<420;first+=12){grid.FirstDisplayedScrollingRowIndex=first;Call(engine,"Refresh",grid);Pump();}
            Check((int)Get(binding,"PreparedCount")<=256,"Prepared row cache grew with scroll history.");
            grid.FirstDisplayedScrollingRowIndex=400;Call(engine,"Refresh",grid);Pump();Hover(binding,grid,400,0);Check(((string)Get(binding,"hoverText")).Contains("Enchant 400"),"Scrolling reused another row's tooltip.");
            var sprite=Get(View(0xb0b),"Icon") as Bitmap;grid.Dispose();Check((bool)Get(binding,"disposed")&&((IDictionary)Get(binding,"prepared")).Count==0,"Grid disposal retained item binding state.");Check(sprite.GetPixel(0,0).A>=0,"Disposing the grid disposed a shared item bitmap.");
        }
    }
    static void Tooltips(string root)
    {
        using(var form=new Form {Width=830,Height=420,StartPosition=FormStartPosition.Manual,Location=new Point(40,40)})
        using(var grid=new DataGridView {Location=new Point(8,8),Size=new Size(780,180),AutoGenerateColumns=false,AllowUserToAddRows=false,RowHeadersVisible=false,ReadOnly=true,AccessibleDescription="Item table"})
        using(var inventory=new Panel {Location=new Point(8,210),Size=new Size(780,140),AccessibleDescription="Inventory slots"})
        {
            grid.Columns.Add(new DataGridViewTextBoxColumn {Name="Item",DataPropertyName="Item",Width=250});
            grid.Columns.Add(new DataGridViewTextBoxColumn {Name="Source",DataPropertyName="Source",Width=150});
            grid.Columns.Add(new DataGridViewTextBoxColumn {Name="HP",Width=90});grid.RowTemplate.Height=42;
            grid.DataSource=new BindingList<BoundRow>(new[]{new BoundRow {Item="A long item name clipped by its column",Source="Lost Halls"},new BoundRow {Item="Unresolved entry",Source="Nexus"}});
            form.Controls.Add(grid);form.Controls.Add(inventory);form.Show();Pump();
            grid.Rows[0].Tag=new Fixture {Name="First bound item",Enchant="Bound instance enchant"};
            grid.Rows[1].Tag=new Fixture {Ids=new int[0]};
            grid.Rows[0].Cells[0].ToolTipText="Native fallback should never appear";
            grid.Rows[0].Cells[1].ToolTipText="Observed in Lost Halls";
            grid.Rows[1].Cells[0].ToolTipText="Unresolved item: keep this useful context";
            object binding=Call(engine,"Attach",grid,"Item",Provider(),true);Call(engine,"Refresh",grid);Pump();
            object coordinator=Get(binding,"tooltip"),tip=Tip(binding);
            Rectangle cell=grid.GetCellDisplayRectangle(0,0,false);var move=new MouseEventArgs(MouseButtons.None,0,cell.Left+18,cell.Top+18,0);
            Call(grid,"OnMouseMove",move);
            Check(!grid.ShowCellToolTips&&((string)Get(tip,"Text")).Contains("Bound instance enchant")&&!((string)Get(tip,"Text")).Contains("Native fallback"),"Bound nonvirtual item cells still used native explicit/truncation tooltips.");
            Check((bool)Get(tip,"IsPending")&&((Timer)Get(tip,"timer")).Interval==350&&!((bool)Get(tip,"IsVisible")),"Initial hover did not respect the item tooltip delay.");
            int calls=providerCalls;object body=Get(tip,"body"),timer=Get(tip,"timer"),native=Get(tip,"tooltip");
            for(int i=0;i<500;i++)Call(grid,"OnMouseMove",move);
            Check(providerCalls==calls&&ReferenceEquals(body,Get(tip,"body"))&&ReferenceEquals(timer,Get(tip,"timer"))&&ReferenceEquals(native,Get(tip,"tooltip")),"Repeated hover rebuilt item metadata, tooltip text, timer, or native tooltip.");
            Call(tip,"Tick",tip,EventArgs.Empty);
            Check((bool)Get(tip,"IsVisible")&&((ToolTip)native).OwnerDraw&&grid.AccessibleDescription.Contains("Bound instance enchant"),"The displayed item hint did not use the shared themed and accessible renderer.");
            for(int i=0;i<40;i++){grid.Rows[0].Cells["HP"].Value=600+i;Pump();}
            Check((bool)Get(tip,"IsVisible")&&ReferenceEquals(body,Get(tip,"body")),"Unrelated live HP changes dismissed or rebuilt a stable item tooltip.");
            var theme=app.GetType("ExaltHelper.HudTheme",true);var prefs=Get(app.GetType("ExaltHelper.HudPreferences",true),"Current");
            foreach(string name in new[]{"Midnight","Light","Slate"})
            {
                var previous=(Color[])Get(theme,"Palette");Set(prefs,"Theme",name);Call(theme,"Apply",form,previous);Pump();
                Check((int)Get(tip,"surfaceColor")==((Color)Get(theme,"Surface")).ToArgb(),"Visible tooltip retained the old palette after a theme switch: "+name);
                using(var bitmap=new Bitmap(420,280,PixelFormat.Format24bppRgb))using(var graphics=Graphics.FromImage(bitmap))
                {
                    Call(tip,"Draw",graphics,new Rectangle(0,0,420,280));
                    Check(bitmap.GetPixel(3,3).ToArgb()==((Color)Get(theme,"Surface")).ToArgb(),"Tooltip surface did not match HUD theme: "+name);
                    bitmap.Save(Path.Combine(root,"tooltip-"+name.ToLowerInvariant()+".png"),ImageFormat.Png);
                }
            }
            Rectangle source=grid.GetCellDisplayRectangle(1,0,false);Call(grid,"OnMouseMove",new MouseEventArgs(MouseButtons.None,0,source.Left+10,source.Top+12,0));
            Check((string)Get(tip,"Text")=="Observed in Lost Halls"&&Get(tip,"Item")==null,"Disabling native grid tips discarded non-item cell hints.");
            grid.Rows[0].Cells[1].ToolTipText="";grid.Rows[0].Cells[1].Value=new string('W',90);Call(grid,"OnMouseMove",new MouseEventArgs(MouseButtons.None,0,source.Left+10,source.Top+12,0));
            Check((string)Get(tip,"Text")==new string('W',90),"Clipped non-item text lost its fallback tooltip.");
            Rectangle empty=grid.GetCellDisplayRectangle(0,1,false);Call(grid,"OnMouseMove",new MouseEventArgs(MouseButtons.None,0,empty.Left+10,empty.Top+12,0));
            Check(((string)Get(tip,"Text")).Contains("keep this useful context")&&Get(tip,"Item")==null,"A mixed item column discarded hints for a row without an item.");
            grid.Rows[1].Cells[0].ToolTipText="";grid.Rows[1].Cells[0].Value=new string('M',90);Call(engine,"Refresh",grid);Pump();Call(grid,"OnMouseMove",new MouseEventArgs(MouseButtons.None,0,empty.Left+10,empty.Top+12,0));
            Check((string)Get(tip,"Text")==new string('M',90),"A mixed item column discarded clipped-text hints for a row without an item.");
            object second=Call(engine,"Attach",grid,"Source",Provider(),true);Call(engine,"Refresh",grid);Pump();
            Check(ReferenceEquals(tip,Tip(second)),"A grid with two item columns created competing tooltip renderers.");
            ((IDisposable)binding).Dispose();Check(!grid.ShowCellToolTips&&!(bool)Get(tip,"disposed"),"Disposing one item binding reenabled native tips or disposed another binding's renderer.");
            ((IDisposable)second).Dispose();Check(grid.ShowCellToolTips&&(bool)Get(tip,"disposed")&&grid.AccessibleDescription=="Item table","Last binding disposal did not restore tooltip/accessibility settings and release resources.");
            object item=View(0xb0b,null,"Legendary",true,"Inventory enchant","Vault");
            var tipType=app.GetType("ExaltHelper.HudItemToolTip",true);object inventoryTip=Activator.CreateInstance(tipType,F,null,new object[]{inventory},null);
            string content="Vault / slot 5\nStack: 2\nObserved today\n"+(string)Get(item,"Tooltip");
            Call(inventoryTip,"Show",item,content,new Point(20,30),false);
            Check(((string)Get(inventoryTip,"body")).Contains("slot 5")&&((string)Get(inventoryTip,"body")).Contains("Stack: 2")&&((string)Get(inventoryTip,"body")).Contains("Inventory enchant"),"Shared rich tooltip dropped inventory slot, stack, or enchantment context.");
            Check(((ToolTip)Get(inventoryTip,"tooltip")).OwnerDraw&&inventory.AccessibleDescription==content,"Inventory hints still used an unthemed tooltip or omitted accessible text.");
            Call(inventoryTip,"Show",item,content,new Point(20,30),true);Check((bool)Get(inventoryTip,"IsVisible"),"Immediate keyboard inventory hints remained delayed.");
            inventory.Hide();Check(!(bool)Get(inventoryTip,"IsPending")&&!(bool)Get(inventoryTip,"IsVisible")&&inventory.AccessibleDescription=="Inventory slots","Hiding an inventory owner leaked its tooltip or accessible item text.");
            inventory.Show();Call(inventoryTip,"Show",item,content,new Point(20,30),false);inventory.Dispose();
            Check((bool)Get(inventoryTip,"disposed"),"Disposing an inventory owner retained its shared tooltip and timer.");
            var sprite=(Bitmap)Get(item,"Icon");Check(sprite.GetPixel(0,0).A>=0,"Disposing a rich tooltip disposed its shared item icon.");
            Set(prefs,"Theme","Midnight");
        }
        using(var grid=new DataGridView {ShowCellToolTips=false})
        {
            grid.Columns.Add("Item","Item");object binding=Call(engine,"Attach",grid,"Item",Provider(),true);((IDisposable)binding).Dispose();
            Check(!grid.ShowCellToolTips,"An originally disabled native-tooltip preference was changed on detach.");
        }
    }
    public static void Run(string path,string root)
    {
        app=Assembly.LoadFrom(Path.GetFullPath(path));engine=app.GetType("ExaltHelper.HudItemPresentation",true);viewType=app.GetType("ExaltHelper.HudItemView",true);
        try{Metadata();Grid(root);Tooltips(root);Console.WriteLine("PASS "+checks+" item presentation checks. Artifacts: "+root);}catch(Exception ex){Console.Error.WriteLine("After "+checks+" checks: "+ex);throw;}
    }
}
'@
[HudItemPresentationRegression]::Run((Resolve-Path $AssemblyPath).Path,$testRoot)
