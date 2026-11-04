param(
    [string]$AssemblyPath = "$PSScriptRoot/../bin/Release/net48/ExaltHelper.exe",
    [string]$ScreenshotDirectory = ''
)
$ErrorActionPreference = 'Stop'
$originalHudData = $env:EXALTHELPER_HUD_DATA
$testHudData = Join-Path ([IO.Path]::GetTempPath()) ('exalthelper-account-test-' + [guid]::NewGuid().ToString('N'))
$env:EXALTHELPER_HUD_DATA = $testHudData
Add-Type -AssemblyName System.Drawing,System.Windows.Forms
$assembly = [Reflection.Assembly]::LoadFrom((Resolve-Path -LiteralPath $AssemblyPath).Path)
$flags = [Reflection.BindingFlags]'Public,NonPublic,Static,Instance'
$checks = 0
function Get-TestType([string]$name) { $assembly.GetType($name,$true) }
function New-TestObject([string]$name) { [Activator]::CreateInstance((Get-TestType $name),$true) }
function Read-TestMember($target,[string]$name) {
    $type = if($target -is [Type]) {$target} else {$target.GetType()}
    $object = if($target -is [Type]) {$null} else {$target}
    $field = $type.GetField($name,$flags)
    if($field) { return ,$field.GetValue($object) }
    return ,$type.GetProperty($name,$flags).GetValue($object,$null)
}
function Write-TestMember($target,[string]$name,$value) {
    $type = if($target -is [Type]) {$target} else {$target.GetType()}
    $object = if($target -is [Type]) {$null} else {$target}
    $field = $type.GetField($name,$flags)
    if($field) { $field.SetValue($object,$value) } else { $type.GetProperty($name,$flags).SetValue($object,$value,$null) }
}
function Invoke-TestMember($target,[string]$name,[object[]]$values = @()) {
    $type = if($target -is [Type]) {$target} else {$target.GetType()}
    $object = if($target -is [Type]) {$null} else {$target}
    return ,$type.GetMethod($name,$flags).Invoke($object,$values)
}
function Check([bool]$ok,[string]$message) { if(!$ok) { throw $message }; $script:checks++ }
function Stat([int]$id,[int]$value,[string]$text='') {
    $s=New-TestObject 'ExaltHelper.Proxy.Networking.Packets.DataObjects.StatData'
    $statType=Get-TestType 'ExaltHelper.Proxy.Networking.Packets.DataObjects.StatType'
    $conversion=@($statType.GetMethods($flags) | Where-Object { $_.Name -eq 'op_Implicit' -and $_.ReturnType -eq $statType -and $_.GetParameters()[0].ParameterType -eq [int] })[0]
    Write-TestMember $s Type ($conversion.Invoke($null,@($id)))
    Write-TestMember $s Value $value; Write-TestMember $s StringValue $text
    return $s
}
function Enchant([int16[]]$slots) {
    $bytes=New-Object 'Collections.Generic.List[byte]'; $bytes.Add(0); $bytes.Add(2); $bytes.Add(4)
    foreach($slot in $slots) { $bytes.AddRange([BitConverter]::GetBytes($slot)) }
    [Convert]::ToBase64String($bytes.ToArray()).Replace('+','-').Replace('/','_')
}
function Status([int]$objectId,[object[]]$values) {
    $object=New-TestObject 'ExaltHelper.Proxy.Networking.Packets.DataObjects.ObjectStatsData'
    Write-TestMember $object ObjectId $objectId
    $listType=[Collections.Generic.List``1].MakeGenericType((Get-TestType 'ExaltHelper.Proxy.Networking.Packets.DataObjects.StatData'))
    $stats=[Activator]::CreateInstance($listType); foreach($value in $values) { $stats.Add($value) }
    Write-TestMember $object StatList $stats
    return $object
}
function Slot([int]$objectId,[int]$slotId,[int]$itemId) {
    $slot=New-TestObject 'ExaltHelper.Proxy.Networking.Packets.DataObjects.SlotObjectData'
    Write-TestMember $slot ObjectId $objectId; Write-TestMember $slot SlotId $slotId; Write-TestMember $slot ItemType $itemId
    return $slot
}
$workspace=Get-TestType 'ExaltHelper.AccountWorkspace'
$fame=Get-TestType 'ExaltHelper.AccountWorkspaceFame'
$fixture=@'
<Chars maxNumChars="8">
 <Account><AccountId>synthetic-account</AccountId><Name>Fixture</Name><Credits>1234</Credits>
 <Stats><Fame>5678</Fame><ClassStats objectType="0x300"><BestBaseFame>2500</BestBaseFame></ClassStats></Stats>
 <OwnedSkins>100,200</OwnedSkins><OwnedEmotes>300</OwnedEmotes><RegularForgeFireBlueprints>1001,1002</RegularForgeFireBlueprints>
 <Vault><Chest>1001,-1,1002#unique-instance,1001</Chest><Chest>-1,1003</Chest></Vault>
 <MaterialStorage><Chest>1004,1004</Chest></MaterialStorage><Gifts>1001</Gifts><Potions>1005,-1</Potions>
 <UniqueItemInfo><ItemData id="unique-instance">ENCHANT_A</ItemData></UniqueItemInfo></Account>
 <Char id="5"><ObjectType>782</ObjectType><Level>20</Level><CurrentFame>2001</CurrentFame><Seasonal>True</Seasonal>
 <Equipment>1001,1002,1003,1004,1001,-1</Equipment><MaxHitPoints>670</MaxHitPoints><MaxMagicPoints>385</MaxMagicPoints>
 <Attack>75</Attack><Defense>25</Defense><Speed>50</Speed><Dexterity>75</Dexterity><HpRegen>40</HpRegen><MpRegen>60</MpRegen>
 <EquipQS>1009|6,-1|0,1010|2</EquipQS>
 <UniqueItemInfo><ItemData type="1001">ENCHANT_A</ItemData><ItemData type="1001">ENCHANT_B</ItemData></UniqueItemInfo>
 <Pet instanceId="42" name="Fixture pet" skin="100" type="1" rarity="3" maxAbilityPower="90" inv="0,8;X;1007,1008#unique"><Ability type="407" power="90"/><Ability type="408" power="80"/></Pet>
 </Char>
 <Char id="6"><ObjectType>768</ObjectType><Level>10</Level><CurrentFame>100</CurrentFame><Seasonal>False</Seasonal><Equipment>1001,-1,-1,-1</Equipment></Char>
 <PowerUpStats><ClassStats class="782">5,15,30,50,75,4,74,150</ClassStats></PowerUpStats>
