# Invoked by Test-AccountWorkspace.ps1 after loading its reflection helpers and fixtures.
$parityOriginal = Invoke-TestMember $workspace Snapshot
$parityFixture = $fixture.Replace('synthetic-account','synthetic-parity-account')
function Write-DeathPayload([string]$account,[int]$character=5) {
    $stream = [IO.MemoryStream]::new()
    $constructor = (Get-TestType 'ExaltHelper.Proxy.Networking.PacketWriter').GetConstructor([Type[]]@([IO.MemoryStream]))
    $writer = $constructor.Invoke([object[]]@($stream))
    $writer.Write([string]$account); $writer.WriteCompressedInt($character); $writer.Write([string]'Synthetic boss')
    $writer.Write([int]1800); $writer.WriteCompressedInt(5000); $writer.WriteCompressedInt(2001); $writer.Write([int]12345)
    $writer.WriteCompressedInt(1); $writer.Write([string]'Explorer'); $writer.WriteCompressedInt(3); $writer.WriteCompressedInt(2999)
    $writer.Write([string]'AQAAAAEAAEEAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAqA8yrAQ=')
    $payload=$stream.ToArray(); $writer.Dispose(); return ,$payload
}
function Read-DerivedLine($lines,[string]$name) { return @($lines | Where-Object { (Read-TestMember $_ Name) -eq $name })[0] }
function Write-ChallengePayload([string]$json) {
    $stream=[IO.MemoryStream]::new()
    $writer=(Get-TestType 'ExaltHelper.Proxy.Networking.PacketWriter').GetConstructor([Type[]]@([IO.MemoryStream])).Invoke([object[]]@($stream))
    $writer.Write([int16]1); $writer.Write([int]1234); $writer.Write([int16]1); $writer.Write($json)
    $payload=$stream.ToArray(); $writer.Dispose(); return ,$payload
}
try {
    $parityState=Invoke-TestMember $workspace ParseAccountXml @($parityFixture)
    Write-TestMember $workspace state $parityState; Write-TestMember $parityState CurrentCharacterId 5
    $parityKey=Read-TestMember $parityState AccountKey
    $parityCharacters=Read-TestMember $parityState Characters
    $third=Invoke-TestMember $parityCharacters[0] Clone; Write-TestMember $third Id 7; $parityCharacters.Add($third)
    Invoke-TestMember $workspace SetCharacterLabel @([int]5,'  Main wizard  ') | Out-Null
    Invoke-TestMember $workspace MoveCharacter @([int]7,[int]-1) | Out-Null
    Check ((Read-TestMember $parityCharacters[0] Label) -eq 'Main wizard') 'Character labels trim whitespace'
    Check ((Read-TestMember $third Order) -eq 0 -and (Read-TestMember $parityCharacters[0] Order) -eq 1 -and (Read-TestMember $parityCharacters[1] Order) -eq [int]::MaxValue) 'Character ordering moves only within its season pool'
    Invoke-TestMember $workspace SetCharacterLabel @([int]7,('x'*100)) | Out-Null
    Check ((Read-TestMember $third Label).Length -eq 80) 'Character label bounds persist beyond the editor'
    Invoke-TestMember $workspace SaveTreasuryLayout @([string[]]@('regular.vault','bad','regular.vault'),[string[]]@('Rings','Rings','bad')) | Out-Null
    Invoke-TestMember $workspace ToggleTreasurySection @('regular.vault') | Out-Null
    Check ((Read-TestMember $parityState TreasurySectionOrder)[0] -eq 'regular.vault' -and (Read-TestMember $parityState TreasurySectionOrder).Length -eq 13) 'Treasury order rejects unknown and duplicate sections while retaining every section'
    Check ((Read-TestMember $parityState TreasuryCategoryOrder)[0] -eq 'Rings' -and (Read-TestMember $parityState TreasuryCategoryOrder).Length -eq 9) 'Treasury category order preserves canonical categories'
    Check ((Read-TestMember $parityState CollapsedTreasurySections) -contains 'regular.vault') 'Treasury collapse is tracked by stable section key'
    $incoming=Invoke-TestMember $parityState Clone
    foreach($c in (Read-TestMember $incoming Characters)) { Write-TestMember $c Label ''; Write-TestMember $c Order ([int]::MaxValue); Write-TestMember $c Fame 0 }
    Invoke-TestMember $workspace MergeApiLocked @($incoming,([DateTime]::UtcNow.AddSeconds(1)),(Invoke-TestMember $parityState Clone)) | Out-Null
    $parityCharacters=Read-TestMember $parityState Characters
    Check ((Read-TestMember $parityCharacters[0] Label) -eq 'Main wizard' -and (Read-TestMember $parityCharacters[0] Order) -eq 1 -and (Read-TestMember $parityCharacters[0] Fame) -eq 2001) 'API refresh preserves user labels, custom order and newer fame'
    $staleCrucible=Invoke-TestMember $workspace ParseAccountXml @($parityFixture.Replace('<Level>20</Level>','<Level>20</Level><CrucibleActive>True</CrucibleActive>'))
    Check (!(Read-TestMember ((Read-TestMember $staleCrucible Characters)[0]) Crucible)) 'Stale API Crucible flags are ignored until live acceptance'
    # Packet handlers queue only; rapid A -> B -> A must use the freshest queued copy.
    $parityPath=Join-Path (Read-TestMember $workspace DataDirectory) ($parityKey+'.json')
    Invoke-TestMember $workspace SavePendingLocked | Out-Null
    Check (!(Test-Path -LiteralPath $parityPath)) 'Queueing an account snapshot performs no disk write'
    $secondKey=Invoke-TestMember $workspace HashAccountId @('synthetic-parity-second')
    Invoke-TestMember $workspace SelectAccountLocked @($secondKey) | Out-Null
    Check ((Read-TestMember (Invoke-TestMember $workspace Snapshot) Characters).Count -eq 0) 'Switching accounts isolates the roster'
    Invoke-TestMember $workspace SelectAccountLocked @($parityKey) | Out-Null
    $parityState=Read-TestMember $workspace state
    Check ((Read-TestMember ((Read-TestMember $parityState Characters)[0]) Label) -eq 'Main wizard') 'Returning before flush restores the queued snapshot rather than old disk state'
    Check ((Read-TestMember $parityState CurrentCharacterId) -eq -1 -and @((Read-TestMember $parityState Storage) | Where-Object { (Read-TestMember $_ ObjectId) -ne -1 }).Count -eq 0) 'Queued account recovery clears live character and container bindings'
    Invoke-TestMember $workspace SavePending | Out-Null
    $saved=Invoke-TestMember $workspace Load @($parityKey)
    Check ((Read-TestMember ((Read-TestMember $saved Characters)[0]) Label) -eq 'Main wizard' -and (Read-TestMember ((Read-TestMember $saved Characters)[0]) Order) -eq 1) 'Character label and order survive shutdown flush and cache reload'
    Check ((Read-TestMember $saved TreasuryCategoryOrder)[0] -eq 'Rings' -and (Read-TestMember $saved CollapsedTreasurySections) -contains 'regular.vault') 'Treasury ordering and collapsed sections survive cache reload'
    $oldQueued=Invoke-TestMember $parityState Clone
    Invoke-TestMember $workspace SetCharacterLabel @([int]5,'Newer label') | Out-Null
    Invoke-TestMember $workspace SavePending | Out-Null
    $queue=Read-TestMember $workspace pendingSaves; $queue[$parityKey]=$oldQueued
    Invoke-TestMember $workspace SavePending | Out-Null
    $saved=Invoke-TestMember $workspace Load @($parityKey)
    Check ((Read-TestMember ((Read-TestMember $saved Characters)[0]) Label) -eq 'Newer label') 'An older queued revision cannot overwrite a newer persisted revision'
    Invoke-TestMember $workspace SelectAccountLocked @($secondKey) | Out-Null; Invoke-TestMember $workspace SavePending | Out-Null
    Check ([IO.File]::ReadAllText((Join-Path (Read-TestMember $workspace DataDirectory) 'last-account.txt')) -eq $secondKey) 'Shutdown records the selected account even when no data changed'
    Invoke-TestMember $workspace SelectAccountLocked @($parityKey) | Out-Null; $parityState=Read-TestMember $workspace state
    Write-TestMember $parityState CurrentCharacterId 5
    # Missing old characters become archived; ones observed during a refresh survive it.
    $incoming=Invoke-TestMember $parityState Clone
    foreach($n in @(8,9)) { $c=Invoke-TestMember ((Read-TestMember $parityState Characters)[0]) Clone; Write-TestMember $c Id $n; Write-TestMember $c UpdatedUtc ([DateTime]::UtcNow.AddSeconds($(if($n -eq 8){-10}else{0}))); (Read-TestMember $parityState Characters).Add($c) }
    Invoke-TestMember $workspace MergeApiLocked @($incoming,([DateTime]::UtcNow.AddSeconds(-5)),(Invoke-TestMember $parityState Clone)) | Out-Null
    Check (@((Read-TestMember $parityState DeadCharacters) | Where-Object { (Read-TestMember $_ Id) -eq 8 }).Count -eq 1) 'Missing older API character moves to the graveyard'
    Check (@((Read-TestMember $parityState Characters) | Where-Object { (Read-TestMember $_ Id) -eq 9 }).Count -eq 1) 'Character observed during an API request survives its older roster'
    $foreign=Invoke-TestMember $workspace ParseAccountXml @($parityFixture.Replace('synthetic-parity-account','synthetic-foreign'))
    try { Invoke-TestMember $workspace MergeApiLocked @($foreign,[DateTime]::UtcNow,$incoming) | Out-Null; throw 'Expected foreign API rejection' } catch { Check ($_.Exception.ToString().Contains('Account identity changed')) 'API response cannot replace a different account' }
    $deathCount=(Read-TestMember $parityState DeadCharacters).Count
    Invoke-TestMember $workspace ApplyRawLocked @([byte]46,(Write-DeathPayload 'synthetic-foreign')) | Out-Null
    Check ((Read-TestMember $parityState DeadCharacters).Count -eq $deathCount) 'Foreign-account death packet is ignored'
    $countBefore=Invoke-TestMember $workspace GetItemCount @([int]1009,$true)
    $death=Write-DeathPayload 'synthetic-parity-account'
    Invoke-TestMember $workspace ApplyRawLocked @([byte]46,[byte[]]$death[0..12]) | Out-Null
    Check ((Read-TestMember $parityState DeadCharacters).Count -eq $deathCount) 'Truncated death packet cannot partially mutate the roster'
    Invoke-TestMember $workspace ApplyRawLocked @([byte]46,$death) | Out-Null
    $dead=@((Read-TestMember $parityState DeadCharacters) | Where-Object { (Read-TestMember $_ Id) -eq 5 })[0]
    Check ((Read-TestMember $dead KilledBy) -eq 'Synthetic boss' -and (Read-TestMember $dead DeathFame) -eq 5000 -and (Read-TestMember $dead GraveType) -eq 1800) 'Death wire metadata archives the correct character'
    Check ((Read-TestMember $dead DeathBonuses).Count -eq 1 -and (Read-TestMember ((Read-TestMember $dead DeathBonuses)[0]) Fame) -eq 2999 -and (Read-TestMember $dead PcStats)['kills'] -eq 50) 'Death fame bonuses and final PCStats are decoded'
    Check ((Read-TestMember $parityState CurrentCharacterId) -eq -1 -and (Invoke-TestMember $workspace GetItemCount @([int]1009,$true)) -eq ($countBefore-6)) 'Dead inventory is excluded from Treasury and current-character selection'
    Invoke-TestMember $workspace ApplyRawLocked @([byte]46,$death) | Out-Null
    Check ((Read-TestMember $parityState DeadCharacters).Count -eq ($deathCount+1)) 'Repeated death packets update one graveyard entry'
    Invoke-TestMember $workspace MergeApiLocked @((Invoke-TestMember $workspace ParseAccountXml @($parityFixture)),([DateTime]::UtcNow.AddSeconds(-5)),(Invoke-TestMember $parityState Clone)) | Out-Null
    Check (@((Read-TestMember $parityState Characters) | Where-Object { (Read-TestMember $_ Id) -eq 5 }).Count -eq 0) 'A delayed API response cannot resurrect a confirmed death'
    Invoke-TestMember $workspace SavePending | Out-Null
    $saved=Invoke-TestMember $workspace Load @($parityKey)
    Check (@((Read-TestMember $saved DeadCharacters) | Where-Object { (Read-TestMember $_ Id) -eq 5 }).Count -eq 1) 'Graveyard survives shutdown persistence'
    # Definitions use both the flat S30 and percentage S31 conventions.
    $challengeJson='{"array":[{"id":"season-a","title":"Synthetic Crucible","crucible":{"restrictions":[{"type":2,"stat":"ATT","percent":0,"value":5},{"type":2,"stat":"VIT","percent":1,"value":1.3},{"type":5,"effects":"Exposed","effectChance":0.3,"threshold":15,"effectsDuration":3,"effectsCooldown":4}],"bonuses":[{"type":1,"amount":1.15},{"type":2,"amount":1.1},{"type":3,"amount":1.1},{"type":5,"amount":1.2}]}},{"id":"ritual-a","title":"III","bloodRitual":{"bonuses":[{"type":4,"percent":1,"amount":0.8},{"type":5,"percent":1,"amount":1.24},{"type":3,"percent":1,"amount":1.05}]}}]}'
    Invoke-TestMember $workspace ApplyChallengeJsonLocked (,([string[]]@($challengeJson))) | Out-Null
    $crucible=Read-TestMember $parityState Crucible; $ritual=(Read-TestMember $parityState BloodRituals)[0]
    Check ((Read-TestMember $crucible FlatStats)[2] -eq 5 -and (Read-TestMember $crucible StatMultipliers)[6] -eq 1.3 -and (Read-TestMember $crucible Effects).Length -eq 1) 'Crucible supports flat stats, percentage seasons, and on-hit effects'
    Check ((Read-TestMember $ritual StatMultipliers)[0] -eq 0.8 -and (Read-TestMember $ritual DamageMultiplier) -eq 1.24) 'Blood Ritual includes both HP penalties and damage bonuses'
    $challengePayload=Write-ChallengePayload $challengeJson
    Invoke-TestMember $workspace ApplyRawLocked @([byte]183,$challengePayload) | Out-Null
    Check ((Read-TestMember (Read-TestMember $parityState Crucible) Id) -eq 'season-a' -and (Read-TestMember $parityState BloodRituals).Count -eq 1) 'Raw challenge response parses ID prefixes and string payloads'
    Invoke-TestMember $workspace ApplyRawLocked @([byte]183,[byte[]]$challengePayload[0..12]) | Out-Null
    Check ((Read-TestMember (Read-TestMember $parityState Crucible) Id) -eq 'season-a') 'A truncated challenge packet preserves the last complete definition'
    $combat=Invoke-TestMember $workspace ParseAccountXml @($parityFixture); $fighter=(Read-TestMember $combat Characters)[0]
    Write-TestMember $combat Crucible $crucible; (Read-TestMember $combat BloodRituals).Add($ritual)
    Write-TestMember $fighter Crucible $true; Write-TestMember $fighter BloodRitualId 'ritual-a'; Write-TestMember $fighter Inventory ([int[]]@()); Write-TestMember $fighter Enchants ([string[]]@()); Write-TestMember $fighter PetId 0
    (Read-TestMember $combat Exaltations)['782']=[int[]]@(75,75,75,75,75,75,75,75)
    $derived=Get-TestType 'ExaltHelper.AccountWorkspaceDerived'; $lines=Invoke-TestMember $derived Build @($fighter,$combat,[int]0)
    Check ((Read-TestMember (Read-DerivedLine $lines 'HP') Value) -eq '561' -and (Read-TestMember (Read-DerivedLine $lines 'ATT') Value) -eq '85' -and (Read-TestMember (Read-DerivedLine $lines 'VIT') Value) -eq '57') 'Derived stats apply exaltations plus challenge changes to base stats'
    Check ((Read-TestMember (Read-DerivedLine $lines 'Mastery damage') Value) -eq '+10%' -and (Read-TestMember (Read-DerivedLine $lines 'XP boost') Value) -eq '+30%') 'Derived mastery and Fast Learner use the shared proficiency model'
    $weapon=Invoke-TestMember (Get-TestType 'ExaltHelper.HudCalculatorData') Named @('Staff of the Cosmic Whole')
    Write-TestMember $fighter Inventory ([int[]]@((Read-TestMember $weapon Id)))
    $lines=Invoke-TestMember $derived Build @($fighter,$combat,[int]0); $dps0=[double](Read-TestMember (Read-DerivedLine $lines 'Weapon DPS') Value)
    $lines=Invoke-TestMember $derived Build @($fighter,$combat,[int]100); $dps100=[double](Read-TestMember (Read-DerivedLine $lines 'Weapon DPS') Value)
    Check ($dps0 -gt $dps100 -and $dps100 -gt 0) 'Character weapon DPS responds to target defense'
    $gearXp=Read-TestMember $weapon XpBonus
    Check ([double](Read-TestMember (Read-DerivedLine $lines 'XP boost') Value).Trim('+','%') -eq (30+$gearXp)) 'Equipment XP bonuses are counted once'
    Write-TestMember $combat CurrentCharacterId 5; Write-TestMember $fighter ObservedStats ([int[]]@(900,500,90,30,60,100,80,90)); Write-TestMember $fighter ObservedUtc ([DateTime]::UtcNow)
    $lines=Invoke-TestMember $derived Build @($fighter,$combat,[int]0)
    Check ((Read-TestMember (Read-DerivedLine $lines 'HP') Value) -eq '900' -and (Read-TestMember (Read-DerivedLine $lines 'Snapshot') Value) -eq 'Live effective stats') 'Fresh live server totals override the offline stat estimate'
    $fullRegen=[double]((Read-TestMember (Read-DerivedLine $lines 'HP regeneration') Value).Split(' ')[0])
    Write-TestMember $fighter Effects ([int](1-shl 20)); $lines=Invoke-TestMember $derived Build @($fighter,$combat,[int]0)
    Check ([Math]::Abs([double]((Read-TestMember (Read-DerivedLine $lines 'HP regeneration') Value).Split(' ')[0])-$fullRegen/2) -lt 0.01) 'In-combat health regeneration halves the stat contribution'
    Write-TestMember $fighter BoostObservedUtc ([DateTime]::UtcNow.AddSeconds(-5))
    Check ((Invoke-TestMember $derived RemainingBoost @($fighter,[int]60,$true)) -le 55 -and (Invoke-TestMember $derived RemainingBoost @($fighter,[int]60,$false)) -eq 60) 'Live potion countdown advances while cached observations retain their known value'
    Write-TestMember $fighter LootBoostObservedUtc ([DateTime]::UtcNow.AddSeconds(-20))
    Write-TestMember $fighter BoostObservedUtc ([DateTime]::UtcNow)
    Check ((Invoke-TestMember $derived RemainingBoostAt @($fighter,[int]60,$true,(Read-TestMember $fighter LootBoostObservedUtc))) -le 40) 'An unrelated potion update does not restart a previously observed boost timer'
    $live=(Read-TestMember $parityState Characters)[0]; Write-TestMember $live Crucible $true
    Invoke-TestMember $workspace ApplyChallengeJsonLocked (,([string[]]@($challengeJson))) | Out-Null
    Check ((Read-TestMember $live Crucible)) 'Repeated challenge definition preserves acceptance'
    Invoke-TestMember $workspace ApplyChallengeJsonLocked (,([string[]]@($challengeJson.Replace('season-a','season-b')))) | Out-Null
    Check (!(Read-TestMember $live Crucible)) 'A new Crucible season clears old acceptance'
    Invoke-TestMember $workspace ApplyChallengeJsonLocked (,([string[]]@('broken','{}'))) | Out-Null
    Check ((Read-TestMember $parityState BloodRituals).Count -eq 1) 'Malformed or unrelated challenge JSON preserves known ritual definitions'
    Invoke-TestMember $workspace ApplyChallengeJsonLocked (,([string[]]@('{"array":[]}'))) | Out-Null
    Check ((Read-TestMember $parityState BloodRituals).Count -eq 0 -and $null -ne (Read-TestMember $parityState Crucible)) 'A valid empty feed clears rituals while retaining the last Crucible definition'
    $nowUnix=[DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
    $accelerators=Invoke-TestMember $workspace ParseAccelerators @('bad,20,751,60,900,30,751,9223372036854775807',([string]$nowUnix))
    Check ($accelerators.Count -eq 2 -and (Read-TestMember $accelerators[0] ItemType) -eq 751) 'Accelerator pairs preserve alignment and reject overflow'
    Check ((Invoke-TestMember $workspace ParseAccelerators @('751,60','')).Count -eq 0) 'Accelerators require an authoritative timestamp anchor'
    $withBoost=Invoke-TestMember $workspace ParseAccountXml @($parityFixture.Replace('</Chars>',('<Timestamp>'+$nowUnix+'</Timestamp><Accelerators>751,60</Accelerators></Chars>')))
    Check ((Read-TestMember $withBoost Accelerators).Count -eq 1) 'Account API reads account-wide accelerator timers'
    Write-TestMember $combat Accelerators (Read-TestMember $withBoost Accelerators)
    $lines=Invoke-TestMember $derived Build @($fighter,$combat,[int]0)
    Check ((Read-TestMember (Read-DerivedLine $lines 'Dust boost') Value) -eq '+60%') 'An active account dust potion contributes its 60 percent bonus'
    Write-TestMember ((Read-TestMember $combat Accelerators)[0]) ExpiresUtc ([DateTime]::UtcNow.AddSeconds(-1))
    $lines=Invoke-TestMember $derived Build @($fighter,$combat,[int]0)
    Check ((Read-TestMember (Read-DerivedLine $lines 'Dust boost') Value) -eq '+0%') 'Expired account potions do not contribute to derived boosts'
    Invoke-TestMember $workspace ApplyRawLocked @([byte]153,[byte[]]@(0,0,2,239)) | Out-Null
    $activated=(Read-TestMember $parityState Accelerators)[0]
    Check ((Read-TestMember $activated Estimated) -and (Read-TestMember $activated ExpiresUtc) -gt [DateTime]::UtcNow.AddHours(23)) 'Live accelerator activation provides an explicitly estimated timer'
    Invoke-TestMember $workspace MergeApiLocked @((Invoke-TestMember $workspace ParseAccountXml @($parityFixture)),([DateTime]::UtcNow.AddSeconds(-5)),(Invoke-TestMember $parityState Clone)) | Out-Null
    Check ((Read-TestMember $parityState Accelerators).Count -eq 1) 'Older in-flight account response cannot erase a newly activated accelerator'
    $malformedKey=Invoke-TestMember $workspace HashAccountId @('synthetic-parity-old-cache')
    $malformedPath=Join-Path (Read-TestMember $workspace DataDirectory) ($malformedKey+'.json')
    [IO.File]::WriteAllText($malformedPath,('{"AccountKey":"'+$malformedKey+'","Characters":[{"Id":1},{"Id":1},{"Id":2,"IsDead":true}],"DeadCharacters":[null,{"Id":3,"Label":null,"DeathBonuses":[null],"ObservedStats":null}],"TreasurySectionOrder":["bad","regular.vault","regular.vault"],"TreasuryCategoryOrder":null,"CollapsedTreasurySections":["bad","regular.vault"],"Crucible":{"FlatStats":null,"StatMultipliers":null,"Effects":null},"BloodRituals":[null],"Accelerators":[null]}'))
    $normalizedNew=Invoke-TestMember $workspace Load @($malformedKey)
    Check ((Read-TestMember $normalizedNew Characters).Count -eq 1 -and (Read-TestMember $normalizedNew DeadCharacters).Count -eq 2) 'Older cache migration deduplicates characters and separates already-dead entries'
    Check ((Read-TestMember $normalizedNew TreasurySectionOrder)[0] -eq 'regular.vault' -and (Read-TestMember $normalizedNew CollapsedTreasurySections).Length -eq 1) 'Malformed layout keys normalize without losing valid preferences'
    Check ((Read-TestMember (Read-TestMember $normalizedNew Crucible) StatMultipliers)[0] -eq 1 -and (Read-TestMember $normalizedNew Accelerators).Count -eq 0) 'Incomplete challenge and accelerator caches normalize safely'
    Check ($null -ne (Invoke-TestMember $normalizedNew Clone)) 'A normalized older cache remains safe to snapshot'
    if($ScreenshotDirectory) { [void][IO.Directory]::CreateDirectory($ScreenshotDirectory) }
    $form=New-Object Windows.Forms.Form; $form.ClientSize=New-Object Drawing.Size(1020,720); $form.ShowInTaskbar=$false
    $form.StartPosition='Manual'; $form.Location=New-Object Drawing.Point(-2500,-2500)
    try {
        $page=Invoke-TestMember $workspace CreatePage @('characters'); $form.Controls.Add($page); $form.Show(); [Windows.Forms.Application]::DoEvents()
        (Read-TestMember $page roster).SelectedIndex=1
        $deadTable=Read-TestMember $page table
        Check ($deadTable.Rows.Count -ge 1 -and (Read-TestMember $deadTable.Rows[0].Tag IsDead)) 'Graveyard roster renders archived characters'
        $deathRow=@($deadTable.Rows | Where-Object { (Read-TestMember $_.Tag Id) -eq 5 })[0]; $deadTable.CurrentCell=$deathRow.Cells[0]
        $detailTabs=(Read-TestMember $page details).Controls[0]; $detailTabs.SelectedIndex=1
        $famePanel=(Read-TestMember $detailTabs pages)[1]
        $deathFameGrid=@($famePanel.Controls | Where-Object { $_ -is [Windows.Forms.DataGridView] })[0]
        Check ($deathFameGrid.Rows.Count -eq 1 -and [int]$deathFameGrid.Rows[0].Cells[1].Value -eq 2999) 'Graveyard fame tab renders the actual awarded bonus list'
        $detailTabs.SelectedIndex=3; $combatPanel=(Read-TestMember $detailTabs pages)[3]
        $combatFilter=@($combatPanel.Controls | Where-Object { $_ -is [Windows.Forms.FlowLayoutPanel] })[0]
        $targetDefense=@($combatFilter.Controls | Where-Object { $_ -is [Windows.Forms.NumericUpDown] })[0]; $targetDefense.Value=123
        Invoke-TestMember $page Rebuild @($true) | Out-Null
        Check ((Read-TestMember $page derivedTargetDefense) -eq 123 -and (Read-TestMember $page details).Controls[0].SelectedIndex -eq 3) 'Live character refresh preserves the combat tab and chosen target defense'
        if($ScreenshotDirectory) { $bitmap=New-Object Drawing.Bitmap(1020,720); try { $form.DrawToBitmap($bitmap,(New-Object Drawing.Rectangle(0,0,1020,720))); $bitmap.Save((Join-Path $ScreenshotDirectory 'graveyard-derived.png')) } finally { $bitmap.Dispose() } }
    } finally { $form.Dispose() }
    $form=New-Object Windows.Forms.Form; $form.ClientSize=New-Object Drawing.Size(1280,860); $form.ShowInTaskbar=$false
    $form.StartPosition='Manual'; $form.Location=New-Object Drawing.Point(-2500,-2500)
    try {
        $page=Invoke-TestMember $workspace CreatePage @('treasury'); $form.Controls.Add($page); $form.Show(); [Windows.Forms.Application]::DoEvents()
        $treasuryGrid=Read-TestMember $page treasuryItems; $sections=Read-TestMember $treasuryGrid sections
        Check ((Read-TestMember $sections[0] Key) -eq 'regular.vault' -and (Read-TestMember $sections[0] Collapsed)) 'Treasury display applies the persisted section order and collapse state'
        $click=[Windows.Forms.MouseEventArgs]::new([Windows.Forms.MouseButtons]::Left,1,20,15,0)
        Invoke-TestMember $treasuryGrid OnMouseDown @($click) | Out-Null
        $sections=Read-TestMember $treasuryGrid sections
        Check (!(Read-TestMember $sections[0] Collapsed) -and (Read-TestMember $sections[1] Title) -eq 'Vault') 'Treasury header click expands the matching storage section'
        if($ScreenshotDirectory) { $bitmap=New-Object Drawing.Bitmap(1280,860); try { $form.DrawToBitmap($bitmap,(New-Object Drawing.Rectangle(0,0,1280,860))); $bitmap.Save((Join-Path $ScreenshotDirectory 'treasury-arranged.png')) } finally { $bitmap.Dispose() } }
    } finally { $form.Dispose() }
} finally {
    Write-TestMember $workspace state $parityOriginal
    Write-TestMember $workspace dirty $false
}