</Chars>
'@
$enchantA=Enchant @([int16]1458,[int16]-1,[int16]-2,[int16]-3)
$enchantB=Enchant @([int16]-1,[int16]-3,[int16]781,[int16]5)
$fixture=$fixture.Replace('ENCHANT_A',$enchantA).Replace('ENCHANT_B',$enchantB)
try {
    # Keep persistence checks deterministic; production uses the same explicit shutdown flush.
    (Read-TestMember $workspace saveTimer).Change(-1,-1) | Out-Null
    $priorDirectory=[Environment]::CurrentDirectory
    try {
        [Environment]::CurrentDirectory=Split-Path -Parent (Resolve-Path -LiteralPath $AssemblyPath).Path
        Invoke-TestMember (Get-TestType 'ExaltHelper.Proxy.DataStructures.GameData') LoadGameData | Out-Null
    } finally { [Environment]::CurrentDirectory=$priorDirectory }
    Check ((Invoke-TestMember $workspace ClassMaximums @([int]782))[0] -gt 0) 'Class maximums load from packaged game data'
    $parsed=Invoke-TestMember $workspace ParseAccountXml @($fixture)
    Check ((Read-TestMember $parsed AccountKey).Length -eq 64) 'Account identity must be hashed before storage'
    Check ((Read-TestMember $parsed Name) -eq 'Fixture') 'Account name'
    Check ((Read-TestMember $parsed Gold) -eq 1234 -and (Read-TestMember $parsed Fame) -eq 5678) 'Account currencies'
    Check ((Read-TestMember $parsed Stars) -eq 5) 'Account stars derived from class fame'
    Check ((Read-TestMember $parsed CharacterSlots) -eq 8) 'Character slots from Chars attribute'
    $characters=Read-TestMember $parsed Characters
    Check ($characters.Count -eq 2) 'All characters loaded'
    Check ((Read-TestMember $characters[0] Seasonal) -and !(Read-TestMember $characters[1] Seasonal)) 'Seasonal and regular flags kept separate'
    Check ((Read-TestMember $characters[0] Stats)[6] -eq 40 -and (Read-TestMember $characters[0] Stats)[7] -eq 60) 'Vitality and wisdom stat order'
    Check ((Read-TestMember $characters[0] Inventory).Length -eq 6) 'Empty character slots retained'
    $storage=Read-TestMember $parsed Storage
    Check ($storage.Count -eq 4) 'Vault, materials, gifts, potions'
    Check ((Read-TestMember $storage[0] Items).Length -eq 6) 'Vault chest slots concatenate without removing holes'
    Check ((Read-TestMember $storage[0] Items)[2] -eq 1002) 'Unique item suffix removed from item type'
    $pets=Read-TestMember $parsed Pets
    Check ($pets.Count -eq 1 -and (Read-TestMember $pets[0] Inventory).Length -eq 8) 'Pet inventory capacity and empty slots'
    Check ((Read-TestMember $pets[0] AbilityPowers)[1] -eq 80 -and (Read-TestMember $pets[0] AbilityLevels).Length -eq 0) 'API pet power is distinct from observed level'
    Check ((Read-TestMember $characters[0] Enchants)[0] -eq $enchantA -and (Read-TestMember $characters[0] Enchants)[4] -eq $enchantB) 'Repeated item types consume distinct enchant entries in order'
    Check ((Read-TestMember $storage[0] Enchants)[2] -eq $enchantA) 'Storage enchant info resolves the unique instance suffix'
    Check ((Read-TestMember $characters[0] QuickSlots)[0] -eq 1009 -and (Read-TestMember $characters[0] QuickSlotCounts)[0] -eq 6) 'Quickslot API stacks preserve quantities'
    $exalts=Read-TestMember $parsed Exaltations
    Check (($exalts['782'] -join ',') -eq '150,74,4,75,15,5,30,50') 'Exaltation packet order mapped to display stats'
    foreach($example in @(@(0,0),@(4,0),@(5,1),@(14,1),@(15,2),@(29,2),@(30,3),@(49,3),@(50,4),@(74,4),@(75,5),@(150,5))) {
        Check ((Invoke-TestMember $workspace ExaltLevel @([int]$example[0])) -eq $example[1]) ('Exalt threshold '+$example[0])
    }
    Write-TestMember $workspace state $parsed
    Check ((Invoke-TestMember $workspace GetItemCount @([int]1001,$false)) -eq 4) 'Regular count includes vault, gifts, characters only'
    Check ((Invoke-TestMember $workspace GetItemCount @([int]1001,$true)) -eq 2) 'Seasonal character count isolated'
    Check ((Invoke-TestMember $workspace GetItemCount @([int]1007,$true)) -eq 1) 'Pet inventory contributes to account search'
    Check ((Invoke-TestMember $workspace GetItemCount @([int]1009,$true)) -eq 6) 'Quickslot stack quantity contributes to Treasury and quest stock'
    $clone=Invoke-TestMember $workspace Snapshot
    (Read-TestMember $clone Characters).Clear()
    Check ((Read-TestMember (Invoke-TestMember $workspace Snapshot) Characters).Count -eq 2) 'Snapshot mutations must not affect tracker'
    Write-TestMember $workspace dirty $true
    Invoke-TestMember $workspace SavePending | Out-Null
    $cache=Join-Path (Read-TestMember $workspace DataDirectory) ((Read-TestMember $parsed AccountKey)+'.json')
    Check (Test-Path -LiteralPath $cache) 'Account cache saved'
    $cacheText=[IO.File]::ReadAllText($cache)
    Check (!$cacheText.Contains('synthetic-account') -and !$cacheText.Contains('accessToken')) 'Cache excludes raw account identifier and credentials'
    $loaded=Invoke-TestMember $workspace Load @((Read-TestMember $parsed AccountKey))
    Check ((Read-TestMember $loaded Characters).Count -eq 2 -and (Read-TestMember $loaded Storage).Count -eq 4) 'Cache round trip'

    # Multi-part vault commits once complete and subsequent batches replace old slots.
    Write-TestMember $workspace currentSeasonal $true
    $vault=New-TestObject 'ExaltHelper.Proxy.Networking.Packets.VaultContentPacket'
    Write-TestMember $vault VaultContents ([int[]]@(111,222)); Write-TestMember $vault MaterialContents ([int[]]@()); Write-TestMember $vault GiftContents ([int[]]@())
    Write-TestMember $vault PotionContents ([int[]]@()); Write-TestMember $vault SeasonalSpoilContents ([int[]]@()); Write-TestMember $vault VaultChestObjectId 88
    Write-TestMember $vault IsLastVaultPacket $false
    Invoke-TestMember $workspace ApplyVaultLocked @($vault) | Out-Null
    Check ((Invoke-TestMember $workspace GetItemCount @([int]111,$true)) -eq 0) 'Partial vault batch does not publish inconsistent counts'
    Write-TestMember $vault VaultContents ([int[]]@(333,-1)); Write-TestMember $vault IsLastVaultPacket $true
    Invoke-TestMember $workspace ApplyVaultLocked @($vault) | Out-Null
    Check ((Invoke-TestMember $workspace GetItemCount @([int]111,$true)) -eq 1 -and (Invoke-TestMember $workspace GetItemCount @([int]333,$true)) -eq 1) 'Completed vault batch publishes all chunks'
    Write-TestMember $vault VaultContents ([int[]]@(444)); Invoke-TestMember $workspace ApplyVaultLocked @($vault) | Out-Null
    Check ((Invoke-TestMember $workspace GetItemCount @([int]111,$true)) -eq 0 -and (Invoke-TestMember $workspace GetItemCount @([int]444,$true)) -eq 1) 'Next full vault refresh replaces prior contents'
    Check ((Invoke-TestMember $workspace GetItemCount @([int]1001,$false)) -eq 4) 'Seasonal vault refresh leaves regular storage intact'
    foreach($example in @(@(8,0),@(19,11),@(131,12),@(138,19),@(139,20),@(146,27),@(147,-1),@(24,-1))) {
        Check ((Invoke-TestMember $workspace InventorySlot @([int]$example[0])) -eq $example[1]) ('Inventory slot '+$example[0])
    }
    $object=New-TestObject 'ExaltHelper.Proxy.Networking.Packets.DataObjects.ObjectStatsData'
    Write-TestMember $object ObjectId 88
    $listType=[Collections.Generic.List``1].MakeGenericType((Get-TestType 'ExaltHelper.Proxy.Networking.Packets.DataObjects.StatData'))
    $stats=[Activator]::CreateInstance($listType); $stats.Add((Stat 8 555))
    Write-TestMember $object StatList $stats
    Invoke-TestMember $workspace ApplyStorageStatsLocked @($object) | Out-Null
    Check ((Invoke-TestMember $workspace GetItemCount @([int]555,$true)) -eq 1 -and (Invoke-TestMember $workspace GetItemCount @([int]444,$true)) -eq 0) 'Authoritative vault slot update changes item counts'
    # Real vault statuses address a visible eight-slot page, not absolute slot zero.
    $pageItems=[int[]](1..18 | ForEach-Object { 2000+$_ })
    Write-TestMember $vault VaultContents $pageItems; Write-TestMember $vault GiftContents ([int[]](3001..3010))
    Write-TestMember $vault GiftChestObjectId 89; Write-TestMember $vault UnknownString1 ($enchantA+','+(','*16))
    Write-TestMember $vault UnknownString2 ($enchantA+','+(','*8))
    Invoke-TestMember $workspace ApplyVaultLocked @($vault) | Out-Null
    $s=((Read-TestMember (Invoke-TestMember $workspace Snapshot) Storage) | Where-Object { (Read-TestMember $_ Name) -eq 'Vault' -and (Read-TestMember $_ Seasonal) })
    Invoke-TestMember $workspace ApplyStorageStatsLocked @((Status 88 @((Stat 8 9999)))) | Out-Null
    Check ((Invoke-TestMember $workspace GetItemCount @([int]9999,$true)) -eq 0) 'Unknown multi-page position does not overwrite the first page'
    $pageStats=@(0..7 | ForEach-Object { Stat (8+$_) (2009+$_) })
    Invoke-TestMember $workspace ApplyStorageStatsLocked @((Status 88 $pageStats)) | Out-Null
    Invoke-TestMember $workspace ApplyStorageStatsLocked @((Status 88 @((Stat 8 9001)))) | Out-Null
    $s=((Read-TestMember (Invoke-TestMember $workspace Snapshot) Storage) | Where-Object { (Read-TestMember $_ Name) -eq 'Vault' -and (Read-TestMember $_ Seasonal) })
    Check ((Read-TestMember $s Items)[8] -eq 9001 -and (Read-TestMember $s Items)[0] -eq 2001) 'Second-page updates leave first-page slots intact'
    $g=((Read-TestMember (Invoke-TestMember $workspace Snapshot) Storage) | Where-Object { (Read-TestMember $_ Name) -eq 'Gifts' -and (Read-TestMember $_ Seasonal) })
    Check (((Read-TestMember $g Items) -join ',') -eq ((3010..3001) -join ',')) 'Gift chunks reverse into display order'
    Check ((Read-TestMember $g Enchants)[9] -eq $enchantA) 'Reversed gifts preserve enchant-to-item association'
    Invoke-TestMember $workspace ApplyStorageStatsLocked @((Status 89 @((Stat 8 3009),(Stat 9 3010)))) | Out-Null
    Invoke-TestMember $workspace ApplyStorageStatsLocked @((Status 89 @((Stat 8 9002),(Stat 10 9998)))) | Out-Null
    $g=((Read-TestMember (Invoke-TestMember $workspace Snapshot) Storage) | Where-Object { (Read-TestMember $_ Name) -eq 'Gifts' -and (Read-TestMember $_ Seasonal) })
    Check ((Read-TestMember $g Items)[1] -eq 9002 -and (Read-TestMember $g Items)[9] -eq 3001) 'Partial final gift page maps through the mirrored index'
    Check ((Invoke-TestMember $workspace GetItemCount @([int]9998,$true)) -eq 0) 'Partial gift page ignores positions outside storage bounds'
    # Failed swaps never mutate; successful result slots are absolute and carry item metadata.
    Write-TestMember $workspace activeObjectId 777; Write-TestMember $parsed CurrentCharacterId 5
    $swap=New-TestObject 'ExaltHelper.Proxy.Networking.Packets.InvSwapPacket'
    Write-TestMember $swap Slot1 (Slot 777 0 1001); Write-TestMember $swap Slot2 (Slot 88 17 2018)
    Write-TestMember $swap Result $false; Invoke-TestMember $workspace ApplySwapLocked @($swap) | Out-Null
    Check ((Read-TestMember $characters[0] Inventory)[0] -eq 1001) 'Failed inventory result leaves cache unchanged'
    Write-TestMember $swap Result $true; Invoke-TestMember $workspace ApplySwapLocked @($swap) | Out-Null
    $s=((Read-TestMember (Invoke-TestMember $workspace Snapshot) Storage) | Where-Object { (Read-TestMember $_ Name) -eq 'Vault' -and (Read-TestMember $_ Seasonal) })
    Check ((Read-TestMember $characters[0] Inventory)[0] -eq 2018 -and (Read-TestMember $s Items)[17] -eq 1001) 'Successful swap exchanges character and absolute storage slots'
    Check ((Read-TestMember $s Enchants)[17] -eq $enchantA -and [string]::IsNullOrEmpty((Read-TestMember $characters[0] Enchants)[0])) 'Enchant data follows the swapped item'
    $enchantInfo=Get-TestType 'ExaltHelper.AccountEnchantmentInfo'
    $a=Invoke-TestMember $enchantInfo Parse @($enchantA); $b=Invoke-TestMember $enchantInfo Parse @($enchantB)
    Check ((Read-TestMember $a Total) -eq 3 -and (Read-TestMember $a Empty) -eq 1 -and (Read-TestMember $a Locked) -eq 1) 'Enchant rarity includes filled, empty and locked slots'
    Check ((Read-TestMember $b Total) -eq 1 -and (Read-TestMember $b Filled) -eq 0) 'Nonexistent enchant slot terminates parsing before trailing data'
    $petStack=Stat 8 1007; Write-TestMember $petStack SecondaryValue ([byte]12)
    Invoke-TestMember $workspace ApplyPetStatsLocked @((Status 444 @((Stat 81 42),(Stat 82 0 'Wrong nearby name'),(Stat 84 0),(Stat 90 89),$petStack)),[int]999) | Out-Null
    Check ((Read-TestMember $pets[0] Name) -eq 'Fixture pet' -and (Read-TestMember $pets[0] Rarity) -eq 3) 'Live proximity stats cannot replace API-verified pet identity'
    Check ((Read-TestMember $pets[0] AbilityLevels)[0] -eq 89 -and (Invoke-TestMember $workspace GetItemCount @([int]1007,$true)) -eq 12) 'Pet live level and stack counts update independently'
    $belt=Stat 116 1009; Write-TestMember $belt SecondaryValue ([byte]18)
    Invoke-TestMember $workspace ApplyObjectLocked @($null,(Status 777 @($belt,(Stat 127 0 '1:12,4:9,5:3'),(Stat 147 0 '1:2000,4:1000,5:500'),(Stat 71 0 '1:10,3:7'))),[int]0) | Out-Null
    Invoke-TestMember $workspace ApplyObjectLocked @($null,(Status 777 @((Stat 127 0 '4:20'),(Stat 71 0 '3:8'))),[int]0) | Out-Null
    $current=Invoke-TestMember $workspace Snapshot
    Check (((Read-TestMember $current SeasonalDustAmounts) -join ',') -eq '12,20,3') 'Partial dust updates preserve unaffected denominations'
    Check ((Read-TestMember $current SeasonalDust) -eq 35 -and (Read-TestMember $current SeasonalDustCaps)[1] -eq 1000) 'Dust totals and caps remain distinct'
    Check ((Read-TestMember $current SeasonalMaterials)[0] -eq 10 -and (Read-TestMember $current SeasonalMaterials)[2] -eq 8) 'Material tier deltas retain other tiers'
    Check ((Invoke-TestMember $workspace GetItemCount @([int]1009,$true)) -eq 18) 'Live quickslot update uses the stack count'
    Invoke-TestMember $workspace ApplySeasonInfoLocked @('{"name":"Synthetic season","start":1768298400,"end":1771927140}',$false) | Out-Null
    Invoke-TestMember $workspace ApplySeasonInfoLocked @('{"battlePass":{"title":"Synthetic pass ","start":1768298401,"end":1770112740}}',$true) | Out-Null
    $current=Invoke-TestMember $workspace Snapshot
    Check ((Read-TestMember $current SeasonName) -eq 'Synthetic season' -and (Read-TestMember $current BattlePassName) -eq 'Synthetic pass') 'Season and nested battle-pass windows parsed independently'
    Invoke-TestMember $workspace ApplySeasonInfoLocked @('{}',$true) | Out-Null
    Check ($null -eq (Read-TestMember (Invoke-TestMember $workspace Snapshot) BattlePassEndUtc)) 'Missing battle pass clears its expired window'
    Invoke-TestMember $workspace ApplyRawLocked @([byte]78,[byte[]]@(0,0,0,4)) | Out-Null
    Check ((Read-TestMember (Invoke-TestMember $workspace Snapshot) SeasonalPetYard) -eq 4) 'Pet-yard upgrade packet preserves the seasonal yard tier'
    Invoke-TestMember $workspace ApplyRawLocked @([byte]120,[byte[]]@(1,2,20,30)) | Out-Null
    Check (((Read-TestMember (Invoke-TestMember $workspace Snapshot) SeasonalBlueprints)-join ',') -eq '20,30') 'Blueprint packet records unlocked recipes in its own season pool'
    Invoke-TestMember $workspace ApplyRawLocked @([byte]114,[byte[]]@(3,0,1,2,3,4,5,6,7,8)) | Out-Null
    Check (((Read-TestMember (Invoke-TestMember $workspace Snapshot) Exaltations)['768']-join ',') -eq '8,7,6,5,2,1,3,4') 'Live exaltation packet uses the same stat ordering as the account API'
    $before=Invoke-TestMember $workspace Snapshot
    $regularStore=@((Read-TestMember $parsed Storage) | Where-Object { (Read-TestMember $_ Name) -eq 'Vault' -and !(Read-TestMember $_ Seasonal) })[0]
    Write-TestMember $regularStore ObjectId 90; Write-TestMember $regularStore UpdatedUtc ([DateTime]::UtcNow.AddMinutes(-2))
    Write-TestMember $parsed Gold 7777; (Read-TestMember $characters[0] Inventory)[4]=4444; Write-TestMember $characters[0] UpdatedUtc ([DateTime]::UtcNow)
    $apiSnapshot=Invoke-TestMember $workspace ParseAccountXml @($fixture)
    Invoke-TestMember $workspace MergeApiLocked @($apiSnapshot,([DateTime]::UtcNow.AddSeconds(-5)),$before) | Out-Null
    $merged=Invoke-TestMember $workspace Snapshot
    $mergedLive=@((Read-TestMember $merged Characters) | Where-Object { (Read-TestMember $_ Id) -eq 5 })[0]
    $mergedStore=@((Read-TestMember $merged Storage) | Where-Object { (Read-TestMember $_ Name) -eq 'Vault' -and !(Read-TestMember $_ Seasonal) })[0]
    Check ((Read-TestMember $mergedLive Inventory)[4] -eq 4444 -and (Read-TestMember $merged Gold) -eq 7777) 'New live inventory and currency updates survive an older in-flight API response'
    Check ((Read-TestMember $mergedStore ObjectId) -eq 90) 'API refresh retains the current storage entity binding for subsequent live updates'
    # Malformed/older cache fields must not crash offline page rendering.
    $key=Read-TestMember $parsed AccountKey
    [IO.File]::WriteAllText($cache,('{"AccountKey":"'+$key+'","Characters":[null,{"Inventory":null,"Stats":null,"PcStats":null}],"Storage":[{"Items":null}],"Pets":[{"Inventory":null}],"Exaltations":{"782":null},"RegularMaterials":null,"RegularDustAmounts":null}'))
    $normalized=Invoke-TestMember $workspace Load @($key)
    Check ((Read-TestMember $normalized Characters).Count -eq 1 -and (Read-TestMember ((Read-TestMember $normalized Characters)[0]) Stats).Length -eq 8) 'Cache normalizes null entries and fixed stat arrays'
    Check ((Read-TestMember ((Read-TestMember $normalized Storage)[0]) Items).Length -eq 0 -and (Read-TestMember $normalized RegularMaterials).Length -eq 4) 'Cache normalizes missing storage and material arrays'
    $otherKey=Invoke-TestMember $workspace HashAccountId @('synthetic-other-account')
    Check ((Read-TestMember (Invoke-TestMember $workspace Load @($otherKey)) Characters).Count -eq 0) 'Missing account cache cannot reuse another account profile'
    # Current and legacy PCStats with hits=1, kills=2, first two general-order fields.
    foreach($bitfield in @(16,32)) {
        $bytes=New-Object byte[] ($bitfield+6); $bytes[0]=1; $bytes[7]=66; $bytes[$bitfield+4]=1; $bytes[$bitfield+5]=2
        $encoded=[Convert]::ToBase64String($bytes).Replace('+','-').Replace('/','_')
        $decoded=Invoke-TestMember $fame Decode @($encoded)
        Check ($decoded['hits'] -eq 1 -and $decoded['kills'] -eq 2) ('PCStats '+$bitfield+' byte field')
    }
    # Published RealmHound regression blob; counters deliberately differ from bit order.
    $decoded=Invoke-TestMember $fame Decode @('AQAAAAEAAEEAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAqA8yrAQ=')
    Check ($decoded['dungeons.manor_of_the_immortals'] -eq 300 -and $decoded['shots_fired'] -eq 1000 -and $decoded['kills'] -eq 50) 'Current 256-bit source regression decodes exact counters'
    $legacy=New-Object byte[] 40; $legacy[0]=1; $legacy[6]=3; $legacy[7]=255
    for($i=0;$i -lt 10;$i++) { $value=301+$i; $legacy[20+$i*2]=[byte](($value-band 63)-bor 128); $legacy[21+$i*2]=[byte]($value-shr 6) }
    $decoded=Invoke-TestMember $fame Decode @([Convert]::ToBase64String($legacy).Replace('+','-').Replace('/','_'))
    Check ($decoded['shots_fired'] -eq 301 -and $decoded['assists'] -eq 308 -and $decoded['god_kills'] -eq 309 -and $decoded['assists_against_gods'] -eq 310) 'Long legacy bitfield is distinguished from a plausible 256-bit field'
    $legacyDungeons=New-Object byte[] 60; $legacyDungeons[0]=1
    foreach($bit in @(136,137,138,144,145,146,147,148,149,150,151,159)) { $offset=4+[int][Math]::Floor($bit/8); $legacyDungeons[$offset]=[byte]($legacyDungeons[$offset]-bor (1-shl ($bit%8))) }
    $values=@(5,3,3,95,9,10,255,34,49,55,21,9)
    for($i=0;$i -lt $values.Length;$i++) { $legacyDungeons[36+$i*2]=[byte](($values[$i]-band 63)-bor 128); $legacyDungeons[37+$i*2]=[byte]($values[$i]-shr 6) }
    $decoded=Invoke-TestMember $fame Decode @([Convert]::ToBase64String($legacyDungeons).Replace('+','-').Replace('/','_'))
    Check ($decoded['dungeons.bilgewaters_grotto'] -eq 255 -and $decoded['dungeons.ivory_wyvern_portal'] -eq 55 -and $decoded['dungeons.legacy_lair_of_shaitan'] -eq 21 -and $decoded['dungeons.legacy_the_shatters'] -eq 9) 'Legacy dungeon block reads appended Shaitan and Shatters after Ivory Wyvern'
    Check ($null -eq (Invoke-TestMember $fame Decode @('garbage!'))) 'Malformed PCStats rejected'
    Check ((Invoke-TestMember $fame Tiered @([int]5000000,[int[]]@(1,10,20,40,100),[int[]]@(2,5,10,20,60))) -eq 1237) 'Top fame tier caps at 20 repeats'
    $fameLine=New-TestObject 'ExaltHelper.AccountWorkspaceFame+Line'; Write-TestMember $fameLine Percent ([double]7.5); Write-TestMember $fameLine Flat ([long]0)
    Check ((Invoke-TestMember $fameLine Bonus @([long]180723)) -eq 13555) 'Fame percentage rounds per bonus line'
    $breakdown=Invoke-TestMember $fame Breakdown @($characters[0])
    Check ($breakdown.Count -gt 100) 'Full fame and collection milestone breakdown'
    $maxed=Invoke-TestMember $characters[0] Clone
    Write-TestMember $maxed Stats (Invoke-TestMember $workspace ClassMaximums @([int]782))
    Write-TestMember $maxed Fame 180723
    $pc=Read-TestMember $maxed PcStats; $pc.Clear()
    $lines=Invoke-TestMember $fame Breakdown @($maxed)
    $total=0L; foreach($line in $lines) { $total+=Invoke-TestMember $line Bonus @([long]180723) }
    Check ($total -eq 46188) 'Eight maxed stats apply separate percentage ceilings and 1000 flat fame'
    $pc['biomes.beach_enemy_kills']=200; $pc['biomes.dead_church_enemy_kills']=5000
    $lines=Invoke-TestMember $fame Breakdown @($maxed)
    $biomeTotal=0L; foreach($line in $lines) { if((Read-TestMember $line Name) -in @('Beach Enemy Kills','Dead Church Enemy Kills')) { $biomeTotal+=Read-TestMember $line Flat } }
    Check ($biomeTotal -eq 1640) 'Biome payout preserves the game observed early second and fourth tiers'
    foreach($key in @('pirate_cave','forest_maze','forbidden_jungle','spider_den','the_hive')) { $pc['dungeons.'+$key]=1 }
    $lines=Invoke-TestMember $fame Breakdown @($maxed)
    $firstSteps=@($lines | Where-Object { (Read-TestMember $_ Name) -eq 'First Steps' })[0]
    Check ((Invoke-TestMember $firstSteps Bonus @([long]180723)) -eq 4619) 'Completed First Steps collection grants flat fame plus its own rounded percentage'
    $pc.Remove('dungeons.the_hive') | Out-Null
    $lines=Invoke-TestMember $fame Breakdown @($maxed)
    $firstSteps=@($lines | Where-Object { (Read-TestMember $_ Name) -eq 'First Steps' })[0]
    Check ((Invoke-TestMember $firstSteps Bonus @([long]180723)) -eq 0 -and (Read-TestMember $firstSteps Progress).Contains('The Hive')) 'Incomplete collection identifies the missing dungeon without awarding fame'
    $dungeonMap=Read-TestMember (Get-TestType 'ExaltHelper.AccountWorkspaceDrops') Dungeons
    Check ($dungeonMap.ContainsKey(('Belladonna'+[char]0x2019+'s Garden')) -and $dungeonMap.Count -eq 80) 'Dungeon filters preserve UTF-8 names and all 80 reference collections'
    . "$PSScriptRoot/Test-AccountWorkspaceParity.ps1"
    # UI smoke: all account pages render in a normal dashboard-sized viewport.
    if($ScreenshotDirectory) { [void][IO.Directory]::CreateDirectory($ScreenshotDirectory) }
    foreach($key in @('characters','vault','treasury','exaltations','petyard')) {
        $form=New-Object Windows.Forms.Form; $form.ClientSize=New-Object Drawing.Size(1020,720); $form.ShowInTaskbar=$false
        $form.StartPosition=[Windows.Forms.FormStartPosition]::Manual; $form.Location=New-Object Drawing.Point(-2500,-2500)
        $page=Invoke-TestMember $workspace CreatePage @($key); $form.Controls.Add($page); $form.Show(); [Windows.Forms.Application]::DoEvents()
        if($key -eq 'characters') {
            $panel=Read-TestMember $page details; $tabs=$panel.Controls[0]; $tabs.SelectedIndex=1
            Invoke-TestMember $page Rebuild @($true) | Out-Null
            Check ($panel.Controls[0].SelectedIndex -eq 1 -and (Read-TestMember $page selectedCharacter) -eq 5) 'Live page refresh preserves the selected character and fame detail tab'
        }
        if($key -eq 'treasury') {
            $search=Read-TestMember $page search; $search.Text='1009'
            $table=Read-TestMember $page table
            Check ($table.Rows.Count -eq 1 -and [int]$table.Rows[0].Cells[4].Value -eq 18) 'Treasury search renders the combined quickslot stack count'
            $search.Text=''; $tier=Read-TestMember $page enchantTier; $tier.SelectedItem='Legendary'
            Check ($table.Rows.Count -gt 0) 'Treasury rarity filter recognizes empty and locked enchant slots'
            $tier.SelectedIndex=0
            $category=Read-TestMember $page category; $category.SelectedItem='Unlocked blueprints'
            Check ($table.Rows.Count -eq 4) 'Unlocked blueprints show both seasonal and regular recipe sets'
            $category.SelectedIndex=0
        }
        $bitmap=New-Object Drawing.Bitmap(1020,720)
        try {
            $form.DrawToBitmap($bitmap,(New-Object Drawing.Rectangle(0,0,1020,720)))
            Check ($page.Width -gt 800 -and $page.Height -gt 500) ($key+' fits dashboard')
            if($ScreenshotDirectory) { $bitmap.Save((Join-Path $ScreenshotDirectory ($key+'.png'))) }
        } finally { $bitmap.Dispose(); $form.Dispose() }
    }
    $blocked=Invoke-TestMember $workspace FetchApiAsync @('https://outside.invalid/steal')
    try { $blocked.GetAwaiter().GetResult() | Out-Null; throw 'Expected endpoint rejection' } catch { Check ($_.Exception.ToString().Contains('Unsupported account endpoint')) 'Only known read-only endpoints may receive credentials' }
    Write-Output ('PASS: '+$checks+' account workspace checks')
} finally {
    Write-TestMember $workspace dirty $false
    $env:EXALTHELPER_HUD_DATA=$originalHudData
}
